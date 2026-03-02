using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class ActivationViewModel : ObservableObject
{
    private readonly EmailAuthService _emailAuth;

    [ObservableProperty] private string email = "";
    [ObservableProperty] private string code = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusText = "";

    public string TitleText => LocalizationService.T("activation_title");
    public string SubtitleText => LocalizationService.T("activation_subtitle");
    public string EmailPlaceholder => LocalizationService.T("login_email_placeholder");
    public string CodePlaceholder => LocalizationService.T("verification_code_placeholder");
    public string VerifyButtonText => LocalizationService.T("verify_email_button");
    public string ResendButtonText => LocalizationService.T("resend_activation_button");

    public ActivationViewModel(EmailAuthService emailAuth)
    {
        _emailAuth = emailAuth;
    }

    public void PreFill(string? email)
    {
        if (!string.IsNullOrWhiteSpace(email))
            Email = email.Trim();
    }

    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(SubtitleText));
        OnPropertyChanged(nameof(EmailPlaceholder));
        OnPropertyChanged(nameof(CodePlaceholder));
        OnPropertyChanged(nameof(VerifyButtonText));
        OnPropertyChanged(nameof(ResendButtonText));
    }

    [RelayCommand]
    private async Task VerifyCode()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var (ok, message) = await _emailAuth.VerifyEmailCodeAsync(Email, Code);
            StatusText = message;
            await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("login_title"), message, "OK");
            if (ok && Application.Current?.MainPage?.Navigation != null)
                await Application.Current.MainPage.Navigation.PopAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResendCode()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var (ok, message) = await _emailAuth.ResendVerificationCodeAsync(Email);
            StatusText = message;
            if (!ok)
                await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("login_title"), message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
