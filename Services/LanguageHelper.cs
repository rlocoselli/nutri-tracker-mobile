using System.Text.RegularExpressions;

namespace NutritionTracker.Services;

public static class LanguageHelper
{
    // Very small heuristic detector (good enough for UI-level language routing)
    private static readonly Regex Tokenizer = new(@"[^\p{L}]+", RegexOptions.Compiled);

    private static readonly string[] Pt = { "o", "a", "de", "que", "não", "para", "com", "uma", "eu", "você", "também", "por" };
    private static readonly string[] Fr = { "le", "la", "les", "de", "que", "pas", "pour", "avec", "une", "je", "vous", "aussi" };
    private static readonly string[] Es = { "el", "la", "los", "de", "que", "no", "para", "con", "una", "yo", "usted", "también" };
    private static readonly string[] It = { "il", "la", "lo", "di", "che", "non", "per", "con", "una", "io", "voi", "anche" };
    private static readonly string[] En = { "the", "and", "not", "to", "for", "with", "a", "i", "you", "also" };
    private static readonly string[] De = { "der", "die", "das", "und", "nicht", "für", "mit", "ein", "ich", "du", "auch" };

    public static string DetectLanguageCode(string? text, string fallback = "pt")
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;

        var lower = text.Trim().ToLowerInvariant();
        var tokens = Tokenizer.Split(lower).Where(t => t.Length > 0).ToArray();
        if (tokens.Length == 0) return fallback;

        int Score(string[] words) => tokens.Count(t => words.Contains(t));

        var scores = new Dictionary<string, int>
        {
            ["pt"] = Score(Pt),
            ["fr"] = Score(Fr),
            ["es"] = Score(Es),
            ["it"] = Score(It),
            ["en"] = Score(En),
            ["de"] = Score(De),
        };

        var best = scores.OrderByDescending(kv => kv.Value).First();
        return best.Value == 0 ? fallback : best.Key;
    }
}
