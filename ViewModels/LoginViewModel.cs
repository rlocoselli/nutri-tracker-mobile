using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Pages;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private const string TermsUrl = "https://nutritiontracker.fr/terms";
    private const string PrivacyUrl = "https://nutritiontracker.fr/privacy";
    private const string RgpdUrl = "https://nutritiontracker.fr/rgpd";

    private readonly AuthService _auth;
    private readonly EmailAuthService _emailAuth;
    private readonly BackendSyncService _sync;
    private readonly IServiceProvider _sp;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string displayName = "";
    [ObservableProperty] private string verificationCode = "";

    public string TermsLinkText => LocalizationService.T("login_terms_link");
    public string PrivacyLinkText => LocalizationService.T("login_privacy_link");
    public string RgpdLinkText => LocalizationService.T("login_rgpd_link");

    public LoginViewModel(AuthService auth, EmailAuthService emailAuth, BackendSyncService sync, IServiceProvider sp)
    {
        _auth = auth;
        _emailAuth = emailAuth;
        _sync = sync;
        _sp = sp;
    }

    [RelayCommand]
    private async Task LoginGoogle()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var result = await _auth.LoginAsync();

            // Persist auth + profile
            Preferences.Default.Set("auth_id_token", result.IdToken);
            Preferences.Default.Set("auth_access_token", result.AccessToken);
            Preferences.Default.Set("profile_name", result.Name);
            Preferences.Default.Set("profile_email", result.Email);
            Preferences.Default.Set("profile_picture", result.PictureUrl);
            Preferences.Default.Set("auth_method", "google");
            Preferences.Default.Remove("email_session_active");

            _ = await _sync.EnsureBackendIdentityAsync(result.IdToken);

            // Switch to the main shell
            Application.Current!.MainPage = _sp.GetRequiredService<AppShell>();
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("Connexion échouée", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoginEmail()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var (ok, message, name) = await _emailAuth.LoginAsync(Email, Password);
            if (!ok)
            {
                await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("login_title"), message, "OK");
                return;
            }

            Preferences.Default.Remove("auth_id_token");
            Preferences.Default.Remove("auth_access_token");
            Preferences.Default.Set("profile_name", name);
            Preferences.Default.Set("profile_email", (Email ?? "").Trim().ToLowerInvariant());
            Preferences.Default.Set("profile_picture", "");
            Preferences.Default.Set("auth_method", "email");
            Preferences.Default.Set("email_session_active", true);

            Application.Current!.MainPage = _sp.GetRequiredService<AppShell>();
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("login_title"), ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RegisterEmail()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var (ok, message) = await _emailAuth.RegisterAsync(Email, Password, DisplayName);
            await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("login_title"), message, "OK");
            if (!ok)
                return;

            await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("login_title"), LocalizationService.T("verification_needed"), "OK");
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("login_title"), ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task VerifyEmail()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var (ok, message) = await _emailAuth.VerifyEmailCodeAsync(Email, VerificationCode);
            await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("login_title"), message, "OK");
            if (ok)
            {
                VerificationCode = "";
            }
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("login_title"), ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenForgotPassword()
    {
        await OpenForgotPasswordPageAsync(Email, null);
    }

    public async Task HandlePendingResetDeepLinkAsync()
    {
        var email = Preferences.Default.Get("pending_reset_email", "");
        var code = Preferences.Default.Get("pending_reset_code", "");
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(code))
            return;

        Preferences.Default.Remove("pending_reset_email");
        Preferences.Default.Remove("pending_reset_code");
        await OpenForgotPasswordPageAsync(email, code);
    }

    private async Task OpenForgotPasswordPageAsync(string? email, string? code)
    {
        var page = _sp.GetRequiredService<ResetPasswordPage>();
        page.PreFill(email, code);
        await Application.Current!.MainPage!.Navigation.PushAsync(page);
    }

    [RelayCommand]
    private Task OpenTerms()
    {
        return Launcher.Default.OpenAsync(TermsUrl);
    }

    [RelayCommand]
    private Task OpenPrivacyPolicy()
    {
        return Launcher.Default.OpenAsync(PrivacyUrl);
    }

    [RelayCommand]
    private Task OpenRgpd()
    {
        return Launcher.Default.OpenAsync(RgpdUrl);
    }
}
