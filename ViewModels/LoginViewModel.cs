using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly EmailAuthService _emailAuth;
    private readonly BackendSyncService _sync;
    private readonly IServiceProvider _sp;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string displayName = "";
    [ObservableProperty] private string verificationCode = "";
    [ObservableProperty] private string resetCode = "";
    [ObservableProperty] private string newPassword = "";

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
    private async Task SendResetCode()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var (ok, message) = await _emailAuth.RequestPasswordResetAsync(Email);
            await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("login_title"), message, "OK");
            if (!ok)
                return;
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
    private async Task ResetPassword()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var (ok, message) = await _emailAuth.ResetPasswordAsync(Email, ResetCode, NewPassword);
            await Application.Current!.MainPage!.DisplayAlert(LocalizationService.T("login_title"), message, "OK");
            if (ok)
            {
                ResetCode = "";
                NewPassword = "";
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
}
