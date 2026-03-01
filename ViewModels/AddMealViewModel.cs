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
    public string AnalyzeText => T("analyze");
    public string ClearText => T("clear");
    public string ResultTitle => T("result");

    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath);

    public AddMealViewModel(ApiService api, LocalDb db, PointsService points, BackendSyncService sync)
    {
        _api = api;
        _db = db;
        _points = points;
        _sync = sync;
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
            "add_meal_title" => L(lang, "Enregistrer un repas", "Log a meal", "Registrar refeição", "Registrar comida"),
            "add_meal_subtitle" => L(lang, "Décrivez votre repas et/ou ajoutez une photo pour l'analyse IA.", "Describe your meal and/or add a photo for AI analysis.", "Descreva sua refeição e/ou adicione uma foto para análise de IA.", "Describe tu comida y/o agrega una foto para el análisis de IA."),
            "add_placeholder" => L(lang, "Ex : salade de poulet, yaourt grec, pomme...", "Ex: chicken salad, greek yogurt, apple...", "Ex: salada de frango, iogurte grego, maçã...", "Ej: ensalada de pollo, yogur griego, manzana..."),
            "pick_photo" => L(lang, "Choisir une photo", "Choose photo", "Escolher foto", "Elegir foto"),
            "capture_photo" => L(lang, "Prendre une photo", "Take photo", "Tirar foto", "Tomar foto"),
            "analyze" => L(lang, "Analyser", "Analyze", "Analisar", "Analizar"),
            "clear" => L(lang, "Vider", "Clear", "Limpar", "Limpiar"),
            "result" => L(lang, "Résultat", "Result", "Resultado", "Resultado"),
            "quality" => L(lang, "Qualité IA", "AI quality", "Qualidade IA", "Calidad IA"),
            "badge" => L(lang, "Badge", "Badge", "Insígnia", "Insignia"),
            "semaphore" => L(lang, "Sémaphore", "Semaphore", "Semáforo", "Semáforo"),
            "missing_input_title" => L(lang, "Entrée manquante", "Missing input", "Entrada ausente", "Falta información"),
            "missing_input_message" => L(lang, "Ajoutez un texte ou une photo avant l'analyse.", "Add text or a photo before analysis.", "Adicione texto ou foto antes da análise.", "Añade texto o foto antes del análisis."),
            "not_logged_in" => L(lang, "Vous n'êtes pas connecté. Veuillez vous reconnecter.", "Not logged in. Please login again.", "Você não está conectado. Faça login novamente.", "No has iniciado sesión. Vuelve a iniciar sesión."),
            "saved_title" => L(lang, "Enregistré", "Saved", "Salvo", "Guardado"),
            "saved_message" => L(lang, "Repas enregistré dans PostgreSQL.", "Meal saved to PostgreSQL.", "Refeição salva no PostgreSQL.", "Comida guardada en PostgreSQL."),
            "backend_identity_error" => L(lang, "Impossible de synchroniser l'identité backend.", "Unable to sync backend identity.", "Não foi possível sincronizar identidade no backend.", "No se pudo sincronizar la identidad del backend."),
            "backend_save_error" => L(lang, "Impossible d'enregistrer le repas dans PostgreSQL.", "Unable to save meal to PostgreSQL.", "Não foi possível salvar a refeição no PostgreSQL.", "No se pudo guardar la comida en PostgreSQL."),
            "earned_points" => L(lang, "+{0} pièces gagnées · Solde : {1}", "+{0} coins earned · Balance: {1}", "+{0} moedas ganhas · Saldo: {1}", "+{0} monedas ganadas · Saldo: {1}"),
            "error_title" => L(lang, "Erreur", "Error", "Erro", "Error"),
            _ => key,
        };
    }
}
