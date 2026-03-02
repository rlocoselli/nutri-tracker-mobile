using NutritionTracker.ViewModels;

namespace NutritionTracker.Pages;

public partial class ResetPasswordPage : ContentPage
{
    private readonly ResetPasswordViewModel _vm;

    public ResetPasswordPage(ResetPasswordViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    public void PreFill(string? email, string? code)
    {
        _vm.PreFill(email, code);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.RefreshTexts();
    }
}
