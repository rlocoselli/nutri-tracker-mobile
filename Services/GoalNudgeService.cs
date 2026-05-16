using NutritionTracker.Models;

namespace NutritionTracker.Services;

public sealed class GoalNudgeService
{
    private const string LastNudgeSlotDayKey = "goal_nudge_last_slot_day";
    private const string LastNudgeSlotNameKey = "goal_nudge_last_slot_name";

    private readonly BackendSyncService _sync;
    private bool _isPolling;

    public GoalNudgeService(BackendSyncService sync)
    {
        _sync = sync;
    }

    public async Task PollAndNotifyAsync()
    {
        if (_isPolling)
            return;

        _isPolling = true;
        try
        {
            var now = DateTime.Now;
            var slot = ResolveSlot(now);
            if (slot == null)
                return;

            var dayKey = now.ToString("yyyy-MM-dd");
            var lastDay = Preferences.Default.Get(LastNudgeSlotDayKey, "").Trim();
            var lastSlot = Preferences.Default.Get(LastNudgeSlotNameKey, "").Trim();
            if (string.Equals(dayKey, lastDay, StringComparison.Ordinal) && string.Equals(slot.Name, lastSlot, StringComparison.Ordinal))
                return;

            var token = Preferences.Default.Get("auth_id_token", "");
            var identityOk = await _sync.EnsureBackendIdentityAsync(token);
            if (!identityOk)
                return;

            var goals = await _sync.GetGoalsAsync();
            var mealsToday = await LoadTodayMealsAsync();
            var cal = mealsToday.Sum(x => x.TotalCalories);
            var protein = mealsToday.Sum(x => x.TotalProteinG);
            var carbs = mealsToday.Sum(x => x.TotalCarbsG);

            var nudge = BuildNudge(slot, goals, cal, protein, carbs);
            if (string.IsNullOrWhiteSpace(nudge))
                return;

            Preferences.Default.Set(LastNudgeSlotDayKey, dayKey);
            Preferences.Default.Set(LastNudgeSlotNameKey, slot.Name);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var page = Application.Current?.Windows.Count > 0 ? Application.Current.Windows[0].Page : null;
                if (page == null)
                    return;

                await page.DisplayAlert(LocalizationService.T("goal_nudge_title"), nudge, "OK");
            });
        }
        finally
        {
            _isPolling = false;
        }
    }

    private async Task<List<MealEntry>> LoadTodayMealsAsync()
    {
        var localDay = DateTime.Now.Date;
        var fromUtc = DateTime.SpecifyKind(localDay, DateTimeKind.Local).ToUniversalTime();
        var toUtc = DateTime.SpecifyKind(localDay.AddDays(1), DateTimeKind.Local).ToUniversalTime();
        var backend = await _sync.GetMealsBetweenUtcAsync(fromUtc, toUtc, includePhoto: false);

        return backend.Select(x => new MealEntry
        {
            Id = x.id,
            DateUtc = x.date_utc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(x.date_utc, DateTimeKind.Utc) : x.date_utc.ToUniversalTime(),
            TotalCalories = x.total_calories,
            TotalProteinG = x.total_protein_g,
            TotalCarbsG = x.total_carbs_g,
            StoryVisibility = x.story_visibility,
            QualityScore = x.quality_score,
        }).Where(x => x.DateUtc >= fromUtc && x.DateUtc < toUtc).ToList();
    }

    private static NudgeSlot? ResolveSlot(DateTime now)
    {
        var hour = now.Hour;
        if (hour >= 10 && hour < 12)
            return new NudgeSlot("mid_morning", "10:00-12:00");

        if (hour >= 14 && hour < 16)
            return new NudgeSlot("afternoon", "14:00-16:00");

        if (hour >= 19 && hour < 21)
            return new NudgeSlot("evening", "19:00-21:00");

        return null;
    }

    private static string BuildNudge(NudgeSlot slot, UserGoals goals, double cal, double protein, double carbs)
    {
        var lang = (Preferences.Default.Get("app_lang", "fr") ?? "fr").Trim().ToLowerInvariant();
        var isEn = lang == "en";

        var calRatio = goals.CaloriesTarget <= 0 ? 1 : cal / goals.CaloriesTarget;
        var protRatio = goals.ProteinGTarget <= 0 ? 1 : protein / goals.ProteinGTarget;
        var carbsRatio = goals.CarbsGTarget <= 0 ? 1 : carbs / goals.CarbsGTarget;

        var deficits = new List<(string Type, double Score)>
        {
            ("protein", Math.Max(0, 1 - protRatio)),
            ("calories", Math.Max(0, 1 - calRatio)),
            ("carbs", Math.Max(0, 0.85 - carbsRatio))
        };

        var max = deficits.OrderByDescending(x => x.Score).First();
        if (max.Score < 0.15)
            return "";

        if (isEn)
        {
            return max.Type switch
            {
                "protein" => $"{slot.Label}: You are behind on protein. Add a protein-first snack or dinner to stay on target.",
                "calories" => $"{slot.Label}: You are below your energy target. Add a balanced meal to avoid late cravings.",
                _ => $"{slot.Label}: Carbs look low. Add quality carbs (fruit, oats, legumes) to keep your day balanced."
            };
        }

        return max.Type switch
        {
            "protein" => $"{slot.Label}: Tu es en retard sur les proteines. Ajoute une collation ou un repas riche en proteines.",
            "calories" => $"{slot.Label}: Tu es sous ton objectif calorique. Ajoute un repas equilibre pour eviter les fringales tardives.",
            _ => $"{slot.Label}: Les glucides semblent bas. Ajoute des glucides de qualite (fruits, avoine, legumes)."
        };
    }

    private sealed class NudgeSlot
    {
        public string Name { get; }
        public string Label { get; }

        public NudgeSlot(string name, string label)
        {
            Name = name;
            Label = label;
        }
    }
}
