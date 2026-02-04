using NutritionTracker.ViewModels;

namespace NutritionTracker.Pages;

public partial class DiaryPage : ContentPage
{
    private readonly DiaryViewModel _vm;

    public DiaryPage(DiaryViewModel vm)
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
