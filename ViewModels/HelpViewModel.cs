using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class HelpViewModel : ObservableObject
{
    public string TitleText => LocalizationService.T("help_title");
    public string SubtitleText => LocalizationService.T("help_subtitle");
    public string OpenHelpText => LocalizationService.T("help_open");
    public string UrlText => "https://nutritiontracker.fr/";

    [RelayCommand]
    private async Task OpenHelp()
    {
        await Launcher.Default.OpenAsync("https://nutritiontracker.fr/");
    }

    public Task LoadAsync()
    {
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(SubtitleText));
        OnPropertyChanged(nameof(OpenHelpText));
        OnPropertyChanged(nameof(UrlText));
        return Task.CompletedTask;
    }
}
