using NutritionTracker.Pages;

namespace NutritionTracker.Services;

public static class TigrouPopupService
{
    public static async Task ShowAsync(double qualityScore, string lang)
    {
        var app = Application.Current;
        var page = app?.MainPage;
        if (page == null)
            return;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var popup = new TigrouPopupPage(qualityScore, lang);
            await page.Navigation.PushModalAsync(popup, animated: false);
        });
    }
}