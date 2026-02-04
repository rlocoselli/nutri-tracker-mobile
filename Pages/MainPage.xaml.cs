using Microsoft.Maui.Storage;
using NutritionTracker.Services;

namespace NutritionTracker.Pages;

public partial class MainPage : ContentPage
{
    private readonly LoginPage _loginPage;
    private readonly AppShell _appShell;
    private readonly LocalDb _db;
    private bool _initialized;

    public MainPage(LoginPage loginPage, AppShell appShell, LocalDb db)
    {
        InitializeComponent();
        _loginPage = loginPage;
        _appShell = appShell;
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;

        // ✅ init async (sans bloquer)
        await _db.InitAsync();

        var idToken = Preferences.Default.Get("auth_id_token", "");
        if (string.IsNullOrWhiteSpace(idToken))
        {
            await Navigation.PushAsync(_loginPage);
        }
        else
        {
            Application.Current!.MainPage = _appShell;
        }
    }
}
