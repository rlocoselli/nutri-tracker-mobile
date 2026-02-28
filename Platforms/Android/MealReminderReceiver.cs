using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace NutritionTracker.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
public class MealReminderReceiver : BroadcastReceiver
{
    private const string ChannelId = "meal_reminders";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;

        CreateNotificationChannel(context);

        var id = intent?.GetIntExtra("notif_id", 3001) ?? 3001;
        var title = intent?.GetStringExtra("notif_title") ?? "Rappel repas";
        var message = intent?.GetStringExtra("notif_message") ?? "Pense à enregistrer ton repas.";

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
