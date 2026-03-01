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

    public async Task<bool> ScheduleDailyMealRemindersAsync(bool enabled, TimeSpan breakfastTime, TimeSpan lunchTime, TimeSpan dinnerTime)
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            if (context == null)
                return false;

            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.PostNotifications>();
                    if (status != PermissionStatus.Granted)
                        return false;
                }
            }

            if (!NotificationManagerCompat.From(context).AreNotificationsEnabled())
                return false;

            EnsureNotificationChannel(context);

            if (!enabled)
            {
                Cancel(BreakfastNotificationId);
                Cancel(LunchNotificationId);
                Cancel(DinnerNotificationId);
                return true;
            }

            ScheduleDaily(BreakfastNotificationId, breakfastTime, "Rappel petit-déjeuner", "Pense à enregistrer ton repas du matin.");
            ScheduleDaily(LunchNotificationId, lunchTime, "Rappel déjeuner", "Pense à enregistrer ton repas de midi.");
            ScheduleDaily(DinnerNotificationId, dinnerTime, "Rappel dîner", "Pense à enregistrer ton repas du soir.");
            return true;
        }
        catch
        {
            return false;
        }
    #else
        await Task.CompletedTask;
        return true;
    #endif
    }

#if ANDROID
    private const string ChannelId = "meal_reminders";

    private static void EnsureNotificationChannel(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (manager == null) return;

        var existing = manager.GetNotificationChannel(ChannelId);
        if (existing != null) return;

        var channel = new NotificationChannel(ChannelId, "Meal reminders", NotificationImportance.Default)
        {
            Description = "Rappels quotidiens pour enregistrer les repas"
        };

        manager.CreateNotificationChannel(channel);
    }

    private static void ScheduleDaily(int id, TimeSpan timeOfDay, string title, string message)
    {
        var context = Android.App.Application.Context;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        var intent = new Intent(context, Java.Lang.Class.FromType(typeof(Platforms.Android.MealReminderReceiver)));
        intent.PutExtra("notif_id", id);
        intent.PutExtra("notif_title", title);
        intent.PutExtra("notif_message", message);
        intent.PutExtra("hour", timeOfDay.Hours);
        intent.PutExtra("minute", timeOfDay.Minutes);

        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
            flags |= PendingIntentFlags.Immutable;

        var pendingIntent = PendingIntent.GetBroadcast(context, id, intent, flags);
        if (pendingIntent == null) return;

        alarmManager.Cancel(pendingIntent);

        var triggerAt = NextTriggerUtcMillis(timeOfDay);
        SetBestAlarm(alarmManager, triggerAt, pendingIntent);
    }

    private static void SetBestAlarm(AlarmManager alarmManager, long triggerAtMillis, PendingIntent pendingIntent)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(31) && alarmManager.CanScheduleExactAlarms())
        {
            alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
            return;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
            return;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(19))
        {
            alarmManager.Set(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
            return;
        }

        alarmManager.Set(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
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
