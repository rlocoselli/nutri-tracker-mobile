using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Models;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly LocalDb _db;
    private readonly GoogleFitService _googleFit;
    private readonly IServiceProvider _sp;
    private readonly PointsService _points;
    private readonly BackendSyncService _sync;

    [ObservableProperty] private string userPictureUrl = "";
    [ObservableProperty] private string userName = "";
    [ObservableProperty] private string greeting = "Bonjour";

    [ObservableProperty] private string todayCaloriesText = "0 / 0";
    [ObservableProperty] private string todayProteinText = "0 / 0 g";
    [ObservableProperty] private string todayCarbsText = "0 / 0 g";
    [ObservableProperty] private string todayStepsText = "0";
    [ObservableProperty] private string todayBurnText = "0 kcal";
    [ObservableProperty] private string todayNetCaloriesText = "0 kcal";
    [ObservableProperty] private string fitSyncStatusText = "";
    [ObservableProperty] private string streakText = "🔥 0";
    [ObservableProperty] private string streakHintText = "";
    [ObservableProperty] private string balanceStatusText = "";
    [ObservableProperty] private string recordingStreakText = "📝 0";
    [ObservableProperty] private string coinsText = "🪙 0";

    [ObservableProperty] private double caloriesProgress;
    [ObservableProperty] private double proteinProgress;
    [ObservableProperty] private double carbsProgress;

    public string HelloText => LocalizationService.T("hello");
    public string RecordMealText => LocalizationService.T("record_meal_plus");
    public string AdviceText => LocalizationService.T("advice");
    public string GoalsText => LocalizationService.T("goals");
    public string DailySummaryText => LocalizationService.T("daily_summary");
    public string TodayOnlyHintText => LocalizationService.T("dashboard_today_only");
    public string CaloriesLabelText => LocalizationService.T("metric_calories");
    public string ProteinLabelText => LocalizationService.T("metric_proteins");
    public string CarbsLabelText => LocalizationService.T("macro_carbs");
    public string NetLabelText => LocalizationService.T("net_label");
    public string DailySummaryHintText => LocalizationService.T("daily_summary_hint");
    public string StepsLabelText => LocalizationService.T("steps");
    public string BurnLabelText => LocalizationService.T("burned_calories");
    public string StreakTitleText => LocalizationService.T("reward_streak_title");
    public string StreakSubtitleText => LocalizationService.T("reward_streak_subtitle");
    public string BalanceInfoTitleText => LocalizationService.T("balance_info_title");
    public string RecordingStreakTitleText => LocalizationService.T("recording_streak_title");
    public string CoinsTitleText => LocalizationService.T("coins_balance");
    public bool ShowGoogleFitUi => FeatureFlags.EnableGoogleFit;

    public DashboardViewModel(LocalDb db, GoogleFitService googleFit, IServiceProvider sp, PointsService points, BackendSyncService sync)
    {
        _db = db;
        _googleFit = googleFit;
        _sp = sp;
        _points = points;
        _sync = sync;
    }

    public async Task LoadAsync()
    {
        UserName = Preferences.Default.Get("profile_name", "");
        UserPictureUrl = Preferences.Default.Get("profile_picture", "");

        OnPropertyChanged(nameof(HelloText));
        OnPropertyChanged(nameof(RecordMealText));
        OnPropertyChanged(nameof(AdviceText));
        OnPropertyChanged(nameof(GoalsText));
        OnPropertyChanged(nameof(DailySummaryText));
        OnPropertyChanged(nameof(TodayOnlyHintText));
        OnPropertyChanged(nameof(CaloriesLabelText));
        OnPropertyChanged(nameof(ProteinLabelText));
        OnPropertyChanged(nameof(CarbsLabelText));
        OnPropertyChanged(nameof(NetLabelText));
        OnPropertyChanged(nameof(DailySummaryHintText));
        OnPropertyChanged(nameof(StepsLabelText));
        OnPropertyChanged(nameof(BurnLabelText));
        OnPropertyChanged(nameof(StreakTitleText));
        OnPropertyChanged(nameof(StreakSubtitleText));
        OnPropertyChanged(nameof(BalanceInfoTitleText));
        OnPropertyChanged(nameof(RecordingStreakTitleText));
        OnPropertyChanged(nameof(CoinsTitleText));
        OnPropertyChanged(nameof(ShowGoogleFitUi));

        var accessToken = Preferences.Default.Get("auth_access_token", "");
        if (!GoogleFitService.Enabled)
        {
            FitSyncStatusText = "";
        }
        else if (!string.IsNullOrWhiteSpace(accessToken))
        {
            try
            {
                var fit = await _googleFit.GetTodaySummaryAsync(accessToken);
                await _db.UpsertGoogleFitDailyAsync(DateTime.Now.Date, fit.steps, fit.burnedCalories);
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

        var goals = await _db.GetGoalsAsync();
        var mealsToday = await GetMealsForRangeAsync(fromUtc, toUtc);
        var cal = mealsToday.Sum(m => m.TotalCalories);
        var carbs = mealsToday.Sum(m => m.TotalCarbsG);
        var prot = mealsToday.Sum(m => m.TotalProteinG);

        var exercise = await _db.GetExerciseTotalsBetweenUtcAsync(fromUtc, toUtc);

        TodayCaloriesText = $"{Math.Round(cal)} / {Math.Round(goals.CaloriesTarget)}";
        TodayProteinText  = $"{Math.Round(prot)} / {Math.Round(goals.ProteinGTarget)} g";
        TodayCarbsText    = $"{Math.Round(carbs)} / {Math.Round(goals.CarbsGTarget)} g";
        TodayStepsText = exercise.steps.ToString();
        TodayBurnText = $"{Math.Round(exercise.burnedCalories)} kcal";
        TodayNetCaloriesText = string.Format(LocalizationService.T("net_calories"), $"{Math.Round(cal - exercise.burnedCalories)} kcal");

        CaloriesProgress = goals.CaloriesTarget <= 0 ? 0 : Math.Min(1, cal / goals.CaloriesTarget);
        ProteinProgress  = goals.ProteinGTarget <= 0 ? 0 : Math.Min(1, prot / goals.ProteinGTarget);
        CarbsProgress    = goals.CarbsGTarget <= 0 ? 0 : Math.Min(1, carbs / goals.CarbsGTarget);

        var balancedToday = DailyRewardService.IsBalancedDay(goals, cal, carbs, prot, exercise.burnedCalories);
        var streak = await DailyRewardService.ComputeCurrentStreakAsync(goals, async dayLocal =>
        {
            var start = DateTime.SpecifyKind(dayLocal.Date, DateTimeKind.Local).ToUniversalTime();
            var end = DateTime.SpecifyKind(dayLocal.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

            var meals = await GetMealsForRangeAsync(start, end);
            var ex = await _db.GetExerciseTotalsBetweenUtcAsync(start, end);
            return (meals.Sum(x => x.TotalCalories), meals.Sum(x => x.TotalCarbsG), meals.Sum(x => x.TotalProteinG), ex.burnedCalories);
        });

        var dayWord = Preferences.Default.Get("app_lang", "fr") switch
        {
            "en" => "days",
            "pt" => "dias",
            "es" => "días",
            _ => "jours",
        };
        StreakText = $"🔥 {streak} {dayWord}";
        StreakHintText = balancedToday
            ? LocalizationService.T("reward_balanced_today")
            : LocalizationService.T("reward_not_balanced_today");
        BalanceStatusText = balancedToday
            ? LocalizationService.T("reward_status_good")
            : LocalizationService.T("reward_status_pending");

        var loggingStreak = await ComputeRecordingStreakAsync();
        RecordingStreakText = $"📝 {loggingStreak} {dayWord}";
        CoinsText = $"🪙 {_points.GetBalance()}";
    }

    private async Task<int> ComputeRecordingStreakAsync(int maxDays = 30)
    {
        var streak = 0;
        for (var i = 0; i < maxDays; i++)
        {
            var dayLocal = DateTime.Now.Date.AddDays(-i);
            var fromUtc = DateTime.SpecifyKind(dayLocal, DateTimeKind.Local).ToUniversalTime();
            var toUtc = DateTime.SpecifyKind(dayLocal.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            var meals = await GetMealsForRangeAsync(fromUtc, toUtc);
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

    private async Task<List<MealEntry>> GetMealsForRangeAsync(DateTime fromUtc, DateTime toUtc)
    {
        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        if (identityOk)
        {
            var backendMeals = await _sync.GetMealsBetweenUtcAsync(fromUtc, toUtc);
            return backendMeals.Select(ToMealEntry).ToList();
        }

        return await _db.GetMealsBetweenUtcAsync(fromUtc, toUtc);
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
