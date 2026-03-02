using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Models;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class StatisticsViewModel : ObservableObject
{
    private readonly BackendSyncService _sync;

    public ObservableCollection<StatisticsToggleItem> PeriodTabs { get; } = new();

    [ObservableProperty] private string selectedPeriod = "Week";
    [ObservableProperty] private IList<double> chartValues = Array.Empty<double>();
    [ObservableProperty] private IList<string> chartLabels = Array.Empty<string>();
    [ObservableProperty] private IList<double> qualityDonutValues = Array.Empty<double>();
    [ObservableProperty] private IList<string> qualityDonutLabels = Array.Empty<string>();
    [ObservableProperty] private IList<double> hydrationValues = Array.Empty<double>();
    [ObservableProperty] private IList<string> hydrationLabels = Array.Empty<string>();

    [ObservableProperty] private string avgCaloriesText = "0 kcal";
    [ObservableProperty] private string avgProteinText = "0 g";
    [ObservableProperty] private string avgCarbsText = "0 g";
    [ObservableProperty] private string avgQualityText = "0/100";
    [ObservableProperty] private string avgHydrationText = "0 L";
    [ObservableProperty] private bool isLoading;

    public string TitleText => LocalizationService.T("stats_title");
    public string SubtitleText => LocalizationService.T("stats_subtitle");
    public string PeriodTitle => LocalizationService.T("period");
    public string AvgCaloriesTitle => LocalizationService.T("stats_avg_calories");
    public string AvgProteinTitle => LocalizationService.T("stats_avg_protein");
    public string AvgCarbsTitle => LocalizationService.T("stats_avg_carbs");
    public string AvgQualityTitle => LocalizationService.T("stats_avg_quality");
    public string AvgHydrationTitle => LocalizationService.T("stats_avg_hydration");
    public string QualitySplitTitle => LocalizationService.T("stats_quality_split");
    public string HydrationSeriesTitle => LocalizationService.T("stats_hydration_series");
    public string LoadingText => LocalizationService.T("main_loading");

    public StatisticsViewModel(BackendSyncService sync)
    {
        _sync = sync;
        RebuildPeriodTabs();
    }

    partial void OnSelectedPeriodChanged(string value)
    {
        RebuildPeriodTabs();
        _ = LoadAsync();
    }

    [RelayCommand]
    private Task SelectPeriodTab(StatisticsToggleItem? tab)
    {
        if (tab == null)
            return Task.CompletedTask;

        SelectedPeriod = tab.Key;
        return Task.CompletedTask;
    }

    public async Task LoadAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(SubtitleText));
        OnPropertyChanged(nameof(PeriodTitle));
        OnPropertyChanged(nameof(AvgCaloriesTitle));
        OnPropertyChanged(nameof(AvgProteinTitle));
        OnPropertyChanged(nameof(AvgCarbsTitle));
        OnPropertyChanged(nameof(AvgQualityTitle));
        OnPropertyChanged(nameof(AvgHydrationTitle));
        OnPropertyChanged(nameof(QualitySplitTitle));
        OnPropertyChanged(nameof(HydrationSeriesTitle));
        OnPropertyChanged(nameof(LoadingText));

        await LoadStatsAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadStatsAsync()
    {
        var anchorLocal = DateTime.Now.Date;
        DateTime fromLocal;
        DateTime toLocalExclusive;

        if (SelectedPeriod == "Day")
        {
            fromLocal = anchorLocal.AddDays(-13);
            toLocalExclusive = anchorLocal.AddDays(1);
        }
        else if (SelectedPeriod == "Week")
        {
            fromLocal = anchorLocal.AddDays(-7 * 11);
            toLocalExclusive = anchorLocal.AddDays(1);
        }
        else
        {
            fromLocal = new DateTime(anchorLocal.Year, anchorLocal.Month, 1).AddMonths(-11);
            toLocalExclusive = new DateTime(anchorLocal.Year, anchorLocal.Month, 1).AddMonths(1);
        }

        var fromUtc = DateTime.SpecifyKind(fromLocal, DateTimeKind.Local).ToUniversalTime();
        var toUtc = DateTime.SpecifyKind(toLocalExclusive, DateTimeKind.Local).ToUniversalTime();

        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        var backendMeals = identityOk
            ? await _sync.GetMealsBetweenUtcAsync(fromUtc, toUtc)
            : new List<BackendMeal>();
        var waterPoints = identityOk
            ? await _sync.GetWaterIntakeSeriesAsync(fromLocal.Date, toLocalExclusive.Date)
            : new List<BackendWaterPoint>();

        var entries = backendMeals.Select(ToMealEntry).ToList();

        if (entries.Count == 0)
        {
            AvgCaloriesText = "0 kcal";
            AvgProteinText = "0 g";
            AvgCarbsText = "0 g";
            AvgQualityText = "0/100";
            AvgHydrationText = "0 L";
            ChartValues = Array.Empty<double>();
            ChartLabels = Array.Empty<string>();
            HydrationValues = BuildHydrationSeries(new List<BackendWaterPoint>(), fromLocal.Date, toLocalExclusive.Date).Select(x => x.Value).ToList();
            HydrationLabels = BuildHydrationSeries(new List<BackendWaterPoint>(), fromLocal.Date, toLocalExclusive.Date).Select(x => x.Label).ToList();
            QualityDonutValues = new List<double> { 0, 0, 0 };
            QualityDonutLabels = new List<string> { LocalizationService.T("stats_quality_good"), LocalizationService.T("stats_quality_medium"), LocalizationService.T("stats_quality_low") };
            return;
        }

        AvgCaloriesText = $"{Math.Round(entries.Average(x => x.TotalCalories))} kcal";
        AvgProteinText = $"{Math.Round(entries.Average(x => x.TotalProteinG))} g";
        AvgCarbsText = $"{Math.Round(entries.Average(x => x.TotalCarbsG))} g";
        AvgQualityText = $"{Math.Round(entries.Average(x => x.QualityScore))}/100";
        AvgHydrationText = waterPoints.Count == 0
            ? "0 L"
            : $"{Math.Round(waterPoints.Average(x => x.Liters), 1):0.0} L";

        var points = BuildAverageSeries(entries, fromLocal.Date, toLocalExclusive.Date);
        ChartValues = points.Select(x => x.Value).ToList();
        ChartLabels = points.Select(x => x.Label).ToList();

        var hydration = BuildHydrationSeries(waterPoints, fromLocal.Date, toLocalExclusive.Date);
        HydrationValues = hydration.Select(x => x.Value).ToList();
        HydrationLabels = hydration.Select(x => x.Label).ToList();

        var good = entries.Count(x => x.QualityScore >= 75);
        var medium = entries.Count(x => x.QualityScore >= 45 && x.QualityScore < 75);
        var low = Math.Max(0, entries.Count - good - medium);

        QualityDonutValues = new List<double> { good, medium, low };
        QualityDonutLabels = new List<string> { LocalizationService.T("stats_quality_good"), LocalizationService.T("stats_quality_medium"), LocalizationService.T("stats_quality_low") };
    }

    private List<(string Label, double Value)> BuildHydrationSeries(List<BackendWaterPoint> rows, DateTime fromLocalInclusive, DateTime toLocalExclusive)
    {
        if (SelectedPeriod == "Day")
        {
            var grouped = rows
                .GroupBy(x => x.DayLocal.Date)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Liters));

            var result = new List<(string Label, double Value)>();
            for (var day = fromLocalInclusive; day < toLocalExclusive; day = day.AddDays(1))
            {
                grouped.TryGetValue(day, out var value);
                result.Add((day.ToString("dd/MM"), value));
            }

            return result;
        }

        if (SelectedPeriod == "Week")
        {
            var grouped = rows
                .GroupBy(x => IsoWeekKey(x.DayLocal.Date))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Liters));

            var result = new List<(string Label, double Value)>();
            for (var day = fromLocalInclusive; day < toLocalExclusive; day = day.AddDays(7))
            {
                var key = IsoWeekKey(day);
                grouped.TryGetValue(key, out var value);
                result.Add((WeekLabel(key), value));
            }

            return result;
        }

        var groupedByMonth = rows
            .GroupBy(x => (x.DayLocal.Year, x.DayLocal.Month))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Liters));

        var start = new DateTime(fromLocalInclusive.Year, fromLocalInclusive.Month, 1);
        var end = new DateTime(toLocalExclusive.AddDays(-1).Year, toLocalExclusive.AddDays(-1).Month, 1);
        var outPoints = new List<(string Label, double Value)>();

        for (var month = start; month <= end; month = month.AddMonths(1))
        {
            groupedByMonth.TryGetValue((month.Year, month.Month), out var value);
            outPoints.Add((month.ToString("MM/yy", CultureInfo.InvariantCulture), value));
        }

        return outPoints;
    }

    private List<(string Label, double Value)> BuildAverageSeries(List<MealEntry> entries, DateTime fromLocalInclusive, DateTime toLocalExclusive)
    {
        if (SelectedPeriod == "Day")
        {
            var grouped = entries
                .GroupBy(x => x.DateUtc.ToLocalTime().Date)
                .ToDictionary(g => g.Key, g => g.Average(x => x.TotalCalories));

            var result = new List<(string Label, double Value)>();
            for (var day = fromLocalInclusive; day < toLocalExclusive; day = day.AddDays(1))
            {
                grouped.TryGetValue(day, out var value);
                result.Add((day.ToString("dd/MM"), value));
            }

            return result;
        }

        if (SelectedPeriod == "Week")
        {
            var grouped = entries
                .GroupBy(x => IsoWeekKey(x.DateUtc.ToLocalTime().Date))
                .ToDictionary(g => g.Key, g => g.Average(x => x.TotalCalories));

            var result = new List<(string Label, double Value)>();
            for (var day = fromLocalInclusive; day < toLocalExclusive; day = day.AddDays(7))
            {
                var key = IsoWeekKey(day);
                grouped.TryGetValue(key, out var value);
                result.Add((WeekLabel(key), value));
            }

            return result;
        }

        var groupedByMonth = entries
            .GroupBy(x => (x.DateUtc.ToLocalTime().Year, x.DateUtc.ToLocalTime().Month))
            .ToDictionary(g => g.Key, g => g.Average(x => x.TotalCalories));

        var start = new DateTime(fromLocalInclusive.Year, fromLocalInclusive.Month, 1);
        var end = new DateTime(toLocalExclusive.AddDays(-1).Year, toLocalExclusive.AddDays(-1).Month, 1);
        var outPoints = new List<(string Label, double Value)>();

        for (var month = start; month <= end; month = month.AddMonths(1))
        {
            groupedByMonth.TryGetValue((month.Year, month.Month), out var value);
            outPoints.Add((month.ToString("MM/yy", CultureInfo.InvariantCulture), value));
        }

        return outPoints;
    }

    private static int IsoWeekKey(DateTime d)
    {
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

    private static MealEntry ToMealEntry(BackendMeal meal)
    {
        var dateUtc = meal.date_utc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(meal.date_utc, DateTimeKind.Utc)
            : meal.date_utc.ToUniversalTime();

        return new MealEntry
        {
            Id = meal.id,
            DateUtc = dateUtc,
            TotalCalories = meal.total_calories,
            TotalCarbsG = meal.total_carbs_g,
            TotalProteinG = meal.total_protein_g,
            QualityScore = meal.quality_score,
        };
    }

    private void RebuildPeriodTabs()
    {
        PeriodTabs.Clear();
        PeriodTabs.Add(new StatisticsToggleItem { Key = "Day", Label = LocalizationService.T("period_day"), IsSelected = SelectedPeriod == "Day" });
        PeriodTabs.Add(new StatisticsToggleItem { Key = "Week", Label = LocalizationService.T("period_week"), IsSelected = SelectedPeriod == "Week" });
        PeriodTabs.Add(new StatisticsToggleItem { Key = "Month", Label = LocalizationService.T("period_month"), IsSelected = SelectedPeriod == "Month" });
    }
}

public class StatisticsToggleItem
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public bool IsSelected { get; set; }
}
