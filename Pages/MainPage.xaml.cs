using Microsoft.Maui.Storage;
using NutritionTracker.Services;
using Microsoft.Extensions.DependencyInjection;

namespace NutritionTracker.Pages;

public partial class MainPage : ContentPage
{
    private readonly LoginPage _loginPage;
    private readonly IServiceProvider _services;
    private readonly SessionService _session;
    private bool _initialized;

    public MainPage(LoginPage loginPage, IServiceProvider services, SessionService session)
    {
        InitializeComponent();
        _loginPage = loginPage;
        _services = services;
        _session = session;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;

        if (!_session.HasValidIdToken())
        {
            _session.ClearAuth();
            await Navigation.PushAsync(_loginPage);
        }
        else
        {
            var appShell = _services.GetRequiredService<AppShell>();
            Application.Current!.MainPage = appShell;
        }
    }
}
