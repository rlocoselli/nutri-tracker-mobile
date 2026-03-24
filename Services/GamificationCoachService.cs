using System.Globalization;
using NutritionTracker.Models;

namespace NutritionTracker.Services;

public sealed class GamificationCoachService
{
    private const string SharedStreakLastDayKey = "shared_post_streak_last_day";
    private const string SharedStreakCountKey = "shared_post_streak_count";
    private const string SharedPostBonusAwardedDayKey = "shared_post_bonus_awarded_day";

    private const string SharedWeekKey = "shared_post_week_key";
    private const string SharedWeekCountKey = "shared_post_week_count";
    private const string SharedQualityStreakLastDayKey = "shared_quality_streak_last_day";
    private const string SharedQualityStreakCountKey = "shared_quality_streak_count";

    public GamificationPostResult EvaluateSharedPostBonus(MealEntry entry)
    {
        if (entry == null)
            return GamificationPostResult.None(CurrentLanguage());

        var lang = CurrentLanguage();
        var isShared = !string.Equals((entry.StoryVisibility ?? "").Trim(), "self", StringComparison.OrdinalIgnoreCase);
        if (!isShared)
            return GamificationPostResult.None(lang);

        var dayLocal = entry.DateUtc.ToLocalTime().Date;
        var dayKey = dayLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var awardedDay = Preferences.Default.Get(SharedPostBonusAwardedDayKey, "").Trim();

        var streak = ComputeAndPersistStreak(dayLocal);
        var weeklyCount = ComputeAndPersistWeeklyCount(dayLocal);
        var qualityStreak = ComputeAndPersistQualityStreak(dayLocal, entry.QualityScore >= 70);
        var hasPhoto = !string.IsNullOrWhiteSpace((entry.PhotoPath ?? "").Trim());
        var qualityStrong = entry.QualityScore >= 80;

        var alreadyAwardedToday = string.Equals(awardedDay, dayKey, StringComparison.Ordinal);
        if (alreadyAwardedToday)
        {
            return new GamificationPostResult(
                0,
                streak,
                weeklyCount,
                BuildStatus(lang, streak, weeklyCount, qualityStreak, 0, awarded: false));
        }

        var bonus = 2;
        var bonusParts = new List<string>();
        bonusParts.Add(lang == "en" ? "+2 shared post" : "+2 post partage");

        if (streak >= 3)
        {
            bonus += 2;
            bonusParts.Add(lang == "en" ? "+2 streak 3+ days" : "+2 serie 3+ jours");
        }

        if (weeklyCount >= 5)
        {
            bonus += 2;
            bonusParts.Add(lang == "en" ? "+2 week activity 5+" : "+2 activite semaine 5+");
        }

        if (qualityStrong)
        {
            bonus += 2;
            bonusParts.Add(lang == "en" ? "+2 strong quality (80+)" : "+2 qualite elevee (80+)");
        }

        if (hasPhoto)
        {
            bonus += 1;
            bonusParts.Add(lang == "en" ? "+1 photo proof" : "+1 preuve photo");
        }

        if (qualityStreak >= 3)
        {
            bonus += 2;
            bonusParts.Add(lang == "en" ? "+2 quality streak 3+" : "+2 serie qualite 3+");
        }

        Preferences.Default.Set(SharedPostBonusAwardedDayKey, dayKey);

        return new GamificationPostResult(
            bonus,
            streak,
            weeklyCount,
            BuildStatus(lang, streak, weeklyCount, qualityStreak, bonus, awarded: true, bonusParts));
    }

    private static int ComputeAndPersistStreak(DateTime dayLocal)
    {
        var lastDay = Preferences.Default.Get(SharedStreakLastDayKey, "").Trim();
        var current = Math.Max(0, Preferences.Default.Get(SharedStreakCountKey, 0));

        if (DateTime.TryParseExact(lastDay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastLocal))
        {
            if (lastLocal.Date == dayLocal.Date)
            {
                Preferences.Default.Set(SharedStreakCountKey, current == 0 ? 1 : current);
                return current == 0 ? 1 : current;
            }

            if (lastLocal.Date == dayLocal.Date.AddDays(-1))
                current = Math.Max(1, current + 1);
            else
                current = 1;
        }
        else
        {
            current = 1;
        }

        Preferences.Default.Set(SharedStreakLastDayKey, dayLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Preferences.Default.Set(SharedStreakCountKey, current);
        return current;
    }

    private static int ComputeAndPersistWeeklyCount(DateTime dayLocal)
    {
        var week = ISOWeek.GetWeekOfYear(dayLocal);
        var year = ISOWeek.GetYear(dayLocal);
        var weekKey = $"{year:0000}-W{week:00}";

        var storedWeekKey = Preferences.Default.Get(SharedWeekKey, "").Trim();
        var count = Math.Max(0, Preferences.Default.Get(SharedWeekCountKey, 0));

        if (!string.Equals(storedWeekKey, weekKey, StringComparison.Ordinal))
            count = 0;

        count += 1;

        Preferences.Default.Set(SharedWeekKey, weekKey);
        Preferences.Default.Set(SharedWeekCountKey, count);
        return count;
    }

    private static int ComputeAndPersistQualityStreak(DateTime dayLocal, bool isQualitySharedPost)
    {
        var lastDay = Preferences.Default.Get(SharedQualityStreakLastDayKey, "").Trim();
        var current = Math.Max(0, Preferences.Default.Get(SharedQualityStreakCountKey, 0));

        if (!isQualitySharedPost)
        {
            Preferences.Default.Set(SharedQualityStreakLastDayKey, dayLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Preferences.Default.Set(SharedQualityStreakCountKey, 0);
            return 0;
        }

        if (DateTime.TryParseExact(lastDay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastLocal))
        {
            if (lastLocal.Date == dayLocal.Date)
            {
                var sameDay = current == 0 ? 1 : current;
                Preferences.Default.Set(SharedQualityStreakCountKey, sameDay);
                return sameDay;
            }

            current = lastLocal.Date == dayLocal.Date.AddDays(-1)
                ? Math.Max(1, current + 1)
                : 1;
        }
        else
        {
            current = 1;
        }

        Preferences.Default.Set(SharedQualityStreakLastDayKey, dayLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Preferences.Default.Set(SharedQualityStreakCountKey, current);
        return current;
    }

    private static string BuildStatus(string lang, int streak, int weeklyCount, int qualityStreak, int bonusPoints, bool awarded, List<string>? bonusParts = null)
    {
        if (lang == "en")
        {
            if (!awarded)
                return $"Shared streak: {streak} day(s) | quality streak: {qualityStreak} | week posts: {weeklyCount} (bonus already claimed today).";

            var details = bonusParts == null || bonusParts.Count == 0
                ? ""
                : $" {string.Join(", ", bonusParts)}.";
            return $"Social bonus +{bonusPoints}. Shared streak: {streak} | quality streak: {qualityStreak} | week posts: {weeklyCount}.{details}";
        }

        if (!awarded)
            return $"Serie partage: {streak} jour(s) | serie qualite: {qualityStreak} | posts semaine: {weeklyCount} (bonus deja recupere aujourd'hui).";

        var detailsFr = bonusParts == null || bonusParts.Count == 0
            ? ""
            : $" {string.Join(", ", bonusParts)}.";
        return $"Bonus social +{bonusPoints}. Serie partage: {streak} | serie qualite: {qualityStreak} | posts semaine: {weeklyCount}.{detailsFr}";
    }

    private static string CurrentLanguage()
    {
        var lang = (Preferences.Default.Get("app_lang", "fr") ?? "fr").Trim().ToLowerInvariant();
        return lang == "en" ? "en" : "fr";
    }
}

public sealed class GamificationPostResult
{
    public int BonusPoints { get; }
    public int SharedStreakDays { get; }
    public int WeeklySharedPosts { get; }
    public string Status { get; }

    public GamificationPostResult(int bonusPoints, int sharedStreakDays, int weeklySharedPosts, string status)
    {
        BonusPoints = Math.Max(0, bonusPoints);
        SharedStreakDays = Math.Max(0, sharedStreakDays);
        WeeklySharedPosts = Math.Max(0, weeklySharedPosts);
        Status = status ?? "";
    }

    public static GamificationPostResult None(string lang)
    {
        var text = lang == "en"
            ? "Private entry. Share a healthy post to unlock social streak bonuses."
            : "Entree privee. Partage un post sain pour debloquer des bonus de serie sociale.";

        return new GamificationPostResult(0, 0, 0, text);
    }
}
