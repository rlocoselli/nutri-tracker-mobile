namespace NutritionTracker.Services.Dto;

public class RecommendationsResponse
{
    public string schema_version { get; set; } = "";
    public string user_id { get; set; } = "";
    public string datetime_utc { get; set; } = "";
    public List<RecommendationItem> recommendations { get; set; } = new();
    public Insights insights { get; set; } = new();
    public List<string> warnings { get; set; } = new();
}

public class RecommendationItem
{
    public string title { get; set; } = "";
    public string why { get; set; } = "";
    public List<string> actions { get; set; } = new();
    public string image { get; set; } = "reco_balance.svg";
}

public class Insights
{
    public double avg_calories { get; set; }
    public double avg_carbs_g { get; set; }
    public double avg_protein_g { get; set; }
}
