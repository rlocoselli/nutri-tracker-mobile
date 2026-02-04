using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Models;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class GoalsViewModel : ObservableObject
{
    private readonly LocalDb _db;

    [ObservableProperty] private string caloriesTarget = "2000";
    [ObservableProperty] private string proteinGTarget = "120";
    [ObservableProperty] private string carbsGTarget = "220";

    [ObservableProperty] private double caloriesProgress;
    [ObservableProperty] private double proteinProgress;
    [ObservableProperty] private double carbsProgress;

    public GoalsViewModel(LocalDb db)
    {
        _db = db;
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
        await Application.Current!.MainPage!.DisplayAlert("Enregistré", "Objectifs mis à jour.", "OK");
        if (Shell.Current?.Navigation != null)
            await Shell.Current.Navigation.PopAsync();
    }
}
