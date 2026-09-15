using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public record ExerciseSuggestion(string Name, string Detail, string Benefit);
public record WeeklyRecipe(string Day, string Meal, string Ingredients, int Calories);

public partial class PlanViewModel : ObservableObject
{
    private readonly BackendSyncService _sync;
    public ObservableCollection<ExerciseSuggestion> Exercises { get; } = new();
    public ObservableCollection<WeeklyRecipe> Recipes { get; } = new();

    [ObservableProperty] private string calorieSummary = "";
    [ObservableProperty] private string projectionText = "";
    [ObservableProperty] private string estimateText = "";
    [ObservableProperty] private bool isBusy;

    public string Title => LocalizationService.T("plan_title");
    public string Subtitle => LocalizationService.T("plan_subtitle");
    public string ExercisesTitle => LocalizationService.T("plan_exercises");
    public string RecipesTitle => LocalizationService.T("plan_recipes");
    public string ProjectionTitle => LocalizationService.T("plan_projection");
    public string RefreshText => LocalizationService.T("refresh");

    public PlanViewModel(BackendSyncService sync) => _sync = sync;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var goals = await _sync.GetGoalsAsync();
            var consumed = 0d;
            try
            {
                var start = DateTime.Now.Date.ToUniversalTime();
                var meals = await _sync.GetMealsBetweenUtcAsync(start, start.AddDays(1), includePhoto: false);
                consumed = meals.Sum(m => m.total_calories);
            }
            catch { }

            var remaining = Math.Max(0, goals.CaloriesTarget - consumed);
            CalorieSummary = string.Format(LocalizationService.T("plan_calories_format"), Math.Round(consumed), Math.Round(goals.CaloriesTarget), Math.Round(remaining));
            var dailyDeficit = Math.Max(0, goals.CaloriesTarget - 1800);
            ProjectionText = string.Format(LocalizationService.T("plan_projection_format"), Math.Round(dailyDeficit), Math.Round(dailyDeficit * 7 / 7700, 1), Math.Round(dailyDeficit * 30 / 7700, 1));
            EstimateText = LocalizationService.T("plan_estimate");

            Exercises.Clear();
            Exercises.Add(new("Marche active", "30 min · intensité modérée", "Cardio doux et accessible"));
            Exercises.Add(new("Renforcement express", "20 min · 3 fois cette semaine", "Préserver la force et la masse musculaire"));
            Exercises.Add(new("Mobilité", "10 min · chaque jour", "Bouger sans pression et récupérer"));

            Recipes.Clear();
            var items = new[] { ("Lundi", "Bowl poulet, quinoa et légumes", "Poulet · quinoa · courgette · yaourt citron", 620), ("Mardi", "Saumon, pommes de terre et haricots", "Saumon · pommes de terre · haricots verts", 650), ("Mercredi", "Chili doux aux haricots", "Haricots rouges · tomate · maïs · riz", 590), ("Jeudi", "Omelette épinards et pain complet", "Œufs · épinards · feta · pain complet", 510), ("Vendredi", "Pâtes complètes au thon", "Pâtes complètes · thon · tomate · roquette", 610), ("Samedi", "Curry lentilles et riz", "Lentilles · carotte · lait de coco · riz", 570), ("Dimanche", "Salade grecque et pois chiches", "Pois chiches · concombre · tomate · feta", 540) };
            foreach (var x in items) Recipes.Add(new(x.Item1, x.Item2, x.Item3, x.Item4));
        }
        finally { IsBusy = false; }
    }
}
