namespace NutritionTracker.Services;

public static class MealTypeService
{
    public static readonly string[] SupportedTypes = ["breakfast", "lunch", "dinner", "snack"];

    public static string Normalize(string? mealType)
    {
        var normalized = (mealType ?? "").Trim().ToLowerInvariant();
        return normalized switch
        {
            "breakfast" => "breakfast",
            "lunch" => "lunch",
            "dinner" => "dinner",
            "snack" => "snack",
            _ => "snack",
        };
    }

    public static string DetectByLocalTime(DateTime localDateTime)
    {
        var hour = localDateTime.Hour;
        if (hour >= 5 && hour < 10)
            return "breakfast";

        if (hour >= 10 && hour < 15)
            return "lunch";

        if (hour >= 18 && hour < 23)
            return "dinner";

        return "snack";
    }

    public static string Label(string mealType)
    {
        var normalized = Normalize(mealType);
        return normalized switch
        {
            "breakfast" => LocalizationService.T("meal_type_breakfast"),
            "lunch" => LocalizationService.T("meal_type_lunch"),
            "dinner" => LocalizationService.T("meal_type_dinner"),
            _ => LocalizationService.T("meal_type_snack"),
        };
    }
}
