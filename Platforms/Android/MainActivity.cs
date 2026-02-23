using Android.App;
using Android.Content.PM;
using Microsoft.Maui;

namespace NutritionTracker;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    Icon = "@mipmap/appicon",
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // Rien à faire ici pour Google login : WebAuthenticator gère le flux via une activité de callback.
}


