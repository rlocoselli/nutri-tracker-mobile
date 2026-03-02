namespace NutritionTracker.Services;

public sealed class SubscriptionService
{
    private const string TrialStartUtcKey = "subscription_trial_start_utc";
    private const string IsSubscribedKey = "subscription_is_active";

    private const int TrialDays = 7;

    public bool IsEnabled => FeatureFlags.EnableSubscriptions;

    public SubscriptionState GetState()
    {
        if (!IsEnabled)
        {
            return new SubscriptionState
            {
                IsFeatureEnabled = false,
                IsSubscribed = false,
                IsTrialActive = false,
                DaysRemaining = 0,
                HasTrialBeenUsed = false,
            };
        }

        var isSubscribed = Preferences.Default.Get(IsSubscribedKey, false);
        var trialStartRaw = Preferences.Default.Get(TrialStartUtcKey, "");

        DateTime? trialStartUtc = null;
        if (DateTime.TryParse(trialStartRaw, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
            trialStartUtc = parsed;

        var nowUtc = DateTime.UtcNow;
        var trialEndUtc = trialStartUtc?.AddDays(TrialDays);
        var isTrialActive = trialStartUtc.HasValue && trialEndUtc.HasValue && nowUtc < trialEndUtc.Value;
        var daysRemaining = isTrialActive && trialEndUtc.HasValue
            ? Math.Max(1, (int)Math.Ceiling((trialEndUtc.Value - nowUtc).TotalDays))
            : 0;

        return new SubscriptionState
        {
            IsFeatureEnabled = true,
            IsSubscribed = isSubscribed,
            TrialStartUtc = trialStartUtc,
            TrialEndUtc = trialEndUtc,
            IsTrialActive = isTrialActive,
            DaysRemaining = daysRemaining,
            HasTrialBeenUsed = trialStartUtc.HasValue,
        };
    }

    public Task<bool> StartFreeTrialAsync()
    {
        if (!IsEnabled)
            return Task.FromResult(false);

        var state = GetState();
        if (state.HasTrialBeenUsed)
            return Task.FromResult(false);

        Preferences.Default.Set(TrialStartUtcKey, DateTime.UtcNow.ToString("o"));
        return Task.FromResult(true);
    }

    public Task OpenGooglePlaySubscriptionAsync()
    {
        if (!IsEnabled)
            return Task.CompletedTask;

        const string url = "https://play.google.com/store/account/subscriptions?sku=nutritiontracker_premium_monthly&package=com.audela.nutritiontracker";
        return Launcher.Default.OpenAsync(url);
    }

    public Task ConfirmGoogleSubscriptionAsync()
    {
        if (!IsEnabled)
            return Task.CompletedTask;

        Preferences.Default.Set(IsSubscribedKey, true);
        return Task.CompletedTask;
    }

    public Task CancelLocalSubscriptionFlagAsync()
    {
        Preferences.Default.Set(IsSubscribedKey, false);
        return Task.CompletedTask;
    }
}

public sealed class SubscriptionState
{
    public bool IsFeatureEnabled { get; set; }
    public bool IsSubscribed { get; set; }
    public DateTime? TrialStartUtc { get; set; }
    public DateTime? TrialEndUtc { get; set; }
    public bool IsTrialActive { get; set; }
    public int DaysRemaining { get; set; }
    public bool HasTrialBeenUsed { get; set; }
}
