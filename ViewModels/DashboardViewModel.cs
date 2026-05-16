using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using NutritionTracker.Models;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly GoogleFitService _googleFit;
    private readonly IServiceProvider _sp;
    private readonly PointsService _points;
    private readonly BackendSyncService _sync;
    private readonly WeeklyMissionService _missions;

    [ObservableProperty] private string userPictureUrl = "";
    [ObservableProperty] private string userName = "";
    [ObservableProperty] private string greeting = "Bonjour";
    public ImageSource ProfilePhotoSource => string.IsNullOrWhiteSpace(UserPictureUrl)
        ? ImageSource.FromFile("ic_profile.svg")
        : ImageSource.FromUri(new Uri(UserPictureUrl));

    partial void OnUserPictureUrlChanged(string value)
    {
        OnPropertyChanged(nameof(ProfilePhotoSource));
    }

    [ObservableProperty] private string todayCaloriesText = "0 / 0";
    [ObservableProperty] private string todayProteinText = "0 / 0 g";
    [ObservableProperty] private string todayCarbsText = "0 / 0 g";
    [ObservableProperty] private string todayStepsText = "0";
    [ObservableProperty] private string todayBurnText = "0 kcal";
    [ObservableProperty] private string todayNetCaloriesText = "0 kcal";
    [ObservableProperty] private string fitSyncStatusText = "";
    [ObservableProperty] private string streakText = "0 jours";
    [ObservableProperty] private string streakHintText = "";
    [ObservableProperty] private string balanceStatusText = "";
    [ObservableProperty] private string recordingStreakText = "0 jours";
    [ObservableProperty] private string coinsText = "0";
    [ObservableProperty] private string gamificationScoreText = "0 XP";
    [ObservableProperty] private string gamificationLevelText = "1";
    [ObservableProperty] private string weeklyMissionStatusText = "";
    [ObservableProperty] private bool isLoading;

    [ObservableProperty] private double caloriesProgress;
    [ObservableProperty] private double proteinProgress;
    [ObservableProperty] private double carbsProgress;
    [ObservableProperty] private IList<double> macroChartValues = Array.Empty<double>();
    [ObservableProperty] private IList<string> macroChartLabels = Array.Empty<string>();
    [ObservableProperty] private IList<double> goalChartValues = Array.Empty<double>();
    [ObservableProperty] private IList<string> goalChartLabels = Array.Empty<string>();
    public IList<WeeklyMissionItem> WeeklyMissions { get; private set; } = Array.Empty<WeeklyMissionItem>();
    public ObservableCollection<DashboardPublicStoryItem> PublicStories { get; } = new();

    public string HelloText => LocalizationService.T("hello");
    public string HomeTitleText => LocalizationService.T("dashboard_title");
    public string RecordMealText => LocalizationService.T("record_meal_plus");
    public string AdviceText => LocalizationService.T("advice");
    public string GoalsText => LocalizationService.T("goals");
    public string DailySummaryText => LocalizationService.T("daily_summary");
    public string TodayOnlyHintText => LocalizationService.T("dashboard_today_only");
    public string KpiSectionTitle => LocalizationService.T("dashboard_kpis_title");
    public string CaloriesLabelText => LocalizationService.T("metric_calories");
    public string ProteinLabelText => LocalizationService.T("metric_proteins");
    public string CarbsLabelText => LocalizationService.T("macro_carbs");
    public string FatLabelText => LocalizationService.T("macro_fat");
    public string NetLabelText => LocalizationService.T("net_label");
    public string MacroChartTitle => LocalizationService.T("dashboard_macro_chart_title");
    public string MacroLegendTitle => LocalizationService.T("dashboard_macro_legend_title");
    public string GoalChartTitle => LocalizationService.T("dashboard_goal_chart_title");
    public string LoadingText => LocalizationService.T("main_loading");
    public string DailySummaryHintText => LocalizationService.T("daily_summary_hint");
    public string StepsLabelText => LocalizationService.T("steps");
    public string BurnLabelText => LocalizationService.T("burned_calories");
    public string StreakTitleText => LocalizationService.T("reward_streak_title");
    public string StreakSubtitleText => LocalizationService.T("reward_streak_subtitle");
    public string BalanceInfoTitleText => LocalizationService.T("balance_info_title");
    public string RecordingStreakTitleText => LocalizationService.T("recording_streak_title");
    public string CoinsTitleText => LocalizationService.T("coins_balance");
    public string GamificationTitleText => LocalizationService.T("dashboard_gamification_title");
    public string GamificationScoreLabelText => LocalizationService.T("dashboard_gamification_score");
    public string GamificationLevelLabelText => LocalizationService.T("dashboard_gamification_level");
    public string WeeklyMissionTitleText => LocalizationService.T("weekly_mission_title");
    public string PublicStoriesTitleText => LocalizationService.T("dashboard_public_stories_title");
    public string PublicStoriesSubtitleText => LocalizationService.T("dashboard_public_stories_subtitle");
    public string PublicStoriesEmptyText => LocalizationService.T("dashboard_public_stories_empty");
    public string AddFriendQuickText => LocalizationService.T("dashboard_add_friend_quick");
    public bool HasPublicStories => PublicStories.Count > 0;
    public bool IsPublicStoriesEmpty => !HasPublicStories;
    public bool ShowGoogleFitUi => FeatureFlags.EnableGoogleFit;

    public DashboardViewModel(GoogleFitService googleFit, IServiceProvider sp, PointsService points, BackendSyncService sync, WeeklyMissionService missions)
    {
        _googleFit = googleFit;
        _sp = sp;
        _points = points;
        _sync = sync;
        _missions = missions;
    }

    public async Task LoadAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
        UserName = Preferences.Default.Get("profile_name", "");
        UserPictureUrl = Preferences.Default.Get("profile_picture", "");

        OnPropertyChanged(nameof(HelloText));
        OnPropertyChanged(nameof(HomeTitleText));
        OnPropertyChanged(nameof(RecordMealText));
        OnPropertyChanged(nameof(AdviceText));
        OnPropertyChanged(nameof(GoalsText));
        OnPropertyChanged(nameof(DailySummaryText));
        OnPropertyChanged(nameof(TodayOnlyHintText));
        OnPropertyChanged(nameof(KpiSectionTitle));
        OnPropertyChanged(nameof(CaloriesLabelText));
        OnPropertyChanged(nameof(ProteinLabelText));
        OnPropertyChanged(nameof(CarbsLabelText));
        OnPropertyChanged(nameof(FatLabelText));
        OnPropertyChanged(nameof(NetLabelText));
        OnPropertyChanged(nameof(MacroChartTitle));
        OnPropertyChanged(nameof(MacroLegendTitle));
        OnPropertyChanged(nameof(GoalChartTitle));
        OnPropertyChanged(nameof(LoadingText));
        OnPropertyChanged(nameof(DailySummaryHintText));
        OnPropertyChanged(nameof(StepsLabelText));
        OnPropertyChanged(nameof(BurnLabelText));
        OnPropertyChanged(nameof(StreakTitleText));
        OnPropertyChanged(nameof(StreakSubtitleText));
        OnPropertyChanged(nameof(BalanceInfoTitleText));
        OnPropertyChanged(nameof(RecordingStreakTitleText));
        OnPropertyChanged(nameof(CoinsTitleText));
        OnPropertyChanged(nameof(GamificationTitleText));
        OnPropertyChanged(nameof(GamificationScoreLabelText));
        OnPropertyChanged(nameof(GamificationLevelLabelText));
        OnPropertyChanged(nameof(WeeklyMissionTitleText));
        OnPropertyChanged(nameof(PublicStoriesTitleText));
        OnPropertyChanged(nameof(PublicStoriesSubtitleText));
        OnPropertyChanged(nameof(PublicStoriesEmptyText));
        OnPropertyChanged(nameof(AddFriendQuickText));
        OnPropertyChanged(nameof(HasPublicStories));
        OnPropertyChanged(nameof(IsPublicStoriesEmpty));
        OnPropertyChanged(nameof(ShowGoogleFitUi));

        var accessToken = Preferences.Default.Get("auth_access_token", "");
        var todaySteps = 0;
        var todayBurnedCalories = 0d;
        if (!GoogleFitService.Enabled)
        {
            FitSyncStatusText = "";
        }
        else if (!string.IsNullOrWhiteSpace(accessToken))
        {
            try
            {
                var fit = await _googleFit.GetTodaySummaryAsync(accessToken);
                todaySteps = fit.steps;
                todayBurnedCalories = fit.burnedCalories;
                FitSyncStatusText = LocalizationService.T("sync_ok");
            }
            catch (Exception ex)
            {
                FitSyncStatusText = $"{LocalizationService.T("sync_error")}: {ex.Message}";
            }
        }
        else
        {
            FitSyncStatusText = LocalizationService.T("sync_no_token");
        }

        var todayLocal = DateTime.Now.Date;
        var fromUtc = DateTime.SpecifyKind(todayLocal, DateTimeKind.Local).ToUniversalTime();
        var toUtc = DateTime.SpecifyKind(todayLocal.AddDays(1), DateTimeKind.Local).ToUniversalTime();
        var streakWindowStartLocal = todayLocal.AddDays(-29);
        var streakWindowFromUtc = DateTime.SpecifyKind(streakWindowStartLocal, DateTimeKind.Local).ToUniversalTime();

        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        var goalsTask = _sync.GetGoalsAsync();
        var mealsTodayTask = GetMealsForRangeAsync(fromUtc, toUtc, identityAlreadyEnsured: identityOk);
        var dailySummaryTask = _sync.GetMealDailySummaryAsync(streakWindowFromUtc, toUtc);
        await Task.WhenAll(goalsTask, mealsTodayTask, dailySummaryTask);

        var goals = goalsTask.Result;
        var mealsToday = mealsTodayTask.Result;
        var dailySummaries = dailySummaryTask.Result;
        var summaryByDay = dailySummaries
            .Where(x => !string.IsNullOrWhiteSpace(x.day_key_local))
            .ToDictionary(x => x.day_key_local, x => x, StringComparer.Ordinal);
        var cal = mealsToday.Sum(m => m.TotalCalories);
        var carbs = mealsToday.Sum(m => m.TotalCarbsG);
        var prot = mealsToday.Sum(m => m.TotalProteinG);

        TodayCaloriesText = $"{Math.Round(cal)} / {Math.Round(goals.CaloriesTarget)}";
        TodayProteinText  = $"{Math.Round(prot)} / {Math.Round(goals.ProteinGTarget)} g";
        TodayCarbsText    = $"{Math.Round(carbs)} / {Math.Round(goals.CarbsGTarget)} g";
        TodayStepsText = todaySteps.ToString();
        TodayBurnText = $"{Math.Round(todayBurnedCalories)} kcal";
        TodayNetCaloriesText = string.Format(LocalizationService.T("net_calories"), $"{Math.Round(cal - todayBurnedCalories)} kcal");

        CaloriesProgress = goals.CaloriesTarget <= 0 ? 0 : Math.Min(1, cal / goals.CaloriesTarget);
        ProteinProgress  = goals.ProteinGTarget <= 0 ? 0 : Math.Min(1, prot / goals.ProteinGTarget);
        CarbsProgress    = goals.CarbsGTarget <= 0 ? 0 : Math.Min(1, carbs / goals.CarbsGTarget);

        var proteinKcal = Math.Max(0, prot * 4);
        var carbsKcal = Math.Max(0, carbs * 4);
        var fatKcal = Math.Max(0, cal - proteinKcal - carbsKcal);
        MacroChartValues = new List<double> { proteinKcal, carbsKcal, fatKcal };
        MacroChartLabels = new List<string>
        {
            LocalizationService.T("macro_protein"),
            LocalizationService.T("macro_carbs"),
            LocalizationService.T("macro_fat")
        };

        GoalChartValues = new List<double>
        {
            Math.Round(CaloriesProgress * 100),
            Math.Round(ProteinProgress * 100),
            Math.Round(CarbsProgress * 100)
        };
        GoalChartLabels = new List<string>
        {
            LocalizationService.T("metric_calories"),
            LocalizationService.T("metric_proteins"),
            LocalizationService.T("macro_carbs")
        };

        var dayWord = Preferences.Default.Get("app_lang", "fr") switch
        {
            "en" => "days",
            "pt" => "dias",
            "es" => "días",
            _ => "jours",
        };

        try
        {
            var balancedToday = DailyRewardService.IsBalancedDay(goals, cal, carbs, prot, todayBurnedCalories);
            var streak = summaryByDay.Count > 0
                ? ComputeBalancedStreak(goals, todayLocal, summaryByDay)
                : await DailyRewardService.ComputeCurrentStreakAsync(goals, async dayLocal =>
                {
                    var start = DateTime.SpecifyKind(dayLocal.Date, DateTimeKind.Local).ToUniversalTime();
                    var end = DateTime.SpecifyKind(dayLocal.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

                    var meals = await GetMealsForRangeAsync(start, end, identityAlreadyEnsured: identityOk);
                    return (meals.Sum(x => x.TotalCalories), meals.Sum(x => x.TotalCarbsG), meals.Sum(x => x.TotalProteinG), 0d);
                });

            StreakText = $"{streak} {dayWord}";
            StreakHintText = balancedToday
                ? LocalizationService.T("reward_balanced_today")
                : LocalizationService.T("reward_not_balanced_today");
            BalanceStatusText = balancedToday
                ? LocalizationService.T("reward_status_good")
                : LocalizationService.T("reward_status_pending");
        }
        catch
        {
            StreakText = $"0 {dayWord}";
            StreakHintText = LocalizationService.T("reward_not_balanced_today");
            BalanceStatusText = LocalizationService.T("reward_status_pending");
        }

        try
        {
            var loggingStreak = summaryByDay.Count > 0
                ? ComputeRecordingStreak(todayLocal, summaryByDay)
                : await ComputeRecordingStreakAsync(identityAlreadyEnsured: identityOk);
            RecordingStreakText = $"{loggingStreak} {dayWord}";
        }
        catch
        {
            RecordingStreakText = $"0 {dayWord}";
        }

        try
        {
            CoinsText = _points.GetBalance().ToString();
        }
        catch
        {
            CoinsText = "0";
        }

        try
        {
            var events = await _sync.GetGamificationEventsAsync(limit: 120);
            var eventsXp = events.Sum(ExtractPointsFromEvent);
            var totalXp = Math.Max(_points.GetBalance(), eventsXp);
            var level = (totalXp / 100) + 1;
            GamificationScoreText = $"{totalXp} XP";
            GamificationLevelText = level.ToString();
        }
        catch
        {
            var totalXp = Math.Max(0, _points.GetBalance());
            var level = (totalXp / 100) + 1;
            GamificationScoreText = $"{totalXp} XP";
            GamificationLevelText = level.ToString();
        }

        try
        {
            var startOfWeekLocal = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday);
            if (DateTime.Today.DayOfWeek == DayOfWeek.Sunday)
                startOfWeekLocal = DateTime.Today.AddDays(-6);

            var startWeekUtc = DateTime.SpecifyKind(startOfWeekLocal.Date, DateTimeKind.Local).ToUniversalTime();
            var nowUtc = DateTime.UtcNow;
            var weekMeals = await GetMealsForRangeAsync(startWeekUtc, nowUtc.AddMinutes(1), identityAlreadyEnsured: identityOk);

            var missionState = _missions.BuildState(weekMeals, goals);
            WeeklyMissions = missionState.Missions.ToList();
            WeeklyMissionStatusText = missionState.StatusText;
            if (missionState.BonusAwarded && missionState.BonusPoints > 0)
            {
                _points.Award(missionState.BonusPoints);
                CoinsText = _points.GetBalance().ToString();
            }
            OnPropertyChanged(nameof(WeeklyMissions));
        }
        catch
        {
            WeeklyMissions = Array.Empty<WeeklyMissionItem>();
            WeeklyMissionStatusText = "";
            OnPropertyChanged(nameof(WeeklyMissions));
        }

        await LoadPublicStoriesAsync(identityAlreadyEnsured: identityOk);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static int ComputeBalancedStreak(UserGoals goals, DateTime todayLocal, IReadOnlyDictionary<string, BackendMealDailySummary> summaryByDay, int maxDays = 30)
    {
        var streak = 0;
        for (var i = 0; i < maxDays; i++)
        {
            var dayLocal = todayLocal.AddDays(-i);
            var key = dayLocal.ToString("yyyy-MM-dd");
            if (!summaryByDay.TryGetValue(key, out var summary) || summary.meal_count <= 0)
                break;

            if (!DailyRewardService.IsBalancedDay(goals, summary.total_calories, summary.total_carbs_g, summary.total_protein_g, 0d))
                break;

            streak++;
        }

        return streak;
    }

    private static int ComputeRecordingStreak(DateTime todayLocal, IReadOnlyDictionary<string, BackendMealDailySummary> summaryByDay, int maxDays = 30)
    {
        var streak = 0;
        for (var i = 0; i < maxDays; i++)
        {
            var dayLocal = todayLocal.AddDays(-i);
            var key = dayLocal.ToString("yyyy-MM-dd");
            if (!summaryByDay.TryGetValue(key, out var summary) || summary.meal_count <= 0)
                break;

            streak++;
        }

        return streak;
    }

    private static int ExtractPointsFromEvent(BackendGamificationEvent ev)
    {
        if (ev.metadata_json == null || ev.metadata_json.Count == 0)
            return 0;

        if (TryReadInt(ev.metadata_json, "points_earned", out var pointsEarned))
            return Math.Max(0, pointsEarned);
        if (TryReadInt(ev.metadata_json, "points", out var points))
            return Math.Max(0, points);
        if (TryReadInt(ev.metadata_json, "xp", out var xp))
            return Math.Max(0, xp);

        return 0;
    }

    private static bool TryReadInt(Dictionary<string, object> metadata, string key, out int value)
    {
        value = 0;
        if (!metadata.TryGetValue(key, out var raw) || raw == null)
            return false;

        switch (raw)
        {
            case int intVal:
                value = intVal;
                return true;
            case long longVal:
                value = (int)longVal;
                return true;
            case double doubleVal:
                value = (int)Math.Round(doubleVal);
                return true;
            case decimal decimalVal:
                value = (int)Math.Round(decimalVal);
                return true;
            case JsonElement json:
                if (json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out var jsonInt))
                {
                    value = jsonInt;
                    return true;
                }
                if (json.ValueKind == JsonValueKind.String && int.TryParse(json.GetString(), out var parsed))
                {
                    value = parsed;
                    return true;
                }
                return false;
            default:
                return int.TryParse(raw.ToString(), out value);
        }
    }

    private async Task<int> ComputeRecordingStreakAsync(int maxDays = 30, bool identityAlreadyEnsured = false)
    {
        var streak = 0;
        for (var i = 0; i < maxDays; i++)
        {
            var dayLocal = DateTime.Now.Date.AddDays(-i);
            var fromUtc = DateTime.SpecifyKind(dayLocal, DateTimeKind.Local).ToUniversalTime();
            var toUtc = DateTime.SpecifyKind(dayLocal.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            var meals = await GetMealsForRangeAsync(fromUtc, toUtc, identityAlreadyEnsured);
            if (meals.Count == 0)
                break;
            streak++;
        }

        return streak;
    }

    [RelayCommand]
    private async Task AddMeal()
    {
        await Shell.Current.GoToAsync("//add");
    }

    [RelayCommand]
    private async Task OpenProfile()
    {
        await Shell.Current.GoToAsync("//profile");
    }

    [RelayCommand]
    private async Task OpenMessages()
    {
        await Shell.Current.GoToAsync("//friends");
    }

    [RelayCommand]
    private async Task OpenNotifications()
    {
        await Shell.Current.GoToAsync("//stories");
    }

    [RelayCommand]
    private async Task OpenGoals()
    {
        await Shell.Current.GoToAsync("//goals");
    }

    [RelayCommand]
    private async Task OpenRecommendations()
    {
        await Shell.Current.Navigation.PushAsync(_sp.GetRequiredService<Pages.RecommendationsPage>());
    }

    [RelayCommand]
    private async Task ShowBalanceInfo()
    {
        await Application.Current!.MainPage!.DisplayAlert(
            BalanceInfoTitleText,
            LocalizationService.T("balance_info_body"),
            "OK");
    }

    [RelayCommand]
    private async Task AddPublicStoryAuthor(DashboardPublicStoryItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.AuthorEmail) || item.IsInvited)
            return;

        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        if (!identityOk)
        {
            await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("friends_title"), LocalizationService.T("friend_action_signin_needed"), "OK");
            return;
        }

        var sent = await _sync.TryInviteFriendAsync(item.AuthorEmail);
        if (sent)
        {
            item.IsInvited = true;
            await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("friends_title"), LocalizationService.T("invite_sent"), "OK");
            return;
        }

        await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("friends_title"), LocalizationService.T("invite_send_failed"), "OK");
    }

    private async Task LoadPublicStoriesAsync(bool identityAlreadyEnsured)
    {
        PublicStories.Clear();
        OnPropertyChanged(nameof(HasPublicStories));
        OnPropertyChanged(nameof(IsPublicStoriesEmpty));

        if (!identityAlreadyEnsured)
        {
            var token = Preferences.Default.Get("auth_id_token", "");
            var identityOk = await _sync.EnsureBackendIdentityAsync(token);
            if (!identityOk)
                return;
        }

        var lang = Preferences.Default.Get("app_lang", "fr");
        var rows = await _sync.GetPublicFeedAsync(days: 21, limit: 5, includePhoto: false);
        foreach (var row in rows)
        {
            var fallback = DashboardStoryPhotoSourceHelper.Build(MealIllustrationService.GenerateDataUri(row.raw_text, null, lang))
                ?? ImageSource.FromFile("ic_profile.svg");

            var item = new DashboardPublicStoryItem
            {
                MealId = row.meal_id,
                AuthorName = ResolvePublicAuthorName(row),
                AuthorEmail = (row.author_email ?? "").Trim().ToLowerInvariant(),
                Caption = string.IsNullOrWhiteSpace(row.raw_text) ? LocalizationService.T("story_meal") : row.raw_text,
                PhotoSource = fallback,
                NutritionText = $"{Math.Round(row.total_calories)} kcal · P {Math.Round(row.total_protein_g)}g · C {Math.Round(row.total_carbs_g)}g",
            };

            PublicStories.Add(item);
        }

        OnPropertyChanged(nameof(HasPublicStories));
        OnPropertyChanged(nameof(IsPublicStoriesEmpty));

        foreach (var item in PublicStories)
        {
            if (string.IsNullOrWhiteSpace(item.MealId))
                continue;

            var raw = await _sync.GetMealPhotoUrlAsync(item.MealId);
            var source = DashboardStoryPhotoSourceHelper.Build(raw);
            if (source != null)
                item.PhotoSource = source;
        }
    }

    private static string ResolvePublicAuthorName(BackendStory story)
    {
        var name = (story.display_name ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, "new user", StringComparison.OrdinalIgnoreCase))
            return name;

        var email = (story.author_email ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(email) && email.Contains('@'))
            return email.Split('@')[0];

        return LocalizationService.T("story_default_author");
    }

    private async Task<List<MealEntry>> GetMealsForRangeAsync(DateTime fromUtc, DateTime toUtc, bool identityAlreadyEnsured = false)
    {
        if (!identityAlreadyEnsured)
        {
            var token = Preferences.Default.Get("auth_id_token", "");
            var identityOk = await _sync.EnsureBackendIdentityAsync(token);
            if (!identityOk)
                return new List<MealEntry>();
        }

        var backendMeals = await _sync.GetMealsBetweenUtcAsync(fromUtc, toUtc, includePhoto: false);
        return backendMeals
            .Select(ToMealEntry)
            .Where(x => x.DateUtc >= fromUtc && x.DateUtc < toUtc)
            .ToList();
    }

    private static MealEntry ToMealEntry(BackendMeal meal)
    {
        var dateUtc = meal.date_utc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(meal.date_utc, DateTimeKind.Utc)
            : meal.date_utc.ToUniversalTime();

        return new MealEntry
        {
            Id = meal.id,
            DateUtc = dateUtc,
            DayKeyUtc = string.IsNullOrWhiteSpace(meal.day_key_utc) ? dateUtc.ToString("yyyy-MM-dd") : meal.day_key_utc,
            RawText = meal.raw_text,
            Description = meal.description,
            AiNotes = meal.ai_notes,
            PhotoPath = meal.photo_url,
            TotalCalories = meal.total_calories,
            TotalCarbsG = meal.total_carbs_g,
            TotalProteinG = meal.total_protein_g,
            OverallConfidence = meal.overall_confidence,
            QualityScore = meal.quality_score,
            QualityLabel = meal.quality_label,
        };
    }
}

public partial class DashboardPublicStoryItem : ObservableObject
{
    public string MealId { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string AuthorEmail { get; set; } = "";
    public string Caption { get; set; } = "";
    public string NutritionText { get; set; } = "";

    [ObservableProperty] private ImageSource photoSource = ImageSource.FromFile("ic_profile.svg");
    [ObservableProperty] private bool isInvited;
}

internal static class DashboardStoryPhotoSourceHelper
{
    public static ImageSource? Build(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var value = raw.Trim();

        if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = value.IndexOf(',');
            if (commaIndex > 0 && commaIndex < value.Length - 1)
            {
                var base64 = value[(commaIndex + 1)..];
                try
                {
                    var bytes = Convert.FromBase64String(base64);
                    return ImageSource.FromStream(() => new MemoryStream(bytes));
                }
                catch
                {
                    return null;
                }
            }
        }

        if ((value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            && Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return ImageSource.FromUri(uri);
        }

        if (File.Exists(value))
            return ImageSource.FromFile(value);

        return null;
    }
}
