using NutritionTracker.Pages;

namespace NutritionTracker.Services;

public static class RewardPopupService
{
    public static async Task ShowAsync(int earnedCoins, int balanceCoins, int streakDays)
    {
        var app = Application.Current;
        var page = app?.MainPage;
        if (page == null)
            return;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var popup = new RewardPopupPage(earnedCoins, balanceCoins, streakDays);
            await page.Navigation.PushModalAsync(popup, animated: false);
        });
    }
}
