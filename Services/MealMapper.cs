using NutritionTracker.Models;
using NutritionTracker.Services.Dto;

namespace NutritionTracker.Services;

public static class MealMapper
{
    public static (MealEntry entry, List<MealItem> items) MapToDb(AnalyzeResponse r, string rawText, string photoPath)
    {
        var dt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        var cleanedRaw = rawText?.Trim() ?? "";
        var fallbackFromItems = string.Join(", ",
            r.meal.items
                .Select(i => i.name?.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .Take(5));

        var description = !string.IsNullOrWhiteSpace(cleanedRaw)
            ? cleanedRaw
            : !string.IsNullOrWhiteSpace(r.meal.notes)
                ? r.meal.notes.Trim()
                : fallbackFromItems;

        var quality = MealQualityService.Classify(
            cleanedRaw,
            r.meal.notes,
            r.meal.items.Select(i => i.name),
            r.meal.totals.calories,
            r.meal.totals.protein_g,
            r.meal.totals.carbs_g,
            r.meal.overall_confidence);

        var entry = new MealEntry
        {
            DateUtc = dt,
            DayKeyUtc = dt.ToString("yyyy-MM-dd"),
            RawText = cleanedRaw,
            Description = description,
            AiNotes = r.meal.notes?.Trim() ?? "",
            PhotoPath = photoPath ?? "",
            MealType = MealTypeService.DetectByLocalTime(dt.ToLocalTime()),
            TotalCalories = r.meal.totals.calories,
            TotalCarbsG = r.meal.totals.carbs_g,
            TotalProteinG = r.meal.totals.protein_g,
            OverallConfidence = r.meal.overall_confidence,
            QualityScore = quality.score,
            QualityLabel = quality.label,
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
