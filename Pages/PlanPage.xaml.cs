using NutritionTracker.ViewModels;
namespace NutritionTracker.Pages;
public partial class PlanPage : ContentPage
{
    private readonly PlanViewModel _vm;
    public PlanPage(PlanViewModel vm) { InitializeComponent(); BindingContext = _vm = vm; }
    protected override async void OnAppearing() { base.OnAppearing(); await _vm.LoadAsync(); }
}
