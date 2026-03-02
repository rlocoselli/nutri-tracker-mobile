using NutritionTracker.Pages;

namespace NutritionTracker.Services;

public class SessionService
{
    private readonly IServiceProvider _services;

    public SessionService(IServiceProvider services)
    {
        _services = services;
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

    public void ClearAuth()
    {
        Preferences.Default.Remove("auth_id_token");
        Preferences.Default.Remove("auth_access_token");
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
