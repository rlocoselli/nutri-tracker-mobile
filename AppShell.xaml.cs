namespace NutritionTracker;

using NutritionTracker.Services;
using NutritionTracker.Pages;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Graphics;

public partial class AppShell : Shell
{
    private readonly SocialNotificationService _socialNotifications;
    private readonly GoalNudgeService _goalNudges;
    private bool _backgroundPollersStarted;

    public AppShell(SocialNotificationService socialNotifications, GoalNudgeService goalNudges)
    {
        _socialNotifications = socialNotifications;
        _goalNudges = goalNudges;
        InitializeComponent();
        Shell.SetBackgroundColor(this, Color.FromArgb("#FFFFFF"));
        Shell.SetForegroundColor(this, Color.FromArgb("#2F855A"));
        Shell.SetTitleColor(this, Color.FromArgb("#2F855A"));
        Shell.SetUnselectedColor(this, Color.FromArgb("#7A8A91"));
        Routing.RegisterRoute(nameof(ResetPasswordPage), typeof(ResetPasswordPage));
        Routing.RegisterRoute(nameof(FriendChatPage), typeof(FriendChatPage));
        Routing.RegisterRoute(nameof(ScoreHistoryPage), typeof(ScoreHistoryPage));
        Routing.RegisterRoute(nameof(RecommendationsPage), typeof(RecommendationsPage));
        RefreshLocalizedText();
    }

    public void RefreshLocalizedText()
    {
        DashboardTab.Title = LocalizationService.T("tab_dashboard");
        DiaryTab.Title = LocalizationService.T("tab_diary");
        AddTab.Title = LocalizationService.T("tab_add");
        GoalsTab.Title = LocalizationService.T("tab_goals");
        StoriesTab.Title = LocalizationService.T("tab_stories");
        FriendsTab.Title = LocalizationService.T("tab_friends");
        StatisticsTab.Title = LocalizationService.T("tab_statistics");
        HelpTab.Title = LocalizationService.T("tab_help");
        ProfileTab.Title = LocalizationService.T("tab_profile");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StartBackgroundPollersIfNeeded();
    }

    private void StartBackgroundPollersIfNeeded()
    {
        if (_backgroundPollersStarted)
            return;

        // Avoid starting periodic network tasks while user is not authenticated.
        var idToken = Preferences.Default.Get("auth_id_token", "");
        if (string.IsNullOrWhiteSpace(idToken))
            return;

        _backgroundPollersStarted = true;
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
