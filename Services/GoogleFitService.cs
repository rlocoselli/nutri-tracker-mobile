using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NutritionTracker.Services;

public class GoogleFitService
{
    public static bool Enabled => FeatureFlags.EnableGoogleFit && Preferences.Default.Get("fit_integration_enabled", false);

    private readonly HttpClient _http = new();

    public async Task<(int steps, double burnedCalories)> GetTodaySummaryAsync(string accessToken)
    {
        if (!Enabled)
            return (0, 0);

        if (string.IsNullOrWhiteSpace(accessToken))
            return (0, 0);

        var startLocal = DateTime.Now.Date;
        var endLocal = startLocal.AddDays(1);

        long startMillis = new DateTimeOffset(startLocal).ToUnixTimeMilliseconds();
        long endMillis = new DateTimeOffset(endLocal).ToUnixTimeMilliseconds();

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/fitness/v1/users/me/dataset:aggregate");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var payload = new
        {
            aggregateBy = new[]
            {
                new { dataTypeName = "com.google.step_count.delta" },
                new { dataTypeName = "com.google.calories.expended" }
            },
            bucketByTime = new { durationMillis = endMillis - startMillis },
            startTimeMillis = startMillis,
            endTimeMillis = endMillis
        };

        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            if ((int)resp.StatusCode == 403 && json.Contains("insufficient", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Google Fit scope manquant. Déconnectez-vous, supprimez l'accès NutritionTracker dans votre compte Google, puis reconnectez-vous en acceptant Google Fit.");

            throw new Exception($"Google Fit sync failed: {(int)resp.StatusCode} {json}");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        int steps = 0;
        double burned = 0;

        if (root.TryGetProperty("bucket", out var buckets) && buckets.ValueKind == JsonValueKind.Array)
        {
            foreach (var bucket in buckets.EnumerateArray())
            {
                if (!bucket.TryGetProperty("dataset", out var datasets) || datasets.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var dataset in datasets.EnumerateArray())
                {
                    if (!dataset.TryGetProperty("point", out var points) || points.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var point in points.EnumerateArray())
                    {
                        if (!point.TryGetProperty("dataTypeName", out var dtNameEl))
                            continue;

                        var dataTypeName = dtNameEl.GetString() ?? "";
                        if (!point.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
                            continue;

                        foreach (var value in values.EnumerateArray())
                        {
                            if (dataTypeName == "com.google.step_count.delta" && value.TryGetProperty("intVal", out var i))
                                steps += i.GetInt32();

                            if (dataTypeName == "com.google.calories.expended")
                            {
                                if (value.TryGetProperty("fpVal", out var fp))
                                {
                                    var s = fp.GetRawText();
                                    if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                                        burned += d;
                                }
                                else if (value.TryGetProperty("intVal", out var ci))
                                {
                                    burned += ci.GetInt32();
                                }
                            }
                        }
                    }
                }
            }
        }

        if (steps == 0)
            steps = await ReadStepsFromEstimatedDataSourceAsync(accessToken, startLocal, endLocal);

        if (burned <= 0)
            burned = await ReadCaloriesFromMergedDataSourceAsync(accessToken, startLocal, endLocal);

        return (Math.Max(0, steps), Math.Max(0, burned));
    }

    private async Task<int> ReadStepsFromEstimatedDataSourceAsync(string accessToken, DateTime startLocal, DateTime endLocal)
    {
        var startNs = new DateTimeOffset(startLocal).ToUnixTimeMilliseconds() * 1_000_000;
        var endNs = new DateTimeOffset(endLocal).ToUnixTimeMilliseconds() * 1_000_000;
        var datasetId = $"{startNs}-{endNs}";

        var url = $"https://www.googleapis.com/fitness/v1/users/me/dataSources/derived:com.google.step_count.delta:com.google.android.gms:estimated_steps/datasets/{datasetId}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return 0;
        var json = await resp.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("point", out var points) || points.ValueKind != JsonValueKind.Array)
            return 0;

        var steps = 0;
        foreach (var point in points.EnumerateArray())
        {
            if (!point.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var value in values.EnumerateArray())
            {
                if (value.TryGetProperty("intVal", out var i))
                    steps += i.GetInt32();
            }
        }

        return Math.Max(0, steps);
    }

    private async Task<double> ReadCaloriesFromMergedDataSourceAsync(string accessToken, DateTime startLocal, DateTime endLocal)
    {
        var startNs = new DateTimeOffset(startLocal).ToUnixTimeMilliseconds() * 1_000_000;
        var endNs = new DateTimeOffset(endLocal).ToUnixTimeMilliseconds() * 1_000_000;
        var datasetId = $"{startNs}-{endNs}";

        var url = $"https://www.googleapis.com/fitness/v1/users/me/dataSources/derived:com.google.calories.expended:com.google.android.gms:merge_calories_expended/datasets/{datasetId}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return 0;
        var json = await resp.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("point", out var points) || points.ValueKind != JsonValueKind.Array)
            return 0;

        double burned = 0;
        foreach (var point in points.EnumerateArray())
        {
            if (!point.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var value in values.EnumerateArray())
            {
                if (value.TryGetProperty("fpVal", out var fp))
                {
                    var s = fp.GetRawText();
                    if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                        burned += d;
                }
                else if (value.TryGetProperty("intVal", out var i))
                {
                    burned += i.GetInt32();
                }
            }
        }

        return Math.Max(0, burned);
    }
}
