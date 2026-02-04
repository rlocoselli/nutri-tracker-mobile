using NutritionTracker.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace NutritionTracker;

public partial class App : Application
{
    public App(IServiceProvider services)
    {
        InitializeComponent();

        var mainPage = services.GetRequiredService<MainPage>();
        MainPage = new NavigationPage(mainPage);
    }
}
