using NutritionTracker.ViewModels;
using System.Collections.Specialized;

namespace NutritionTracker.Pages;

public partial class FriendChatPage : ContentPage
{
    private readonly FriendChatViewModel _vm;
    private bool _timerStarted;

    public FriendChatPage(FriendChatViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Messages.CollectionChanged += OnMessagesChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_timerStarted)
            return;

        _timerStarted = true;

        Dispatcher.StartTimer(TimeSpan.FromSeconds(6), () =>
        {
            _ = _vm.ReloadMessagesAsync();
            return Navigation?.NavigationStack?.Contains(this) == true;
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timerStarted = false;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_vm.Messages.Count == 0)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var last = _vm.Messages[^1];
            MessagesList.ScrollTo(last, position: ScrollToPosition.End, animate: true);
        });
    }
}
