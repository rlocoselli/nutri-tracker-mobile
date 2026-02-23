using SQLite;

namespace NutritionTracker.Models;

public class MealEntry
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Indexed]
    public DateTime DateUtc { get; set; }

    [Indexed]
    public string DayKeyUtc { get; set; } = ""; // yyyy-MM-dd

    public string RawText { get; set; } = "";
    public string Description { get; set; } = "";
    public string PhotoPath { get; set; } = "";

    public double TotalCalories { get; set; }
    public double TotalCarbsG { get; set; }
    public double TotalProteinG { get; set; }

    public double OverallConfidence { get; set; }
}
