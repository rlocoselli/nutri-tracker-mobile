namespace NutritionTracker.Services;

public static class FeatureFlags
{
    public const bool EnableGoogleFit = false;
    public const bool EnableSubscriptions = false;

    // A/B test gate for the dashboard public stories strip.
    public static bool EnableDashboardPublicStories
    {
        get
        {
            // Optional manual override via Preferences for quick QA toggling.
            if (Preferences.Default.ContainsKey("ff_dashboard_public_stories"))
                return Preferences.Default.Get("ff_dashboard_public_stories", true);

            return true;
        }
    }
}
