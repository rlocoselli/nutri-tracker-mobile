using Microsoft.Maui.Storage;
using NutritionTracker.Services;

namespace NutritionTracker.Pages;

public partial class MainPage : ContentPage
{
    private readonly LoginPage _loginPage;
    private readonly AppShell _appShell;
    private readonly SessionService _session;
    private bool _initialized;

    public MainPage(LoginPage loginPage, AppShell appShell, SessionService session)
    {
        InitializeComponent();
        _loginPage = loginPage;
        _appShell = appShell;
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
            Application.Current!.MainPage = _appShell;
        }
    }
}
