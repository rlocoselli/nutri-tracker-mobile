using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly LocalDb _db;
    private readonly IServiceProvider _sp;

    [ObservableProperty] private string userPictureUrl = "";
    [ObservableProperty] private string userName = "";
    [ObservableProperty] private string greeting = "Bonjour";

    [ObservableProperty] private string todayCaloriesText = "0 / 0";
    [ObservableProperty] private string todayProteinText = "0 / 0 g";
    [ObservableProperty] private string todayCarbsText = "0 / 0 g";

    [ObservableProperty] private double caloriesProgress;
    [ObservableProperty] private double proteinProgress;
    [ObservableProperty] private double carbsProgress;

    public DashboardViewModel(LocalDb db, IServiceProvider sp)
    {
        _db = db;
        _sp = sp;
    }

    public async Task LoadAsync()
    {
        UserName = Preferences.Default.Get("profile_name", "");
        UserPictureUrl = Preferences.Default.Get("profile_picture", "");

        var goals = await _db.GetGoalsAsync();
        var (cal, carbs, prot) = await _db.GetTotalsForDayUtcAsync(DateTime.UtcNow.Date);

        TodayCaloriesText = $"{Math.Round(cal)} / {Math.Round(goals.CaloriesTarget)}";
        TodayProteinText  = $"{Math.Round(prot)} / {Math.Round(goals.ProteinGTarget)} g";
        TodayCarbsText    = $"{Math.Round(carbs)} / {Math.Round(goals.CarbsGTarget)} g";

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
