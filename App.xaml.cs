using NutritionTracker.Pages;
using NutritionTracker.Services;
using Microsoft.Extensions.DependencyInjection;

namespace NutritionTracker;

public partial class App : Application
{
    public App(IServiceProvider services)
    {
        LocalizationService.EnsureAppLanguageConfigured();
        InitializeComponent();

        var mainPage = services.GetRequiredService<MainPage>();
        MainPage = new NavigationPage(mainPage);
    }

    protected override void OnResume()
    {
        base.OnResume();
        LocalizationService.EnsureAppLanguageConfigured();
    }
}
