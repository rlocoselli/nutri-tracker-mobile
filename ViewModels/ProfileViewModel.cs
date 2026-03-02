using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using NutritionTracker.Pages;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly IServiceProvider _sp;
    private readonly Services.GoogleFitService _googleFit;
    private readonly IMealReminderService _mealReminderService;
    private readonly PointsService _points;
    private readonly SocialService _social;
    private readonly BackendSyncService _sync;
    private readonly EmailAuthService _emailAuth;
    private readonly SubscriptionService _subscription;

    public List<string> LanguageOptions { get; } = new() { "Français", "English", "Português (BR)", "Español (LatAm)" };

    [ObservableProperty] private string name = "";
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string pictureUrl = "";
    [ObservableProperty] private string selectedLanguage = "Français";
    [ObservableProperty] private string currentLanguageText = "Langue actuelle : Français";
    [ObservableProperty] private string todayStepsText = "0";
    [ObservableProperty] private string todayBurnText = "0 kcal";
    [ObservableProperty] private string fitSyncStatusText = "";
    [ObservableProperty] private bool remindersEnabled;
    [ObservableProperty] private TimeSpan breakfastReminder = new(8, 0, 0);
    [ObservableProperty] private TimeSpan lunchReminder = new(13, 0, 0);
    [ObservableProperty] private TimeSpan dinnerReminder = new(20, 0, 0);
    [ObservableProperty] private string reminderStatusText = "";
    [ObservableProperty] private string inviteEmail = "";
    [ObservableProperty] private string socialStatusText = "";
    [ObservableProperty] private bool isEmailAccount;
    [ObservableProperty] private string currentPassword = "";
    [ObservableProperty] private string newPassword = "";
    [ObservableProperty] private string deletePassword = "";
    [ObservableProperty] private string accountActionStatusText = "";
    [ObservableProperty] private string subscriptionStatusText = "";
    [ObservableProperty] private bool canStartFreeTrial;
    [ObservableProperty] private bool canSubscribe;

    public ObservableCollection<FriendInviteItem> Friends { get; } = new();
    public ObservableCollection<FriendRankItem> FriendRanks { get; } = new();

    public string LanguageLabel => LocalizationService.T("language");
    public string ProfileTitle => LocalizationService.T("profile_title");
    public string StepsTodayLabel => LocalizationService.T("steps_today");
    public string BurnedCaloriesLabel => LocalizationService.T("burned_calories");
    public string AdviceTitle => LocalizationService.T("advice_title");
    public string AdviceHint => LocalizationService.T("advice_hint");
    public string GenerateRecoText => LocalizationService.T("generate_reco");
    public string LogoutText => LocalizationService.T("logout");
    public string ReminderTitle => LocalizationService.T("reminder_title");
    public string ReminderEnabledText => LocalizationService.T("reminder_enabled");
    public string ReminderBreakfastText => LocalizationService.T("reminder_breakfast");
    public string ReminderLunchText => LocalizationService.T("reminder_lunch");
    public string ReminderDinnerText => LocalizationService.T("reminder_dinner");
    public string SaveReminderText => LocalizationService.T("save_reminders");
    public string FriendsTitle => LocalizationService.T("friends_title");
    public string InvitePlaceholder => LocalizationService.T("invite_email_placeholder");
    public string InviteButtonText => LocalizationService.T("invite_friend");
    public string AddBuddyButtonText => LocalizationService.T("add_buddy");
    public string AcceptText => LocalizationService.T("accept");
    public string RemoveText => LocalizationService.T("remove");
    public string FriendsLeagueTitle => LocalizationService.T("friends_league_title");
    public string PrivacyMenuText => LocalizationService.T("privacy_menu_item");
    public string AccountSecurityTitle => LocalizationService.T("account_security_title");
    public string CurrentPasswordLabel => LocalizationService.T("current_password_label");
    public string NewPasswordLabel => LocalizationService.T("new_password_label");
    public string ChangePasswordButton => LocalizationService.T("change_password_button");
    public string DeleteAccountTitle => LocalizationService.T("delete_account_title");
    public string DeleteAccountHint => LocalizationService.T("delete_account_hint");
    public string DeleteAccountButton => LocalizationService.T("delete_account_button");
    public string SubscriptionTitle => LocalizationService.T("subscription_title");
    public string SubscriptionPlanText => LocalizationService.T("subscription_plan");
    public string StartFreeTrialText => LocalizationService.T("subscription_start_trial");
    public string SubscribeGoogleText => LocalizationService.T("subscription_subscribe_google");
    public string ConfirmGoogleText => LocalizationService.T("subscription_confirm_google");
    public bool ShowSubscriptionUi => FeatureFlags.EnableSubscriptions;
    public bool ShowGoogleFitUi => FeatureFlags.EnableGoogleFit;

    public ProfileViewModel(IServiceProvider sp, Services.GoogleFitService googleFit, IMealReminderService mealReminderService, PointsService points, SocialService social, BackendSyncService sync, EmailAuthService emailAuth, SubscriptionService subscription)
    {
        _sp = sp;
        _googleFit = googleFit;
        _mealReminderService = mealReminderService;
        _points = points;
        _social = social;
        _sync = sync;
        _emailAuth = emailAuth;
        _subscription = subscription;
    }

    public async Task LoadAsync()
    {
        Name = Preferences.Default.Get("profile_name", "");
        Email = Preferences.Default.Get("profile_email", "");
        PictureUrl = Preferences.Default.Get("profile_picture", "");
        IsEmailAccount = string.Equals(Preferences.Default.Get("auth_method", "google"), "email", StringComparison.OrdinalIgnoreCase);

        var appLang = Preferences.Default.Get("app_lang", "fr");
        SelectedLanguage = appLang switch
        {
            "en" => "English",
            "pt" => "Português (BR)",
            "es" => "Español (LatAm)",
            _ => "Français",
        };
        CurrentLanguageText = appLang switch
        {
            "en" => LocalizationService.T("current_lang_en"),
            "pt" => LocalizationService.T("current_lang_pt"),
            "es" => LocalizationService.T("current_lang_es"),
            _ => LocalizationService.T("current_lang_fr"),
        };

        OnPropertyChanged(nameof(LanguageLabel));
        OnPropertyChanged(nameof(ProfileTitle));
        OnPropertyChanged(nameof(StepsTodayLabel));
        OnPropertyChanged(nameof(BurnedCaloriesLabel));
        OnPropertyChanged(nameof(AdviceTitle));
        OnPropertyChanged(nameof(AdviceHint));
        OnPropertyChanged(nameof(GenerateRecoText));
        OnPropertyChanged(nameof(LogoutText));
        OnPropertyChanged(nameof(ReminderTitle));
        OnPropertyChanged(nameof(ReminderEnabledText));
        OnPropertyChanged(nameof(ReminderBreakfastText));
        OnPropertyChanged(nameof(ReminderLunchText));
        OnPropertyChanged(nameof(ReminderDinnerText));
        OnPropertyChanged(nameof(SaveReminderText));
        OnPropertyChanged(nameof(FriendsTitle));
        OnPropertyChanged(nameof(InvitePlaceholder));
        OnPropertyChanged(nameof(InviteButtonText));
        OnPropertyChanged(nameof(AddBuddyButtonText));
        OnPropertyChanged(nameof(AcceptText));
        OnPropertyChanged(nameof(RemoveText));
        OnPropertyChanged(nameof(FriendsLeagueTitle));
        OnPropertyChanged(nameof(PrivacyMenuText));
        OnPropertyChanged(nameof(AccountSecurityTitle));
        OnPropertyChanged(nameof(CurrentPasswordLabel));
        OnPropertyChanged(nameof(NewPasswordLabel));
        OnPropertyChanged(nameof(ChangePasswordButton));
        OnPropertyChanged(nameof(DeleteAccountTitle));
        OnPropertyChanged(nameof(DeleteAccountHint));
        OnPropertyChanged(nameof(DeleteAccountButton));
        OnPropertyChanged(nameof(SubscriptionTitle));
        OnPropertyChanged(nameof(SubscriptionPlanText));
        OnPropertyChanged(nameof(StartFreeTrialText));
        OnPropertyChanged(nameof(SubscribeGoogleText));
        OnPropertyChanged(nameof(ConfirmGoogleText));
        OnPropertyChanged(nameof(ShowSubscriptionUi));
        OnPropertyChanged(nameof(ShowGoogleFitUi));

        RemindersEnabled = Preferences.Default.Get("meal_reminders_enabled", false);
        BreakfastReminder = ParseTimeOrDefault(Preferences.Default.Get("meal_reminder_breakfast", "08:00"), new TimeSpan(8, 0, 0));
        LunchReminder = ParseTimeOrDefault(Preferences.Default.Get("meal_reminder_lunch", "13:00"), new TimeSpan(13, 0, 0));
        DinnerReminder = ParseTimeOrDefault(Preferences.Default.Get("meal_reminder_dinner", "20:00"), new TimeSpan(20, 0, 0));
        ReminderStatusText = "";
        SocialStatusText = "";
        AccountActionStatusText = "";
        RefreshSubscriptionUi();
        LoadFriends();

        var accessToken = Preferences.Default.Get("auth_access_token", "");
        var todaySteps = 0;
        var todayBurnedCalories = 0d;
        if (!GoogleFitService.Enabled)
        {
            FitSyncStatusText = "";
        }
        else if (!string.IsNullOrWhiteSpace(accessToken))
        {
            try
            {
                var fit = await _googleFit.GetTodaySummaryAsync(accessToken);
                todaySteps = fit.steps;
                todayBurnedCalories = fit.burnedCalories;
                FitSyncStatusText = LocalizationService.T("sync_ok");
            }
            catch (Exception ex)
            {
                FitSyncStatusText = $"{LocalizationService.T("sync_error")}: {ex.Message}";
            }
        }
        else
        {
            FitSyncStatusText = LocalizationService.T("sync_no_token");
        }

        TodayStepsText = todaySteps.ToString();
        TodayBurnText = $"{Math.Round(todayBurnedCalories)} kcal";
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        var lang = value switch
        {
            "English" => "en",
            "Português (BR)" => "pt",
            "Español (LatAm)" => "es",
            _ => "fr",
        };
        Preferences.Default.Set("app_lang", lang);
        CurrentLanguageText = lang switch
        {
            "en" => LocalizationService.T("current_lang_en"),
            "pt" => LocalizationService.T("current_lang_pt"),
            "es" => LocalizationService.T("current_lang_es"),
            _ => LocalizationService.T("current_lang_fr"),
        };
    }

    [RelayCommand]
    private async Task OpenRecommendations()
    {
        await Shell.Current.Navigation.PushAsync(_sp.GetRequiredService<RecommendationsPage>());
    }

    [RelayCommand]
    private async Task StartFreeTrial()
    {
        var started = await _subscription.StartFreeTrialAsync();
        var key = started ? "subscription_started_ok" : "subscription_trial_unavailable";
        SubscriptionStatusText = LocalizationService.T(key);
        RefreshSubscriptionUi();
    }

    [RelayCommand]
    private async Task SubscribeWithGoogle()
    {
        await _subscription.OpenGooglePlaySubscriptionAsync();
        SubscriptionStatusText = LocalizationService.T("subscription_google_opened");
    }

    [RelayCommand]
    private async Task ConfirmGoogleSubscription()
    {
        await _subscription.ConfirmGoogleSubscriptionAsync();
        SubscriptionStatusText = LocalizationService.T("subscription_confirmed");
        RefreshSubscriptionUi();
    }

    [RelayCommand]
    private async Task OpenPrivacyMenu()
    {
        await Shell.Current.Navigation.PushAsync(_sp.GetRequiredService<HelpPage>());
    }

    [RelayCommand]
    private void InviteFriend()
    {
        if (string.IsNullOrWhiteSpace(InviteEmail) || !InviteEmail.Contains('@'))
        {
            SocialStatusText = LocalizationService.T("invite_invalid_email");
            return;
        }

        var added = _social.Invite(InviteEmail);
        if (!added)
        {
            SocialStatusText = LocalizationService.T("invite_already_exists");
            return;
        }

        var email = InviteEmail.Trim();
        InviteEmail = "";
        SocialStatusText = LocalizationService.T("invite_sent");
        _ = _sync.TryInviteFriendAsync(email);
        LoadFriends();
    }

    [RelayCommand]
    private void AddBuddy()
    {
        if (string.IsNullOrWhiteSpace(InviteEmail) || !InviteEmail.Contains('@'))
        {
            SocialStatusText = LocalizationService.T("invite_invalid_email");
            return;
        }

        var email = InviteEmail.Trim();
        var added = _social.AddFriend(email);
        if (!added)
        {
            SocialStatusText = LocalizationService.T("invite_already_exists");
            return;
        }

        InviteEmail = "";
        SocialStatusText = LocalizationService.T("buddy_added");
        _ = _sync.TryInviteFriendAsync(email);
        LoadFriends();
    }

    [RelayCommand]
    private void AcceptFriend(FriendInviteItem? item)
    {
        if (item == null) return;

        _social.Accept(item.Email);
        SocialStatusText = LocalizationService.T("friend_accepted");
        LoadFriends();
    }

    [RelayCommand]
    private void RemoveFriend(FriendInviteItem? item)
    {
        if (item == null) return;

        _social.Remove(item.Email);
        SocialStatusText = LocalizationService.T("friend_removed");
        LoadFriends();
    }

    [RelayCommand]
    private async Task SaveReminders()
    {
        var breakfast = NormalizeHourMinute(BreakfastReminder);
        var lunch = NormalizeHourMinute(LunchReminder);
        var dinner = NormalizeHourMinute(DinnerReminder);

        try
        {
            var scheduleOk = await _mealReminderService.ScheduleDailyMealRemindersAsync(RemindersEnabled, breakfast, lunch, dinner);
            if (!scheduleOk)
            {
                ReminderStatusText = LocalizationService.T("reminders_failed");
                return;
            }

            Preferences.Default.Set("meal_reminders_enabled", RemindersEnabled);
            Preferences.Default.Set("meal_reminder_breakfast", breakfast.ToString(@"hh\:mm"));
            Preferences.Default.Set("meal_reminder_lunch", lunch.ToString(@"hh\:mm"));
            Preferences.Default.Set("meal_reminder_dinner", dinner.ToString(@"hh\:mm"));

            _ = await _sync.TryPushRemindersAsync(RemindersEnabled, breakfast, lunch, dinner);
            var balance = _points.Award(3);
            ReminderStatusText = $"{LocalizationService.T("reminders_saved")} · +3 · {LocalizationService.T("coins_balance")}: {balance}";
        }
        catch (Exception ex)
        {
            ReminderStatusText = $"{LocalizationService.T("sync_error")}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Logout()
    {
        Preferences.Default.Remove("auth_id_token");
        Preferences.Default.Remove("auth_access_token");
        Preferences.Default.Remove("profile_name");
        Preferences.Default.Remove("profile_email");
        Preferences.Default.Remove("profile_picture");
        Preferences.Default.Remove("backend_user_id");
        Preferences.Default.Remove("backend_identity_subject");

        // Return to login screen
        var login = _sp.GetRequiredService<LoginPage>();
        Application.Current!.MainPage = new NavigationPage(login);
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ChangePassword()
    {
        if (!IsEmailAccount)
        {
            AccountActionStatusText = LocalizationService.T("change_password_email_only");
            return;
        }

        var (ok, message) = await _emailAuth.ChangePasswordAsync(CurrentPassword, NewPassword);
        AccountActionStatusText = message;
        if (ok)
        {
            CurrentPassword = "";
            NewPassword = "";
        }
    }

    [RelayCommand]
    private async Task DeleteAccount()
    {
        var confirm = await Application.Current!.MainPage!.DisplayAlert(
            LocalizationService.T("delete_account_title"),
            LocalizationService.T("delete_account_confirm"),
            LocalizationService.T("delete_account_button"),
            LocalizationService.T("cancel"));

        if (!confirm)
            return;

        var password = IsEmailAccount ? DeletePassword : null;
        var (ok, message) = await _emailAuth.DeleteAccountAsync(password);
        AccountActionStatusText = message;
        if (!ok)
            return;

        Preferences.Default.Remove("auth_id_token");
        Preferences.Default.Remove("auth_access_token");
        Preferences.Default.Remove("profile_name");
        Preferences.Default.Remove("profile_email");
        Preferences.Default.Remove("profile_picture");
        Preferences.Default.Remove("backend_user_id");
        Preferences.Default.Remove("backend_identity_subject");
        Preferences.Default.Remove("auth_method");
        Preferences.Default.Remove("email_session_active");

        var login = _sp.GetRequiredService<LoginPage>();
        Application.Current!.MainPage = new NavigationPage(login);
    }

    private static bool TryParseTime(string input, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        if (TimeSpan.TryParse(trimmed, out time))
            return true;

        return TimeSpan.TryParseExact(trimmed, @"hh\:mm", null, out time);
    }

    private static TimeSpan ParseTimeOrDefault(string input, TimeSpan fallback)
    {
        if (TryParseTime(input, out var parsed))
            return NormalizeHourMinute(parsed);

        return fallback;
    }

    private static TimeSpan NormalizeHourMinute(TimeSpan value)
    {
        var hours = Math.Clamp(value.Hours, 0, 23);
        var minutes = Math.Clamp(value.Minutes, 0, 59);
        return new TimeSpan(hours, minutes, 0);
    }

    private void LoadFriends()
    {
        Friends.Clear();
        foreach (var f in _social.GetInvites())
        {
            var isPending = string.Equals(f.Status, "pending", StringComparison.OrdinalIgnoreCase);
            Friends.Add(new FriendInviteItem
            {
                Email = f.Email,
                StatusText = isPending ? LocalizationService.T("status_pending") : LocalizationService.T("status_friend"),
                BadgeText = isPending ? "🟡" : "🟢",
                IsPending = isPending,
            });
        }

        FriendRanks.Clear();
        var rows = _social.GetLeaderboard(Email, Name, _points.GetBalance());
        var rank = 1;
        foreach (var row in rows)
        {
            var medal = rank switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => $"#{rank}"
            };

            var meTag = row.IsMe ? $" ({LocalizationService.T("you")})" : "";
            FriendRanks.Add(new FriendRankItem
            {
                RankBadge = medal,
                Name = $"{row.DisplayName}{meTag}",
                Detail = $"XP: {row.WeeklyXp} · 🔥 {row.StreakDays}"
            });
            rank++;
        }
    }

    private void RefreshSubscriptionUi()
    {
        if (!ShowSubscriptionUi)
        {
            SubscriptionStatusText = "";
            CanStartFreeTrial = false;
            CanSubscribe = false;
            return;
        }

        var state = _subscription.GetState();
        CanStartFreeTrial = !state.IsSubscribed && !state.HasTrialBeenUsed;
        CanSubscribe = !state.IsSubscribed;

        if (state.IsSubscribed)
        {
            SubscriptionStatusText = LocalizationService.T("subscription_active");
            return;
        }

        if (state.IsTrialActive)
        {
            SubscriptionStatusText = string.Format(LocalizationService.T("subscription_trial_active"), state.DaysRemaining);
            return;
        }

        SubscriptionStatusText = state.HasTrialBeenUsed
            ? LocalizationService.T("subscription_trial_expired")
            : LocalizationService.T("subscription_trial_available");
    }
}

public class FriendInviteItem
{
    public string Email { get; set; } = "";
    public string StatusText { get; set; } = "";
    public string BadgeText { get; set; } = "";
    public bool IsPending { get; set; }
}

public class FriendRankItem
{
    public string RankBadge { get; set; } = "";
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";
}
