using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class HelpViewModel : ObservableObject
{
    public string TitleText => LocalizationService.T("help_title");
    public string SubtitleText => LocalizationService.T("help_subtitle");
    public string OpenHelpText => LocalizationService.T("help_open");
    public string PrivacyTitle => LocalizationService.T("privacy_title");
    public string PrivacyIntro => LocalizationService.T("privacy_intro");
    public string PrivacyLawMessage => LocalizationService.T("privacy_law_message");
    public string PrivacyRetentionTitle => LocalizationService.T("privacy_retention_title");
    public string PrivacyRetentionBody => LocalizationService.T("privacy_retention_body");
    public string PrivacySecurityTitle => LocalizationService.T("privacy_security_title");
    public string PrivacySecurityBody => LocalizationService.T("privacy_security_body");
    public string PrivacyOpenAiTitle => LocalizationService.T("privacy_openai_title");
    public string PrivacyOpenAiBody => LocalizationService.T("privacy_openai_body");
    public string PrivacyStorageTitle => LocalizationService.T("privacy_storage_title");
    public string PrivacyStorageBody => LocalizationService.T("privacy_storage_body");
    public string CharityTitle => LocalizationService.T("charity_title");
    public string CharityBody => LocalizationService.T("charity_body");
    public string UrlText => "https://nutritiontracker.fr/";

    [RelayCommand]
    private async Task OpenHelp()
    {
        await Launcher.Default.OpenAsync("https://nutritiontracker.fr/");
    }

    public Task LoadAsync()
    {
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(SubtitleText));
        OnPropertyChanged(nameof(OpenHelpText));
        OnPropertyChanged(nameof(PrivacyTitle));
        OnPropertyChanged(nameof(PrivacyIntro));
        OnPropertyChanged(nameof(PrivacyLawMessage));
        OnPropertyChanged(nameof(PrivacyRetentionTitle));
        OnPropertyChanged(nameof(PrivacyRetentionBody));
        OnPropertyChanged(nameof(PrivacySecurityTitle));
        OnPropertyChanged(nameof(PrivacySecurityBody));
        OnPropertyChanged(nameof(PrivacyOpenAiTitle));
        OnPropertyChanged(nameof(PrivacyOpenAiBody));
        OnPropertyChanged(nameof(PrivacyStorageTitle));
        OnPropertyChanged(nameof(PrivacyStorageBody));
        OnPropertyChanged(nameof(CharityTitle));
        OnPropertyChanged(nameof(CharityBody));
        OnPropertyChanged(nameof(UrlText));
        return Task.CompletedTask;
    }
}
