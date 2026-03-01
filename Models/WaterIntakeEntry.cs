using SQLite;

namespace NutritionTracker.Models;

public class WaterIntakeEntry
{
    [PrimaryKey]
    public string DayKeyUtc { get; set; } = ""; // yyyy-MM-dd

    [Indexed]
    public DateTime DateUtc { get; set; }

    public double Liters { get; set; }
}
