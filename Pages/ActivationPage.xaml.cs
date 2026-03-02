using NutritionTracker.ViewModels;

namespace NutritionTracker.Pages;

public partial class ActivationPage : ContentPage
{
    private readonly ActivationViewModel _vm;

    public ActivationPage(ActivationViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    public void PreFill(string? email)
    {
        _vm.PreFill(email);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.RefreshTexts();
    }
}
