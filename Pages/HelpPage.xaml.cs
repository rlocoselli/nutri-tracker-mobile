using NutritionTracker.ViewModels;

namespace NutritionTracker.Pages;

public partial class HelpPage : ContentPage
{
    private readonly HelpViewModel _vm;

    public HelpPage(HelpViewModel vm)
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
