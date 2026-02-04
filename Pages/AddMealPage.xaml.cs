using NutritionTracker.ViewModels;

namespace NutritionTracker.Pages;

public partial class AddMealPage : ContentPage
{
    public AddMealPage(AddMealViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
