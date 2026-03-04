namespace NutritionTracker.Services;

public static class MealQualityService
{
    public static (double score, string label) Classify(
        string rawText,
        string notes,
        IEnumerable<string>? detectedItems,
        double calories,
        double proteinG,
        double carbsG,
        double overallConfidence)
    {
        var score = 50d;

        score += Clamp(overallConfidence, 0, 1) * 20d;

        var proteinDensity = proteinG / Math.Max(1d, calories / 100d);
        score += Clamp(proteinDensity * 4d, 0, 18);

        if (calories is >= 250 and <= 900) score += 12;
        else if (calories is >= 150 and <= 1200) score += 6;
        else score -= 6;

        if (carbsG > proteinG * 3.5) score -= 8;

        var lowered = BuildContext(rawText, notes, detectedItems).ToLowerInvariant();
        if (ContainsAny(lowered, "frit", "fried", "sucre", "sugar", "ultra", "soda", "transfo"))
            score -= 10;

        if (ContainsAny(lowered,
            "big mac", "bigmac", "mcdo", "mcdonald", "burger", "fries", "frites", "kfc", "pizza", "shawarma", "donut"))
            score -= 18;

        if (ContainsAny(lowered, "veget", "légume", "fib", "grill", "lean", "complet", "nature"))
            score += 8;

        if (ContainsAny(lowered, "eau", "water") && !ContainsAny(lowered, "soda"))
            score += 2;

        score = Clamp(score, 0, 100);

        var label = score >= 80
            ? "Excellent"
            : score >= 65
                ? "Bon"
                : score >= 45
                    ? "Moyen"
                    : "À améliorer";

        return (score, label);
    }

    public static string GetBadge(double score, string lang)
    {
        if (score >= 85)
            return lang == "en" ? "🏅 Gold badge" : "🏅 Badge Or";
        if (score >= 70)
            return lang == "en" ? "🥈 Silver badge" : "🥈 Badge Argent";
        if (score >= 55)
            return lang == "en" ? "🥉 Bronze badge" : "🥉 Badge Bronze";

        return lang == "en" ? "🎯 Starter badge" : "🎯 Badge Starter";
    }

    public static string GetSemaphore(double score, string lang)
    {
        if (score >= 80)
            return lang == "en" ? "🟢 Green: excellent meal" : "🟢 Vert : repas excellent";
        if (score >= 60)
            return lang == "en" ? "🟡 Yellow: acceptable meal" : "🟡 Jaune : repas acceptable";

        return lang == "en" ? "🔴 Red: to improve" : "🔴 Rouge : à améliorer";
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static double Clamp(double value, double min, double max)
        => Math.Max(min, Math.Min(max, value));

    private static string BuildContext(string rawText, string notes, IEnumerable<string>? detectedItems)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(rawText))
            parts.Add(rawText);

        if (!string.IsNullOrWhiteSpace(notes))
            parts.Add(notes);

        if (detectedItems != null)
        {
            foreach (var item in detectedItems)
            {
                if (!string.IsNullOrWhiteSpace(item))
                    parts.Add(item);
            }
        }

        return string.Join(" | ", parts);
    }
}
