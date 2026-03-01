namespace NutritionTracker;

using NutritionTracker.Services;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        DashboardTab.Title = LocalizationService.T("tab_dashboard");
        DiaryTab.Title = LocalizationService.T("tab_diary");
        AddTab.Title = LocalizationService.T("tab_add");
        GoalsTab.Title = LocalizationService.T("tab_goals");
        StoriesTab.Title = LocalizationService.T("tab_stories");
        ProfileTab.Title = LocalizationService.T("tab_profile");
    }
}
