using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using NutritionTracker.Models;

namespace NutritionTracker.Services;

public class BackendSyncService
{
    private const string BackendUserIdKey = "backend_user_id";
    private const string BackendIdentitySubjectKey = "backend_identity_subject";
    private readonly HttpClient _http = new();
    private UserGoals? _cachedGoals;
    private DateTime _cachedGoalsFetchedUtc;
    private List<FriendDirectoryDto>? _cachedFriendDirectory;
    private DateTime _cachedFriendDirectoryFetchedUtc;
    private string _cachedMealsRangeKey = "";
    private List<BackendMeal>? _cachedMealsRange;
    private DateTime _cachedMealsRangeFetchedUtc;
    private string _cachedDailySummaryKey = "";
    private List<BackendMealDailySummary>? _cachedDailySummary;
    private DateTime _cachedDailySummaryFetchedUtc;
    private string _cachedFriendsFeedKey = "";
    private List<BackendStory>? _cachedFriendsFeed;
    private DateTime _cachedFriendsFeedFetchedUtc;

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
        if (!IsConfigured)
            return false;

        var existingUserId = Preferences.Default.Get(BackendUserIdKey, "").Trim();
        var authMethod = Preferences.Default.Get("auth_method", "google");
        if (string.Equals(authMethod, "email", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(existingUserId))
                return false;

            var storedEmailSubject = Preferences.Default.Get(BackendIdentitySubjectKey, "").Trim().ToLowerInvariant();
            var currentEmail = Preferences.Default.Get("profile_email", "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(currentEmail))
                return !string.IsNullOrWhiteSpace(existingUserId);

            if (string.IsNullOrWhiteSpace(storedEmailSubject))
            {
                Preferences.Default.Set(BackendIdentitySubjectKey, currentEmail);
                return true;
            }

            if (string.Equals(storedEmailSubject, currentEmail, StringComparison.Ordinal))
                return true;

            Preferences.Default.Remove(BackendUserIdKey);
            Preferences.Default.Remove(BackendIdentitySubjectKey);
            return false;
        }

        if (string.IsNullOrWhiteSpace(idToken))
            return false;

        var incomingSubject = ExtractJwtSubject(idToken);
        var storedSubject = Preferences.Default.Get(BackendIdentitySubjectKey, "").Trim();

        if (!string.IsNullOrWhiteSpace(existingUserId))
        {
            if (string.IsNullOrWhiteSpace(incomingSubject))
                return true;

            if (string.IsNullOrWhiteSpace(storedSubject))
            {
                Preferences.Default.Set(BackendIdentitySubjectKey, incomingSubject);
                return true;
            }

            if (string.Equals(storedSubject, incomingSubject, StringComparison.Ordinal))
                return true;

            Preferences.Default.Remove(BackendUserIdKey);
            Preferences.Default.Remove(BackendIdentitySubjectKey);
        }

        try
        {
            var payload = new { id_token = idToken };
            var resp = await _http.PostAsJsonAsync($"{ApiBaseUrl}/auth/google", payload);
            if (!resp.IsSuccessStatusCode)
                return false;

            var parsed = await resp.Content.ReadFromJsonAsync<AuthResponse>();
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.user_id))
                return false;

            Preferences.Default.Set(BackendUserIdKey, parsed.user_id);
            if (!string.IsNullOrWhiteSpace(incomingSubject))
                Preferences.Default.Set(BackendIdentitySubjectKey, incomingSubject);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<(bool ok, string message)> RegisterEmailAsync(string email, string password, string? displayName)
    {
        if (!IsConfigured)
            return (false, LocalizationService.T("backend_identity_error"));

        try
        {
            var payload = new { email = (email ?? "").Trim(), password = password ?? "", display_name = displayName ?? "" };
            var resp = await _http.PostAsJsonAsync($"{ApiBaseUrl}/auth/email/register", payload);
            var parsed = await resp.Content.ReadFromJsonAsync<MessageResponseDto>();
            if (!resp.IsSuccessStatusCode)
                return (false, parsed?.detail ?? parsed?.message ?? LocalizationService.T("login_failed"));

            return (true, parsed?.message ?? LocalizationService.T("login_register_success"));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool ok, string message)> VerifyEmailCodeAsync(string email, string code)
    {
        if (!IsConfigured)
            return (false, LocalizationService.T("backend_identity_error"));

        try
        {
            var payload = new { email = (email ?? "").Trim(), code = (code ?? "").Trim() };
            var resp = await _http.PostAsJsonAsync($"{ApiBaseUrl}/auth/email/verify", payload);
            var parsed = await resp.Content.ReadFromJsonAsync<MessageResponseDto>();
            if (!resp.IsSuccessStatusCode)
                return (false, parsed?.detail ?? parsed?.message ?? LocalizationService.T("login_failed"));

            return (true, parsed?.message ?? LocalizationService.T("login_success"));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool ok, string message)> ResendVerificationCodeAsync(string email)
    {
        if (!IsConfigured)
            return (false, LocalizationService.T("backend_identity_error"));

        try
        {
            var payload = new { email = (email ?? "").Trim() };
            var resp = await _http.PostAsJsonAsync($"{ApiBaseUrl}/auth/email/verify/resend", payload);
            var parsed = await resp.Content.ReadFromJsonAsync<MessageResponseDto>();
            if (!resp.IsSuccessStatusCode)
                return (false, parsed?.detail ?? parsed?.message ?? LocalizationService.T("login_failed"));

            return (true, parsed?.message ?? LocalizationService.T("activation_code_resent"));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool ok, string message, string userId, string name)> LoginEmailAsync(string email, string password)
    {
        if (!IsConfigured)
            return (false, LocalizationService.T("backend_identity_error"), "", "");

        try
        {
            var payload = new { email = (email ?? "").Trim(), password = password ?? "" };
            var resp = await _http.PostAsJsonAsync($"{ApiBaseUrl}/auth/email/login", payload);
            var parsed = await resp.Content.ReadFromJsonAsync<EmailLoginResponseDto>();
            if (!resp.IsSuccessStatusCode || parsed == null)
                return (false, parsed?.detail ?? parsed?.message ?? LocalizationService.T("login_failed"), "", "");

            if (string.IsNullOrWhiteSpace(parsed.user_id))
                return (false, LocalizationService.T("login_failed"), "", "");

            Preferences.Default.Set(BackendUserIdKey, parsed.user_id);
            Preferences.Default.Set(BackendIdentitySubjectKey, (email ?? "").Trim().ToLowerInvariant());
            return (true, parsed.message ?? LocalizationService.T("login_success"), parsed.user_id, parsed.name ?? "");
        }
        catch (Exception ex)
        {
            return (false, ex.Message, "", "");
        }
    }

    public async Task<(bool ok, string message)> RequestPasswordResetAsync(string email)
    {
        if (!IsConfigured)
            return (false, LocalizationService.T("backend_identity_error"));

        try
        {
            var payload = new { email = (email ?? "").Trim() };
            var resp = await _http.PostAsJsonAsync($"{ApiBaseUrl}/auth/email/password/forgot", payload);
            var parsed = await resp.Content.ReadFromJsonAsync<MessageResponseDto>();
            if (!resp.IsSuccessStatusCode)
                return (false, parsed?.detail ?? parsed?.message ?? LocalizationService.T("login_failed"));

            return (true, parsed?.message ?? LocalizationService.T("password_reset_sent"));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool ok, string message)> ResetPasswordAsync(string email, string code, string newPassword)
    {
        if (!IsConfigured)
            return (false, LocalizationService.T("backend_identity_error"));

        try
        {
            var payload = new { email = (email ?? "").Trim(), code = (code ?? "").Trim(), new_password = newPassword ?? "" };
            var resp = await _http.PostAsJsonAsync($"{ApiBaseUrl}/auth/email/password/reset", payload);
            var parsed = await resp.Content.ReadFromJsonAsync<MessageResponseDto>();
            if (!resp.IsSuccessStatusCode)
                return (false, parsed?.detail ?? parsed?.message ?? LocalizationService.T("login_failed"));

            return (true, parsed?.message ?? LocalizationService.T("password_reset_done"));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool ok, string message)> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return (false, LocalizationService.T("backend_identity_error"));

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/auth/email/password/change");
            req.Headers.Add("X-User-Id", userId);
            req.Content = JsonContent.Create(new { current_password = currentPassword ?? "", new_password = newPassword ?? "" });
            var resp = await _http.SendAsync(req);
            var parsed = await resp.Content.ReadFromJsonAsync<MessageResponseDto>();
            if (!resp.IsSuccessStatusCode)
                return (false, parsed?.detail ?? parsed?.message ?? LocalizationService.T("login_failed"));

            return (true, parsed?.message ?? LocalizationService.T("password_change_success"));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool ok, string message)> DeleteAccountAsync(string? password)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return (false, LocalizationService.T("backend_identity_error"));

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, $"{ApiBaseUrl}/auth/account");
            req.Headers.Add("X-User-Id", userId);
            req.Content = JsonContent.Create(new { password = password ?? "" });

            var resp = await _http.SendAsync(req);
            var parsed = await resp.Content.ReadFromJsonAsync<MessageResponseDto>();
            if (!resp.IsSuccessStatusCode)
                return (false, parsed?.detail ?? parsed?.message ?? LocalizationService.T("login_failed"));

            return (true, parsed?.message ?? LocalizationService.T("account_delete_success"));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
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
            meal_type = MealTypeService.Normalize(meal.MealType),
            story_visibility = NormalizeStoryVisibility(meal.StoryVisibility),
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

    public async Task<string> GetMealPhotoUrlAsync(string mealId)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(mealId))
            return "";

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/meals/{mealId}/photo");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return "";

        var parsed = await resp.Content.ReadFromJsonAsync<BackendMealPhoto>();
        return parsed?.photo_url ?? "";
    }

    public async Task<List<BackendMeal>> GetMealsBetweenUtcAsync(DateTime fromUtc, DateTime toUtc, bool includePhoto = true)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return new List<BackendMeal>();

        var fromDate = fromUtc.Date.ToString("yyyy-MM-dd");
        var toDate = toUtc.AddSeconds(-1).Date.ToString("yyyy-MM-dd");
        var cacheKey = $"{userId}|{fromDate}|{toDate}";

        if (_cachedMealsRange != null &&
            string.Equals(_cachedMealsRangeKey, cacheKey, StringComparison.Ordinal) &&
            DateTime.UtcNow - _cachedMealsRangeFetchedUtc < TimeSpan.FromSeconds(20))
        {
            return _cachedMealsRange.ToList();
        }

        var includePhotoQuery = includePhoto ? "true" : "false";
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/meals?from={fromDate}&to={toDate}&includePhoto={includePhotoQuery}");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return new List<BackendMeal>();

        var parsed = await resp.Content.ReadFromJsonAsync<List<BackendMeal>>();
        _cachedMealsRangeKey = cacheKey;
        _cachedMealsRangeFetchedUtc = DateTime.UtcNow;
        _cachedMealsRange = parsed ?? new List<BackendMeal>();
        return _cachedMealsRange.ToList();
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
        if (resp.IsSuccessStatusCode)
        {
            _cachedGoals = new UserGoals
            {
                Id = 1,
                CaloriesTarget = goals.CaloriesTarget,
                CarbsGTarget = goals.CarbsGTarget,
                ProteinGTarget = goals.ProteinGTarget,
            };
            _cachedGoalsFetchedUtc = DateTime.UtcNow;
        }
        return resp.IsSuccessStatusCode;
    }

    public async Task<UserGoals> GetGoalsAsync()
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return CreateDefaultGoals();

        if (_cachedGoals != null && DateTime.UtcNow - _cachedGoalsFetchedUtc < TimeSpan.FromSeconds(45))
        {
            return new UserGoals
            {
                Id = _cachedGoals.Id,
                CaloriesTarget = _cachedGoals.CaloriesTarget,
                CarbsGTarget = _cachedGoals.CarbsGTarget,
                ProteinGTarget = _cachedGoals.ProteinGTarget,
            };
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/goals");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return CreateDefaultGoals();

        var parsed = await resp.Content.ReadFromJsonAsync<BackendGoalsDto>();
        if (parsed == null)
            return CreateDefaultGoals();

        var goals = new UserGoals
        {
            Id = 1,
            CaloriesTarget = parsed.calories_target,
            CarbsGTarget = parsed.carbs_g_target,
            ProteinGTarget = parsed.protein_g_target,
        };

        _cachedGoals = goals;
        _cachedGoalsFetchedUtc = DateTime.UtcNow;
        return goals;
    }

    public async Task<List<BackendMealDailySummary>> GetMealDailySummaryAsync(DateTime fromUtc, DateTime toUtc)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return new List<BackendMealDailySummary>();

        var offsetMinutes = (int)Math.Round(TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes);
        var fromEncoded = Uri.EscapeDataString(fromUtc.ToUniversalTime().ToString("O"));
        var toEncoded = Uri.EscapeDataString(toUtc.ToUniversalTime().ToString("O"));
        var cacheKey = $"{userId}|{fromEncoded}|{toEncoded}|{offsetMinutes}";

        if (_cachedDailySummary != null &&
            string.Equals(_cachedDailySummaryKey, cacheKey, StringComparison.Ordinal) &&
            DateTime.UtcNow - _cachedDailySummaryFetchedUtc < TimeSpan.FromSeconds(25))
        {
            return _cachedDailySummary.ToList();
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/meals/daily-summary?fromUtc={fromEncoded}&toUtc={toEncoded}&tzOffsetMinutes={offsetMinutes}");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return new List<BackendMealDailySummary>();

        var parsed = await resp.Content.ReadFromJsonAsync<List<BackendMealDailySummary>>();
        _cachedDailySummaryKey = cacheKey;
        _cachedDailySummaryFetchedUtc = DateTime.UtcNow;
        _cachedDailySummary = parsed ?? new List<BackendMealDailySummary>();
        return _cachedDailySummary.ToList();
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

    public async Task<double> GetWaterIntakeForDayLocalAsync(DateTime dayLocal)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return 0;

        var dayUtc = DateTime.SpecifyKind(dayLocal.Date, DateTimeKind.Local).ToUniversalTime();
        var day = dayUtc.ToString("yyyy-MM-dd");

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/water-intake?day={day}");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return 0;

        var parsed = await resp.Content.ReadFromJsonAsync<BackendWaterIntake>();
        return parsed?.liters ?? 0;
    }

    public async Task<List<BackendWaterPoint>> GetWaterIntakeSeriesAsync(DateTime fromLocalInclusive, DateTime toLocalExclusive)
    {
        var result = new List<BackendWaterPoint>();
        var start = fromLocalInclusive.Date;
        var end = toLocalExclusive.Date;

        for (var day = start; day < end; day = day.AddDays(1))
        {
            var liters = await GetWaterIntakeForDayLocalAsync(day);
            result.Add(new BackendWaterPoint
            {
                DayLocal = day,
                Liters = Math.Max(0, liters)
            });
        }

        return result;
    }

    public async Task<bool> TryInviteFriendAsync(string email)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return false;

        var lang = LocalizationService.CurrentLanguageCode();

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/friends/invites");
        req.Headers.Add("X-User-Id", userId);
        req.Content = JsonContent.Create(new { invitee_email = email, locale = lang });

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

    public async Task<bool> TryDeclineInviteAsync(string inviteId)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(inviteId))
            return false;

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/friends/invites/{inviteId}/decline");
        req.Headers.Add("X-User-Id", userId);
        req.Content = JsonContent.Create(new { });
        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode;
    }

    public async Task<List<IncomingInviteDto>> GetIncomingInvitesAsync()
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return new List<IncomingInviteDto>();

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/friends/invites/incoming");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return new List<IncomingInviteDto>();

        var parsed = await resp.Content.ReadFromJsonAsync<List<IncomingInviteDto>>();
        return parsed ?? new List<IncomingInviteDto>();
    }

    public async Task<List<OutgoingInviteDto>> GetOutgoingInvitesAsync()
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return new List<OutgoingInviteDto>();

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/friends/invites");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return new List<OutgoingInviteDto>();

        var parsed = await resp.Content.ReadFromJsonAsync<List<OutgoingInviteDto>>();
        return parsed ?? new List<OutgoingInviteDto>();
    }

    public async Task<List<FriendDirectoryDto>> SearchFriendUsersAsync(string query, int limit = 20)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return new List<FriendDirectoryDto>();

        var safeQ = Uri.EscapeDataString((query ?? "").Trim());
        if (string.IsNullOrWhiteSpace(safeQ))
            return new List<FriendDirectoryDto>();

        var safeLimit = Math.Clamp(limit, 1, 30);
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/friends/users/search?q={safeQ}&limit={safeLimit}");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return new List<FriendDirectoryDto>();

        var parsed = await resp.Content.ReadFromJsonAsync<List<FriendDirectoryDto>>();
        return parsed ?? new List<FriendDirectoryDto>();
    }

    public async Task<List<BackendStory>> GetFriendsFeedAsync(int days = 2, int limit = 40, bool includePhoto = true)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return new List<BackendStory>();

        var safeDays = Math.Clamp(days, 1, 14);
        var safeLimit = Math.Clamp(limit, 1, 120);
        var cacheKey = $"{userId}|{safeDays}|{safeLimit}";

        if (_cachedFriendsFeed != null &&
            string.Equals(_cachedFriendsFeedKey, cacheKey, StringComparison.Ordinal) &&
            DateTime.UtcNow - _cachedFriendsFeedFetchedUtc < TimeSpan.FromSeconds(15))
        {
            return _cachedFriendsFeed.ToList();
        }

        var includePhotoQuery = includePhoto ? "true" : "false";
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/friends/feed?days={safeDays}&limit={safeLimit}&includePhoto={includePhotoQuery}");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return new List<BackendStory>();

        var parsed = await resp.Content.ReadFromJsonAsync<List<BackendStory>>();
        _cachedFriendsFeedKey = cacheKey;
        _cachedFriendsFeedFetchedUtc = DateTime.UtcNow;
        _cachedFriendsFeed = parsed ?? new List<BackendStory>();
        return _cachedFriendsFeed.ToList();
    }

    public async Task<List<BackendStory>> GetPublicFeedAsync(int days = 14, int limit = 5, bool includePhoto = false)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return new List<BackendStory>();

        var safeDays = Math.Clamp(days, 1, 30);
        var safeLimit = Math.Clamp(limit, 1, 20);
        var includePhotoQuery = includePhoto ? "true" : "false";

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/friends/public-feed?days={safeDays}&limit={safeLimit}&includePhoto={includePhotoQuery}");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return new List<BackendStory>();

        var parsed = await resp.Content.ReadFromJsonAsync<List<BackendStory>>();
        return parsed ?? new List<BackendStory>();
    }

    public async Task<string> GetStoryVisibilityDefaultAsync()
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return NormalizeStoryVisibility(Preferences.Default.Get("story_visibility_default", "friends"));

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/friends/story-visibility-default");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return NormalizeStoryVisibility(Preferences.Default.Get("story_visibility_default", "friends"));

        var parsed = await resp.Content.ReadFromJsonAsync<StoryVisibilityDefaultDto>();
        var resolved = NormalizeStoryVisibility(parsed?.default_story_visibility);
        Preferences.Default.Set("story_visibility_default", resolved);
        return resolved;
    }

    public async Task<bool> TrySetStoryVisibilityDefaultAsync(string visibility)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return false;

        var normalized = NormalizeStoryVisibility(visibility);
        using var req = new HttpRequestMessage(HttpMethod.Put, $"{ApiBaseUrl}/friends/story-visibility-default");
        req.Headers.Add("X-User-Id", userId);
        req.Content = JsonContent.Create(new { default_story_visibility = normalized });
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return false;

        Preferences.Default.Set("story_visibility_default", normalized);
        return true;
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

        if (_cachedFriendDirectory != null && DateTime.UtcNow - _cachedFriendDirectoryFetchedUtc < TimeSpan.FromSeconds(60))
            return _cachedFriendDirectory.Select(CloneFriendDirectory).ToList();

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/friends/directory");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return new List<FriendDirectoryDto>();

        var parsed = await resp.Content.ReadFromJsonAsync<List<FriendDirectoryDto>>();
        _cachedFriendDirectory = (parsed ?? new List<FriendDirectoryDto>()).Select(CloneFriendDirectory).ToList();
        _cachedFriendDirectoryFetchedUtc = DateTime.UtcNow;
        return _cachedFriendDirectory.Select(CloneFriendDirectory).ToList();
    }

    public async Task<List<BackendGamificationEvent>> GetGamificationEventsAsync(int limit = 60)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return new List<BackendGamificationEvent>();

        var safeLimit = Math.Clamp(limit, 1, 200);
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/gamification/events?limit={safeLimit}");
        req.Headers.Add("X-User-Id", userId);

        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return new List<BackendGamificationEvent>();

        var parsed = await resp.Content.ReadFromJsonAsync<List<BackendGamificationEvent>>();
        return parsed ?? new List<BackendGamificationEvent>();
    }

    public async Task<bool> TryPostGamificationEventAsync(string eventType, string title, string message, Dictionary<string, object>? metadata = null)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(eventType))
            return false;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/gamification/events");
            req.Headers.Add("X-User-Id", userId);
            req.Content = JsonContent.Create(new
            {
                event_type = eventType.Trim(),
                title = (title ?? "").Trim(),
                message = (message ?? "").Trim(),
                metadata_json = metadata ?? new Dictionary<string, object>()
            });

            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TryUpdateGamificationStateAsync(
        int? sharedStreakDays = null,
        int? weeklySharedPosts = null,
        string? weeklyMissionStatus = null,
        int? weeklyMissionCompleted = null,
        int? weeklyMissionTarget = null,
        string? leagueTier = null,
        string? seasonKey = null)
    {
        var userId = Preferences.Default.Get("backend_user_id", "");
        if (!IsConfigured || string.IsNullOrWhiteSpace(userId))
            return false;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, $"{ApiBaseUrl}/gamification/season");
            req.Headers.Add("X-User-Id", userId);

            var payload = new Dictionary<string, object>();
            if (sharedStreakDays.HasValue) payload["shared_streak_days"] = Math.Max(0, sharedStreakDays.Value);
            if (weeklySharedPosts.HasValue) payload["weekly_shared_posts"] = Math.Max(0, weeklySharedPosts.Value);
            if (weeklyMissionCompleted.HasValue) payload["weekly_mission_completed"] = Math.Max(0, weeklyMissionCompleted.Value);
            if (weeklyMissionTarget.HasValue) payload["weekly_mission_target"] = Math.Max(1, weeklyMissionTarget.Value);
            if (!string.IsNullOrWhiteSpace(weeklyMissionStatus)) payload["weekly_mission_status"] = weeklyMissionStatus.Trim();
            if (!string.IsNullOrWhiteSpace(leagueTier)) payload["league_tier"] = leagueTier.Trim();
            if (!string.IsNullOrWhiteSpace(seasonKey)) payload["season_key"] = seasonKey.Trim();

            if (payload.Count == 0)
                return true;

            req.Content = JsonContent.Create(payload);
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private sealed class AuthResponse
    {
        public string user_id { get; set; } = "";
    }

    private sealed class EmailLoginResponseDto
    {
        public bool ok { get; set; }
        public string message { get; set; } = "";
        public string user_id { get; set; } = "";
        public string email { get; set; } = "";
        public string name { get; set; } = "";
        public string token { get; set; } = "";
        public string auth_method { get; set; } = "";
        public string detail { get; set; } = "";
    }

    private sealed class MessageResponseDto
    {
        public bool ok { get; set; }
        public string message { get; set; } = "";
        public string detail { get; set; } = "";
    }

    private sealed class CreateMealResponse
    {
        public string id { get; set; } = "";
    }

    private sealed class StoryVisibilityDefaultDto
    {
        public string default_story_visibility { get; set; } = "friends";
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
            meal_type = MealTypeService.Normalize(meal.MealType),
            story_visibility = NormalizeStoryVisibility(meal.StoryVisibility),
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

    public static string NormalizeStoryVisibility(string? value)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant();
        return normalized switch
        {
            "friends" => "friends",
            "public" => "public",
            "self" => "self",
            _ => "friends",
        };
    }

    private static UserGoals CreateDefaultGoals()
    {
        return new UserGoals
        {
            Id = 1,
            CaloriesTarget = 2000,
            CarbsGTarget = 220,
            ProteinGTarget = 120,
        };
    }

    private static FriendDirectoryDto CloneFriendDirectory(FriendDirectoryDto input)
    {
        return new FriendDirectoryDto
        {
            user_id = input.user_id,
            email = input.email,
            display_name = input.display_name,
            picture_url = input.picture_url,
        };
    }

    private static string ExtractJwtSubject(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return "";

        try
        {
            var parts = idToken.Split('.');
            if (parts.Length < 2)
                return "";

            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');

            var pad = payload.Length % 4;
            if (pad > 0)
                payload = payload.PadRight(payload.Length + (4 - pad), '=');

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("sub", out var subEl))
                return (subEl.GetString() ?? "").Trim();

            return "";
        }
        catch
        {
            return "";
        }
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
    public string meal_type { get; set; } = "snack";
    public string story_visibility { get; set; } = "friends";
    public List<BackendMealItem> items { get; set; } = new();
}

public class BackendMealPhoto
{
    public string meal_id { get; set; } = "";
    public string photo_url { get; set; } = "";
}

public class BackendGoalsDto
{
    public double calories_target { get; set; }
    public double carbs_g_target { get; set; }
    public double protein_g_target { get; set; }
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
    public string story_visibility { get; set; } = "friends";
    public int like_count { get; set; }
    public int comment_count { get; set; }
    public bool liked_by_me { get; set; }
    public List<StoryCommentDto> preview_comments { get; set; } = new();
}

public class StoryLikeResultDto
{
    public bool liked { get; set; }
    public int like_count { get; set; }
}

public class BackendMealDailySummary
{
    public string day_key_local { get; set; } = "";
    public int meal_count { get; set; }
    public double total_calories { get; set; }
    public double total_carbs_g { get; set; }
    public double total_protein_g { get; set; }
}

public class BackendWaterIntake
{
    public string day_key_utc { get; set; } = "";
    public double liters { get; set; }
}

public class BackendWaterPoint
{
    public DateTime DayLocal { get; set; }
    public double Liters { get; set; }
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

public class IncomingInviteDto
{
    public string id { get; set; } = "";
    public string inviter_user_id { get; set; } = "";
    public string inviter_display_name { get; set; } = "";
    public string inviter_email { get; set; } = "";
    public string invitee_email { get; set; } = "";
    public string status { get; set; } = "";
    public DateTime created_at_utc { get; set; }
}

public class OutgoingInviteDto
{
    public string id { get; set; } = "";
    public string invitee_email { get; set; } = "";
    public string status { get; set; } = "";
    public DateTime created_at_utc { get; set; }
}

public class BackendGamificationEvent
{
    public string id { get; set; } = "";
    public string event_type { get; set; } = "";
    public string title { get; set; } = "";
    public string message { get; set; } = "";
    public Dictionary<string, object> metadata_json { get; set; } = new();
    public DateTime created_at_utc { get; set; }
}
