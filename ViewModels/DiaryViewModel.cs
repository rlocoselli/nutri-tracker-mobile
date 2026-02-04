using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Models;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class DiaryViewModel : ObservableObject
{
    private readonly LocalDb _db;

    // --- Day navigation ---
    [ObservableProperty] private DateTime selectedDayLocal = DateTime.Now.Date;
    [ObservableProperty] private string selectedDayText = "";

    // Meals for the selected day
    public ObservableCollection<DiaryMealItem> Meals { get; } = new();

    [ObservableProperty] private string dayTotalsText = "";

    // --- Chart ---
    public ObservableCollection<string> MetricOptions { get; } = new() { "Calories", "Proteína", "Carboidratos" };
    public ObservableCollection<string> PeriodOptions { get; } = new() { "Dia", "Semana", "Mês" };

    [ObservableProperty] private string selectedMetric = "Calories";
    [ObservableProperty] private string selectedPeriod = "Dia";

    [ObservableProperty] private IList<double> chartValues = Array.Empty<double>();
    [ObservableProperty] private IList<string> chartLabels = Array.Empty<string>();

    public DiaryViewModel(LocalDb db)
    {
        _db = db;
        UpdateSelectedDayText();
    }

    partial void OnSelectedDayLocalChanged(DateTime value)
    {
        UpdateSelectedDayText();
    }

    partial void OnSelectedMetricChanged(string value)
    {
        // Rebuild chart for the new metric
        _ = LoadChartAsync();
    }

    partial void OnSelectedPeriodChanged(string value)
    {
        // Rebuild chart for the new period
        _ = LoadChartAsync();
    }

    private void UpdateSelectedDayText()
    {
        // Example: "terça 04 fev"
        SelectedDayText = SelectedDayLocal.ToString("dddd dd MMM", CultureInfo.CurrentCulture);
    }

    public async Task LoadAsync()
    {
        // Called on page appearing
        await LoadDayAsync(SelectedDayLocal);
        await LoadChartAsync();
    }

    [RelayCommand]
    private async Task PrevDay()
    {
        SelectedDayLocal = SelectedDayLocal.AddDays(-1);
        await LoadDayAsync(SelectedDayLocal);
    }

    [RelayCommand]
    private async Task NextDay()
    {
        SelectedDayLocal = SelectedDayLocal.AddDays(1);
        await LoadDayAsync(SelectedDayLocal);
    }

    private async Task LoadDayAsync(DateTime dayLocal)
    {
        Meals.Clear();

        // Convert the local day range to UTC for DB queries
        var startLocal = DateTime.SpecifyKind(dayLocal.Date, DateTimeKind.Local);
        var fromUtc = startLocal.ToUniversalTime();
        var toUtc = startLocal.AddDays(1).ToUniversalTime();

        var entries = await _db.GetMealsBetweenUtcAsync(fromUtc, toUtc);
        foreach (var e in entries.OrderByDescending(e => e.DateUtc))
            Meals.Add(DiaryMealItem.FromEntry(e));

        var cal = entries.Sum(x => x.TotalCalories);
        var carbs = entries.Sum(x => x.TotalCarbsG);
        var prot = entries.Sum(x => x.TotalProteinG);

        DayTotalsText = $"Total: {Math.Round(cal)} kcal · C {Math.Round(carbs)}g · P {Math.Round(prot)}g";
    }

    private async Task LoadChartAsync()
    {
        // Window sizes (feel free to tweak)
        var nowLocal = DateTime.Now;

        DateTime fromLocal;
        if (SelectedPeriod == "Dia") fromLocal = nowLocal.Date.AddDays(-29);
        else if (SelectedPeriod == "Semana") fromLocal = nowLocal.Date.AddDays(-7 * 11); // ~12 weeks
        else fromLocal = new DateTime(nowLocal.Year, nowLocal.Month, 1).AddMonths(-11);   // 12 months

        var fromUtc = DateTime.SpecifyKind(fromLocal, DateTimeKind.Local).ToUniversalTime();
        var toUtc = DateTime.SpecifyKind(nowLocal.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

        var entries = await _db.GetMealsBetweenUtcAsync(fromUtc, toUtc);
        // Build a continuous series (fills missing days/weeks/months with zeros)
        var points = BuildSeries(entries, fromLocal.Date, nowLocal.Date.AddDays(1));

        ChartValues = points.Select(p => p.Value).ToList();
        ChartLabels = points.Select(p => p.Label).ToList();
    }

    private List<(string Label, double Value)> BuildSeries(List<MealEntry> entries, DateTime fromLocalInclusive, DateTime toLocalExclusive)
    {
        double Selector(MealEntry e) => SelectedMetric switch
        {
            "Proteína" => e.TotalProteinG,
            "Carboidratos" => e.TotalCarbsG,
            _ => e.TotalCalories,
        };

        if (SelectedPeriod == "Dia")
        {
            var byDay = entries
                .GroupBy(e => e.DateUtc.ToLocalTime().Date)
                .ToDictionary(g => g.Key, g => g.Sum(Selector));

            var points = new List<(string Label, double Value)>();
            for (var d = fromLocalInclusive.Date; d < toLocalExclusive.Date; d = d.AddDays(1))
            {
                byDay.TryGetValue(d, out var v);
                points.Add((d.ToString("dd/MM"), v));
            }
            return points;
        }

        if (SelectedPeriod == "Semana")
        {
            var byWeek = entries
                .GroupBy(e => IsoWeekKey(e.DateUtc.ToLocalTime().Date))
                .ToDictionary(g => g.Key, g => g.Sum(Selector));

            // Iterate weeks by advancing 7 days from the start date.
            var points = new List<(string Label, double Value)>();
            for (var d = fromLocalInclusive.Date; d <= toLocalExclusive.Date.AddDays(-1); d = d.AddDays(7))
            {
                var k = IsoWeekKey(d);
                byWeek.TryGetValue(k, out var v);
                points.Add((WeekLabel(k), v));
            }

            // In case the window is small and produces only 1 point, keep it.
            return points;
        }

        // "Mês"
        var byMonth = entries
            .GroupBy(e => (y: e.DateUtc.ToLocalTime().Year, m: e.DateUtc.ToLocalTime().Month))
            .ToDictionary(g => g.Key, g => g.Sum(Selector));

        var startMonth = new DateTime(fromLocalInclusive.Year, fromLocalInclusive.Month, 1);
        var endMonth = new DateTime(toLocalExclusive.AddDays(-1).Year, toLocalExclusive.AddDays(-1).Month, 1);

        var outPoints = new List<(string Label, double Value)>();
        for (var m = startMonth; m <= endMonth; m = m.AddMonths(1))
        {
            var label = $"{m.Month:00}/{(m.Year % 100):00}";
            byMonth.TryGetValue((m.Year, m.Month), out var v);
            outPoints.Add((label, v));
        }
        return outPoints;
    }

    private static int IsoWeekKey(DateTime d)
    {
        // Combine year and ISO week into a sortable key
        var week = ISOWeek.GetWeekOfYear(d);
        var year = ISOWeek.GetYear(d);
        return year * 100 + week;
    }

    private static string WeekLabel(int key)
    {
        var year = key / 100;
        var week = key % 100;
        return $"S{week:00}/{(year % 100):00}";
    }
}

public class DiaryMealItem
{
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string CaloriesText { get; set; } = "";
    public string ProteinText { get; set; } = "";
    public string CarbsText { get; set; } = "";

    public static DiaryMealItem FromEntry(MealEntry e)
    {
        var local = e.DateUtc.ToLocalTime();
        var title = string.IsNullOrWhiteSpace(e.RawText) ? "Refeição" : e.RawText;
        title = title.Length > 28 ? title.Substring(0, 28) + "…" : title;

        return new DiaryMealItem
        {
            Title = title,
            Subtitle = local.ToString("dddd dd MMM · HH:mm", CultureInfo.CurrentCulture),
            CaloriesText = $"{Math.Round(e.TotalCalories)} kcal",
            ProteinText = $"P {Math.Round(e.TotalProteinG)}g",
            CarbsText = $"C {Math.Round(e.TotalCarbsG)}g"
        };
    }
}
