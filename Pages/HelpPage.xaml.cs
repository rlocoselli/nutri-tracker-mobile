using NutritionTracker.ViewModels;

namespace NutritionTracker.Pages;

public partial class HelpPage : ContentPage
{
    private readonly HelpViewModel _vm;
    private bool _opened;

    public HelpPage(HelpViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
        if (_opened)
            return;

        _opened = true;
        await _vm.OpenHelpCommand.ExecuteAsync(null);
    }
}
