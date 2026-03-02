using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class AddMealViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly LocalDb _db;
    private readonly PointsService _points;
    private readonly BackendSyncService _sync;
    private readonly IVoiceInputService _voiceInput;

    [ObservableProperty] private string text = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string photoPath = "";
    [ObservableProperty] private byte[]? photoBytes;
    [ObservableProperty] private string photoMime = "image/jpeg";

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

    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath);

    public AddMealViewModel(ApiService api, LocalDb db, PointsService points, BackendSyncService sync, IVoiceInputService voiceInput)
    {
        _api = api;
        _db = db;
        _points = points;
        _sync = sync;
        _voiceInput = voiceInput;
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
}
