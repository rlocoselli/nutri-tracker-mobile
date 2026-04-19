using Microsoft.Maui.Controls.Shapes;
using NutritionTracker.Services;

namespace NutritionTracker.Pages;

public sealed class TigrouPopupPage : ContentPage
{
    private readonly Border _card;
    private readonly Image _tigrouImage;

    public TigrouPopupPage(double qualityScore, string lang)
    {
        BackgroundColor = Color.FromArgb("#8A000000");

        var normalizedLang = (lang ?? "fr").Trim().ToLowerInvariant();
        var isEnglish = normalizedLang.StartsWith("en", StringComparison.Ordinal);

        var title = new Label
        {
            Text = isEnglish ? "Great meal!" : "Tres bon repas !",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = (Color)Application.Current!.Resources["Text"]
        };

        var score = new Label
        {
            Text = isEnglish
                ? $"Quality score: {Math.Round(qualityScore)}/100"
                : $"Score qualite : {Math.Round(qualityScore)}/100",
            FontSize = 15,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = (Color)Application.Current!.Resources["Muted"]
        };

        _tigrouImage = new Image
        {
            Source = "tigrou.png",
            WidthRequest = 150,
            HeightRequest = 150,
            HorizontalOptions = LayoutOptions.Center,
            Opacity = 0,
            Scale = 0.7,
            Rotation = -8,
        };

        var subtitle = new Label
        {
            Text = isEnglish ? "Tigrou is happy!" : "Tigrou est content !",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = (Color)Application.Current!.Resources["Primary"]
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
            Scale = 0.88,
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
                    score,
                    _tigrouImage,
                    subtitle,
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
        await _card.ScaleTo(1.06, 180, Easing.CubicOut);
        await _card.ScaleTo(1, 110, Easing.CubicInOut);

        await Task.WhenAll(
            _tigrouImage.FadeTo(1, 180, Easing.SinIn),
            _tigrouImage.ScaleTo(1, 220, Easing.SpringOut),
            _tigrouImage.RotateTo(0, 220, Easing.CubicOut));

        _ = AnimatePulseAsync();
    }

    private async Task AnimatePulseAsync()
    {
        while (Navigation.ModalStack.LastOrDefault() == this)
        {
            await _tigrouImage.ScaleTo(1.08, 320, Easing.CubicInOut);
            await _tigrouImage.ScaleTo(1.0, 320, Easing.CubicInOut);
        }
    }

    private async Task CloseAsync()
    {
        await _card.ScaleTo(0.94, 90, Easing.CubicIn);
        await _card.FadeTo(0, 90, Easing.CubicOut);
        await Navigation.PopModalAsync(animated: false);
    }
}