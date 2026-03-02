using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Microsoft.Maui;
using Microsoft.Maui.Storage;

namespace NutritionTracker;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    Icon = "@mipmap/appicon_generated",
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "nutritiontracker",
    DataHost = "reset-password")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        HandleResetDeepLink(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleResetDeepLink(intent);
    }

    private static void HandleResetDeepLink(Intent? intent)
    {
        var data = intent?.Data;
        if (data == null)
            return;

        if (!string.Equals(data.Scheme, "nutritiontracker", StringComparison.OrdinalIgnoreCase))
            return;

        if (!string.Equals(data.Host, "reset-password", StringComparison.OrdinalIgnoreCase))
            return;

        var email = data.GetQueryParameter("email") ?? "";
        var code = data.GetQueryParameter("code") ?? "";
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(code))
            return;

        Preferences.Default.Set("pending_reset_email", email);
        Preferences.Default.Set("pending_reset_code", code);
    }
}


