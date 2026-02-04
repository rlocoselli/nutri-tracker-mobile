using NutritionTracker.ViewModels;

namespace NutritionTracker.Pages;

public partial class RecommendationsPage : ContentPage
{
    private readonly RecommendationsViewModel _vm;

    public RecommendationsPage(RecommendationsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }
}
