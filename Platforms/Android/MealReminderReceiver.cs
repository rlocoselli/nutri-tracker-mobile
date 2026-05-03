using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace NutritionTracker.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
public class MealReminderReceiver : BroadcastReceiver
{
    private const string ChannelId = "meal_reminders";
    private const string LastMealLoggedDayKey = "last_meal_logged_day_local";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;

        CreateNotificationChannel(context);

        var id = intent?.GetIntExtra("notif_id", 3001) ?? 3001;
        var title = intent?.GetStringExtra("notif_title") ?? "Rappel repas";
        var message = intent?.GetStringExtra("notif_message") ?? "Pense à enregistrer ton repas.";
        var hour = intent?.GetIntExtra("hour", 8) ?? 8;
        var minute = intent?.GetIntExtra("minute", 0) ?? 0;
        var skipIfMealLoggedToday = intent?.GetBooleanExtra("skip_if_meal_logged_today", false) ?? false;

        if (skipIfMealLoggedToday && HasMealLoggedToday())
        {
            ScheduleNextDay(context, id, hour, minute, title, message, skipIfMealLoggedToday);
            return;
        }

        var openIntent = new Intent(context, typeof(MainActivity));
        openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
            flags |= PendingIntentFlags.Immutable;

        var pendingOpen = PendingIntent.GetActivity(context, id + 10000, openIntent, flags);

        var notification = new NotificationCompat.Builder(context, ChannelId)
            .SetContentTitle(title)
            .SetContentText(message)
            .SetSmallIcon(Resource.Mipmap.appicon_generated)
            .SetAutoCancel(true)
            .SetPriority((int)NotificationPriority.High)
            .SetContentIntent(pendingOpen)
            .Build();

        NotificationManagerCompat.From(context).Notify(id, notification);
        ScheduleNextDay(context, id, hour, minute, title, message, skipIfMealLoggedToday);
    }

    private static void ScheduleNextDay(Context context, int id, int hour, int minute, string title, string message, bool skipIfMealLoggedToday)
    {
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        var intent = new Intent(context, Java.Lang.Class.FromType(typeof(MealReminderReceiver)));
        intent.PutExtra("notif_id", id);
        intent.PutExtra("notif_title", title);
        intent.PutExtra("notif_message", message);
        intent.PutExtra("hour", hour);
        intent.PutExtra("minute", minute);
        intent.PutExtra("skip_if_meal_logged_today", skipIfMealLoggedToday);

        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
            flags |= PendingIntentFlags.Immutable;

        var pendingIntent = PendingIntent.GetBroadcast(context, id, intent, flags);
        if (pendingIntent == null) return;

        var now = DateTime.Now;
        var trigger = new DateTime(now.Year, now.Month, now.Day, Math.Clamp(hour, 0, 23), Math.Clamp(minute, 0, 59), 0).AddDays(1);
        var triggerAtMillis = new DateTimeOffset(trigger).ToUnixTimeMilliseconds();

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

        alarmManager.Set(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
    }

    private static bool HasMealLoggedToday()
    {
        var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
        var lastMealDay = Preferences.Default.Get(LastMealLoggedDayKey, "").Trim();
        return string.Equals(todayKey, lastMealDay, StringComparison.Ordinal);
    }

    private static void CreateNotificationChannel(Context context)
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
}
