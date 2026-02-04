using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Pages;

namespace NutritionTracker.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly IServiceProvider _sp;

    [ObservableProperty] private string name = "";
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string pictureUrl = "";

    public ProfileViewModel(IServiceProvider sp)
    {
        _sp = sp;
    }

    public void Load()
    {
        Name = Preferences.Default.Get("profile_name", "");
        Email = Preferences.Default.Get("profile_email", "");
        PictureUrl = Preferences.Default.Get("profile_picture", "");
    }

    [RelayCommand]
    private async Task OpenRecommendations()
    {
        await Shell.Current.Navigation.PushAsync(_sp.GetRequiredService<RecommendationsPage>());
    }

    [RelayCommand]
    private async Task Logout()
    {
        Preferences.Default.Remove("auth_id_token");
        Preferences.Default.Remove("auth_access_token");
        Preferences.Default.Remove("profile_name");
        Preferences.Default.Remove("profile_email");
        Preferences.Default.Remove("profile_picture");

        // Return to login screen
        var login = _sp.GetRequiredService<LoginPage>();
        Application.Current!.MainPage = new NavigationPage(login);
        await Task.CompletedTask;
    }
}
