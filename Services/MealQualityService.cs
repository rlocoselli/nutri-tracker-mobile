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
        var result = Compute(
            rawText,
            notes,
            detectedItems,
            calories,
            proteinG,
            carbsG,
            overallConfidence);

        return (result.Score, result.Label);
    }

    public static string BuildScoreExplanation(
        string rawText,
        string notes,
        IEnumerable<string>? detectedItems,
        double calories,
        double proteinG,
        double carbsG,
        double overallConfidence,
        string lang,
        int maxFactors = 6,
        bool includeHeader = true)
    {
        var result = Compute(rawText, notes, detectedItems, calories, proteinG, carbsG, overallConfidence);
        var normalized = NormalizeLang(lang);

        var factors = result.Factors
            .Where(x => !string.Equals(x.Reason, "Base quality baseline", StringComparison.Ordinal))
            .Where(x => Math.Abs(x.Delta) >= 0.4)
            .OrderByDescending(x => Math.Abs(x.Delta))
            .Take(Math.Max(1, maxFactors))
            .Select(x => FormatFactor(x, normalized))
            .ToList();

        if (factors.Count == 0)
            factors.Add(normalized == "en" ? "No strong factor detected." : "Aucun facteur fort detecte.");

        var separator = normalized == "en" ? " | " : " | ";
        var body = string.Join(separator, factors);

        if (!includeHeader)
            return body;

        return normalized == "en"
            ? $"Why this score: {body}"
            : $"Pourquoi ce score: {body}";
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

    public static string GetFoodStyleBadge(double score, string lang)
    {
        var normalized = NormalizeLang(lang);

        if (score >= 75)
            return normalized == "en" ? "🥗 Healthy food badge" : "🥗 Badge bouffe saine";

        if (score <= 45)
            return normalized == "en" ? "🍔 Junk food badge" : "🍔 Badge mauvaise bouffe";

        return normalized == "en" ? "⚖️ Balanced food badge" : "⚖️ Badge bouffe equilibree";
    }

    public static string GetTigerCatMood(double score, string lang)
    {
        var normalized = NormalizeLang(lang);
        if (score >= 70)
            return normalized == "en" ? "🐯😺 Happy tiger cat" : "🐯😺 Chat tigre content";

        return normalized == "en" ? "🐯😾 Grumpy tiger cat" : "🐯😾 Chat tigre mecontent";
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

    private static string NormalizeLang(string lang)
    {
        var normalized = (lang ?? "fr").Trim().ToLowerInvariant();
        return normalized == "en" ? "en" : "fr";
    }

    private static MealQualityComputation Compute(
        string rawText,
        string notes,
        IEnumerable<string>? detectedItems,
        double calories,
        double proteinG,
        double carbsG,
        double overallConfidence)
    {
        var factors = new List<MealQualityFactor>();
        var score = 0d;

        AddFactor(factors, ref score, +42d, "Base quality baseline");

        var confidenceDelta = Clamp(overallConfidence, 0, 1) * 12d;
        AddFactor(factors, ref score, confidenceDelta, "AI confidence");

        var proteinDensity = proteinG / Math.Max(1d, calories / 100d);
        var proteinDensityDelta = Clamp(proteinDensity * 3.2d, 0, 14);
        AddFactor(factors, ref score, proteinDensityDelta, "Protein density");

        if (calories is >= 250 and <= 780) AddFactor(factors, ref score, +8, "Balanced calorie range");
        else if (calories is >= 150 and <= 980) AddFactor(factors, ref score, +3, "Acceptable calorie range");
        else AddFactor(factors, ref score, -8, "Calories outside healthy range");

        if (carbsG > proteinG * 3.2) AddFactor(factors, ref score, -10, "Carbs much higher than protein");
        if (calories >= 750 && proteinG < 25) AddFactor(factors, ref score, -8, "High calories with low protein");

        var lowered = BuildContext(rawText, notes, detectedItems).ToLowerInvariant();
        var isFastFood = ContainsAny(lowered,
            "big mac", "bigmac", "mcdo", "mcdonald", "burger", "whopper", "double cheese", "cheeseburger", "fries", "frites", "kfc", "pizza", "shawarma", "tacos", "nuggets", "donut", "hot dog");
        var isFried = ContainsAny(lowered, "frit", "fried", "pané", "pane", "breaded");
        var hasSugaryDrinks = ContainsAny(lowered, "soda", "cola", "fanta", "sprite", "boisson sucr", "sugary drink", "milkshake");
        var isUltraProcessed = ContainsAny(lowered, "ultra", "transfo", "processed", "transforme", "industrial");

        if (isFastFood) AddFactor(factors, ref score, -30, "Fast-food detected");
        if (isFried) AddFactor(factors, ref score, -15, "Fried preparation");
        if (hasSugaryDrinks) AddFactor(factors, ref score, -18, "Sugary drink detected");
        if (isUltraProcessed) AddFactor(factors, ref score, -14, "Ultra-processed profile");
        if (isFastFood && (isFried || hasSugaryDrinks)) AddFactor(factors, ref score, -15, "Fast-food combo penalty");

        var hasVegetables = ContainsAny(lowered, "veget", "légume", "legume", "salad", "brocoli", "carotte", "epinard", "spinach");
        var hasLeanProtein = ContainsAny(lowered, "grill", "lean", "poisson", "fish", "chicken breast", "blanc de poulet", "tofu", "oeuf", "egg");
        var hasWholeFoods = ContainsAny(lowered, "complet", "avoine", "oats", "lentil", "lentille", "pois chiche", "beans", "fruit");
        var hasWater = ContainsAny(lowered, "eau", "water") && !hasSugaryDrinks;

        if (hasVegetables) AddFactor(factors, ref score, +8, "Vegetables/fiber sources");
        if (hasLeanProtein) AddFactor(factors, ref score, +8, "Lean protein source");
        if (hasWholeFoods) AddFactor(factors, ref score, +6, "Whole-food ingredients");
        if (hasWater) AddFactor(factors, ref score, +2, "Water with meal");

        if (isFastFood)
        {
            var cap = isFried || hasSugaryDrinks ? 35d : 42d;
            var beforeCap = score;
            score = Math.Min(score, cap);
            var capDelta = score - beforeCap;
            if (capDelta < 0)
                AddFactor(factors, ref score, 0, $"Guardrail cap for fast-food ({Math.Round(cap)}/100)", capDelta);
        }

        var beforeClamp = score;
        score = Clamp(score, 0, 100);
        if (Math.Abs(score - beforeClamp) >= 0.1)
            AddFactor(factors, ref score, 0, "Clamped to score bounds", score - beforeClamp);

        var label = score >= 78
            ? "Excellent"
            : score >= 62
                ? "Bon"
                : score >= 42
                    ? "Moyen"
                    : "À améliorer";

        return new MealQualityComputation(score, label, factors);
    }

    private static void AddFactor(List<MealQualityFactor> factors, ref double score, double delta, string reason, double? recordedDelta = null)
    {
        score += delta;
        factors.Add(new MealQualityFactor(recordedDelta ?? delta, reason));
    }

    private static string FormatFactor(MealQualityFactor factor, string lang)
    {
        var sign = factor.Delta >= 0 ? "+" : "";
        var reason = TranslateReason(factor.Reason, lang);
        return $"{sign}{Math.Round(factor.Delta)} {reason}";
    }

    private static string TranslateReason(string reason, string lang)
    {
        if (lang == "en")
            return reason;

        return reason switch
        {
            "Base quality baseline" => "base de qualite",
            "AI confidence" => "confiance IA",
            "Protein density" => "densite proteique",
            "Balanced calorie range" => "calories dans la zone ideale",
            "Acceptable calorie range" => "calories dans une zone acceptable",
            "Calories outside healthy range" => "calories hors zone saine",
            "Carbs much higher than protein" => "glucides trop eleves vs proteines",
            "High calories with low protein" => "calories elevees avec peu de proteines",
            "Fast-food detected" => "fast-food detecte",
            "Fried preparation" => "preparation frite",
            "Sugary drink detected" => "boisson sucree detectee",
            "Ultra-processed profile" => "profil ultra-transforme",
            "Fast-food combo penalty" => "penalite combo fast-food",
            "Vegetables/fiber sources" => "presence de legumes/fibres",
            "Lean protein source" => "source de proteines maigres",
            "Whole-food ingredients" => "ingredients peu transformes",
            "Water with meal" => "eau associee au repas",
            _ when reason.StartsWith("Guardrail cap for fast-food", StringComparison.Ordinal) => "plafond anti fast-food",
            "Clamped to score bounds" => "ajustement aux bornes du score",
            _ => reason
        };
    }

    private sealed class MealQualityComputation
    {
        public double Score { get; }
        public string Label { get; }
        public IReadOnlyList<MealQualityFactor> Factors { get; }

        public MealQualityComputation(double score, string label, IReadOnlyList<MealQualityFactor> factors)
        {
            Score = score;
            Label = label;
            Factors = factors;
        }
    }

    private sealed class MealQualityFactor
    {
        public double Delta { get; }
        public string Reason { get; }

        public MealQualityFactor(double delta, string reason)
        {
            Delta = delta;
            Reason = reason;
        }
    }

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
