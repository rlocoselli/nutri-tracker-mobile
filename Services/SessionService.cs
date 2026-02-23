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
