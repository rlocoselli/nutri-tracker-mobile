using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly LocalDb _db;
    private readonly GoogleFitService _googleFit;
    private readonly IServiceProvider _sp;

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

    [ObservableProperty] private double caloriesProgress;
    [ObservableProperty] private double proteinProgress;
    [ObservableProperty] private double carbsProgress;

    public string HelloText => LocalizationService.T("hello");
    public string RecordMealText => LocalizationService.T("record_meal_plus");
    public string AdviceText => LocalizationService.T("advice");
    public string GoalsText => LocalizationService.T("goals");
    public string DailySummaryText => LocalizationService.T("daily_summary");
    public string DailySummaryHintText => LocalizationService.T("daily_summary_hint");
    public string StepsLabelText => LocalizationService.T("steps");
    public string BurnLabelText => LocalizationService.T("burned_calories");

    public DashboardViewModel(LocalDb db, GoogleFitService googleFit, IServiceProvider sp)
    {
        _db = db;
        _googleFit = googleFit;
        _sp = sp;
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
        OnPropertyChanged(nameof(DailySummaryHintText));
        OnPropertyChanged(nameof(StepsLabelText));
        OnPropertyChanged(nameof(BurnLabelText));

        var accessToken = Preferences.Default.Get("auth_access_token", "");
        if (!string.IsNullOrWhiteSpace(accessToken))
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

        var goals = await _db.GetGoalsAsync();
        var (cal, carbs, prot) = await _db.GetTotalsForDayUtcAsync(DateTime.UtcNow.Date);

        var todayLocal = DateTime.Now.Date;
        var fromUtc = DateTime.SpecifyKind(todayLocal, DateTimeKind.Local).ToUniversalTime();
        var toUtc = DateTime.SpecifyKind(todayLocal.AddDays(1), DateTimeKind.Local).ToUniversalTime();
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
}
