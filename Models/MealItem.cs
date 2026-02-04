using SQLite;

namespace NutritionTracker.Models;

public class MealItem
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Indexed]
    public string MealEntryId { get; set; } = "";

    public string Name { get; set; } = "";
    public double Quantity { get; set; }
    public string Unit { get; set; } = "";
    public double EstimatedGrams { get; set; }

    public double Calories { get; set; }
    public double CarbsG { get; set; }
    public double ProteinG { get; set; }

    public double Confidence { get; set; }
}
