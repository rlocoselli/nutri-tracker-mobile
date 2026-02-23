using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class AddMealViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly LocalDb _db;

    [ObservableProperty] private string text = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string photoPath = "";
    [ObservableProperty] private byte[]? photoBytes;
    [ObservableProperty] private string photoMime = "image/jpeg";

    [ObservableProperty] private bool hasResult;
    [ObservableProperty] private string resultSummary = "";
    [ObservableProperty] private string resultNotes = "";

    public string TitleText => T("add_meal_title");
    public string SubtitleText => T("add_meal_subtitle");
    public string EditorPlaceholder => T("add_placeholder");
    public string PickPhotoText => T("pick_photo");
    public string CapturePhotoText => T("capture_photo");
    public string AnalyzeText => T("analyze");
    public string ClearText => T("clear");
    public string ResultTitle => T("result");

    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath);

    public AddMealViewModel(ApiService api, LocalDb db)
    {
        _api = api;
        _db = db;
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
            await _db.SaveMealAsync(entry, items);

            ResultSummary = $"Calories: {Math.Round(resp.meal.totals.calories)} | Carbs: {Math.Round(resp.meal.totals.carbs_g)}g | Protein: {Math.Round(resp.meal.totals.protein_g)}g";
            ResultNotes = resp.meal.notes;
            HasResult = true;

            await Application.Current!.MainPage!.DisplayAlert(T("saved_title"), T("saved_message"), "OK");
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
        var lang = Preferences.Default.Get("app_lang", "fr");
        return key switch
        {
            "add_meal_title" => lang == "en" ? "Log a meal" : "Enregistrer un repas",
            "add_meal_subtitle" => lang == "en" ? "Describe your meal and/or add a photo for AI analysis." : "Décrivez votre repas et/ou ajoutez une photo pour l'analyse IA.",
            "add_placeholder" => lang == "en" ? "Ex: chicken salad, greek yogurt, apple..." : "Ex : salade de poulet, yaourt grec, pomme...",
            "pick_photo" => lang == "en" ? "Choose photo" : "Choisir une photo",
            "capture_photo" => lang == "en" ? "Take photo" : "Prendre une photo",
            "analyze" => lang == "en" ? "Analyze" : "Analyser",
            "clear" => lang == "en" ? "Clear" : "Vider",
            "result" => lang == "en" ? "Result" : "Résultat",
            "missing_input_title" => lang == "en" ? "Missing input" : "Entrée manquante",
            "missing_input_message" => lang == "en" ? "Add text or a photo before analysis." : "Ajoutez un texte ou une photo avant l'analyse.",
            "not_logged_in" => lang == "en" ? "Not logged in. Please login again." : "Vous n'êtes pas connecté. Veuillez vous reconnecter.",
            "saved_title" => lang == "en" ? "Saved" : "Enregistré",
            "saved_message" => lang == "en" ? "Meal saved to local database." : "Repas enregistré dans la base locale.",
            "error_title" => lang == "en" ? "Error" : "Erreur",
            _ => key,
        };
    }
}
