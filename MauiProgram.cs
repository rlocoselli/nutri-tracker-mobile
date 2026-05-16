using CommunityToolkit.Mvvm.Messaging;
using NutritionTracker.Pages;
using NutritionTracker.Services;
using NutritionTracker.ViewModels;

namespace NutritionTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-SemiBold.ttf", "OpenSansSemiBold");
            });

        // Services
        builder.Services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<EmailAuthService>();
        builder.Services.AddSingleton<SessionService>();
        builder.Services.AddSingleton<GoogleFitService>();
        builder.Services.AddSingleton<PointsService>();
        builder.Services.AddSingleton<HealthyTipService>();
        builder.Services.AddSingleton<GamificationCoachService>();
        builder.Services.AddSingleton<WeeklyMissionService>();
        builder.Services.AddSingleton<GoalNudgeService>();
        builder.Services.AddSingleton<SocialService>();
        builder.Services.AddSingleton<SocialNotificationService>();
        builder.Services.AddSingleton<BackendSyncService>();
        builder.Services.AddSingleton<SubscriptionService>();
        builder.Services.AddSingleton<IMealReminderService, MealReminderService>();
        builder.Services.AddSingleton<IVoiceInputService, AndroidVoiceInputService>();
        builder.Services.AddSingleton<IEntryFeedbackService, EntryFeedbackService>();
        builder.Services.AddSingleton(sp => new ApiService("https://www.nutritiontracker.fr/api", sp.GetRequiredService<SessionService>()));

        // ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<ActivationViewModel>();
        builder.Services.AddTransient<ResetPasswordViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<DiaryViewModel>();
        builder.Services.AddTransient<AddMealViewModel>();
        builder.Services.AddTransient<GoalsViewModel>();
        builder.Services.AddTransient<RecommendationsViewModel>();
        builder.Services.AddTransient<StoriesViewModel>();
        builder.Services.AddTransient<FriendsViewModel>();
        builder.Services.AddTransient<FriendChatViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<HelpViewModel>();
        builder.Services.AddTransient<StatisticsViewModel>();
        builder.Services.AddTransient<ScoreHistoryViewModel>();

        // Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<ActivationPage>();
        builder.Services.AddTransient<ResetPasswordPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<DiaryPage>();
        builder.Services.AddTransient<AddMealPage>();
        builder.Services.AddTransient<GoalsPage>();
        builder.Services.AddTransient<RecommendationsPage>();
        builder.Services.AddTransient<StoriesPage>();
        builder.Services.AddTransient<FriendsPage>();
        builder.Services.AddTransient<FriendChatPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<HelpPage>();
        builder.Services.AddTransient<StatisticsPage>();
        builder.Services.AddTransient<ScoreHistoryPage>();

        // Shell + Main (loading)
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
