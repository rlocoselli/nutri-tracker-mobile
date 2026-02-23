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

    public string MetricTitle => T("metric");
    public string PeriodTitle => T("period");
    public string MealsTitle => T("your_meals");
    public string MealsSubtitle => T("selected_day_details");
    public string AddManualText => T("add_manual_line");
    public string EditText => T("edit");
    public string DeleteText => T("delete");

    // --- Day navigation ---
    [ObservableProperty] private DateTime selectedDayLocal = DateTime.Now.Date;
    [ObservableProperty] private string selectedDayText = "";

    // Meals for the selected day
    public ObservableCollection<DiaryMealItem> Meals { get; } = new();

    [ObservableProperty] private string dayTotalsText = "";

    [ObservableProperty] private bool isManualPopupVisible;
    [ObservableProperty] private string manualMealName = "";
    [ObservableProperty] private string manualCalories = "";
    [ObservableProperty] private string manualProtein = "";
    [ObservableProperty] private string manualCarbs = "";
    [ObservableProperty] private string manualGoogleFitSteps = "";
    [ObservableProperty] private string manualExerciseMinutes = "";
    [ObservableProperty] private string manualBurnPreviewText = "";

    public string ManualPopupTitle => T("manual_popup_title");
    public string ManualMealLabel => T("meal_label");
    public string CaloriesLabel => T("calories_label");
    public string ProteinLabel => T("protein_label");
    public string CarbsLabel => T("carbs_label");
    public string StepsLabel => T("steps_label");
    public string MinutesLabel => T("minutes_label");
    public string ManualMealPlaceholder => T("manual_name_placeholder");
    public string CaloriesPlaceholder => T("cal_placeholder");
    public string ProteinPlaceholder => T("protein_placeholder");
    public string CarbsPlaceholder => T("carbs_placeholder");
    public string StepsPlaceholder => T("steps_placeholder");
    public string MinutesPlaceholder => T("minutes_placeholder");
    public string SaveText => T("save");
    public string CancelText => T("cancel");

    // --- Chart ---
    public ObservableCollection<string> MetricOptions { get; } = new() { "Calories", "Protein", "Carbs" };
    public ObservableCollection<string> PeriodOptions { get; } = new() { "Day", "Week", "Month" };

    [ObservableProperty] private string selectedMetric = "Calories";
    [ObservableProperty] private string selectedPeriod = "Day";

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
        OnPropertyChanged(nameof(MetricTitle));
        OnPropertyChanged(nameof(PeriodTitle));
        OnPropertyChanged(nameof(MealsTitle));
        OnPropertyChanged(nameof(MealsSubtitle));
        OnPropertyChanged(nameof(AddManualText));
        OnPropertyChanged(nameof(EditText));
        OnPropertyChanged(nameof(DeleteText));
        OnPropertyChanged(nameof(ManualPopupTitle));
        OnPropertyChanged(nameof(ManualMealLabel));
        OnPropertyChanged(nameof(CaloriesLabel));
        OnPropertyChanged(nameof(ProteinLabel));
        OnPropertyChanged(nameof(CarbsLabel));
        OnPropertyChanged(nameof(StepsLabel));
        OnPropertyChanged(nameof(MinutesLabel));
        OnPropertyChanged(nameof(ManualMealPlaceholder));
        OnPropertyChanged(nameof(CaloriesPlaceholder));
        OnPropertyChanged(nameof(ProteinPlaceholder));
        OnPropertyChanged(nameof(CarbsPlaceholder));
        OnPropertyChanged(nameof(StepsPlaceholder));
        OnPropertyChanged(nameof(MinutesPlaceholder));
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(CancelText));

        await LoadDayAsync(SelectedDayLocal);
        await LoadChartAsync();
    }

    [RelayCommand]
    private void AddManual()
    {
        ManualMealName = "";
        ManualCalories = "450";
        ManualProtein = "25";
        ManualCarbs = "40";
        ManualGoogleFitSteps = "6000";
        ManualExerciseMinutes = "30";
        RecomputeBurnPreview();
        IsManualPopupVisible = true;
    }

    [RelayCommand]
    private void CloseManualPopup()
    {
        IsManualPopupVisible = false;
    }

    [RelayCommand]
    private async Task SaveManualPopup()
    {
        if (string.IsNullOrWhiteSpace(ManualMealName))
        {
            await Application.Current!.MainPage!.DisplayAlert(T("manual_name_title"), T("manual_name_required"), "OK");
            return;
        }

        if (!double.TryParse(ManualCalories, out var calories)) calories = 0;
        if (!double.TryParse(ManualProtein, out var protein)) protein = 0;
        if (!double.TryParse(ManualCarbs, out var carbs)) carbs = 0;
        if (!int.TryParse(ManualGoogleFitSteps, out var steps)) steps = 0;
        if (!double.TryParse(ManualExerciseMinutes, out var minutes)) minutes = 0;

        var burned = EstimateBurnedCalories(steps, minutes);

        var baseLocal = SelectedDayLocal.Date == DateTime.Now.Date
            ? DateTime.Now
            : SelectedDayLocal.Date.AddHours(12);
        var dateUtc = DateTime.SpecifyKind(baseLocal, DateTimeKind.Local).ToUniversalTime();

        var entry = new MealEntry
        {
            Id = Guid.NewGuid().ToString(),
            DateUtc = dateUtc,
            DayKeyUtc = dateUtc.ToString("yyyy-MM-dd"),
            RawText = ManualMealName.Trim(),
            Description = ManualMealName.Trim(),
            TotalCalories = calories,
            TotalProteinG = protein,
            TotalCarbsG = carbs,
            OverallConfidence = 1.0,
            PhotoPath = ""
        };

        await _db.UpsertMealEntryAsync(entry);

        if (steps > 0 || minutes > 0 || burned > 0)
        {
            await _db.SaveExerciseAsync(new ExerciseEntry
            {
                DateUtc = dateUtc,
                DayKeyUtc = dateUtc.ToString("yyyy-MM-dd"),
                GoogleFitSteps = steps,
                ExerciseMinutes = minutes,
                BurnedCalories = burned,
                Notes = T("exercise_note")
            });
        }

        IsManualPopupVisible = false;
        await LoadDayAsync(SelectedDayLocal);
        await LoadChartAsync();
    }

    [RelayCommand]
    private async Task EditMeal(DiaryMealItem? item)
    {
        if (item == null) return;

        var name = await Application.Current!.MainPage!.DisplayPromptAsync(
            T("edit_name_title"),
            T("edit_name_msg"),
            accept: T("save"),
            cancel: T("cancel"),
            initialValue: item.RawText,
            placeholder: T("manual_name_placeholder"));
        if (name == null) return;

        var caloriesText = await Application.Current!.MainPage!.DisplayPromptAsync(
            T("edit_cal_title"),
            T("edit_cal_msg"),
            accept: T("next"),
            cancel: T("cancel"),
            keyboard: Keyboard.Numeric,
            initialValue: item.TotalCalories.ToString("0"));
        if (caloriesText == null) return;

        var proteinText = await Application.Current!.MainPage!.DisplayPromptAsync(
            T("edit_protein_title"),
            T("edit_protein_msg"),
            accept: T("next"),
            cancel: T("cancel"),
            keyboard: Keyboard.Numeric,
            initialValue: item.TotalProteinG.ToString("0"));
        if (proteinText == null) return;

        var carbsText = await Application.Current!.MainPage!.DisplayPromptAsync(
            T("edit_carbs_title"),
            T("edit_carbs_msg"),
            accept: T("save"),
            cancel: T("cancel"),
            keyboard: Keyboard.Numeric,
            initialValue: item.TotalCarbsG.ToString("0"));
        if (carbsText == null) return;

        if (!double.TryParse(caloriesText, out var calories)) calories = item.TotalCalories;
        if (!double.TryParse(proteinText, out var protein)) protein = item.TotalProteinG;
        if (!double.TryParse(carbsText, out var carbs)) carbs = item.TotalCarbsG;

        var updated = new MealEntry
        {
            Id = item.Id,
            DateUtc = item.DateUtc,
            DayKeyUtc = item.DayKeyUtc,
            RawText = string.IsNullOrWhiteSpace(name) ? item.RawText : name.Trim(),
            Description = string.IsNullOrWhiteSpace(name) ? item.Description : name.Trim(),
            AiNotes = item.AiNotes,
            PhotoPath = item.PhotoPath,
            TotalCalories = calories,
            TotalProteinG = protein,
            TotalCarbsG = carbs,
            OverallConfidence = item.OverallConfidence
        };

        await _db.UpsertMealEntryAsync(updated);
        await LoadDayAsync(SelectedDayLocal);
        await LoadChartAsync();
    }

    [RelayCommand]
    private async Task DeleteMeal(DiaryMealItem? item)
    {
        if (item == null) return;

        var confirm = await Application.Current!.MainPage!.DisplayAlert(
            T("delete_title"),
            T("delete_msg"),
            T("delete"),
            T("cancel"));

        if (!confirm) return;

        await _db.DeleteMealAsync(item.Id);
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
        var exercise = await _db.GetExerciseTotalsBetweenUtcAsync(fromUtc, toUtc);
        foreach (var e in entries.OrderByDescending(e => e.DateUtc))
        {
            var items = await _db.GetMealItemsForEntryAsync(e.Id);
            Meals.Add(DiaryMealItem.FromEntry(e, items));
        }

        var cal = entries.Sum(x => x.TotalCalories);
        var carbs = entries.Sum(x => x.TotalCarbsG);
        var prot = entries.Sum(x => x.TotalProteinG);

        var netCalories = cal - exercise.burnedCalories;
        DayTotalsText = $"{T("total")}: {Math.Round(cal)} kcal · C {Math.Round(carbs)}g · P {Math.Round(prot)}g · {T("burn")}: {Math.Round(exercise.burnedCalories)} · {T("net")}: {Math.Round(netCalories)}";
    }

    private async Task LoadChartAsync()
    {
        // Window sizes (feel free to tweak)
        var nowLocal = DateTime.Now;

        DateTime fromLocal;
        if (SelectedPeriod == "Day") fromLocal = nowLocal.Date.AddDays(-29);
        else if (SelectedPeriod == "Week") fromLocal = nowLocal.Date.AddDays(-7 * 11); // ~12 weeks
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
            "Protein" => e.TotalProteinG,
            "Carbs" => e.TotalCarbsG,
            _ => e.TotalCalories,
        };

        if (SelectedPeriod == "Day")
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

        if (SelectedPeriod == "Week")
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

    private static string T(string key)
    {
        var lang = Preferences.Default.Get("app_lang", "fr");
        return key switch
        {
            "metric" => lang == "en" ? "Metric" : "Métrique",
            "period" => lang == "en" ? "Period" : "Période",
            "your_meals" => lang == "en" ? "Your meals" : "Vos repas",
            "selected_day_details" => lang == "en" ? "Details of selected day" : "Détails du jour sélectionné",
            "add_manual_line" => lang == "en" ? "Add manual line" : "Ajouter une ligne manuelle",
            "manual_name_title" => lang == "en" ? "Manual meal" : "Repas manuel",
            "manual_name_msg" => lang == "en" ? "Meal name" : "Nom du repas",
            "manual_name_placeholder" => lang == "en" ? "ex: Homemade sandwich" : "ex : Sandwich maison",
            "meal_label" => lang == "en" ? "Meal" : "Repas",
            "calories_label" => lang == "en" ? "Calories" : "Calories",
            "protein_label" => lang == "en" ? "Protein" : "Protéines",
            "carbs_label" => lang == "en" ? "Carbs" : "Glucides",
            "steps_label" => lang == "en" ? "Google Fit steps (test)" : "Pas Google Fit (test)",
            "minutes_label" => lang == "en" ? "Exercise minutes" : "Minutes d'exercice",
            "manual_popup_title" => lang == "en" ? "Manual entry" : "Entrée manuelle",
            "manual_name_required" => lang == "en" ? "Please enter a meal name." : "Veuillez saisir un nom de repas.",
            "cal_placeholder" => lang == "en" ? "Calories" : "Calories",
            "protein_placeholder" => lang == "en" ? "Protein (g)" : "Protéines (g)",
            "carbs_placeholder" => lang == "en" ? "Carbs (g)" : "Glucides (g)",
            "steps_placeholder" => lang == "en" ? "Google Fit steps (test)" : "Pas Google Fit (test)",
            "minutes_placeholder" => lang == "en" ? "Exercise minutes" : "Minutes d'exercice",
            "manual_cal_title" => lang == "en" ? "Calories" : "Calories",
            "manual_cal_msg" => lang == "en" ? "Enter calories" : "Saisissez les calories",
            "manual_protein_title" => lang == "en" ? "Protein (g)" : "Protéines (g)",
            "manual_protein_msg" => lang == "en" ? "Enter protein grams" : "Saisissez les protéines",
            "manual_carbs_title" => lang == "en" ? "Carbs (g)" : "Glucides (g)",
            "manual_carbs_msg" => lang == "en" ? "Enter carb grams" : "Saisissez les glucides",
            "edit_name_title" => lang == "en" ? "Edit meal" : "Modifier le repas",
            "edit_name_msg" => lang == "en" ? "Update meal name" : "Mettre à jour le nom",
            "edit_cal_title" => lang == "en" ? "Edit calories" : "Modifier calories",
            "edit_cal_msg" => lang == "en" ? "Update calories" : "Mettre à jour les calories",
            "edit_protein_title" => lang == "en" ? "Edit protein" : "Modifier protéines",
            "edit_protein_msg" => lang == "en" ? "Update protein grams" : "Mettre à jour les protéines",
            "edit_carbs_title" => lang == "en" ? "Edit carbs" : "Modifier glucides",
            "edit_carbs_msg" => lang == "en" ? "Update carb grams" : "Mettre à jour les glucides",
            "delete_title" => lang == "en" ? "Delete meal" : "Supprimer le repas",
            "delete_msg" => lang == "en" ? "Delete this line?" : "Supprimer cette ligne ?",
            "delete" => lang == "en" ? "Delete" : "Supprimer",
            "edit" => lang == "en" ? "Edit" : "Modifier",
            "cancel" => lang == "en" ? "Cancel" : "Annuler",
            "save" => lang == "en" ? "Save" : "Enregistrer",
            "next" => lang == "en" ? "Next" : "Suivant",
            "total" => lang == "en" ? "Total" : "Total",
            "burn" => lang == "en" ? "Burn" : "Débit",
            "net" => lang == "en" ? "Net" : "Net",
            "exercise_note" => lang == "en" ? "Manual test from Google Fit steps" : "Test manuel basé sur les pas Google Fit",
            _ => key,
        };
    }

    partial void OnManualGoogleFitStepsChanged(string value) => RecomputeBurnPreview();
    partial void OnManualExerciseMinutesChanged(string value) => RecomputeBurnPreview();

    private void RecomputeBurnPreview()
    {
        if (!int.TryParse(ManualGoogleFitSteps, out var steps)) steps = 0;
        if (!double.TryParse(ManualExerciseMinutes, out var minutes)) minutes = 0;
        var burned = EstimateBurnedCalories(steps, minutes);
        ManualBurnPreviewText = $"{T("burn")}: {Math.Round(burned)} kcal";
    }

    private static double EstimateBurnedCalories(int steps, double minutes)
    {
        var stepBurn = Math.Max(0, steps) * 0.04;
        var exerciseBurn = Math.Max(0, minutes) * 5.0;
        return stepBurn + exerciseBurn;
    }
}

public class DiaryMealItem
{
    public string Id { get; set; } = "";
    public string RawText { get; set; } = "";
    public string Description { get; set; } = "";
    public string AiNotes { get; set; } = "";
    public string PhotoPath { get; set; } = "";
    public DateTime DateUtc { get; set; }
    public string DayKeyUtc { get; set; } = "";
    public double TotalCalories { get; set; }
    public double TotalProteinG { get; set; }
    public double TotalCarbsG { get; set; }
    public double OverallConfidence { get; set; }

    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string DescriptionText { get; set; } = "";
    public bool HasDescription => !string.IsNullOrWhiteSpace(DescriptionText);
    public string AnalysisText { get; set; } = "";
    public bool HasAnalysis => !string.IsNullOrWhiteSpace(AnalysisText);
    public string CaloriesText { get; set; } = "";
    public string ProteinText { get; set; } = "";
    public string CarbsText { get; set; } = "";

    public static DiaryMealItem FromEntry(MealEntry e, List<MealItem>? items)
    {
        var local = e.DateUtc.ToLocalTime();
        var displayDescription = string.IsNullOrWhiteSpace(e.Description) ? e.RawText : e.Description;
        var title = string.IsNullOrWhiteSpace(e.RawText) ? (string.IsNullOrWhiteSpace(displayDescription) ? "Refeição" : displayDescription) : e.RawText;
        title = title.Length > 28 ? title.Substring(0, 28) + "…" : title;
        var itemList = items == null
            ? ""
            : string.Join(", ", items
                .Select(i => i.Name?.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .Take(5));
        var analysisText = BuildAnalysisText(e.AiNotes, itemList);

        return new DiaryMealItem
        {
            Id = e.Id,
            RawText = e.RawText,
            Description = e.Description,
            AiNotes = e.AiNotes,
            PhotoPath = e.PhotoPath,
            DateUtc = e.DateUtc,
            DayKeyUtc = e.DayKeyUtc,
            TotalCalories = e.TotalCalories,
            TotalProteinG = e.TotalProteinG,
            TotalCarbsG = e.TotalCarbsG,
            OverallConfidence = e.OverallConfidence,
            Title = title,
            Subtitle = local.ToString("dddd dd MMM · HH:mm", CultureInfo.CurrentCulture),
            DescriptionText = displayDescription,
            AnalysisText = analysisText,
            CaloriesText = $"{Math.Round(e.TotalCalories)} kcal",
            ProteinText = $"P {Math.Round(e.TotalProteinG)}g",
            CarbsText = $"C {Math.Round(e.TotalCarbsG)}g"
        };
    }

    private static string BuildAnalysisText(string aiNotes, string items)
    {
        var notes = aiNotes?.Trim() ?? "";
        var itemsText = items?.Trim() ?? "";

        if (!string.IsNullOrWhiteSpace(notes) && !string.IsNullOrWhiteSpace(itemsText))
            return $"IA: {notes} · {itemsText}";
        if (!string.IsNullOrWhiteSpace(notes))
            return $"IA: {notes}";
        if (!string.IsNullOrWhiteSpace(itemsText))
            return $"IA: {itemsText}";
        return "";
    }
}
