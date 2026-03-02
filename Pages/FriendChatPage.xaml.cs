using NutritionTracker.ViewModels;

namespace NutritionTracker.Pages;

public partial class FriendChatPage : ContentPage
{
    private readonly FriendChatViewModel _vm;

    public FriendChatPage(FriendChatViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        Dispatcher.StartTimer(TimeSpan.FromSeconds(6), () =>
        {
            _ = _vm.ReloadMessagesAsync();
            return Navigation?.NavigationStack?.Contains(this) == true;
        });
    }
}
