using Microsoft.Maui.Storage;
using NutritionTracker.Services;

namespace NutritionTracker.Pages;

public partial class MainPage : ContentPage
{
    private readonly LoginPage _loginPage;
    private readonly AppShell _appShell;
    private readonly LocalDb _db;
    private readonly SessionService _session;
    private bool _initialized;

    public MainPage(LoginPage loginPage, AppShell appShell, LocalDb db, SessionService session)
    {
        InitializeComponent();
        _loginPage = loginPage;
        _appShell = appShell;
        _db = db;
        _session = session;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;

        // ✅ init async (sans bloquer)
        await _db.InitAsync();

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
