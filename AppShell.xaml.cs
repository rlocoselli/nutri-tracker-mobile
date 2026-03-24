namespace NutritionTracker;

using NutritionTracker.Services;
using NutritionTracker.Pages;

public partial class AppShell : Shell
{
    private readonly SocialNotificationService _socialNotifications;
    private readonly GoalNudgeService _goalNudges;

    public AppShell(SocialNotificationService socialNotifications, GoalNudgeService goalNudges)
    {
        _socialNotifications = socialNotifications;
        _goalNudges = goalNudges;
        InitializeComponent();
        Routing.RegisterRoute(nameof(ResetPasswordPage), typeof(ResetPasswordPage));
        Routing.RegisterRoute(nameof(FriendChatPage), typeof(FriendChatPage));
        Routing.RegisterRoute(nameof(ScoreHistoryPage), typeof(ScoreHistoryPage));
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
        StartGoalNudges();
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

    private void StartGoalNudges()
    {
        _ = _goalNudges.PollAndNotifyAsync();
        Dispatcher.StartTimer(TimeSpan.FromMinutes(20), () =>
        {
            _ = _goalNudges.PollAndNotifyAsync();
            return true;
        });
    }
}
