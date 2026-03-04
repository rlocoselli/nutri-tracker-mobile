using NutritionTracker.Services;
using Microsoft.Maui.Controls.Shapes;

namespace NutritionTracker.Pages;

public sealed class RewardPopupPage : ContentPage
{
    private readonly Border _card;

    public RewardPopupPage(int earnedCoins, int balanceCoins, int streakDays)
    {
        BackgroundColor = Color.FromArgb("#88000000");

        var title = new Label
        {
            Text = $"🏆 {LocalizationService.T("saved_title_common")}",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = (Color)Application.Current!.Resources["Text"]
        };

        var streakLabel = new Label
        {
            Text = $"🔥 {LocalizationService.T("reward_streak_title")}: {Math.Max(0, streakDays)}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = (Color)Application.Current!.Resources["Text"]
        };

        var earnedLabel = new Label
        {
            Text = $"🪙 +{Math.Max(0, earnedCoins)}",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = (Color)Application.Current!.Resources["Primary"]
        };

        var balanceLabel = new Label
        {
            Text = $"{LocalizationService.T("coins_balance")}: {Math.Max(0, balanceCoins)}",
            FontSize = 15,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = (Color)Application.Current!.Resources["Muted"]
        };

        var closeButton = new Button
        {
            Text = LocalizationService.T("next"),
            Style = (Style)Application.Current!.Resources["SecondaryButton"],
            HorizontalOptions = LayoutOptions.Fill
        };
        closeButton.Clicked += async (_, _) => await CloseAsync();

        _card = new Border
        {
            Style = (Style)Application.Current!.Resources["Card"],
            Padding = 20,
            Opacity = 0,
            Scale = 0.85,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = 24 },
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                WidthRequest = 300,
                Children =
                {
                    title,
                    streakLabel,
                    earnedLabel,
                    balanceLabel,
                    closeButton,
                }
            }
        };

        Content = new Grid
        {
            Children = { _card }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _card.FadeTo(1, 140, Easing.CubicIn);
        await _card.ScaleTo(1.08, 180, Easing.CubicOut);
        await _card.ScaleTo(1, 110, Easing.CubicInOut);
    }

    private async Task CloseAsync()
    {
        await _card.ScaleTo(0.92, 90, Easing.CubicIn);
        await _card.FadeTo(0, 90, Easing.CubicOut);
        await Navigation.PopModalAsync(animated: false);
    }
}
