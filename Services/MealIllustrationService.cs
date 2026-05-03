using System.Text;

namespace NutritionTracker.Services;

public static class MealIllustrationService
{
    public static string GenerateDataUri(string? mealText, string? mealType, string? lang)
    {
        var safeLabel = BuildLabel(mealText, mealType, lang);
        var line1 = EscapeXml(safeLabel.Count > 0 ? safeLabel[0] : LocalizationService.T("story_meal"));
        var line2 = EscapeXml(safeLabel.Count > 1 ? safeLabel[1] : "");

        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='640' height='640' viewBox='0 0 640 640'>" +
                  "<rect width='100%' height='100%' fill='#fff7ed'/>" +
                  "<circle cx='320' cy='290' r='185' fill='#fef3c7' stroke='#f59e0b' stroke-width='10'/>" +
                  "<circle cx='320' cy='290' r='140' fill='#ffffff' stroke='#fdba74' stroke-width='6'/>" +
                  "<rect x='116' y='430' width='408' height='90' rx='22' fill='#ffffff' stroke='#fed7aa' stroke-width='4'/>" +
                  "<text x='320' y='468' text-anchor='middle' font-size='34' font-family='Arial, sans-serif' fill='#7c2d12'>" + line1 + "</text>" +
                  "<text x='320' y='505' text-anchor='middle' font-size='26' font-family='Arial, sans-serif' fill='#9a3412'>" + line2 + "</text>" +
                  "</svg>";

        var bytes = Encoding.UTF8.GetBytes(svg);
        return $"data:image/svg+xml;base64,{Convert.ToBase64String(bytes)}";
    }

    private static List<string> BuildLabel(string? mealText, string? mealType, string? lang)
    {
        var normalized = (mealText ?? "").Trim();
        var compact = string.Join(" ", normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(compact))
        {
            var typeLabel = MealTypeService.Label((mealType ?? "").Trim());
            if (string.IsNullOrWhiteSpace(typeLabel))
                typeLabel = LocalizationService.T("story_meal");

            compact = typeLabel;
        }

        var first = compact.Length > 26 ? compact[..26].TrimEnd() : compact;
        var rest = compact.Length > 26 ? compact[26..].TrimStart() : "";
        var second = rest.Length > 28 ? rest[..28].TrimEnd() : rest;

        var lines = new List<string> { first };
        if (!string.IsNullOrWhiteSpace(second))
            lines.Add(second);

        return lines;
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }
}
