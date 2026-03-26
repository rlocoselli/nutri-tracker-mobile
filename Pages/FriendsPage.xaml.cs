using NutritionTracker.ViewModels;

namespace NutritionTracker.Pages;

public partial class FriendsPage : ContentPage
{
    private readonly FriendsViewModel _vm;
    private string _lastAnnouncement = "";
    private bool _isBellAnimating;

    public FriendsPage(FriendsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FriendsViewModel.LeagueBadgeAnnouncement))
                _ = ShowLeagueToastIfNeededAsync();

            if (e.PropertyName == nameof(FriendsViewModel.HasUnreadChats) || e.PropertyName == nameof(FriendsViewModel.UnreadChatsBadgeText))
                _ = MainThread.InvokeOnMainThreadAsync(SyncNotificationBellAsync);
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
        await ShowLeagueToastIfNeededAsync();
        await SyncNotificationBellAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isBellAnimating = false;
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

    private async Task SyncNotificationBellAsync()
    {
        if (!_vm.HasUnreadChats)
        {
            _isBellAnimating = false;
            NotificationBellHost.Scale = 1;
            NotificationBellIcon.Rotation = 0;
            return;
        }

        if (_isBellAnimating)
            return;

        _isBellAnimating = true;
        while (_isBellAnimating && _vm.HasUnreadChats && Navigation?.NavigationStack?.Contains(this) == true)
        {
            await Task.WhenAll(
                NotificationBellHost.ScaleTo(1.06, 170, Easing.CubicOut),
                NotificationBellIcon.RotateTo(-8, 120, Easing.SinOut));
            await Task.WhenAll(
                NotificationBellHost.ScaleTo(1.00, 170, Easing.CubicIn),
                NotificationBellIcon.RotateTo(8, 120, Easing.SinIn));
            await NotificationBellIcon.RotateTo(0, 90, Easing.CubicOut);
            await Task.Delay(1000);
        }

        NotificationBellHost.Scale = 1;
        NotificationBellIcon.Rotation = 0;
        _isBellAnimating = false;
    }
}
