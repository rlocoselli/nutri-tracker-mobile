using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Models;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class GoalsViewModel : ObservableObject
{
    private readonly LocalDb _db;
    private readonly PointsService _points;
    private readonly BackendSyncService _sync;

    [ObservableProperty] private string caloriesTarget = "2000";
    [ObservableProperty] private string proteinGTarget = "120";
    [ObservableProperty] private string carbsGTarget = "220";

    [ObservableProperty] private double caloriesProgress;
    [ObservableProperty] private double proteinProgress;
    [ObservableProperty] private double carbsProgress;

    public GoalsViewModel(LocalDb db, PointsService points, BackendSyncService sync)
    {
        _db = db;
        _points = points;
        _sync = sync;
    }

    public async Task LoadAsync()
    {
        var g = await _db.GetGoalsAsync();
        CaloriesTarget = g.CaloriesTarget.ToString();
        ProteinGTarget = g.ProteinGTarget.ToString();
        CarbsGTarget = g.CarbsGTarget.ToString();

        var (cal, carbs, prot) = await _db.GetTotalsForDayUtcAsync(DateTime.UtcNow.Date);
        CaloriesProgress = g.CaloriesTarget <= 0 ? 0 : Math.Min(1, cal / g.CaloriesTarget);
        ProteinProgress = g.ProteinGTarget <= 0 ? 0 : Math.Min(1, prot / g.ProteinGTarget);
        CarbsProgress = g.CarbsGTarget <= 0 ? 0 : Math.Min(1, carbs / g.CarbsGTarget);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (!double.TryParse(CaloriesTarget, out var cal)) cal = 2000;
        if (!double.TryParse(ProteinGTarget, out var prot)) prot = 120;
        if (!double.TryParse(CarbsGTarget, out var carbs)) carbs = 220;

        var g = new UserGoals
        {
            Id = 1,
            CaloriesTarget = cal,
            ProteinGTarget = prot,
            CarbsGTarget = carbs,
        };

        await _db.SaveGoalsAsync(g);
        var token = Preferences.Default.Get("auth_id_token", "");
        _ = await _sync.EnsureBackendIdentityAsync(token);
        _ = await _sync.TryPushGoalsAsync(g);
        var balance = _points.Award(5);
        var title = LocalizationService.T("saved_title_common");
        var message = string.Format(LocalizationService.T("goals_saved_message"), balance);
        await Application.Current!.MainPage!.DisplayAlert(title, message, "OK");
        if (Shell.Current?.Navigation != null)
            await Shell.Current.Navigation.PopAsync();
    }
}
