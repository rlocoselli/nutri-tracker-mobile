using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class ResetPasswordViewModel : ObservableObject
{
    private readonly EmailAuthService _emailAuth;

    [ObservableProperty] private string email = "";
    [ObservableProperty] private string resetCode = "";
    [ObservableProperty] private string newPassword = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusText = "";

    public string TitleText => LocalizationService.T("reset_password_page_title");
    public string SubtitleText => LocalizationService.T("reset_password_page_subtitle");
    public string EmailPlaceholder => LocalizationService.T("login_email_placeholder");
    public string SendCodeText => LocalizationService.T("forgot_password_button");
    public string ResetCodePlaceholder => LocalizationService.T("reset_code_placeholder");
    public string NewPasswordPlaceholder => LocalizationService.T("new_password_placeholder");
    public string ApplyResetText => LocalizationService.T("reset_password_button");

    public ResetPasswordViewModel(EmailAuthService emailAuth)
    {
        _emailAuth = emailAuth;
    }

    public void PreFill(string? email, string? code)
    {
        if (!string.IsNullOrWhiteSpace(email))
            Email = email.Trim();
        if (!string.IsNullOrWhiteSpace(code))
            ResetCode = code.Trim();
    }

    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(SubtitleText));
        OnPropertyChanged(nameof(EmailPlaceholder));
        OnPropertyChanged(nameof(SendCodeText));
        OnPropertyChanged(nameof(ResetCodePlaceholder));
        OnPropertyChanged(nameof(NewPasswordPlaceholder));
        OnPropertyChanged(nameof(ApplyResetText));
    }

    [RelayCommand]
    private async Task SendCode()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var (ok, message) = await _emailAuth.RequestPasswordResetAsync(Email);
            StatusText = message;
            if (!ok)
                await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("login_title"), message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyReset()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var (ok, message) = await _emailAuth.ResetPasswordAsync(Email, ResetCode, NewPassword);
            StatusText = message;
            await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("login_title"), message, "OK");
            if (ok)
            {
                ResetCode = "";
                NewPassword = "";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
