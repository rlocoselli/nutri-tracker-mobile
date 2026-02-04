using System.Net.Http.Headers;
using System.Text.Json;
using NutritionTracker.Services.Dto;

namespace NutritionTracker.Services;

public class ApiService
{
    private readonly HttpClient _http = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _baseUrl;

    public ApiService(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
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

        using var resp = await _http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"API error {(int)resp.StatusCode}: {json}");

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

        using var resp = await _http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"API error {(int)resp.StatusCode}: {json}");

        var parsed = JsonSerializer.Deserialize<RecommendationsResponse>(json, _jsonOptions);
        if (parsed == null) throw new Exception("Invalid JSON from API");
        return parsed;
    }
}
