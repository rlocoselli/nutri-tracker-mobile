using NutritionTracker.ViewModels;

namespace NutritionTracker.Pages;

public partial class ScoreHistoryPage : ContentPage
{
    private readonly ScoreHistoryViewModel _vm;

    public ScoreHistoryPage(ScoreHistoryViewModel vm)
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
