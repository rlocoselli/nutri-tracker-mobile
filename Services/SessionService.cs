using NutritionTracker.Pages;

namespace NutritionTracker.Services;

public class SessionService
{
    private readonly IServiceProvider _services;
    private readonly AuthService _auth;

    public SessionService(IServiceProvider services, AuthService auth)
    {
        _services = services;
        _auth = auth;
    }

    public string GetIdToken() => Preferences.Default.Get("auth_id_token", "");

    public bool HasValidIdToken()
    {
        var authMethod = Preferences.Default.Get("auth_method", "google");
        if (string.Equals(authMethod, "email", StringComparison.OrdinalIgnoreCase))
        {
            var active = Preferences.Default.Get("email_session_active", false);
            var email = Preferences.Default.Get("profile_email", "");
            return active && !string.IsNullOrWhiteSpace(email);
        }

        var token = GetIdToken();
        return !string.IsNullOrWhiteSpace(token) && AuthService.IsIdTokenStillValid(token);
    }

    public async Task<bool> EnsureValidSessionAsync()
    {
        var authMethod = Preferences.Default.Get("auth_method", "google");
        if (string.Equals(authMethod, "email", StringComparison.OrdinalIgnoreCase))
        {
            var active = Preferences.Default.Get("email_session_active", false);
            var email = Preferences.Default.Get("profile_email", "");
            return active && !string.IsNullOrWhiteSpace(email);
        }

        var token = GetIdToken();
        if (!string.IsNullOrWhiteSpace(token) && AuthService.IsIdTokenStillValid(token))
            return true;

        var refreshToken = Preferences.Default.Get("auth_refresh_token", "");
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        var refreshed = await _auth.TryRefreshAsync(refreshToken);
        if (refreshed == null || string.IsNullOrWhiteSpace(refreshed.IdToken))
            return false;

        Preferences.Default.Set("auth_id_token", refreshed.IdToken);
        Preferences.Default.Set("auth_access_token", refreshed.AccessToken ?? "");
        Preferences.Default.Set("auth_refresh_token", string.IsNullOrWhiteSpace(refreshed.RefreshToken) ? refreshToken : refreshed.RefreshToken);

        if (!string.IsNullOrWhiteSpace(refreshed.Name))
            Preferences.Default.Set("profile_name", refreshed.Name);
        if (!string.IsNullOrWhiteSpace(refreshed.Email))
            Preferences.Default.Set("profile_email", refreshed.Email);
        if (!string.IsNullOrWhiteSpace(refreshed.PictureUrl))
            Preferences.Default.Set("profile_picture", refreshed.PictureUrl);

        return true;
    }

    public void ClearAuth()
    {
        Preferences.Default.Remove("auth_id_token");
        Preferences.Default.Remove("auth_access_token");
        Preferences.Default.Remove("auth_refresh_token");
        Preferences.Default.Remove("profile_name");
        Preferences.Default.Remove("profile_email");
        Preferences.Default.Remove("profile_picture");
        Preferences.Default.Remove("backend_user_id");
        Preferences.Default.Remove("backend_identity_subject");
        Preferences.Default.Remove("auth_method");
        Preferences.Default.Remove("email_session_active");
    }

    public Task RedirectToLoginAsync(bool clearAuth = true)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (clearAuth)
                ClearAuth();

            var loginPage = _services.GetRequiredService<LoginPage>();
            Application.Current!.MainPage = new NavigationPage(loginPage);
        });
    }
}
