using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public string HelloText => LocalizationService.T("hello");
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
    public string WeeklyMissionTitleText => LocalizationService.T("weekly_mission_title");
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
        OnPropertyChanged(nameof(WeeklyMissionTitleText));
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

        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        var goalsTask = _sync.GetGoalsAsync();
        var mealsTodayTask = GetMealsForRangeAsync(fromUtc, toUtc, identityAlreadyEnsured: identityOk);
        await Task.WhenAll(goalsTask, mealsTodayTask);

        var goals = goalsTask.Result;
        var mealsToday = mealsTodayTask.Result;
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
            var streak = await DailyRewardService.ComputeCurrentStreakAsync(goals, async dayLocal =>
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
            var loggingStreak = await ComputeRecordingStreakAsync(identityAlreadyEnsured: identityOk);
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
        }
        finally
        {
            IsLoading = false;
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

    private async Task<List<MealEntry>> GetMealsForRangeAsync(DateTime fromUtc, DateTime toUtc, bool identityAlreadyEnsured = false)
    {
        if (!identityAlreadyEnsured)
        {
            var token = Preferences.Default.Get("auth_id_token", "");
            var identityOk = await _sync.EnsureBackendIdentityAsync(token);
            if (!identityOk)
                return new List<MealEntry>();
        }

        var backendMeals = await _sync.GetMealsBetweenUtcAsync(fromUtc, toUtc);
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
