using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using NutritionTracker.Models;
using NutritionTracker.Services;
using Plugin.AdMob.Services;

namespace NutritionTracker.ViewModels;

public partial class AddMealViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly PointsService _points;
    private readonly HealthyTipService _tips;
    private readonly GamificationCoachService _gamification;
    private readonly BackendSyncService _sync;
    private readonly IVoiceInputService _voiceInput;
    private readonly IEntryFeedbackService _entryFeedback;
    private readonly IInterstitialAdService _interstitialAd;

    [ObservableProperty] private string text = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string photoPath = "";
    [ObservableProperty] private byte[]? photoBytes;
    [ObservableProperty] private string photoMime = "image/jpeg";
    [ObservableProperty] private StoryVisibilityOption? selectedStoryVisibilityOption;
    [ObservableProperty] private MealTypeOption? selectedMealTypeOption;

    [ObservableProperty] private bool hasResult;
    [ObservableProperty] private string resultSummary = "";
    [ObservableProperty] private string resultNotes = "";
    [ObservableProperty] private string resultQuality = "";
    [ObservableProperty] private string resultBadge = "";
    [ObservableProperty] private string resultTigerCatMood = "";
    [ObservableProperty] private string resultSemaphore = "";
    [ObservableProperty] private string resultMotivation = "";
    [ObservableProperty] private string resultTipTitle = "";
    [ObservableProperty] private string resultTipMessage = "";
    [ObservableProperty] private string resultTipChallenge = "";
    [ObservableProperty] private string resultTipProgress = "";
    [ObservableProperty] private string resultSocialStatus = "";
    [ObservableProperty] private string resultScoreWhy = "";

    public string TitleText => T("add_meal_title");
    public string SubtitleText => T("add_meal_subtitle");
    public string EditorPlaceholder => T("add_placeholder");
    public string PickPhotoText => T("pick_photo");
    public string CapturePhotoText => T("capture_photo");
    public string VoiceInputText => T("voice_input");
    public string AnalyzeText => T("analyze");
    public string ClearText => T("clear");
    public string ResultTitle => T("result");
    public string PhotoSelectedHint => T("photo_selected_hint");
    public string StoryVisibilityTitle => T("story_visibility_title");
    public string StoryVisibilityLabel => T("story_visibility_label");
    public string MealTypeTitle => T("meal_type_title");
    public string MealTypeLabel => T("meal_type_label");
    public bool HasTip => !string.IsNullOrWhiteSpace(ResultTipMessage);
    public bool HasScoreWhy => !string.IsNullOrWhiteSpace(ResultScoreWhy);

    public ObservableCollection<StoryVisibilityOption> StoryVisibilityOptions { get; } = new();
    public ObservableCollection<MealTypeOption> MealTypeOptions { get; } = new();

    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath);
    public ImageSource MealPreviewSource => string.IsNullOrWhiteSpace(PhotoPath)
        ? ImageSource.FromFile("story_food_default.svg")
        : ImageSource.FromFile(PhotoPath);

    public AddMealViewModel(ApiService api, PointsService points, HealthyTipService tips, GamificationCoachService gamification, BackendSyncService sync, IVoiceInputService voiceInput, IEntryFeedbackService entryFeedback, IInterstitialAdService interstitialAd)
    {
        _api = api;
        _points = points;
        _tips = tips;
        _gamification = gamification;
        _sync = sync;
        _voiceInput = voiceInput;
        _entryFeedback = entryFeedback;
        _interstitialAd = interstitialAd;

        RebuildStoryVisibilityOptions();
        RebuildMealTypeOptions();
        PrepareInterstitial();
        var defaultVisibility = BackendSyncService.NormalizeStoryVisibility(Preferences.Default.Get("story_visibility_default", "friends"));
        SelectedStoryVisibilityOption = StoryVisibilityOptions.FirstOrDefault(x => x.Value == defaultVisibility) ?? StoryVisibilityOptions.FirstOrDefault();
        var detectedType = MealTypeService.DetectByLocalTime(DateTime.Now);
        SelectedMealTypeOption = MealTypeOptions.FirstOrDefault(x => x.Value == detectedType) ?? MealTypeOptions.FirstOrDefault();
    }

    partial void OnPhotoPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasPhoto));
        OnPropertyChanged(nameof(MealPreviewSource));
    }
    partial void OnResultTipMessageChanged(string value) => OnPropertyChanged(nameof(HasTip));
    partial void OnResultScoreWhyChanged(string value) => OnPropertyChanged(nameof(HasScoreWhy));

    [RelayCommand]
    private async Task PickPhoto()
    {
        var r = await ImageHelper.PickOrCaptureJpegAsync(capture: false);
        if (r == null) return;
        (PhotoBytes, PhotoMime, PhotoPath) = r.Value;
    }

    [RelayCommand]
    private async Task CapturePhoto()
    {
        var r = await ImageHelper.PickOrCaptureJpegAsync(capture: true);
        if (r == null) return;
        (PhotoBytes, PhotoMime, PhotoPath) = r.Value;
    }

    [RelayCommand]
    private void CloseResult() => HasResult = false;

    [RelayCommand]
    private void Clear()
    {
        Text = "";
        PhotoPath = "";
        PhotoBytes = null;
        PhotoMime = "image/jpeg";
        HasResult = false;
        ResultSummary = "";
        ResultNotes = "";
        ResultQuality = "";
        ResultBadge = "";
        ResultTigerCatMood = "";
        ResultSemaphore = "";
        ResultMotivation = "";
        ResultTipTitle = "";
        ResultTipMessage = "";
        ResultTipChallenge = "";
        ResultTipProgress = "";
        ResultSocialStatus = "";
        ResultScoreWhy = "";

        var defaultVisibility = BackendSyncService.NormalizeStoryVisibility(Preferences.Default.Get("story_visibility_default", "friends"));
        SelectedStoryVisibilityOption = StoryVisibilityOptions.FirstOrDefault(x => x.Value == defaultVisibility) ?? StoryVisibilityOptions.FirstOrDefault();
        var detectedType = MealTypeService.DetectByLocalTime(DateTime.Now);
        SelectedMealTypeOption = MealTypeOptions.FirstOrDefault(x => x.Value == detectedType) ?? MealTypeOptions.FirstOrDefault();
    }

    [RelayCommand]
    private async Task VoiceInput()
    {
        if (IsBusy)
            return;

        try
        {
            var recognized = await _voiceInput.ListenOnceAsync();
            if (string.IsNullOrWhiteSpace(recognized))
            {
                await Application.Current!.MainPage!.DisplayAlert(T("voice_title"), T("voice_empty"), "OK");
                return;
            }

            Text = string.IsNullOrWhiteSpace(Text)
                ? recognized.Trim()
                : $"{Text.Trim()} {recognized.Trim()}";
        }
        catch
        {
            await Application.Current!.MainPage!.DisplayAlert(T("voice_title"), T("voice_failed"), "OK");
        }
    }

    [RelayCommand]
    private async Task Analyze()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(Text) && PhotoBytes == null)
        {
            await Application.Current!.MainPage!.DisplayAlert(T("missing_input_title"), T("missing_input_message"), "OK");
            return;
        }

        IsBusy = true;
        HasResult = false;
        try
        {
            var idToken = Preferences.Default.Get("auth_id_token", "");
            if (string.IsNullOrWhiteSpace(idToken))
                throw new Exception(T("not_logged_in"));

            var appLang = Preferences.Default.Get("app_lang", "fr");
            var lang = LanguageHelper.DetectLanguageCode(Text, appLang);

            var resp = await _api.AnalyzeMealAsync(idToken, lang, Text, PhotoBytes, PhotoMime);
            var (entry, items) = MealMapper.MapToDb(resp, Text, PhotoPath);
            if (PhotoBytes is { Length: > 0 })
            {
                var mime = string.IsNullOrWhiteSpace(PhotoMime) ? "image/jpeg" : PhotoMime;
                entry.PhotoPath = $"data:{mime};base64,{Convert.ToBase64String(PhotoBytes)}";
            }

            entry.StoryVisibility = SelectedStoryVisibilityOption?.Value
                ?? BackendSyncService.NormalizeStoryVisibility(Preferences.Default.Get("story_visibility_default", "friends"));
            entry.MealType = MealTypeService.Normalize(
                SelectedMealTypeOption?.Value
                ?? MealTypeService.DetectByLocalTime(entry.DateUtc.ToLocalTime()));

            // No real photo — leave PhotoPath empty; client-side fallback generates the cartoon placeholder
            if (PhotoBytes is not { Length: > 0 })
                entry.PhotoPath = "";

            var identityOk = await _sync.EnsureBackendIdentityAsync(idToken);
            if (!identityOk)
                throw new Exception(T("backend_identity_error"));

            var backendId = await _sync.CreateMealAsync(entry, items);
            if (string.IsNullOrWhiteSpace(backendId))
                throw new Exception(T("backend_save_error"));

            await _entryFeedback.PlayEntryAddedAsync();

            Preferences.Default.Set("last_meal_logged_day_local", entry.DateUtc.ToLocalTime().ToString("yyyy-MM-dd"));

            ResultSummary = $"Calories: {Math.Round(resp.meal.totals.calories)} | Carbs: {Math.Round(resp.meal.totals.carbs_g)}g | Protein: {Math.Round(resp.meal.totals.protein_g)}g";
            ResultNotes = resp.meal.notes;
            ResultQuality = T("quality") + $": {entry.QualityLabel} ({Math.Round(entry.QualityScore)}/100)";
            ResultBadge = T("badge") + $": {MealQualityService.GetBadge(entry.QualityScore, appLang)} · {MealQualityService.GetFoodStyleBadge(entry.QualityScore, appLang)}";
            ResultTigerCatMood = MealQualityService.GetTigerCatMood(entry.QualityScore, appLang);
            ResultSemaphore = T("semaphore") + $": {MealQualityService.GetSemaphore(entry.QualityScore, appLang)}";
            var scoreWhy = MealQualityService.BuildScoreExplanation(
                Text,
                resp.meal.notes,
                resp.meal.items.Select(i => i.name),
                entry.TotalCalories,
                entry.TotalProteinG,
                entry.TotalCarbsG,
                entry.OverallConfidence,
                appLang,
                maxFactors: 5,
                includeHeader: true);
            ResultScoreWhy = scoreWhy;
            HasResult = true;

            var pointsEarned = ComputeAwardPoints(entry);
            var postBonus = _gamification.EvaluateSharedPostBonus(entry);
            pointsEarned += postBonus.BonusPoints;
            var newBalance = _points.Award(pointsEarned);
            var streakDays = await ComputeBalancedStreakAsync();
            await RewardPopupService.ShowAsync(pointsEarned, newBalance, streakDays);

            if (entry.QualityScore >= 75)
                await TigrouPopupService.ShowAsync(entry.QualityScore, appLang);

            var tip = await _tips.BuildTipForEntryAsync(entry);
            ResultTipTitle = tip.Title;
            ResultTipMessage = tip.Message;
            ResultTipChallenge = tip.Challenge;
            ResultTipProgress = tip.Progress;
            ResultSocialStatus = postBonus.Status;
            ResultMotivation = BuildMotivationLine(entry, pointsEarned, postBonus.BonusPoints);

            _ = _sync.TryUpdateGamificationStateAsync(
                sharedStreakDays: postBonus.SharedStreakDays,
                weeklySharedPosts: postBonus.WeeklySharedPosts,
                weeklyMissionStatus: postBonus.Status);

            _ = _sync.TryPostGamificationEventAsync(
                eventType: "meal_score_explanation",
                title: "Score transparency",
                message: scoreWhy,
                metadata: new Dictionary<string, object>
                {
                    ["meal_id"] = backendId,
                    ["quality_score"] = Math.Round(entry.QualityScore, 1),
                    ["quality_label"] = entry.QualityLabel,
                    ["points_earned"] = pointsEarned,
                    ["social_bonus"] = postBonus.BonusPoints,
                    ["shared_streak_days"] = postBonus.SharedStreakDays,
                    ["weekly_shared_posts"] = postBonus.WeeklySharedPosts,
                    ["story_visibility"] = entry.StoryVisibility,
                });

            await PromptShareToFriendAsync(resp.meal.notes, entry.TotalCalories, entry.TotalProteinG, entry.TotalCarbsG);
            ShowInterstitialIfReady();
            ClearInputForNextMeal();
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert(T("error_title"), ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PrepareInterstitial()
    {
        if (string.IsNullOrWhiteSpace(AdMobSettings.InterstitialAdUnitId))
            return;

        try
        {
            _interstitialAd.PrepareAd(AdMobSettings.InterstitialAdUnitId);
        }
        catch
        {
            // Ads are optional and must never block meal logging.
        }
    }

    private void ShowInterstitialIfReady()
    {
        if (!_interstitialAd.IsAdLoaded)
            return;

        try
        {
            _interstitialAd.ShowAd();
        }
        catch
        {
            // Ads are optional and must never block meal logging.
        }
    }

    private void ClearInputForNextMeal()
    {
        Text = "";
        PhotoPath = "";
        PhotoBytes = null;
        PhotoMime = "image/jpeg";
        var defaultVisibility = BackendSyncService.NormalizeStoryVisibility(Preferences.Default.Get("story_visibility_default", "friends"));
        SelectedStoryVisibilityOption = StoryVisibilityOptions.FirstOrDefault(x => x.Value == defaultVisibility) ?? StoryVisibilityOptions.FirstOrDefault();
        var detectedType = MealTypeService.DetectByLocalTime(DateTime.Now);
        SelectedMealTypeOption = MealTypeOptions.FirstOrDefault(x => x.Value == detectedType) ?? MealTypeOptions.FirstOrDefault();
    }

    private static string T(string key)
    {
        if (key == "saved_title")
            return LocalizationService.T("saved_title_common");

        return LocalizationService.T(key);
    }

    private async Task<int> ComputeBalancedStreakAsync()
    {
        try
        {
            var goals = await _sync.GetGoalsAsync();
            return await DailyRewardService.ComputeCurrentStreakAsync(goals, async dayLocal =>
            {
                var start = DateTime.SpecifyKind(dayLocal.Date, DateTimeKind.Local).ToUniversalTime();
                var end = DateTime.SpecifyKind(dayLocal.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
                var meals = await _sync.GetMealsBetweenUtcAsync(start, end, includePhoto: false);
                return (
                    meals.Sum(x => x.total_calories),
                    meals.Sum(x => x.total_carbs_g),
                    meals.Sum(x => x.total_protein_g),
                    0d);
            });
        }
        catch
        {
            return 0;
        }
    }

    private void RebuildStoryVisibilityOptions()
    {
        StoryVisibilityOptions.Clear();
        StoryVisibilityOptions.Add(new StoryVisibilityOption("friends", T("story_visibility_friends")));
        StoryVisibilityOptions.Add(new StoryVisibilityOption("public", T("story_visibility_public")));
        StoryVisibilityOptions.Add(new StoryVisibilityOption("self", T("story_visibility_self")));
    }

    private void RebuildMealTypeOptions()
    {
        MealTypeOptions.Clear();
        MealTypeOptions.Add(new MealTypeOption("breakfast", MealTypeService.Label("breakfast")));
        MealTypeOptions.Add(new MealTypeOption("lunch", MealTypeService.Label("lunch")));
        MealTypeOptions.Add(new MealTypeOption("dinner", MealTypeService.Label("dinner")));
        MealTypeOptions.Add(new MealTypeOption("snack", MealTypeService.Label("snack")));
    }

    private async Task PromptShareToFriendAsync(string notes, double calories, double protein, double carbs)
    {
        var page = Application.Current?.MainPage;
        if (page == null)
            return;

        var wantsShare = await page.DisplayAlert(T("share_meal_title"), T("share_meal_prompt"), T("share_meal_yes"), T("share_meal_no"));
        if (!wantsShare)
            return;

        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        if (!identityOk)
        {
            await page.DisplayAlert(T("friends_title"), T("friend_action_signin_needed"), "OK");
            return;
        }

        var directory = await _sync.GetFriendDirectoryAsync();
        if (directory.Count == 0)
        {
            await page.DisplayAlert(T("share_meal_title"), T("share_no_friends"), "OK");
            return;
        }

        var options = directory
            .Select(x => new
            {
                UserId = (x.user_id ?? "").Trim(),
                Display = string.IsNullOrWhiteSpace(x.display_name) ? (x.email ?? "").Trim() : x.display_name.Trim()
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.UserId) && !string.IsNullOrWhiteSpace(x.Display))
            .OrderBy(x => x.Display)
            .ToList();

        if (options.Count == 0)
        {
            await page.DisplayAlert(T("share_meal_title"), T("share_no_friends"), "OK");
            return;
        }

        var selected = await page.DisplayActionSheet(
            T("share_pick_friend"),
            T("cancel"),
            null,
            options.Select(x => x.Display).ToArray());

        if (string.IsNullOrWhiteSpace(selected) || string.Equals(selected, T("cancel"), StringComparison.OrdinalIgnoreCase))
            return;

        var peer = options.FirstOrDefault(x => string.Equals(x.Display, selected, StringComparison.Ordinal));
        if (peer == null)
            return;

        var summary = string.IsNullOrWhiteSpace(notes) ? T("story_meal") : notes.Trim();
        var text = string.Format(
            T("share_meal_message_template"),
            summary,
            Math.Round(calories),
            Math.Round(protein),
            Math.Round(carbs));

        var sent = await _sync.SendPrivateMessageAsync(peer.UserId, text);
        await page.DisplayAlert(T("share_meal_title"), sent ? T("share_meal_sent") : T("friend_message_failed"), "OK");
    }

    private int ComputeAwardPoints(MealEntry entry)
    {
        var points = 10;
        if (!string.Equals(entry.StoryVisibility, "self", StringComparison.OrdinalIgnoreCase))
            points += 3;

        if (!string.IsNullOrWhiteSpace(entry.PhotoPath))
            points += 2;

        if (entry.QualityScore >= 70)
            points += 2;

        if (entry.TotalProteinG >= 25)
            points += 1;

        return points;
    }

    private static string BuildMotivationLine(MealEntry entry, int pointsEarned, int socialBonus)
    {
        var lang = (Preferences.Default.Get("app_lang", "fr") ?? "fr").Trim().ToLowerInvariant();
        if (lang != "en")
            lang = "fr";

        var isShared = !string.Equals(entry.StoryVisibility, "self", StringComparison.OrdinalIgnoreCase);

        if (lang == "en")
        {
            if (isShared)
            {
                if (socialBonus > 0)
                    return $"Great job. +{pointsEarned} coins total, including +{socialBonus} social streak bonus.";

                return $"Great job. +{pointsEarned} coins. Shared entries help you stay accountable and hit your goals faster.";
            }

            return $"Great job. +{pointsEarned} coins. Share your next healthy entry to boost consistency.";
        }

        if (isShared)
        {
            if (socialBonus > 0)
                return $"Super. +{pointsEarned} pieces au total, dont +{socialBonus} de bonus serie sociale.";

            return $"Super. +{pointsEarned} pieces. Les entrees partagees renforcent la regularite et accelerent l'atteinte des objectifs.";
        }

        return $"Super. +{pointsEarned} pieces. Partage la prochaine entree saine pour booster ta constance.";
    }
}

public sealed class StoryVisibilityOption
{
    public string Value { get; }
    public string Label { get; }

    public StoryVisibilityOption(string value, string label)
    {
        Value = value;
        Label = label;
    }
}

public sealed class MealTypeOption
{
    public string Value { get; }
    public string Label { get; }

    public MealTypeOption(string value, string label)
    {
        Value = value;
        Label = label;
    }
}
