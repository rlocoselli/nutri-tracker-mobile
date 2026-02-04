using SQLite;

namespace NutritionTracker.Models;

public class UserGoals
{
    [PrimaryKey]
    public int Id { get; set; } = 1;

    public double CaloriesTarget { get; set; } = 2000;
    public double CarbsGTarget { get; set; } = 220;
    public double ProteinGTarget { get; set; } = 120;
}
