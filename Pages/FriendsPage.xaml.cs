using NutritionTracker.ViewModels;

namespace NutritionTracker.Pages;

public partial class FriendsPage : ContentPage
{
    private readonly FriendsViewModel _vm;
    private string _lastAnnouncement = "";

    public FriendsPage(FriendsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FriendsViewModel.LeagueBadgeAnnouncement))
                _ = ShowLeagueToastIfNeededAsync();
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
        await ShowLeagueToastIfNeededAsync();
    }

    private async Task ShowLeagueToastIfNeededAsync()
    {
        var text = (_vm.LeagueBadgeAnnouncement ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text) || string.Equals(text, _lastAnnouncement, StringComparison.Ordinal))
            return;

        _lastAnnouncement = text;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            LeagueToast.Opacity = 0;
            LeagueToast.Scale = 0.9;
            LeagueToast.IsVisible = true;

            await Task.WhenAll(
                LeagueToast.FadeTo(1, 220, Easing.CubicOut),
                LeagueToast.ScaleTo(1, 220, Easing.CubicOut));

            await Task.Delay(1800);

            await Task.WhenAll(
                LeagueToast.FadeTo(0, 220, Easing.CubicIn),
                LeagueToast.ScaleTo(0.96, 220, Easing.CubicIn));

            LeagueToast.IsVisible = false;
            _vm.LeagueBadgeAnnouncement = "";
        });
    }
}
