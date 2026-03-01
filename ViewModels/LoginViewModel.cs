using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly BackendSyncService _sync;
    private readonly IServiceProvider _sp;

    [ObservableProperty] private bool isBusy;

    public LoginViewModel(AuthService auth, BackendSyncService sync, IServiceProvider sp)
    {
        _auth = auth;
        _sync = sync;
        _sp = sp;
    }

    [RelayCommand]
    private async Task Login()
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
}
