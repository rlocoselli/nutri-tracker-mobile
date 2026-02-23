using SQLite;

namespace NutritionTracker.Models;

public class ExerciseEntry
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Indexed]
    public DateTime DateUtc { get; set; }

    [Indexed]
    public string DayKeyUtc { get; set; } = ""; // yyyy-MM-dd

    public int GoogleFitSteps { get; set; }
    public double ExerciseMinutes { get; set; }
    public double BurnedCalories { get; set; }

    public string Source { get; set; } = "manual-google-fit-test";
    public string Notes { get; set; } = "";
}
