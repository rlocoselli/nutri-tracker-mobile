using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Models;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class GoalsViewModel : ObservableObject
{
    private readonly PointsService _points;
    private readonly BackendSyncService _sync;

    [ObservableProperty] private double caloriesTarget = 2000;
    [ObservableProperty] private double proteinGTarget = 120;
    [ObservableProperty] private double carbsGTarget = 220;

    [ObservableProperty] private double caloriesProgress;
    [ObservableProperty] private double proteinProgress;
    [ObservableProperty] private double carbsProgress;

    public GoalsViewModel(PointsService points, BackendSyncService sync)
    {
        _points = points;
        _sync = sync;
    }

    public string CaloriesTargetDisplay => $"{Math.Round(CaloriesTarget)} kcal";
    public string ProteinGTargetDisplay => $"{Math.Round(ProteinGTarget)} g";
    public string CarbsGTargetDisplay => $"{Math.Round(CarbsGTarget)} g";

    public async Task LoadAsync()
    {
        var token = Preferences.Default.Get("auth_id_token", "");
        _ = await _sync.EnsureBackendIdentityAsync(token);

        var g = await _sync.GetGoalsAsync();
        CaloriesTarget = g.CaloriesTarget;
        ProteinGTarget = g.ProteinGTarget;
        CarbsGTarget = g.CarbsGTarget;

        var todayLocal = DateTime.Now.Date;
        var fromUtc = DateTime.SpecifyKind(todayLocal, DateTimeKind.Local).ToUniversalTime();
        var toUtc = DateTime.SpecifyKind(todayLocal.AddDays(1), DateTimeKind.Local).ToUniversalTime();
        var meals = await _sync.GetMealsBetweenUtcAsync(fromUtc, toUtc, includePhoto: false);
        var cal = meals.Sum(x => x.total_calories);
        var carbs = meals.Sum(x => x.total_carbs_g);
        var prot = meals.Sum(x => x.total_protein_g);

        CaloriesProgress = g.CaloriesTarget <= 0 ? 0 : Math.Min(1, cal / g.CaloriesTarget);
        ProteinProgress = g.ProteinGTarget <= 0 ? 0 : Math.Min(1, prot / g.ProteinGTarget);
        CarbsProgress = g.CarbsGTarget <= 0 ? 0 : Math.Min(1, carbs / g.CarbsGTarget);
    }

    [RelayCommand]
    private async Task Save()
    {
        var cal = Math.Round(Math.Clamp(CaloriesTarget, 1200, 4500));
        var prot = Math.Round(Math.Clamp(ProteinGTarget, 40, 260));
        var carbs = Math.Round(Math.Clamp(CarbsGTarget, 60, 500));

        var g = new UserGoals
        {
            Id = 1,
            CaloriesTarget = cal,
            ProteinGTarget = prot,
            CarbsGTarget = carbs,
        };

        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        if (!identityOk)
        {
            await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("error_title"), LocalizationService.T("backend_identity_error"), "OK");
            return;
        }

        var pushed = await _sync.TryPushGoalsAsync(g);
        if (!pushed)
        {
            await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("error_title"), LocalizationService.T("backend_save_error"), "OK");
            return;
        }

        var balance = _points.Award(5);
        var streakDays = await ComputeBalancedStreakAsync();
        await RewardPopupService.ShowAsync(5, balance, streakDays);

        _ = _sync.TryPostGamificationEventAsync(
            eventType: "goals_updated",
            title: "Goals updated",
            message: "Nutrition goals saved",
            metadata: new Dictionary<string, object>
            {
                ["points_earned"] = 5,
                ["calories_target"] = cal,
                ["protein_target"] = prot,
                ["carbs_target"] = carbs,
            });

        if (Shell.Current?.Navigation != null)
            await Shell.Current.Navigation.PopAsync();
    }

    private async Task<int> ComputeBalancedStreakAsync()
    {
        try
        {
            var goals = await _sync.GetGoalsAsync();
            return await DailyRewardService.ComputeCurrentStreakAsync(goals, async dayLocal =>
            {
                var start = DateTime.SpecifyKind(dayLocal.Date, DateTimeKind.Local).ToUniversalTime();
                var end = DateTime.SpecifyKind(dayLocal.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
                var meals = await _sync.GetMealsBetweenUtcAsync(start, end, includePhoto: false);
                return (
                    meals.Sum(x => x.total_calories),
                    meals.Sum(x => x.total_carbs_g),
                    meals.Sum(x => x.total_protein_g),
                    0d);
            });
        }
        catch
        {
            return 0;
        }
    }

    partial void OnCaloriesTargetChanged(double value)
    {
        OnPropertyChanged(nameof(CaloriesTargetDisplay));
    }

    partial void OnProteinGTargetChanged(double value)
    {
        OnPropertyChanged(nameof(ProteinGTargetDisplay));
    }

    partial void OnCarbsGTargetChanged(double value)
    {
        OnPropertyChanged(nameof(CarbsGTargetDisplay));
    }
}
