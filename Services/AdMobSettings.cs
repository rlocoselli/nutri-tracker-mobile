namespace NutritionTracker.Services;

public static class AdMobSettings
{
    public const string BannerAdUnitId = "ca-app-pub-6158185990205930/6132309402";

    // Replace this with a dedicated AdMob interstitial unit ID. AdMob unit IDs
    // are format-specific, so the banner unit above cannot serve interstitials.
#if DEBUG
    public const string InterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";
#else
    public const string InterstitialAdUnitId = "";
#endif
}
