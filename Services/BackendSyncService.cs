using System.Net.Http.Headers;
using System.Net.Http.Json;
using NutritionTracker.Models;

namespace NutritionTracker.Services;

public class BackendSyncService
{
    private readonly HttpClient _http = new();

    public string BackendBaseUrl => Preferences.Default.Get("backend_api_url", "https://api.nutritiontracker.fr/api").Trim().TrimEnd('/');

    private string ApiBaseUrl
    {
        get
        {
            var baseUrl = BackendBaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl))
                return "";

            return baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase)
                ? baseUrl
                : $"{baseUrl}/api";
        }
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiBaseUrl);

    public async Task<bool> EnsureBackendIdentityAsync(string idToken)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(idToken))
            return false;

        var payload = new { id_token = idToken };
        var resp = await _http.PostAsJsonAsync($"{ApiBaseUrl}/auth/google", payload);
        if (!resp.IsSuccessStatusCode)
            return false;

        var parsed = await resp.Content.ReadFromJsonAsync<AuthResponse>();
        if (parsed == null || string.IsNullOrWhiteSpace(parsed.user_id))
            return false;

        Preferences.Default.Set("backend_user_id", parsed.user_id);
        return true;
    }

    public async Task<bool> TryPushMealAsync(MealEntry meal, List<MealItem> items)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return false;

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/meals");
        req.Headers.Add("X-User-Id", userId);

        var body = new
        {
            date_utc = meal.DateUtc,
            raw_text = meal.RawText,
            description = meal.Description,
            ai_notes = meal.AiNotes,
            photo_url = meal.PhotoPath,
            total_calories = meal.TotalCalories,
            total_carbs_g = meal.TotalCarbsG,
            total_protein_g = meal.TotalProteinG,
            overall_confidence = meal.OverallConfidence,
            quality_score = meal.QualityScore,
            quality_label = meal.QualityLabel,
            items = items.Select(x => new
            {
                name = x.Name,
                quantity = x.Quantity,
                unit = x.Unit,
                estimated_grams = x.EstimatedGrams,
                calories = x.Calories,
                carbs_g = x.CarbsG,
                protein_g = x.ProteinG,
                confidence = x.Confidence
            }).ToList()
        };

        req.Content = JsonContent.Create(body);
        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> TryPushGoalsAsync(UserGoals goals)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return false;

        using var req = new HttpRequestMessage(HttpMethod.Put, $"{ApiBaseUrl}/goals");
        req.Headers.Add("X-User-Id", userId);
        req.Content = JsonContent.Create(new
        {
            calories_target = goals.CaloriesTarget,
            carbs_g_target = goals.CarbsGTarget,
            protein_g_target = goals.ProteinGTarget
        });

        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> TryPushRemindersAsync(bool enabled, TimeSpan breakfast, TimeSpan lunch, TimeSpan dinner)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return false;

        using var req = new HttpRequestMessage(HttpMethod.Put, $"{ApiBaseUrl}/reminders");
        req.Headers.Add("X-User-Id", userId);
        req.Content = JsonContent.Create(new
        {
            enabled,
            breakfast_time_local = breakfast.ToString(@"hh\:mm\:ss"),
            lunch_time_local = lunch.ToString(@"hh\:mm\:ss"),
            dinner_time_local = dinner.ToString(@"hh\:mm\:ss"),
            timezone_name = TimeZoneInfo.Local.Id,
        });

        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> TryInviteFriendAsync(string email)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return false;

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/friends/invites");
        req.Headers.Add("X-User-Id", userId);
        req.Content = JsonContent.Create(new { invitee_email = email });

        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> TryAcceptInviteAsync(string inviteId)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(inviteId))
            return false;

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/friends/invites/{inviteId}/accept");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> TryDeleteInviteAsync(string inviteId)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(inviteId))
            return false;

        using var req = new HttpRequestMessage(HttpMethod.Delete, $"{ApiBaseUrl}/friends/invites/{inviteId}");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode;
    }

    private sealed class AuthResponse
    {
        public string user_id { get; set; } = "";
    }
}
