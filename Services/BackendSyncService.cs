using System.Net.Http.Headers;
using System.Net.Http.Json;
using NutritionTracker.Models;

namespace NutritionTracker.Services;

public class BackendSyncService
{
    private readonly HttpClient _http = new();

    public string BackendBaseUrl => "https://api.nutritiontracker.fr/api";

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

    public async Task<string?> CreateMealAsync(MealEntry meal, List<MealItem> items)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return null;

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/meals");
        req.Headers.Add("X-User-Id", userId);
        req.Content = JsonContent.Create(BuildMealPayload(meal, items));

        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return null;

        var parsed = await resp.Content.ReadFromJsonAsync<CreateMealResponse>();
        return parsed?.id;
    }

    public async Task<bool> UpdateMealAsync(string mealId, MealEntry meal, List<MealItem> items)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(mealId))
            return false;

        using var req = new HttpRequestMessage(HttpMethod.Patch, $"{ApiBaseUrl}/meals/{mealId}");
        req.Headers.Add("X-User-Id", userId);
        req.Content = JsonContent.Create(BuildMealPayload(meal, items));

        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteMealAsync(string mealId)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(mealId))
            return false;

        using var req = new HttpRequestMessage(HttpMethod.Delete, $"{ApiBaseUrl}/meals/{mealId}");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode;
    }

    public async Task<List<BackendMeal>> GetMealsBetweenUtcAsync(DateTime fromUtc, DateTime toUtc)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return new List<BackendMeal>();

        var fromDate = fromUtc.Date.ToString("yyyy-MM-dd");
        var toDate = toUtc.AddSeconds(-1).Date.ToString("yyyy-MM-dd");

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/meals?from={fromDate}&to={toDate}");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return new List<BackendMeal>();

        var parsed = await resp.Content.ReadFromJsonAsync<List<BackendMeal>>();
        return parsed ?? new List<BackendMeal>();
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

    public async Task<bool> TryPushWaterIntakeAsync(DateTime dayLocal, double liters)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return false;

        using var req = new HttpRequestMessage(HttpMethod.Put, $"{ApiBaseUrl}/water-intake");
        req.Headers.Add("X-User-Id", userId);

        var dayUtc = DateTime.SpecifyKind(dayLocal.Date, DateTimeKind.Local).ToUniversalTime();
        req.Content = JsonContent.Create(new
        {
            day_key_utc = dayUtc.ToString("yyyy-MM-dd"),
            liters = Math.Round(Math.Max(0, liters) * 2.0, MidpointRounding.AwayFromZero) / 2.0
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

    public async Task<List<BackendStory>> GetFriendsFeedAsync(int days = 2, int limit = 40)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return new List<BackendStory>();

        var safeDays = Math.Clamp(days, 1, 14);
        var safeLimit = Math.Clamp(limit, 1, 120);

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/friends/feed?days={safeDays}&limit={safeLimit}");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return new List<BackendStory>();

        var parsed = await resp.Content.ReadFromJsonAsync<List<BackendStory>>();
        return parsed ?? new List<BackendStory>();
    }

    public async Task<(bool liked, int likeCount)> ToggleStoryLikeAsync(string mealId)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(mealId))
            return (false, 0);

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/friends/feed/{mealId}/like");
        req.Headers.Add("X-User-Id", userId);
        req.Content = JsonContent.Create(new { });
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return (false, 0);

        var parsed = await resp.Content.ReadFromJsonAsync<StoryLikeResultDto>();
        return (parsed?.liked ?? false, parsed?.like_count ?? 0);
    }

    public async Task<List<StoryCommentDto>> GetStoryCommentsAsync(string mealId, int limit = 40)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(mealId))
            return new List<StoryCommentDto>();

        var safeLimit = Math.Clamp(limit, 1, 120);
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/friends/feed/{mealId}/comments?limit={safeLimit}");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return new List<StoryCommentDto>();

        var parsed = await resp.Content.ReadFromJsonAsync<List<StoryCommentDto>>();
        return parsed ?? new List<StoryCommentDto>();
    }

    public async Task<StoryCommentDto?> AddStoryCommentAsync(string mealId, string text)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(mealId) || string.IsNullOrWhiteSpace(text))
            return null;

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/friends/feed/{mealId}/comments");
        req.Headers.Add("X-User-Id", userId);
        req.Content = JsonContent.Create(new { text });

        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return null;

        return await resp.Content.ReadFromJsonAsync<StoryCommentDto>();
    }

    public async Task<bool> SendPrivateMessageAsync(string otherUserId, string text)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(otherUserId) || string.IsNullOrWhiteSpace(text))
            return false;

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/friends/messages/{otherUserId}");
        req.Headers.Add("X-User-Id", userId);
        req.Content = JsonContent.Create(new { text });
        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode;
    }

    public async Task<List<PrivateMessageDto>> GetPrivateMessagesAsync(string otherUserId, int limit = 80)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(otherUserId))
            return new List<PrivateMessageDto>();

        var safeLimit = Math.Clamp(limit, 1, 200);
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/friends/messages/{otherUserId}?limit={safeLimit}");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return new List<PrivateMessageDto>();

        var parsed = await resp.Content.ReadFromJsonAsync<List<PrivateMessageDto>>();
        return parsed ?? new List<PrivateMessageDto>();
    }

    public async Task<List<FriendDirectoryDto>> GetFriendDirectoryAsync()
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return new List<FriendDirectoryDto>();

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/friends/directory");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return new List<FriendDirectoryDto>();

        var parsed = await resp.Content.ReadFromJsonAsync<List<FriendDirectoryDto>>();
        return parsed ?? new List<FriendDirectoryDto>();
    }

    private sealed class AuthResponse
    {
        public string user_id { get; set; } = "";
    }

    private sealed class CreateMealResponse
    {
        public string id { get; set; } = "";
    }

    private static object BuildMealPayload(MealEntry meal, List<MealItem> items)
    {
        return new
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
    }
}

public class BackendMeal
{
    public string id { get; set; } = "";
    public DateTime date_utc { get; set; }
    public string day_key_utc { get; set; } = "";
    public string raw_text { get; set; } = "";
    public string description { get; set; } = "";
    public string ai_notes { get; set; } = "";
    public string photo_url { get; set; } = "";
    public double total_calories { get; set; }
    public double total_carbs_g { get; set; }
    public double total_protein_g { get; set; }
    public double overall_confidence { get; set; }
    public double quality_score { get; set; }
    public string quality_label { get; set; } = "";
    public List<BackendMealItem> items { get; set; } = new();
}

public class BackendMealItem
{
    public string id { get; set; } = "";
    public string meal_entry_id { get; set; } = "";
    public string name { get; set; } = "";
    public double quantity { get; set; }
    public string unit { get; set; } = "";
    public double estimated_grams { get; set; }
    public double calories { get; set; }
    public double carbs_g { get; set; }
    public double protein_g { get; set; }
    public double confidence { get; set; }
}

public class BackendStory
{
    public string meal_id { get; set; } = "";
    public string user_id { get; set; } = "";
    public string display_name { get; set; } = "";
    public string author_email { get; set; } = "";
    public string picture_url { get; set; } = "";
    public DateTime date_utc { get; set; }
    public string raw_text { get; set; } = "";
    public string photo_url { get; set; } = "";
    public double total_calories { get; set; }
    public double total_carbs_g { get; set; }
    public double total_protein_g { get; set; }
    public string quality_label { get; set; } = "";
    public int like_count { get; set; }
    public int comment_count { get; set; }
    public bool liked_by_me { get; set; }
}

public class StoryLikeResultDto
{
    public bool liked { get; set; }
    public int like_count { get; set; }
}

public class StoryCommentDto
{
    public string id { get; set; } = "";
    public string meal_id { get; set; } = "";
    public string user_id { get; set; } = "";
    public string author_name { get; set; } = "";
    public string text { get; set; } = "";
    public DateTime created_at_utc { get; set; }
}

public class PrivateMessageDto
{
    public string id { get; set; } = "";
    public string sender_user_id { get; set; } = "";
    public string recipient_user_id { get; set; } = "";
    public string text { get; set; } = "";
    public DateTime created_at_utc { get; set; }
}

public class FriendDirectoryDto
{
    public string user_id { get; set; } = "";
    public string email { get; set; } = "";
    public string display_name { get; set; } = "";
    public string picture_url { get; set; } = "";
}
