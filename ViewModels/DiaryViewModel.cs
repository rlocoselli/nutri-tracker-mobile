using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using NutritionTracker.Models;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class DiaryViewModel : ObservableObject
{
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
    public ObservableCollection<StoryPostItem> StoryPosts { get; } = new();
    public bool HasStories => StoryPosts.Count > 0;

    [ObservableProperty] private string dayTotalsText = "";
    [ObservableProperty] private bool isLoading;

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
    [ObservableProperty] private IList<double> macroDonutValues = Array.Empty<double>();
    [ObservableProperty] private IList<string> macroDonutLabels = Array.Empty<string>();
    [ObservableProperty] private string macroProteinText = "P 0g";
    [ObservableProperty] private string macroCarbsText = "C 0g";
    [ObservableProperty] private string macroFatText = "F 0g";
    [ObservableProperty] private double dailyGoalProgress;
    [ObservableProperty] private string dailyGoalText = "0 / 0 kcal";
    [ObservableProperty] private string dailyGoalStatusText = "";

    [ObservableProperty] private double waterLiters;
    [ObservableProperty] private string waterLitersText = "0 L";
    public ObservableCollection<string> WaterBottleImages { get; } = new();
    public string HydrationTitle => T("hydration_title");
    public string AddHalfLiterText => T("add_half_liter");
    public string AddOneLiterText => T("add_one_liter");
    public string RemoveHalfLiterText => T("remove_half_liter");
    public string StoriesTitle => T("stories_title");
    public string NutritionSplitTitle => T("nutrition_split_title");
    public string DailyGoalTitle => T("daily_goal_title");
    public string LoadingText => LocalizationService.T("main_loading");

    public DiaryViewModel(PointsService points, BackendSyncService sync)
    {
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
        if (IsLoading)
            return;

        IsLoading = true;

        try
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
        OnPropertyChanged(nameof(StoriesTitle));
        OnPropertyChanged(nameof(HasStories));
        OnPropertyChanged(nameof(NutritionSplitTitle));
        OnPropertyChanged(nameof(DailyGoalTitle));
        OnPropertyChanged(nameof(LoadingText));
        RebuildMetricTabs();
        RebuildPeriodTabs();
        RebuildDayTabs();

        await LoadDayAsync(SelectedDayLocal);
        await LoadStoriesAsync();
        await LoadChartAsync();
        }
        finally
        {
            IsLoading = false;
        }
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

        IsManualPopupVisible = false;
        var manualBalance = _points.Award(8);
        await Application.Current!.MainPage!.DisplayAlert(T("saved_title"), string.Format(T("earned_points"), 8, manualBalance), "OK");
        await LoadDayAsync(SelectedDayLocal);
        await LoadStoriesAsync();
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
        await LoadStoriesAsync();
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
        await LoadStoriesAsync();
        await LoadChartAsync();
    }

    [RelayCommand]
    private async Task PrevDay()
    {
        SelectedDayLocal = SelectedDayLocal.AddDays(-1);
        await LoadDayAsync(SelectedDayLocal);
        await LoadStoriesAsync();
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
        var exerciseBurned = 0d;
        foreach (var backendMeal in backendMeals.OrderByDescending(x => x.date_utc))
        {
            var e = ToMealEntry(backendMeal);
            var items = ToMealItems(backendMeal);
            Meals.Add(DiaryMealItem.FromEntry(e, items));
        }

        var cal = entries.Sum(x => x.TotalCalories);
        var carbs = entries.Sum(x => x.TotalCarbsG);
        var prot = entries.Sum(x => x.TotalProteinG);

        var netCalories = cal - exerciseBurned;
        DayTotalsText = $"{T("total")}: {Math.Round(cal)} kcal · C {Math.Round(carbs)}g · P {Math.Round(prot)}g · {T("burn")}: {Math.Round(exerciseBurned)} · {T("net")}: {Math.Round(netCalories)}";

        var goals = await _sync.GetGoalsAsync();
        var targetCalories = Math.Max(1, goals.CaloriesTarget);
        DailyGoalProgress = Math.Clamp(cal / targetCalories, 0, 1);
        DailyGoalText = $"{Math.Round(cal)} / {Math.Round(targetCalories)} kcal";
        var delta = targetCalories - cal;
        DailyGoalStatusText = delta >= 0
            ? string.Format(T("daily_goal_remaining"), Math.Round(delta))
            : string.Format(T("daily_goal_exceeded"), Math.Round(Math.Abs(delta)));

        var proteinKcal = Math.Max(0, prot * 4);
        var carbsKcal = Math.Max(0, carbs * 4);
        var fatKcal = Math.Max(0, cal - proteinKcal - carbsKcal);
        var fatGrams = fatKcal / 9d;

        MacroDonutValues = new List<double> { proteinKcal, carbsKcal, fatKcal };
        MacroDonutLabels = new List<string> { T("macro_protein"), T("macro_carbs"), T("macro_fat") };
        MacroProteinText = $"{T("macro_protein")}: {Math.Round(prot)} g";
        MacroCarbsText = $"{T("macro_carbs")}: {Math.Round(carbs)} g";
        MacroFatText = $"{T("macro_fat")}: {Math.Round(fatGrams)} g";

        var liters = await _sync.GetWaterIntakeForDayLocalAsync(dayLocal);
        UpdateWaterUi(liters);
    }

    private async Task LoadStoriesAsync()
    {
        StoryPosts.Clear();
        OnPropertyChanged(nameof(HasStories));

        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        if (!identityOk)
            return;

        var feed = await _sync.GetFriendsFeedAsync(days: 3, limit: 40);
        var meUserId = Preferences.Default.Get("backend_user_id", "").Trim();
        var myProfileName = Preferences.Default.Get("profile_name", "").Trim();
        foreach (var s in feed)
        {
            StoryPosts.Add(new StoryPostItem
            {
            Author = ResolveStoryAuthor(s, meUserId, myProfileName),
                PostedAtText = s.date_utc.ToLocalTime().ToString("dd/MM HH:mm"),
                Caption = string.IsNullOrWhiteSpace(s.raw_text) ? T("story_meal") : s.raw_text,
                NutritionText = $"{Math.Round(s.total_calories)} kcal · P {Math.Round(s.total_protein_g)}g · C {Math.Round(s.total_carbs_g)}g",
                QualityText = string.IsNullOrWhiteSpace(s.quality_label) ? "" : $"IA: {s.quality_label}",
                PhotoSource = PhotoSourceHelper.Build(s.photo_url),
            });
        }

        OnPropertyChanged(nameof(HasStories));
    }

    private string ResolveStoryAuthor(BackendStory story, string meUserId, string myProfileName)
    {
        if (!string.IsNullOrWhiteSpace(meUserId) && string.Equals(story.user_id?.Trim(), meUserId, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(myProfileName))
                return myProfileName;
        }

        var name = (story.display_name ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, "new user", StringComparison.OrdinalIgnoreCase))
            return name;

        var email = (story.author_email ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(email) && email.Contains('@'))
        {
            var localPart = email.Split('@')[0].Trim();
            if (!string.IsNullOrWhiteSpace(localPart))
                return localPart;
        }

        return T("story_default_author");
    }

    private async Task SetWaterLitersAsync(double liters)
    {
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
        var exercises = new List<ExerciseEntry>();
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
        if (key == "saved_title")
            return LocalizationService.T("saved_title_common");

        return LocalizationService.T(key);
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
    public ImageSource? PhotoSource { get; set; }
    public bool HasPhoto => PhotoSource != null;
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
            PhotoSource = PhotoSourceHelper.Build(e.PhotoPath),
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

public class StoryPostItem
{
    public string Author { get; set; } = "";
    public string PostedAtText { get; set; } = "";
    public string Caption { get; set; } = "";
    public string NutritionText { get; set; } = "";
    public string QualityText { get; set; } = "";
    public ImageSource? PhotoSource { get; set; }
    public bool HasPhoto => PhotoSource != null;
}

internal static class PhotoSourceHelper
{
    public static ImageSource? Build(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var value = raw.Trim();

        if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = value.IndexOf(',');
            if (commaIndex > 0 && commaIndex < value.Length - 1)
            {
                var base64 = value[(commaIndex + 1)..];
                try
                {
                    var bytes = Convert.FromBase64String(base64);
                    return ImageSource.FromStream(() => new MemoryStream(bytes));
                }
                catch
                {
                    return null;
                }
            }
        }

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return ImageSource.FromUri(uri);
        }

        if (File.Exists(value))
            return ImageSource.FromFile(value);

        return null;
    }
}
