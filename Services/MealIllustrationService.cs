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
                  // Background
                  "<rect width='100%' height='100%' fill='#fff7ed' rx='24'/>" +
                  // Subtle grid pattern
                  "<rect x='0' y='0' width='640' height='640' fill='url(#grid)' rx='24' opacity='0.4'/>" +
                  "<defs><pattern id='grid' width='40' height='40' patternUnits='userSpaceOnUse'><path d='M 40 0 L 0 0 0 40' fill='none' stroke='#fed7aa' stroke-width='0.5'/></pattern></defs>" +
                  // Steam puffs above bowl
                  "<path d='M260 195 Q250 175 260 155 Q270 135 260 115' stroke='#f59e0b' stroke-width='5' fill='none' stroke-linecap='round' opacity='0.55'/>" +
                  "<path d='M320 185 Q308 162 320 140 Q332 118 320 96' stroke='#f97316' stroke-width='5' fill='none' stroke-linecap='round' opacity='0.55'/>" +
                  "<path d='M380 195 Q370 175 380 155 Q390 135 380 115' stroke='#f59e0b' stroke-width='5' fill='none' stroke-linecap='round' opacity='0.55'/>" +
                  // Bowl shadow
                  "<ellipse cx='320' cy='430' rx='172' ry='18' fill='#f97316' opacity='0.15'/>" +
                  // Bowl body
                  "<path d='M148 310 Q148 460 320 460 Q492 460 492 310 Z' fill='#fef3c7' stroke='#f59e0b' stroke-width='8'/>" +
                  // Bowl rim
                  "<ellipse cx='320' cy='310' rx='172' ry='30' fill='#ffffff' stroke='#f59e0b' stroke-width='8'/>" +
                  // Food inside bowl — colourful dots/circles
                  "<circle cx='285' cy='330' r='22' fill='#f97316'/>" +
                  "<circle cx='340' cy='325' r='18' fill='#84cc16'/>" +
                  "<circle cx='310' cy='350' r='16' fill='#fb923c'/>" +
                  "<circle cx='355' cy='348' r='20' fill='#facc15'/>" +
                  "<circle cx='270' cy='355' r='14' fill='#f43f5e'/>" +
                  "<circle cx='320' cy='368' r='12' fill='#a3e635'/>" +
                  // Label banner
                  "<rect x='100' y='488' width='440' height='88' rx='22' fill='#ffffff' stroke='#fed7aa' stroke-width='4'/>" +
                  "<text x='320' y='526' text-anchor='middle' font-size='32' font-family='Arial, sans-serif' font-weight='bold' fill='#7c2d12'>" + line1 + "</text>" +
                  "<text x='320' y='562' text-anchor='middle' font-size='24' font-family='Arial, sans-serif' fill='#9a3412'>" + line2 + "</text>" +
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
