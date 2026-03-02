namespace NutritionTracker;

using NutritionTracker.Services;
using NutritionTracker.Pages;

public partial class AppShell : Shell
{
    private readonly SocialNotificationService _socialNotifications;

    public AppShell(SocialNotificationService socialNotifications)
    {
        _socialNotifications = socialNotifications;
        InitializeComponent();
        Routing.RegisterRoute(nameof(ResetPasswordPage), typeof(ResetPasswordPage));
        DashboardTab.Title = LocalizationService.T("tab_dashboard");
        DiaryTab.Title = LocalizationService.T("tab_diary");
        AddTab.Title = LocalizationService.T("tab_add");
        GoalsTab.Title = LocalizationService.T("tab_goals");
        StoriesTab.Title = LocalizationService.T("tab_stories");
        FriendsTab.Title = LocalizationService.T("tab_friends");
        StatisticsTab.Title = LocalizationService.T("tab_statistics");
        HelpTab.Title = LocalizationService.T("tab_help");
        ProfileTab.Title = LocalizationService.T("tab_profile");

        StartSocialNotifications();
    }

    private void StartSocialNotifications()
    {
        _ = _socialNotifications.PollAndNotifyAsync();
        Dispatcher.StartTimer(TimeSpan.FromSeconds(45), () =>
        {
            _ = _socialNotifications.PollAndNotifyAsync();
            return true;
        });
    }
}
