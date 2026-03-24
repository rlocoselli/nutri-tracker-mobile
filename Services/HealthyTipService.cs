using NutritionTracker.Models;

namespace NutritionTracker.Services;

public sealed class HealthyTipService
{
    private readonly BackendSyncService _sync;

    public HealthyTipService(BackendSyncService sync)
    {
        _sync = sync;
    }

    public async Task<HealthyTipResult> BuildTipForEntryAsync(MealEntry entry)
    {
        var lang = (Preferences.Default.Get("app_lang", "fr") ?? "fr").Trim().ToLowerInvariant();
        if (lang != "en")
            lang = "fr";

        var goals = await _sync.GetGoalsAsync();

        var todayLocal = entry.DateUtc.ToLocalTime().Date;
        var startUtc = DateTime.SpecifyKind(todayLocal, DateTimeKind.Local).ToUniversalTime();
        var endUtc = DateTime.SpecifyKind(todayLocal.AddDays(1), DateTimeKind.Local).ToUniversalTime();
        var meals = await _sync.GetMealsBetweenUtcAsync(startUtc, endUtc);

        var totalCalories = meals.Sum(x => x.total_calories);
        var totalProtein = meals.Sum(x => x.total_protein_g);
        var totalCarbs = meals.Sum(x => x.total_carbs_g);

        var remainingCalories = Math.Max(0, goals.CaloriesTarget - totalCalories);
        var remainingProtein = Math.Max(0, goals.ProteinGTarget - totalProtein);
        var carbsGap = goals.CarbsGTarget - totalCarbs;

        var tip = PickMainTip(lang, entry, remainingCalories, remainingProtein, carbsGap, goals);
        var challenge = BuildChallengeNudge(lang, entry);
        var progress = BuildProgressNudge(lang, goals, totalCalories, totalProtein, totalCarbs);

        return new HealthyTipResult(tip.Title, tip.Message, challenge, progress);
    }

    private static (string Title, string Message) PickMainTip(
        string lang,
        MealEntry entry,
        double remainingCalories,
        double remainingProtein,
        double carbsGap,
        UserGoals goals)
    {
        var candidates = new List<(string Title, string Message)>();

        if (remainingProtein > 20)
        {
            candidates.Add(lang == "en"
                ? ("AI tip: protein boost", "You are still below your protein target. Add one high-protein option next: eggs, Greek yogurt, tofu, or tuna.")
                : ("Conseil IA: boost proteines", "Tu es encore en dessous de ton objectif proteines. Ajoute une option riche en proteines au prochain repas: oeufs, skyr, tofu ou thon."));
        }

        if (remainingCalories > 450)
        {
            candidates.Add(lang == "en"
                ? ("AI tip: smart completion", "You still have room in your calorie budget. Prefer whole foods to finish the day: legumes, whole grains, and vegetables.")
                : ("Conseil IA: completion intelligente", "Il te reste de la marge calorique. Privilegie des aliments complets pour finir la journee: legumes, cereales completes et legumes verts."));
        }

        if (carbsGap < -30)
        {
            candidates.Add(lang == "en"
                ? ("AI tip: balance carbs", "Carbs are trending high today. For your next meal, reduce refined starch and add fiber + protein.")
                : ("Conseil IA: equilibrer les glucides", "Les glucides sont eleves aujourd'hui. Au prochain repas, reduis les feculents raffines et ajoute fibres + proteines."));
        }

        if (entry.QualityScore >= 75)
        {
            candidates.Add(lang == "en"
                ? ("AI tip: keep momentum", "Great quality entry. Repeating this pattern in your next meal is the fastest way to hit your goals.")
                : ("Conseil IA: garder l'elan", "Tres bonne qualite de repas. Reproduire ce schema au prochain repas est la voie la plus rapide vers tes objectifs."));
        }

        if (entry.TotalProteinG >= 25)
        {
            candidates.Add(lang == "en"
                ? ("AI tip: preserve satiety", "Nice protein density. Keep this level for better satiety and fewer cravings later.")
                : ("Conseil IA: maintenir la satiete", "Bonne densite proteique. Garde ce niveau pour une meilleure satiete et moins de fringales plus tard."));
        }

        var calorieRatio = goals.CaloriesTarget <= 0 ? 0 : entry.TotalCalories / goals.CaloriesTarget;
        if (calorieRatio > 0.45)
        {
            candidates.Add(lang == "en"
                ? ("AI tip: lighter next meal", "This entry is energy-dense. Make the next one lighter with lean protein and vegetables.")
                : ("Conseil IA: prochain repas plus leger", "Cette entree est dense en calories. Fais le prochain repas plus leger avec proteines maigres et legumes."));
        }

        if (candidates.Count == 0)
        {
            candidates.Add(lang == "en"
                ? ("AI tip: consistency first", "Small consistent improvements beat perfect days. Keep logging and adjust one thing at a time.")
                : ("Conseil IA: regularite d'abord", "Les petits progres reguliers valent mieux qu'une journee parfaite. Continue de logger et ajuste une habitude a la fois."));
        }

        return candidates[Random.Shared.Next(candidates.Count)];
    }

    private static string BuildChallengeNudge(string lang, MealEntry entry)
    {
        var isShared = !string.Equals(entry.StoryVisibility, "self", StringComparison.OrdinalIgnoreCase);
        var hasPhoto = !string.IsNullOrWhiteSpace(entry.PhotoPath);

        if (lang == "en")
        {
            if (isShared && hasPhoto)
                return "Challenge: your post is visible. Keep a 3-post healthy streak this week to build momentum.";

            if (isShared)
                return "Challenge: add a photo to your next shared entry for a stronger social commitment.";

            return "Challenge: share your next healthy entry with friends for accountability and motivation.";
        }

        if (isShared && hasPhoto)
            return "Defi: ton post est visible. Vise une serie de 3 posts sains cette semaine pour garder l'elan.";

        if (isShared)
            return "Defi: ajoute une photo a la prochaine entree partagee pour renforcer ton engagement social.";

        return "Defi: partage ta prochaine entree saine avec tes amis pour booster responsabilite et motivation.";
    }

    private static string BuildProgressNudge(string lang, UserGoals goals, double calories, double protein, double carbs)
    {
        var calPct = goals.CaloriesTarget <= 0 ? 0 : Math.Round((calories / goals.CaloriesTarget) * 100);
        var proteinPct = goals.ProteinGTarget <= 0 ? 0 : Math.Round((protein / goals.ProteinGTarget) * 100);
        var carbsPct = goals.CarbsGTarget <= 0 ? 0 : Math.Round((carbs / goals.CarbsGTarget) * 100);

        return lang == "en"
            ? $"Today progress: kcal {calPct}% | protein {proteinPct}% | carbs {carbsPct}% of target."
            : $"Progression du jour: kcal {calPct}% | proteines {proteinPct}% | glucides {carbsPct}% de l'objectif.";
    }
}

public sealed class HealthyTipResult
{
    public string Title { get; }
    public string Message { get; }
    public string Challenge { get; }
    public string Progress { get; }

    public HealthyTipResult(string title, string message, string challenge, string progress)
    {
        Title = title;
        Message = message;
        Challenge = challenge;
        Progress = progress;
    }
}
