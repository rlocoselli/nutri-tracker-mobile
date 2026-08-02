using NutritionTracker.ViewModels;

namespace NutritionTracker.Pages;

public partial class AddMealPage : ContentPage, IQueryAttributable
{
    public AddMealPage(AddMealViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("capture", out var value) && string.Equals(value?.ToString(), "true", StringComparison.OrdinalIgnoreCase)
            && BindingContext is AddMealViewModel vm)
            MainThread.BeginInvokeOnMainThread(async () => await vm.CapturePhotoCommand.ExecuteAsync(null));
    }
}
