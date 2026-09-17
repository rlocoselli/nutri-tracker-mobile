using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public record ExerciseSuggestion(string Name, string Detail, string Benefit, string Level, string Equipment, int Minutes);
public record WeeklyRecipe(string Day, string Meal, string Ingredients, int Calories);

public partial class PlanViewModel : ObservableObject
{
    private readonly BackendSyncService _sync;
    public ObservableCollection<ExerciseSuggestion> Exercises { get; } = new();
    public ObservableCollection<WeeklyRecipe> Recipes { get; } = new();
    public ObservableCollection<string> GroceryItems { get; } = new();
    public List<string> DietOptions { get; } = new() { "Balanced", "Vegetarian", "High protein" };
    public List<string> ExerciseLevels { get; } = new() { "All levels", "Beginner", "Intermediate" };
    public List<string> EquipmentOptions { get; } = new() { "No equipment", "Some equipment" };
    public List<int> DurationOptions { get; } = new() { 10, 20, 30, 45 };

    [ObservableProperty] private string calorieSummary = "";
    [ObservableProperty] private string projectionText = "";
    [ObservableProperty] private string estimateText = "";
    [ObservableProperty] private double calorieProgress;
    [ObservableProperty] private string plannedWeekText = "";
    [ObservableProperty] private string profileHint = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string selectedDiet = "Balanced";
    [ObservableProperty] private string selectedExerciseLevel = "All levels";
    [ObservableProperty] private string selectedEquipment = "No equipment";
    [ObservableProperty] private int selectedDuration = 20;

    public string Title => LocalizationService.T("plan_title");
    public string Subtitle => LocalizationService.T("plan_subtitle");
    public string ExercisesTitle => LocalizationService.T("plan_exercises");
    public string RecipesTitle => LocalizationService.T("plan_recipes");
    public string ProjectionTitle => LocalizationService.T("plan_projection");
    public string RefreshText => LocalizationService.T("refresh");
    public string ConsumptionTitle => LocalizationService.T("plan_consumption");
    public string PlannedWeekTitle => LocalizationService.T("plan_weekly_total");
    public string PreferencesTitle => LocalizationService.T("plan_preferences");
    public string GroceryTitle => LocalizationService.T("plan_grocery");
    public string ReplaceText => LocalizationService.T("plan_replace");

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

            var target = Math.Max(1, goals.CaloriesTarget);
            var remaining = Math.Max(0, target - consumed);
            CalorieProgress = Math.Min(1, consumed / target);
            ProfileHint = string.Format(LocalizationService.T("plan_profile_hint"), Math.Round(target));
            CalorieSummary = string.Format(LocalizationService.T("plan_calories_format"), Math.Round(consumed), Math.Round(goals.CaloriesTarget), Math.Round(remaining));
            EstimateText = LocalizationService.T("plan_estimate");

            Exercises.Clear();
            var level = SelectedExerciseLevel == "All levels" ? "Beginner" : SelectedExerciseLevel;
            Exercises.Add(new("Brisk walk", $"{SelectedDuration} min · moderate intensity", "Accessible cardio", level, "No equipment", SelectedDuration));
            Exercises.Add(new("Express strength", $"{SelectedDuration} min · 3 times this week", "Maintain strength", level, SelectedEquipment, SelectedDuration));
            Exercises.Add(new("Mobility flow", $"{Math.Min(10, SelectedDuration)} min · daily", "Recover and move comfortably", "Beginner", "No equipment", Math.Min(10, SelectedDuration)));

            Recipes.Clear();
            var items = new[] { ("Lundi", "Bowl poulet, quinoa et légumes", "Poulet · quinoa · courgette · yaourt citron", 620), ("Mardi", "Saumon, pommes de terre et haricots", "Saumon · pommes de terre · haricots verts", 650), ("Mercredi", "Chili doux aux haricots", "Haricots rouges · tomate · maïs · riz", 590), ("Jeudi", "Omelette épinards et pain complet", "Œufs · épinards · feta · pain complet", 510), ("Vendredi", "Pâtes complètes au thon", "Pâtes complètes · thon · tomate · roquette", 610), ("Samedi", "Curry lentilles et riz", "Lentilles · carotte · lait de coco · riz", 570), ("Dimanche", "Salade grecque et pois chiches", "Pois chiches · concombre · tomate · feta", 540) };
            var scale = target / 2000d;
            foreach (var x in items) Recipes.Add(new(x.Item1, x.Item2, x.Item3, (int)Math.Round(x.Item4 * scale)));
            GroceryItems.Clear();
            foreach (var item in new[] { "Chicken or tofu", "Salmon", "Eggs", "Lentils and beans", "Quinoa and whole-grain rice", "Seasonal vegetables", "Greek yogurt", "Fruit" }) GroceryItems.Add(item);
            var plannedDaily = Recipes.Average(x => x.Calories);
            var dailyDeficit = Math.Max(0, target - plannedDaily);
            PlannedWeekText = string.Format(LocalizationService.T("plan_weekly_total_format"), Recipes.Sum(x => x.Calories), Math.Round(plannedDaily));
            ProjectionText = string.Format(LocalizationService.T("plan_projection_format"), Math.Round(dailyDeficit), Math.Round(dailyDeficit * 7 / 7700, 1), Math.Round(dailyDeficit * 30 / 7700, 1));
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ReplaceRecipeAsync(WeeklyRecipe recipe)
    {
        var replacement = recipe.Meal.Contains("poulet", StringComparison.OrdinalIgnoreCase)
            ? new WeeklyRecipe(recipe.Day, "Tofu, quinoa et légumes", "Tofu · quinoa · courgette · yaourt citron", recipe.Calories)
            : new WeeklyRecipe(recipe.Day, "Bowl méditerranéen aux lentilles", "Lentilles · tomate · concombre · feta", recipe.Calories);
        var index = Recipes.IndexOf(recipe);
        if (index >= 0) Recipes[index] = replacement;
        await Task.CompletedTask;
    }
}
