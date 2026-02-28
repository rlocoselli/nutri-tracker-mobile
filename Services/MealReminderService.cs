#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
#endif

namespace NutritionTracker.Services;

public class MealReminderService : IMealReminderService
{
    private const int BreakfastNotificationId = 2101;
    private const int LunchNotificationId = 2102;
    private const int DinnerNotificationId = 2103;

    public Task ScheduleDailyMealRemindersAsync(bool enabled, TimeSpan breakfastTime, TimeSpan lunchTime, TimeSpan dinnerTime)
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = Permissions.CheckStatusAsync<Permissions.PostNotifications>().GetAwaiter().GetResult();
            if (status != PermissionStatus.Granted)
            {
                status = Permissions.RequestAsync<Permissions.PostNotifications>().GetAwaiter().GetResult();
                if (status != PermissionStatus.Granted)
                    return Task.CompletedTask;
            }
        }

        if (!enabled)
        {
            Cancel(BreakfastNotificationId);
            Cancel(LunchNotificationId);
            Cancel(DinnerNotificationId);
            return Task.CompletedTask;
        }

        Schedule(BreakfastNotificationId, breakfastTime, "Rappel petit-déjeuner", "Pense à enregistrer ton repas du matin.");
        Schedule(LunchNotificationId, lunchTime, "Rappel déjeuner", "Pense à enregistrer ton repas de midi.");
        Schedule(DinnerNotificationId, dinnerTime, "Rappel dîner", "Pense à enregistrer ton repas du soir.");
#endif
        return Task.CompletedTask;
    }

#if ANDROID
    private static void Schedule(int id, TimeSpan timeOfDay, string title, string message)
    {
        var context = Android.App.Application.Context;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        var intent = new Intent(context, Java.Lang.Class.FromType(typeof(Platforms.Android.MealReminderReceiver)));
        intent.PutExtra("notif_id", id);
        intent.PutExtra("notif_title", title);
        intent.PutExtra("notif_message", message);

        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
            flags |= PendingIntentFlags.Immutable;

        var pendingIntent = PendingIntent.GetBroadcast(context, id, intent, flags);
        if (pendingIntent == null) return;

        alarmManager.Cancel(pendingIntent);

        var triggerAt = NextTriggerUtcMillis(timeOfDay);
        alarmManager.SetInexactRepeating(AlarmType.RtcWakeup, triggerAt, AlarmManager.IntervalDay, pendingIntent);
    }

    private static void Cancel(int id)
    {
        var context = Android.App.Application.Context;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        var intent = new Intent(context, Java.Lang.Class.FromType(typeof(Platforms.Android.MealReminderReceiver)));
        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
            flags |= PendingIntentFlags.Immutable;

        var pendingIntent = PendingIntent.GetBroadcast(context, id, intent, flags);
        if (pendingIntent == null) return;

        alarmManager.Cancel(pendingIntent);

        var notificationManager = NotificationManagerCompat.From(context);
        notificationManager.Cancel(id);
    }

    private static long NextTriggerUtcMillis(TimeSpan timeOfDay)
    {
        var now = DateTime.Now;
        var trigger = new DateTime(now.Year, now.Month, now.Day, timeOfDay.Hours, timeOfDay.Minutes, 0);
        if (trigger <= now)
            trigger = trigger.AddDays(1);

        return new DateTimeOffset(trigger).ToUnixTimeMilliseconds();
    }
#endif
}
