using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using NutritionTracker.Services.Dto;
using System.Net.Sockets;

namespace NutritionTracker.Services;

public class ApiService
{
    private readonly HttpClient _http = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _baseUrl;
    private readonly SessionService _session;

    public ApiService(string baseUrl, SessionService session)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _session = session;
    }

    private async Task EnsureAuthorizedOrRedirectAsync(HttpResponseMessage resp, string json)
    {
        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            await _session.RedirectToLoginAsync(clearAuth: true);
            throw new Exception(LocalizationService.T("not_logged_in"));
        }

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"API error {(int)resp.StatusCode}: {json}");
    }

    public async Task<AnalyzeResponse> AnalyzeMealAsync(string idToken, string lang, string text, byte[]? imageBytes, string? imageMime)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/analyze-meal");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        if (imageBytes == null)
        {
            var body = JsonSerializer.Serialize(new { lang, text }, _jsonOptions);
            req.Content = new StringContent(body);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }
        else
        {
            var mp = new MultipartFormDataContent();
            mp.Add(new StringContent(lang), "lang");
            mp.Add(new StringContent(text ?? ""), "text");

            var imgContent = new ByteArrayContent(imageBytes);
            imgContent.Headers.ContentType = new MediaTypeHeaderValue(imageMime ?? "image/jpeg");
            mp.Add(imgContent, "image", "meal.jpg");

            req.Content = mp;
        }

        using var resp = await SendWithSessionRecoveryAsync(req);
        var json = await resp.Content.ReadAsStringAsync();
        await EnsureAuthorizedOrRedirectAsync(resp, json);

        var parsed = JsonSerializer.Deserialize<AnalyzeResponse>(json, _jsonOptions);
        if (parsed == null) throw new Exception("Invalid JSON from API");
        return parsed;
    }

    public async Task<RecommendationsResponse> GetRecommendationsAsync(string idToken, object payload)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/recommendations");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
        var body = JsonSerializer.Serialize(payload, _jsonOptions);
        req.Content = new StringContent(body);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var resp = await SendWithSessionRecoveryAsync(req);
        var json = await resp.Content.ReadAsStringAsync();
        await EnsureAuthorizedOrRedirectAsync(resp, json);

        var parsed = JsonSerializer.Deserialize<RecommendationsResponse>(json, _jsonOptions);
        if (parsed == null) throw new Exception("Invalid JSON from API");
        return parsed;
    }

    private async Task<HttpResponseMessage> SendWithSessionRecoveryAsync(HttpRequestMessage req)
    {
        try
        {
            return await _http.SendAsync(req);
        }
        catch (Exception ex) when (IsSocketClosedError(ex))
        {
            await _session.RedirectToLoginAsync(clearAuth: true);
            throw new Exception(LocalizationService.T("not_logged_in"));
        }
    }

    private static bool IsSocketClosedError(Exception ex)
    {
        for (Exception? current = ex; current != null; current = current.InnerException)
        {
            if (current is SocketException)
                return true;

            var message = current.Message ?? "";
            if (message.IndexOf("socket closed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("connection reset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("broken pipe", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }
}
