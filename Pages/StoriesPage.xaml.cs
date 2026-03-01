using NutritionTracker.ViewModels;

namespace NutritionTracker.Pages;

public partial class StoriesPage : ContentPage
{
    private readonly StoriesViewModel _vm;

    public StoriesPage(StoriesViewModel vm)
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
