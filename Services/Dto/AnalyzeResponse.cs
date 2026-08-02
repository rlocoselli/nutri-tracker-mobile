namespace NutritionTracker.Services.Dto;

public class AnalyzeResponse
{
    public string schema_version { get; set; } = "";
    public string user_id { get; set; } = "";
    public string datetime_utc { get; set; } = "";
    public Meal meal { get; set; } = new();
}

public class Meal
{
    public string language { get; set; } = "pt";
    public List<MealItemDto> items { get; set; } = new();
    public Totals totals { get; set; } = new();
    public string notes { get; set; } = "";
    public double overall_confidence { get; set; }
}

public class MealItemDto
{
    public string name { get; set; } = "";
    public double quantity { get; set; }
    public string unit { get; set; } = "";
    public double estimated_grams { get; set; }
    public Macros macros { get; set; } = new();
    public double confidence { get; set; }
}

public class Macros
{
    public double calories { get; set; }
    public double carbs_g { get; set; }
    public double protein_g { get; set; }
    public double fat_g { get; set; }
}

public class Totals
{
    public double calories { get; set; }
    public double carbs_g { get; set; }
    public double protein_g { get; set; }
    public double fat_g { get; set; }
}
