using NutritionTracker.Models;

namespace NutritionTracker.Services;

public sealed class WeeklyMissionService
{
    private const string WeeklyBonusWeekKey = "weekly_mission_bonus_week_key";

    public WeeklyMissionState BuildState(IEnumerable<MealEntry> mealsWeek, UserGoals goals)
    {
        var entries = mealsWeek?.ToList() ?? new List<MealEntry>();

        var healthyPosts = entries.Count(x => x.QualityScore >= 70);
        var sharedPosts = entries.Count(x => !string.Equals(x.StoryVisibility, "self", StringComparison.OrdinalIgnoreCase));

        var proteinDays = entries
            .GroupBy(x => x.DateUtc.ToLocalTime().Date)
            .Count(g => g.Sum(x => x.TotalProteinG) >= goals.ProteinGTarget * 0.9);

        var lang = CurrentLanguage();
        var missions = new List<WeeklyMissionItem>
        {
            new(
                lang == "en" ? "Healthy posts" : "Posts sains",
                healthyPosts,
                5,
                lang == "en" ? "Log quality meals (score >= 70)." : "Enregistrer des repas de qualite (score >= 70)."),
            new(
                lang == "en" ? "Shared accountability" : "Partage responsable",
                sharedPosts,
                3,
                lang == "en" ? "Share posts to stay accountable." : "Partager ses posts pour rester motive."),
            new(
                lang == "en" ? "Protein days" : "Jours proteines",
                proteinDays,
                4,
                lang == "en" ? "Reach 90% of protein target on at least 4 days." : "Atteindre 90% de l'objectif proteines sur 4 jours.")
        };

        var completed = missions.Count(x => x.IsCompleted);
        var weekKey = CurrentWeekKey();

        var bonusAwarded = false;
        var bonusPoints = 0;
        if (completed == missions.Count)
        {
            var alreadyAwardedWeek = Preferences.Default.Get(WeeklyBonusWeekKey, "").Trim();
            if (!string.Equals(alreadyAwardedWeek, weekKey, StringComparison.Ordinal))
            {
                bonusAwarded = true;
                bonusPoints = 15;
                Preferences.Default.Set(WeeklyBonusWeekKey, weekKey);
            }
        }

        var status = lang == "en"
            ? $"Weekly mission: {completed}/{missions.Count} completed"
            : $"Mission hebdo: {completed}/{missions.Count} completee(s)";

        if (bonusAwarded)
        {
            status = lang == "en"
                ? $"Weekly mission complete. +{bonusPoints} coins unlocked."
                : $"Mission hebdo complete. +{bonusPoints} pieces debloquees.";
        }

        return new WeeklyMissionState(missions, status, bonusAwarded, bonusPoints);
    }

    private static string CurrentLanguage()
    {
        var lang = (Preferences.Default.Get("app_lang", "fr") ?? "fr").Trim().ToLowerInvariant();
        return lang == "en" ? "en" : "fr";
    }

    private static string CurrentWeekKey()
    {
        var today = DateTime.Today;
        var week = System.Globalization.ISOWeek.GetWeekOfYear(today);
        var year = System.Globalization.ISOWeek.GetYear(today);
        return $"{year:0000}-W{week:00}";
    }
}

public sealed class WeeklyMissionState
{
    public IReadOnlyList<WeeklyMissionItem> Missions { get; }
    public string StatusText { get; }
    public bool BonusAwarded { get; }
    public int BonusPoints { get; }

    public WeeklyMissionState(IReadOnlyList<WeeklyMissionItem> missions, string statusText, bool bonusAwarded, int bonusPoints)
    {
        Missions = missions;
        StatusText = statusText;
        BonusAwarded = bonusAwarded;
        BonusPoints = Math.Max(0, bonusPoints);
    }
}

public sealed class WeeklyMissionItem
{
    public string Title { get; }
    public int Value { get; }
    public int Target { get; }
    public string Hint { get; }
    public bool IsCompleted => Value >= Target;
    public string ProgressText => $"{Math.Min(Value, Target)}/{Target}";

    public WeeklyMissionItem(string title, int value, int target, string hint)
    {
        Title = title;
        Value = Math.Max(0, value);
        Target = Math.Max(1, target);
        Hint = hint;
    }
}
