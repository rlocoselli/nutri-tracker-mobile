using NutritionTracker.ViewModels;

namespace NutritionTracker.Pages;

public partial class FriendsPage : ContentPage
{
    private readonly FriendsViewModel _vm;

    public FriendsPage(FriendsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
