using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Services;
using NutritionTracker.Services.Dto;

namespace NutritionTracker.ViewModels;

public partial class RecommendationsViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly LocalDb _db;

    public ObservableCollection<RecommendationItem> Items { get; } = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool hasResult;
    [ObservableProperty] private string insightsText = "";

    public RecommendationsViewModel(ApiService api, LocalDb db)
    {
        _api = api;
        _db = db;
    }

    [RelayCommand]
    private async Task Generate()
    {
        if (IsBusy) return;
        IsBusy = true;
        HasResult = false;
        Items.Clear();

        try
        {
            var idToken = Preferences.Default.Get("auth_id_token", "");
            if (string.IsNullOrWhiteSpace(idToken))
                throw new Exception("Not logged in.");

            var goals = await _db.GetGoalsAsync();
            var meals = await _db.GetMealsLastDaysAsync(14);

            // Aggregate by day
            var byDay = meals
                .GroupBy(m => m.DayKeyUtc)
                .Select(g => new
                {
                    date = g.Key,
                    calories = g.Sum(x => x.TotalCalories),
                    carbs_g = g.Sum(x => x.TotalCarbsG),
                    protein_g = g.Sum(x => x.TotalProteinG),
                })
                .OrderBy(x => x.date)
                .ToList();

            var payload = new
            {
                lang = Preferences.Default.Get("app_lang", "pt"),
                goals = new { calories = goals.CaloriesTarget, carbs_g = goals.CarbsGTarget, protein_g = goals.ProteinGTarget },
                daily_totals = byDay,
            };

            var resp = await _api.GetRecommendationsAsync(idToken, payload);

            InsightsText = $"Avg calories: {Math.Round(resp.insights.avg_calories)} | Avg carbs: {Math.Round(resp.insights.avg_carbs_g)}g | Avg protein: {Math.Round(resp.insights.avg_protein_g)}g";
            foreach (var it in resp.recommendations)
                Items.Add(it);

            HasResult = true;
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
