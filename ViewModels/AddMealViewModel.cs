using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class AddMealViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly PointsService _points;
    private readonly BackendSyncService _sync;
    private readonly IVoiceInputService _voiceInput;

    [ObservableProperty] private string text = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string photoPath = "";
    [ObservableProperty] private byte[]? photoBytes;
    [ObservableProperty] private string photoMime = "image/jpeg";
    [ObservableProperty] private StoryVisibilityOption? selectedStoryVisibilityOption;

    [ObservableProperty] private bool hasResult;
    [ObservableProperty] private string resultSummary = "";
    [ObservableProperty] private string resultNotes = "";
    [ObservableProperty] private string resultQuality = "";
    [ObservableProperty] private string resultBadge = "";
    [ObservableProperty] private string resultSemaphore = "";

    public string TitleText => T("add_meal_title");
    public string SubtitleText => T("add_meal_subtitle");
    public string EditorPlaceholder => T("add_placeholder");
    public string PickPhotoText => T("pick_photo");
    public string CapturePhotoText => T("capture_photo");
    public string VoiceInputText => T("voice_input");
    public string AnalyzeText => T("analyze");
    public string ClearText => T("clear");
    public string ResultTitle => T("result");
    public string StoryVisibilityTitle => T("story_visibility_title");
    public string StoryVisibilityLabel => T("story_visibility_label");

    public ObservableCollection<StoryVisibilityOption> StoryVisibilityOptions { get; } = new();

    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath);

    public AddMealViewModel(ApiService api, PointsService points, BackendSyncService sync, IVoiceInputService voiceInput)
    {
        _api = api;
        _points = points;
        _sync = sync;
        _voiceInput = voiceInput;

        RebuildStoryVisibilityOptions();
        var defaultVisibility = BackendSyncService.NormalizeStoryVisibility(Preferences.Default.Get("story_visibility_default", "friends"));
        SelectedStoryVisibilityOption = StoryVisibilityOptions.FirstOrDefault(x => x.Value == defaultVisibility) ?? StoryVisibilityOptions.FirstOrDefault();
    }

    partial void OnPhotoPathChanged(string value) => OnPropertyChanged(nameof(HasPhoto));

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
        ResultSemaphore = "";

        var defaultVisibility = BackendSyncService.NormalizeStoryVisibility(Preferences.Default.Get("story_visibility_default", "friends"));
        SelectedStoryVisibilityOption = StoryVisibilityOptions.FirstOrDefault(x => x.Value == defaultVisibility) ?? StoryVisibilityOptions.FirstOrDefault();
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

            var identityOk = await _sync.EnsureBackendIdentityAsync(idToken);
            if (!identityOk)
                throw new Exception(T("backend_identity_error"));

            var backendId = await _sync.CreateMealAsync(entry, items);
            if (string.IsNullOrWhiteSpace(backendId))
                throw new Exception(T("backend_save_error"));

            ResultSummary = $"Calories: {Math.Round(resp.meal.totals.calories)} | Carbs: {Math.Round(resp.meal.totals.carbs_g)}g | Protein: {Math.Round(resp.meal.totals.protein_g)}g";
            ResultNotes = resp.meal.notes;
            ResultQuality = T("quality") + $": {entry.QualityLabel} ({Math.Round(entry.QualityScore)}/100)";
            ResultBadge = T("badge") + $": {MealQualityService.GetBadge(entry.QualityScore, appLang)}";
            ResultSemaphore = T("semaphore") + $": {MealQualityService.GetSemaphore(entry.QualityScore, appLang)}";
            HasResult = true;

            var newBalance = _points.Award(10);
            var earnedText = string.Format(T("earned_points"), 10, newBalance);

            await Application.Current!.MainPage!.DisplayAlert(T("saved_title"), $"{T("saved_message")}\n{earnedText}", "OK");
            await PromptShareToFriendAsync(resp.meal.notes, entry.TotalCalories, entry.TotalProteinG, entry.TotalCarbsG);
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

    private static string T(string key)
    {
        if (key == "saved_title")
            return LocalizationService.T("saved_title_common");

        return LocalizationService.T(key);
    }

    private void RebuildStoryVisibilityOptions()
    {
        StoryVisibilityOptions.Clear();
        StoryVisibilityOptions.Add(new StoryVisibilityOption("friends", T("story_visibility_friends")));
        StoryVisibilityOptions.Add(new StoryVisibilityOption("public", T("story_visibility_public")));
        StoryVisibilityOptions.Add(new StoryVisibilityOption("self", T("story_visibility_self")));
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
