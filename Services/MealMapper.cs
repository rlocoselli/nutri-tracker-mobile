using NutritionTracker.Models;
using NutritionTracker.Services.Dto;

namespace NutritionTracker.Services;

public static class MealMapper
{
    public static (MealEntry entry, List<MealItem> items) MapToDb(AnalyzeResponse r, string rawText, string photoPath)
    {
        var dt = DateTime.TryParse(r.datetime_utc, out var parsedUtc) ? parsedUtc : DateTime.UtcNow;
        dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

        var entry = new MealEntry
        {
            DateUtc = dt,
            DayKeyUtc = dt.ToString("yyyy-MM-dd"),
            RawText = rawText ?? "",
            PhotoPath = photoPath ?? "",
            TotalCalories = r.meal.totals.calories,
            TotalCarbsG = r.meal.totals.carbs_g,
            TotalProteinG = r.meal.totals.protein_g,
            OverallConfidence = r.meal.overall_confidence,
        };

        var items = r.meal.items.Select(i => new MealItem
        {
            MealEntryId = entry.Id,
            Name = i.name,
            Quantity = i.quantity,
            Unit = i.unit,
            EstimatedGrams = i.estimated_grams,
            Calories = i.macros.calories,
            CarbsG = i.macros.carbs_g,
            ProteinG = i.macros.protein_g,
            Confidence = i.confidence,
        }).ToList();

        return (entry, items);
    }
}
