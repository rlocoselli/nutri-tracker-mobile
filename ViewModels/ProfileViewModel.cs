using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Pages;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly IServiceProvider _sp;
    private readonly Services.LocalDb _db;
    private readonly Services.GoogleFitService _googleFit;

    public List<string> LanguageOptions { get; } = new() { "Français", "English" };

    [ObservableProperty] private string name = "";
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string pictureUrl = "";
    [ObservableProperty] private string selectedLanguage = "Français";
    [ObservableProperty] private string currentLanguageText = "Langue actuelle : Français";
    [ObservableProperty] private string todayStepsText = "0";
    [ObservableProperty] private string todayBurnText = "0 kcal";
    [ObservableProperty] private string fitSyncStatusText = "";

    public string LanguageLabel => LocalizationService.T("language");
    public string ProfileTitle => LocalizationService.T("profile_title");
    public string StepsTodayLabel => LocalizationService.T("steps_today");
    public string BurnedCaloriesLabel => LocalizationService.T("burned_calories");
    public string AdviceTitle => LocalizationService.T("advice_title");
    public string AdviceHint => LocalizationService.T("advice_hint");
    public string GenerateRecoText => LocalizationService.T("generate_reco");
    public string LogoutText => LocalizationService.T("logout");

    public ProfileViewModel(IServiceProvider sp, Services.LocalDb db, Services.GoogleFitService googleFit)
    {
        _sp = sp;
        _db = db;
        _googleFit = googleFit;
    }

    public async Task LoadAsync()
    {
        Name = Preferences.Default.Get("profile_name", "");
        Email = Preferences.Default.Get("profile_email", "");
        PictureUrl = Preferences.Default.Get("profile_picture", "");

        var appLang = Preferences.Default.Get("app_lang", "fr");
        SelectedLanguage = appLang == "en" ? "English" : "Français";
        CurrentLanguageText = appLang == "en" ? LocalizationService.T("current_lang_en") : LocalizationService.T("current_lang_fr");

        OnPropertyChanged(nameof(LanguageLabel));
        OnPropertyChanged(nameof(ProfileTitle));
        OnPropertyChanged(nameof(StepsTodayLabel));
        OnPropertyChanged(nameof(BurnedCaloriesLabel));
        OnPropertyChanged(nameof(AdviceTitle));
        OnPropertyChanged(nameof(AdviceHint));
        OnPropertyChanged(nameof(GenerateRecoText));
        OnPropertyChanged(nameof(LogoutText));

        var accessToken = Preferences.Default.Get("auth_access_token", "");
        if (!GoogleFitService.Enabled)
        {
            FitSyncStatusText = LocalizationService.T("sync_disabled");
        }
        else if (!string.IsNullOrWhiteSpace(accessToken))
        {
            try
            {
                var fit = await _googleFit.GetTodaySummaryAsync(accessToken);
                await _db.UpsertGoogleFitDailyAsync(DateTime.Now.Date, fit.steps, fit.burnedCalories);
                FitSyncStatusText = LocalizationService.T("sync_ok");
            }
            catch (Exception ex)
            {
                FitSyncStatusText = $"{LocalizationService.T("sync_error")}: {ex.Message}";
            }
        }
        else
        {
            FitSyncStatusText = LocalizationService.T("sync_no_token");
        }

        var todayLocal = DateTime.Now.Date;
        var fromUtc = DateTime.SpecifyKind(todayLocal, DateTimeKind.Local).ToUniversalTime();
        var toUtc = DateTime.SpecifyKind(todayLocal.AddDays(1), DateTimeKind.Local).ToUniversalTime();
        var totals = await _db.GetExerciseTotalsBetweenUtcAsync(fromUtc, toUtc);

        TodayStepsText = totals.steps.ToString();
        TodayBurnText = $"{Math.Round(totals.burnedCalories)} kcal";
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        var lang = value == "English" ? "en" : "fr";
        Preferences.Default.Set("app_lang", lang);
        CurrentLanguageText = lang == "en" ? LocalizationService.T("current_lang_en") : LocalizationService.T("current_lang_fr");
    }

    [RelayCommand]
    private async Task OpenRecommendations()
    {
        await Shell.Current.Navigation.PushAsync(_sp.GetRequiredService<RecommendationsPage>());
    }

    [RelayCommand]
    private async Task Logout()
    {
        Preferences.Default.Remove("auth_id_token");
        Preferences.Default.Remove("auth_access_token");
        Preferences.Default.Remove("profile_name");
        Preferences.Default.Remove("profile_email");
        Preferences.Default.Remove("profile_picture");

        // Return to login screen
        var login = _sp.GetRequiredService<LoginPage>();
        Application.Current!.MainPage = new NavigationPage(login);
        await Task.CompletedTask;
    }
}
