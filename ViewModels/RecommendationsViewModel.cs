using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Services;
using NutritionTracker.Services.Dto;

namespace NutritionTracker.ViewModels;

public partial class RecommendationsViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly BackendSyncService _sync;

    public string TitleText => T("reco_title");
    public string SubtitleText => T("reco_subtitle");
    public string GenerateText => T("generate");
    public string AnalysisText => T("analysis");

    public ObservableCollection<RecommendationItem> Items { get; } = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool hasResult;
    [ObservableProperty] private string insightsText = "";
    [ObservableProperty] private string warningsText = "";

    public RecommendationsViewModel(ApiService api, BackendSyncService sync)
    {
        _api = api;
        _sync = sync;
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

            var identityOk = await _sync.EnsureBackendIdentityAsync(idToken);
            if (!identityOk)
                throw new Exception(T("backend_identity_error"));

            var goals = await _sync.GetGoalsAsync();
            var toUtc = DateTime.UtcNow.AddDays(1);
            var fromUtc = toUtc.AddDays(-14);
            var meals = (await _sync.GetMealsBetweenUtcAsync(fromUtc, toUtc)).Select(ToMealEntry).ToList();
            var exercises = new List<Models.ExerciseEntry>();
            var lang = Preferences.Default.Get("app_lang", "fr");

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
                lang,
                goals = new { calories = goals.CaloriesTarget, carbs_g = goals.CarbsGTarget, protein_g = goals.ProteinGTarget },
                daily_totals = byDay,
                daily_exercise = exercises
                    .GroupBy(x => x.DayKeyUtc)
                    .Select(g => new
                    {
                        date = g.Key,
                        burned_kcal = g.Sum(x => x.BurnedCalories),
                        steps = g.Sum(x => x.GoogleFitSteps),
                        minutes = g.Sum(x => x.ExerciseMinutes)
                    })
                    .OrderBy(x => x.date)
                    .ToList(),
            };

            var resp = await _api.GetRecommendationsAsync(idToken, payload);

            var avgCal = Math.Round(resp.insights.avg_calories);
            var avgCarbs = Math.Round(resp.insights.avg_carbs_g);
            var avgProt = Math.Round(resp.insights.avg_protein_g);
            var avgBurn = exercises.Count == 0 ? 0 : Math.Round(exercises.Average(x => x.BurnedCalories));

            var calGap = Math.Round(avgCal - goals.CaloriesTarget);
            var carbsGap = Math.Round(avgCarbs - goals.CarbsGTarget);
            var protGap = Math.Round(avgProt - goals.ProteinGTarget);

            InsightsText = lang == "en"
                ? $"Avg calories: {avgCal} (gap {Signed(calGap)}), Avg carbs: {avgCarbs}g (gap {Signed(carbsGap)}g), Avg protein: {avgProt}g (gap {Signed(protGap)}g), Avg burn: {avgBurn} kcal"
                : $"Calories moy.: {avgCal} (écart {Signed(calGap)}), Glucides moy.: {avgCarbs}g (écart {Signed(carbsGap)}g), Protéines moy.: {avgProt}g (écart {Signed(protGap)}g), Débit moyen: {avgBurn} kcal";

            WarningsText = string.Join("\n", resp.warnings ?? new List<string>());

            if (resp.recommendations != null && resp.recommendations.Count > 0)
            {
                foreach (var it in resp.recommendations)
                    Items.Add(it);
            }
            else
            {
                foreach (var item in BuildFallbackRecommendations(lang, avgCal, avgCarbs, avgProt, goals))
                    Items.Add(item);
            }

            HasResult = true;
        }
        catch (Exception ex)
        {
            WarningsText = ex.Message;
            HasResult = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string Signed(double value)
    {
        if (value > 0) return $"+{value:0}";
        return $"{value:0}";
    }

    private static Models.MealEntry ToMealEntry(BackendMeal meal)
    {
        var dateUtc = meal.date_utc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(meal.date_utc, DateTimeKind.Utc)
            : meal.date_utc.ToUniversalTime();

        return new Models.MealEntry
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

    private static List<RecommendationItem> BuildFallbackRecommendations(string lang, double avgCal, double avgCarbs, double avgProt, Models.UserGoals goals)
    {
        var list = new List<RecommendationItem>();

        if (avgCal > goals.CaloriesTarget + 150)
        {
            list.Add(new RecommendationItem
            {
                title = lang == "en" ? "Reduce daily energy density" : "Réduire la densité énergétique quotidienne",
                why = lang == "en" ? "Your average calories are above target." : "Vos calories moyennes dépassent l'objectif.",
                actions = lang == "en"
                    ? new List<string> { "Add more vegetables to main meals", "Replace sugary drinks with water", "Prefer grilled options over fried" }
                    : new List<string> { "Ajouter plus de légumes aux repas principaux", "Remplacer les boissons sucrées par de l'eau", "Privilégier le grillé au frit" }
            });
        }

        if (avgProt < goals.ProteinGTarget - 10)
        {
            list.Add(new RecommendationItem
            {
                title = lang == "en" ? "Increase protein consistency" : "Augmenter la régularité en protéines",
                why = lang == "en" ? "Average protein is below target." : "Les protéines moyennes sont sous l'objectif.",
                actions = lang == "en"
                    ? new List<string> { "Add eggs, fish, poultry or tofu", "Include protein in breakfast", "Keep yogurt or nuts as snack" }
                    : new List<string> { "Ajouter œufs, poisson, volaille ou tofu", "Inclure une source de protéines au petit-déjeuner", "Prévoir yaourt ou noix en collation" }
            });
        }

        if (avgCarbs > goals.CarbsGTarget + 20)
        {
            list.Add(new RecommendationItem
            {
                title = lang == "en" ? "Balance carbohydrate portions" : "Mieux équilibrer les portions de glucides",
                why = lang == "en" ? "Carb intake is above target." : "L'apport en glucides dépasse l'objectif.",
                actions = lang == "en"
                    ? new List<string> { "Measure starch portions", "Favor whole grains", "Reduce late-night refined carbs" }
                    : new List<string> { "Mesurer les portions de féculents", "Favoriser les céréales complètes", "Réduire les glucides raffinés le soir" }
            });
        }

        if (list.Count == 0)
        {
            list.Add(new RecommendationItem
            {
                title = lang == "en" ? "Maintain your current rhythm" : "Maintenir votre rythme actuel",
                why = lang == "en" ? "Your averages are close to your targets." : "Vos moyennes sont proches de vos objectifs.",
                actions = lang == "en"
                    ? new List<string> { "Keep logging meals daily", "Hydrate regularly", "Plan 2-3 balanced meals ahead" }
                    : new List<string> { "Continuer à journaliser les repas", "Bien s'hydrater", "Planifier 2-3 repas équilibrés à l'avance" }
            });
        }

        return list;
    }

    private static string T(string key)
    {
        var lang = Preferences.Default.Get("app_lang", "fr");
        return key switch
        {
            "reco_title" => lang == "en" ? "Recommendations" : "Recommandations",
            "reco_subtitle" => lang == "en" ? "Generated from your recent meal history and goals." : "Générées depuis votre historique récent et vos objectifs.",
            "generate" => lang == "en" ? "Generate" : "Générer",
            "analysis" => lang == "en" ? "Analysis" : "Analyse",
            "backend_identity_error" => LocalizationService.T("backend_identity_error"),
            _ => key,
        };
    }
}
