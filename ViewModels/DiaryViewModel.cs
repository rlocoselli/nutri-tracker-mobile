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
    private readonly PointsService _points;
    private readonly BackendSyncService _sync;

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
    public ObservableCollection<DiaryDayTab> DayTabs { get; } = new();

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

    [ObservableProperty] private bool isEditPopupVisible;
    [ObservableProperty] private string editMealName = "";
    [ObservableProperty] private string editCalories = "";
    [ObservableProperty] private string editProtein = "";
    [ObservableProperty] private string editCarbs = "";
    [ObservableProperty] private string editQualityPreviewText = "";
    [ObservableProperty] private string editBadgePreviewText = "";
    [ObservableProperty] private string editSemaphorePreviewText = "";

    private DiaryMealItem? _editingMeal;

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
    public string EditPopupTitle => T("edit_popup_title");

    // --- Chart ---
    public ObservableCollection<string> MetricOptions { get; } = new() { "Calories", "Proteins", "Activities" };
    public ObservableCollection<string> PeriodOptions { get; } = new() { "Day", "Week", "Month" };
    public ObservableCollection<DiaryToggleItem> MetricTabs { get; } = new();
    public ObservableCollection<DiaryToggleItem> PeriodTabs { get; } = new();

    [ObservableProperty] private string selectedMetric = "Calories";
    [ObservableProperty] private string selectedPeriod = "Day";

    [ObservableProperty] private IList<double> chartValues = Array.Empty<double>();
    [ObservableProperty] private IList<string> chartLabels = Array.Empty<string>();

    [ObservableProperty] private double waterLiters;
    [ObservableProperty] private string waterLitersText = "0 L";
    public ObservableCollection<string> WaterBottleImages { get; } = new();
    public string HydrationTitle => T("hydration_title");
    public string AddHalfLiterText => T("add_half_liter");
    public string AddOneLiterText => T("add_one_liter");
    public string RemoveHalfLiterText => T("remove_half_liter");

    public DiaryViewModel(LocalDb db, PointsService points, BackendSyncService sync)
    {
        _db = db;
        _points = points;
        _sync = sync;
        UpdateSelectedDayText();
        RebuildDayTabs();
        RebuildMetricTabs();
        RebuildPeriodTabs();
    }

    partial void OnSelectedDayLocalChanged(DateTime value)
    {
        UpdateSelectedDayText();
        RebuildDayTabs();
    }

    partial void OnSelectedMetricChanged(string value)
    {
        RebuildMetricTabs();
        // Rebuild chart for the new metric
        _ = LoadChartAsync();
    }

    partial void OnSelectedPeriodChanged(string value)
    {
        RebuildPeriodTabs();
        // Rebuild chart for the new period
        _ = LoadChartAsync();
    }

    private void UpdateSelectedDayText()
    {
        // Example: "terça 04 fev"
        SelectedDayText = SelectedDayLocal.ToString("dddd dd MMM", CultureInfo.CurrentCulture);
    }

    [RelayCommand]
    private async Task SelectDay(DiaryDayTab? tab)
    {
        if (tab == null)
            return;

        SelectedDayLocal = tab.DayLocal.Date;
        await LoadDayAsync(SelectedDayLocal);
        await LoadChartAsync();
    }

    [RelayCommand]
    private Task SelectMetricTab(DiaryToggleItem? tab)
    {
        if (tab == null)
            return Task.CompletedTask;

        SelectedMetric = tab.Key;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task SelectPeriodTab(DiaryToggleItem? tab)
    {
        if (tab == null)
            return Task.CompletedTask;

        SelectedPeriod = tab.Key;
        return Task.CompletedTask;
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
        OnPropertyChanged(nameof(EditPopupTitle));
        OnPropertyChanged(nameof(HydrationTitle));
        OnPropertyChanged(nameof(AddHalfLiterText));
        OnPropertyChanged(nameof(AddOneLiterText));
        OnPropertyChanged(nameof(RemoveHalfLiterText));
        RebuildMetricTabs();
        RebuildPeriodTabs();
        RebuildDayTabs();

        await LoadDayAsync(SelectedDayLocal);
        await LoadChartAsync();
    }

    [RelayCommand]
    private async Task AddHalfLiter()
    {
        await SetWaterLitersAsync(WaterLiters + 0.5);
    }

    [RelayCommand]
    private async Task AddOneLiter()
    {
        await SetWaterLitersAsync(WaterLiters + 1.0);
    }

    [RelayCommand]
    private async Task RemoveHalfLiter()
    {
        await SetWaterLitersAsync(Math.Max(0, WaterLiters - 0.5));
    }

    [RelayCommand]
    private void AddManual()
    {
        ManualMealName = "";
        ManualCalories = "450";
        ManualProtein = "25";
        ManualCarbs = "40";
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
        if (!double.TryParse(ManualExerciseMinutes, out var minutes)) minutes = 0;

        var burned = EstimateBurnedCalories(minutes);

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
            QualityScore = 50,
            QualityLabel = "Moyen",
            PhotoPath = ""
        };

        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        if (!identityOk)
        {
            await Application.Current!.MainPage!.DisplayAlert(T("error_title"), T("backend_identity_error"), "OK");
            return;
        }

        var createdId = await _sync.CreateMealAsync(entry, new List<MealItem>());
        if (string.IsNullOrWhiteSpace(createdId))
        {
            await Application.Current!.MainPage!.DisplayAlert(T("error_title"), T("backend_save_error"), "OK");
            return;
        }

        if (minutes > 0 || burned > 0)
        {
            await _db.SaveExerciseAsync(new ExerciseEntry
            {
                DateUtc = dateUtc,
                DayKeyUtc = dateUtc.ToString("yyyy-MM-dd"),
                GoogleFitSteps = 0,
                ExerciseMinutes = minutes,
                BurnedCalories = burned,
                Notes = T("exercise_note")
            });
        }

        IsManualPopupVisible = false;
        var manualBalance = _points.Award(8);
        await Application.Current!.MainPage!.DisplayAlert(T("saved_title"), string.Format(T("earned_points"), 8, manualBalance), "OK");
        await LoadDayAsync(SelectedDayLocal);
        await LoadChartAsync();
    }

    [RelayCommand]
    private Task EditMeal(DiaryMealItem? item)
    {
        if (item == null) return Task.CompletedTask;

        _editingMeal = item;
        EditMealName = item.RawText;
        EditCalories = item.TotalCalories.ToString("0");
        EditProtein = item.TotalProteinG.ToString("0");
        EditCarbs = item.TotalCarbsG.ToString("0");
        RecomputeEditQualityPreview();
        IsManualPopupVisible = false;
        IsEditPopupVisible = true;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void CloseEditPopup()
    {
        IsEditPopupVisible = false;
        _editingMeal = null;
    }

    [RelayCommand]
    private async Task SaveEditPopup()
    {
        if (_editingMeal == null)
            return;

        if (string.IsNullOrWhiteSpace(EditMealName))
        {
            await Application.Current!.MainPage!.DisplayAlert(T("edit_name_title"), T("manual_name_required"), "OK");
            return;
        }

        if (!double.TryParse(EditCalories, out var calories)) calories = _editingMeal.TotalCalories;
        if (!double.TryParse(EditProtein, out var protein)) protein = _editingMeal.TotalProteinG;
        if (!double.TryParse(EditCarbs, out var carbs)) carbs = _editingMeal.TotalCarbsG;

        var quality = MealQualityService.Classify(
            _editingMeal.AiNotes,
            calories,
            protein,
            carbs,
            _editingMeal.OverallConfidence);

        var updated = new MealEntry
        {
            Id = _editingMeal.Id,
            DateUtc = _editingMeal.DateUtc,
            DayKeyUtc = _editingMeal.DayKeyUtc,
            RawText = EditMealName.Trim(),
            Description = EditMealName.Trim(),
            AiNotes = _editingMeal.AiNotes,
            PhotoPath = _editingMeal.PhotoPath,
            TotalCalories = calories,
            TotalProteinG = protein,
            TotalCarbsG = carbs,
            OverallConfidence = _editingMeal.OverallConfidence,
            QualityScore = quality.score,
            QualityLabel = quality.label
        };

        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        if (!identityOk)
        {
            await Application.Current!.MainPage!.DisplayAlert(T("error_title"), T("backend_identity_error"), "OK");
            return;
        }

        var updatedOk = await _sync.UpdateMealAsync(updated.Id, updated, new List<MealItem>());
        if (!updatedOk)
        {
            await Application.Current!.MainPage!.DisplayAlert(T("error_title"), T("backend_save_error"), "OK");
            return;
        }

        IsEditPopupVisible = false;
        _editingMeal = null;
        var editBalance = _points.Award(4);
        await Application.Current!.MainPage!.DisplayAlert(T("saved_title"), string.Format(T("earned_points"), 4, editBalance), "OK");
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

        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        if (!identityOk)
        {
            await Application.Current!.MainPage!.DisplayAlert(T("error_title"), T("backend_identity_error"), "OK");
            return;
        }

        var deleted = await _sync.DeleteMealAsync(item.Id);
        if (!deleted)
        {
            await Application.Current!.MainPage!.DisplayAlert(T("error_title"), T("backend_delete_error"), "OK");
            return;
        }

        await LoadDayAsync(SelectedDayLocal);
        await LoadChartAsync();
    }

    [RelayCommand]
    private async Task PrevDay()
    {
        SelectedDayLocal = SelectedDayLocal.AddDays(-1);
        await LoadDayAsync(SelectedDayLocal);
        await LoadChartAsync();
    }

    [RelayCommand]
    private async Task NextDay()
    {
        SelectedDayLocal = SelectedDayLocal.AddDays(1);
        await LoadDayAsync(SelectedDayLocal);
        await LoadChartAsync();
    }

    private async Task LoadDayAsync(DateTime dayLocal)
    {
        Meals.Clear();

        // Convert the local day range to UTC for DB queries
        var startLocal = DateTime.SpecifyKind(dayLocal.Date, DateTimeKind.Local);
        var fromUtc = startLocal.ToUniversalTime();
        var toUtc = startLocal.AddDays(1).ToUniversalTime();

        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        var backendMealsRaw = identityOk
            ? await _sync.GetMealsBetweenUtcAsync(fromUtc.AddDays(-1), toUtc.AddDays(1))
            : new List<BackendMeal>();

        var backendMeals = backendMealsRaw
            .Select(m => new { Raw = m, Entry = ToMealEntry(m) })
            .Where(x => x.Entry.DateUtc.ToLocalTime().Date == dayLocal.Date)
            .Select(x => x.Raw)
            .ToList();

        var entries = backendMeals.Select(ToMealEntry).ToList();
        var exercise = await _db.GetExerciseTotalsBetweenUtcAsync(fromUtc, toUtc);
        foreach (var backendMeal in backendMeals.OrderByDescending(x => x.date_utc))
        {
            var e = ToMealEntry(backendMeal);
            var items = ToMealItems(backendMeal);
            Meals.Add(DiaryMealItem.FromEntry(e, items));
        }

        var cal = entries.Sum(x => x.TotalCalories);
        var carbs = entries.Sum(x => x.TotalCarbsG);
        var prot = entries.Sum(x => x.TotalProteinG);

        var netCalories = cal - exercise.burnedCalories;
        DayTotalsText = $"{T("total")}: {Math.Round(cal)} kcal · C {Math.Round(carbs)}g · P {Math.Round(prot)}g · {T("burn")}: {Math.Round(exercise.burnedCalories)} · {T("net")}: {Math.Round(netCalories)}";

        var liters = await _db.GetWaterLitersForDayLocalAsync(dayLocal);
        UpdateWaterUi(liters);
    }

    private async Task SetWaterLitersAsync(double liters)
    {
        await _db.UpsertWaterLitersForDayLocalAsync(SelectedDayLocal, liters);
        var rounded = Math.Round(Math.Max(0, liters) * 2, MidpointRounding.AwayFromZero) / 2.0;
        UpdateWaterUi(rounded);
        _ = await _sync.TryPushWaterIntakeAsync(SelectedDayLocal, rounded);
    }

    private void UpdateWaterUi(double liters)
    {
        WaterLiters = liters;
        var display = liters % 1 == 0
            ? $"{liters:0} L"
            : $"{liters:0.0} L";
        WaterLitersText = display;

        WaterBottleImages.Clear();
        const int maxSlots = 6;

        for (var i = 0; i < maxSlots; i++)
        {
            var remaining = liters - i;
            if (remaining >= 0.99)
                WaterBottleImages.Add("water_bottle_full.svg");
            else if (remaining >= 0.49)
                WaterBottleImages.Add("water_bottle_half.svg");
            else
                WaterBottleImages.Add("water_bottle_empty.svg");
        }
    }

    private async Task LoadChartAsync()
    {
        // Anchor chart window to selected day so navigation updates the graph.
        var anchorLocal = SelectedDayLocal.Date;

        DateTime fromLocal;
        DateTime toLocalExclusive;

        if (SelectedPeriod == "Day")
        {
            fromLocal = anchorLocal.AddDays(-29);
            toLocalExclusive = anchorLocal.AddDays(1);
        }
        else if (SelectedPeriod == "Week")
        {
            fromLocal = anchorLocal.AddDays(-7 * 11); // ~12 weeks
            toLocalExclusive = anchorLocal.AddDays(1);
        }
        else
        {
            fromLocal = new DateTime(anchorLocal.Year, anchorLocal.Month, 1).AddMonths(-11); // 12 months
            toLocalExclusive = new DateTime(anchorLocal.Year, anchorLocal.Month, 1).AddMonths(1);
        }

        var fromUtc = DateTime.SpecifyKind(fromLocal, DateTimeKind.Local).ToUniversalTime();
        var toUtc = DateTime.SpecifyKind(toLocalExclusive, DateTimeKind.Local).ToUniversalTime();

        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        var backendMeals = identityOk
            ? await _sync.GetMealsBetweenUtcAsync(fromUtc, toUtc)
            : new List<BackendMeal>();

        var entries = backendMeals.Select(ToMealEntry).ToList();
        var exercises = await _db.GetExercisesBetweenUtcAsync(fromUtc, toUtc);
        // Build a continuous series (fills missing days/weeks/months with zeros)
        var points = BuildSeries(entries, exercises, fromLocal.Date, toLocalExclusive);

        ChartValues = points.Select(p => p.Value).ToList();
        ChartLabels = points.Select(p => p.Label).ToList();
    }

    private List<(string Label, double Value)> BuildSeries(List<MealEntry> entries, List<ExerciseEntry> exercises, DateTime fromLocalInclusive, DateTime toLocalExclusive)
    {
        var isActivitiesMetric = SelectedMetric == "Activities";

        double Selector(MealEntry e) => SelectedMetric switch
        {
            "Proteína" => e.TotalProteinG,
            "Protein" => e.TotalProteinG,
            "Proteins" => e.TotalProteinG,
            _ => e.TotalCalories,
        };

        double ExerciseSelector(ExerciseEntry e) => e.BurnedCalories;

        if (SelectedPeriod == "Day")
        {
            var byDay = isActivitiesMetric
                ? exercises
                    .GroupBy(e => e.DateUtc.ToLocalTime().Date)
                    .ToDictionary(g => g.Key, g => g.Sum(ExerciseSelector))
                : entries
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
            var byWeek = isActivitiesMetric
                ? exercises
                    .GroupBy(e => IsoWeekKey(e.DateUtc.ToLocalTime().Date))
                    .ToDictionary(g => g.Key, g => g.Sum(ExerciseSelector))
                : entries
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
        var byMonth = isActivitiesMetric
            ? exercises
                .GroupBy(e => (y: e.DateUtc.ToLocalTime().Year, m: e.DateUtc.ToLocalTime().Month))
                .ToDictionary(g => g.Key, g => g.Sum(ExerciseSelector))
            : entries
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
        static string L(string lang, string fr, string en, string pt, string es) => lang switch
        {
            "en" => en,
            "pt" => pt,
            "es" => es,
            _ => fr,
        };

        return key switch
        {
            "metric" => L(lang, "Métrique", "Metric", "Métrica", "Métrica"),
            "period" => L(lang, "Période", "Period", "Período", "Período"),
            "your_meals" => L(lang, "Vos repas", "Your meals", "Suas refeições", "Tus comidas"),
            "selected_day_details" => L(lang, "Détails du jour sélectionné", "Details of selected day", "Detalhes do dia selecionado", "Detalles del día seleccionado"),
            "add_manual_line" => L(lang, "Ajouter une ligne manuelle", "Add manual line", "Adicionar registro manual", "Añadir registro manual"),
            "manual_name_title" => L(lang, "Repas manuel", "Manual meal", "Refeição manual", "Comida manual"),
            "manual_name_msg" => L(lang, "Nom du repas", "Meal name", "Nome da refeição", "Nombre de la comida"),
            "manual_name_placeholder" => L(lang, "ex : Sandwich maison", "ex: Homemade sandwich", "ex: Sanduíche caseiro", "ej: Sándwich casero"),
            "meal_label" => L(lang, "Repas", "Meal", "Refeição", "Comida"),
            "calories_label" => "Calories",
            "protein_label" => L(lang, "Protéines", "Protein", "Proteínas", "Proteínas"),
            "carbs_label" => L(lang, "Glucides", "Carbs", "Carboidratos", "Carbohidratos"),
            "steps_label" => L(lang, "Pas Google Fit (test)", "Google Fit steps (test)", "Passos Google Fit (teste)", "Pasos Google Fit (prueba)"),
            "minutes_label" => L(lang, "Minutes d'exercice", "Exercise minutes", "Minutos de exercício", "Minutos de ejercicio"),
            "hydration_title" => L(lang, "Hydratation (bouteilles de 1L)", "Hydration (1L bottles)", "Hidratação (garrafas de 1L)", "Hidratación (botellas de 1L)"),
            "add_half_liter" => L(lang, "+0,5 L", "+0.5 L", "+0,5 L", "+0,5 L"),
            "add_one_liter" => L(lang, "+1 L", "+1 L", "+1 L", "+1 L"),
            "remove_half_liter" => L(lang, "-0,5 L", "-0.5 L", "-0,5 L", "-0,5 L"),
            "metric_calories" => L(lang, "Calories", "Calories", "Calorias", "Calorías"),
            "metric_proteins" => L(lang, "Protéines", "Proteins", "Proteínas", "Proteínas"),
            "metric_activities" => L(lang, "Activités", "Activities", "Atividades", "Actividades"),
            "period_day" => L(lang, "Jour", "Day", "Dia", "Día"),
            "period_week" => L(lang, "Semaine", "Week", "Semana", "Semana"),
            "period_month" => L(lang, "Mois", "Month", "Mês", "Mes"),
            "backend_identity_error" => L(lang, "Impossible de synchroniser l'identité backend.", "Unable to sync backend identity.", "Não foi possível sincronizar identidade no backend.", "No se pudo sincronizar la identidad del backend."),
            "backend_save_error" => L(lang, "Impossible d'enregistrer le repas dans PostgreSQL.", "Unable to save meal to PostgreSQL.", "Não foi possível salvar a refeição no PostgreSQL.", "No se pudo guardar la comida en PostgreSQL."),
            "backend_delete_error" => L(lang, "Impossible de supprimer le repas dans PostgreSQL.", "Unable to delete meal from PostgreSQL.", "Não foi possível excluir a refeição no PostgreSQL.", "No se pudo eliminar la comida en PostgreSQL."),
            "manual_popup_title" => L(lang, "Entrée manuelle", "Manual entry", "Entrada manual", "Entrada manual"),
            "edit_popup_title" => L(lang, "Modifier l'entrée repas", "Edit meal entry", "Editar registro de refeição", "Editar registro de comida"),
            "quality" => L(lang, "Qualité IA", "AI quality", "Qualidade IA", "Calidad IA"),
            "badge" => L(lang, "Badge", "Badge", "Insígnia", "Insignia"),
            "semaphore" => L(lang, "Sémaphore", "Semaphore", "Semáforo", "Semáforo"),
            "manual_name_required" => L(lang, "Veuillez saisir un nom de repas.", "Please enter a meal name.", "Digite um nome para a refeição.", "Introduce un nombre para la comida."),
            "cal_placeholder" => "Calories",
            "protein_placeholder" => L(lang, "Protéines (g)", "Protein (g)", "Proteínas (g)", "Proteínas (g)"),
            "carbs_placeholder" => L(lang, "Glucides (g)", "Carbs (g)", "Carboidratos (g)", "Carbohidratos (g)"),
            "steps_placeholder" => L(lang, "Pas Google Fit (test)", "Google Fit steps (test)", "Passos Google Fit (teste)", "Pasos Google Fit (prueba)"),
            "minutes_placeholder" => L(lang, "Minutes d'exercice", "Exercise minutes", "Minutos de exercício", "Minutos de ejercicio"),
            "manual_cal_title" => "Calories",
            "manual_cal_msg" => L(lang, "Saisissez les calories", "Enter calories", "Informe as calorias", "Introduce las calorías"),
            "manual_protein_title" => L(lang, "Protéines (g)", "Protein (g)", "Proteínas (g)", "Proteínas (g)"),
            "manual_protein_msg" => L(lang, "Saisissez les protéines", "Enter protein grams", "Informe as proteínas", "Introduce las proteínas"),
            "manual_carbs_title" => L(lang, "Glucides (g)", "Carbs (g)", "Carboidratos (g)", "Carbohidratos (g)"),
            "manual_carbs_msg" => L(lang, "Saisissez les glucides", "Enter carb grams", "Informe os carboidratos", "Introduce los carbohidratos"),
            "edit_name_title" => L(lang, "Modifier le repas", "Edit meal", "Editar refeição", "Editar comida"),
            "edit_name_msg" => L(lang, "Mettre à jour le nom", "Update meal name", "Atualizar nome da refeição", "Actualizar nombre de la comida"),
            "edit_cal_title" => L(lang, "Modifier calories", "Edit calories", "Editar calorias", "Editar calorías"),
            "edit_cal_msg" => L(lang, "Mettre à jour les calories", "Update calories", "Atualizar calorias", "Actualizar calorías"),
            "edit_protein_title" => L(lang, "Modifier protéines", "Edit protein", "Editar proteínas", "Editar proteínas"),
            "edit_protein_msg" => L(lang, "Mettre à jour les protéines", "Update protein grams", "Atualizar proteínas", "Actualizar proteínas"),
            "edit_carbs_title" => L(lang, "Modifier glucides", "Edit carbs", "Editar carboidratos", "Editar carbohidratos"),
            "edit_carbs_msg" => L(lang, "Mettre à jour les glucides", "Update carb grams", "Atualizar carboidratos", "Actualizar carbohidratos"),
            "delete_title" => L(lang, "Supprimer le repas", "Delete meal", "Excluir refeição", "Eliminar comida"),
            "delete_msg" => L(lang, "Supprimer cette ligne ?", "Delete this line?", "Excluir este registro?", "¿Eliminar este registro?"),
            "delete" => L(lang, "Supprimer", "Delete", "Excluir", "Eliminar"),
            "edit" => L(lang, "Modifier", "Edit", "Editar", "Editar"),
            "cancel" => L(lang, "Annuler", "Cancel", "Cancelar", "Cancelar"),
            "save" => L(lang, "Enregistrer", "Save", "Salvar", "Guardar"),
            "saved_title" => L(lang, "Enregistré", "Saved", "Salvo", "Guardado"),
            "earned_points" => L(lang, "+{0} pièces gagnées · Solde : {1}", "+{0} coins earned · Balance: {1}", "+{0} moedas ganhas · Saldo: {1}", "+{0} monedas ganadas · Saldo: {1}"),
            "next" => L(lang, "Suivant", "Next", "Próximo", "Siguiente"),
            "total" => L(lang, "Total", "Total", "Total", "Total"),
            "burn" => L(lang, "Débit", "Burn", "Queima", "Gasto"),
            "net" => L(lang, "Net", "Net", "Líquido", "Neto"),
            "exercise_note" => L(lang, "Test manuel basé sur les pas Google Fit", "Manual test from Google Fit steps", "Teste manual com passos do Google Fit", "Prueba manual con pasos de Google Fit"),
            _ => key,
        };
    }

    private void RebuildMetricTabs()
    {
        MetricTabs.Clear();
        MetricTabs.Add(new DiaryToggleItem { Key = "Calories", Label = T("metric_calories"), IsSelected = SelectedMetric == "Calories" });
        MetricTabs.Add(new DiaryToggleItem { Key = "Proteins", Label = T("metric_proteins"), IsSelected = SelectedMetric == "Proteins" });
        MetricTabs.Add(new DiaryToggleItem { Key = "Activities", Label = T("metric_activities"), IsSelected = SelectedMetric == "Activities" });
    }

    private void RebuildPeriodTabs()
    {
        PeriodTabs.Clear();
        PeriodTabs.Add(new DiaryToggleItem { Key = "Day", Label = T("period_day"), IsSelected = SelectedPeriod == "Day" });
        PeriodTabs.Add(new DiaryToggleItem { Key = "Week", Label = T("period_week"), IsSelected = SelectedPeriod == "Week" });
        PeriodTabs.Add(new DiaryToggleItem { Key = "Month", Label = T("period_month"), IsSelected = SelectedPeriod == "Month" });
    }

    private void RebuildDayTabs()
    {
        DayTabs.Clear();
        var start = SelectedDayLocal.Date.AddDays(-3);
        for (var i = 0; i < 7; i++)
        {
            var day = start.AddDays(i);
            DayTabs.Add(new DiaryDayTab
            {
                DayLocal = day,
                Label = day.ToString("ddd dd", CultureInfo.CurrentCulture),
                IsSelected = day.Date == SelectedDayLocal.Date,
            });
        }
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
            DayKeyUtc = string.IsNullOrWhiteSpace(meal.day_key_utc) ? dateUtc.ToString("yyyy-MM-dd") : meal.day_key_utc,
            RawText = meal.raw_text,
            Description = meal.description,
            AiNotes = meal.ai_notes,
            PhotoPath = meal.photo_url,
            TotalCalories = meal.total_calories,
            TotalCarbsG = meal.total_carbs_g,
            TotalProteinG = meal.total_protein_g,
            OverallConfidence = meal.overall_confidence,
            QualityScore = meal.quality_score,
            QualityLabel = meal.quality_label,
        };
    }

    private static List<MealItem> ToMealItems(BackendMeal meal)
    {
        return (meal.items ?? new List<BackendMealItem>())
            .Select(i => new MealItem
            {
                Id = string.IsNullOrWhiteSpace(i.id) ? Guid.NewGuid().ToString() : i.id,
                MealEntryId = string.IsNullOrWhiteSpace(i.meal_entry_id) ? meal.id : i.meal_entry_id,
                Name = i.name,
                Quantity = i.quantity,
                Unit = i.unit,
                EstimatedGrams = i.estimated_grams,
                Calories = i.calories,
                CarbsG = i.carbs_g,
                ProteinG = i.protein_g,
                Confidence = i.confidence,
            })
            .ToList();
    }

    partial void OnManualExerciseMinutesChanged(string value) => RecomputeBurnPreview();
    partial void OnEditCaloriesChanged(string value) => RecomputeEditQualityPreview();
    partial void OnEditProteinChanged(string value) => RecomputeEditQualityPreview();
    partial void OnEditCarbsChanged(string value) => RecomputeEditQualityPreview();

    private void RecomputeBurnPreview()
    {
        if (!double.TryParse(ManualExerciseMinutes, out var minutes)) minutes = 0;
        var burned = EstimateBurnedCalories(minutes);
        ManualBurnPreviewText = $"{T("burn")}: {Math.Round(burned)} kcal";
    }

    private static double EstimateBurnedCalories(double minutes)
    {
        var exerciseBurn = Math.Max(0, minutes) * 5.0;
        return exerciseBurn;
    }

    private void RecomputeEditQualityPreview()
    {
        if (_editingMeal == null)
        {
            EditQualityPreviewText = "";
            EditBadgePreviewText = "";
            EditSemaphorePreviewText = "";
            return;
        }

        if (!double.TryParse(EditCalories, out var calories)) calories = _editingMeal.TotalCalories;
        if (!double.TryParse(EditProtein, out var protein)) protein = _editingMeal.TotalProteinG;
        if (!double.TryParse(EditCarbs, out var carbs)) carbs = _editingMeal.TotalCarbsG;

        var quality = MealQualityService.Classify(
            _editingMeal.AiNotes,
            calories,
            protein,
            carbs,
            _editingMeal.OverallConfidence);

        var lang = Preferences.Default.Get("app_lang", "fr");
        EditQualityPreviewText = $"{T("quality")}: {quality.label} ({Math.Round(quality.score)}/100)";
        EditBadgePreviewText = $"{T("badge")}: {MealQualityService.GetBadge(quality.score, lang)}";
        EditSemaphorePreviewText = $"{T("semaphore")}: {MealQualityService.GetSemaphore(quality.score, lang)}";
    }
}

public class DiaryDayTab
{
    public DateTime DayLocal { get; set; }
    public string Label { get; set; } = "";
    public bool IsSelected { get; set; }
}

public class DiaryToggleItem
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public bool IsSelected { get; set; }
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
    public double QualityScore { get; set; }
    public string QualityLabel { get; set; } = "";

    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string DescriptionText { get; set; } = "";
    public bool HasDescription => !string.IsNullOrWhiteSpace(DescriptionText);
    public string AnalysisText { get; set; } = "";
    public bool HasAnalysis => !string.IsNullOrWhiteSpace(AnalysisText);
    public string QualityBadgeText { get; set; } = "";
    public bool HasQualityBadge => !string.IsNullOrWhiteSpace(QualityBadgeText);
    public string QualitySemaphoreText { get; set; } = "";
    public bool HasQualitySemaphore => !string.IsNullOrWhiteSpace(QualitySemaphoreText);
    public string CaloriesText { get; set; } = "";
    public string ProteinText { get; set; } = "";
    public string CarbsText { get; set; } = "";

    public static DiaryMealItem FromEntry(MealEntry e, List<MealItem>? items)
    {
        var lang = Preferences.Default.Get("app_lang", "fr");
        var local = e.DateUtc.ToLocalTime();
        var displayDescription = string.IsNullOrWhiteSpace(e.Description) ? e.RawText : e.Description;
        var fallbackTitle = lang switch
        {
            "en" => "Meal",
            "pt" => "Refeição",
            "es" => "Comida",
            _ => "Repas",
        };
        var title = string.IsNullOrWhiteSpace(e.RawText) ? (string.IsNullOrWhiteSpace(displayDescription) ? fallbackTitle : displayDescription) : e.RawText;
        title = title.Length > 28 ? title.Substring(0, 28) + "…" : title;
        var itemList = items == null
            ? ""
            : string.Join(", ", items
                .Select(i => i.Name?.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .Take(5));
        var analysisText = BuildAnalysisText(e.AiNotes, itemList, e.QualityLabel, e.QualityScore);
        var badgeText = MealQualityService.GetBadge(e.QualityScore, lang);
        var semaphoreText = MealQualityService.GetSemaphore(e.QualityScore, lang);

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
            QualityScore = e.QualityScore,
            QualityLabel = e.QualityLabel,
            Title = title,
            Subtitle = local.ToString("dddd dd MMM · HH:mm", CultureInfo.CurrentCulture),
            DescriptionText = displayDescription,
            AnalysisText = analysisText,
            QualityBadgeText = badgeText,
            QualitySemaphoreText = semaphoreText,
            CaloriesText = $"{Math.Round(e.TotalCalories)} kcal",
            ProteinText = $"P {Math.Round(e.TotalProteinG)}g",
            CarbsText = $"C {Math.Round(e.TotalCarbsG)}g"
        };
    }

    private static string BuildAnalysisText(string aiNotes, string items, string qualityLabel = "", double qualityScore = 0)
    {
        var notes = aiNotes?.Trim() ?? "";
        var itemsText = items?.Trim() ?? "";
        var qualityText = string.IsNullOrWhiteSpace(qualityLabel) ? "" : $"Qualité: {qualityLabel} ({Math.Round(qualityScore)}/100)";

        if (!string.IsNullOrWhiteSpace(qualityText) && !string.IsNullOrWhiteSpace(notes) && !string.IsNullOrWhiteSpace(itemsText))
            return $"IA: {qualityText} · {notes} · {itemsText}";
        if (!string.IsNullOrWhiteSpace(qualityText) && !string.IsNullOrWhiteSpace(notes))
            return $"IA: {qualityText} · {notes}";
        if (!string.IsNullOrWhiteSpace(qualityText) && !string.IsNullOrWhiteSpace(itemsText))
            return $"IA: {qualityText} · {itemsText}";
        if (!string.IsNullOrWhiteSpace(qualityText))
            return $"IA: {qualityText}";
        if (!string.IsNullOrWhiteSpace(notes) && !string.IsNullOrWhiteSpace(itemsText))
            return $"IA: {notes} · {itemsText}";
        if (!string.IsNullOrWhiteSpace(notes))
            return $"IA: {notes}";
        if (!string.IsNullOrWhiteSpace(itemsText))
            return $"IA: {itemsText}";
        return "";
    }
}
