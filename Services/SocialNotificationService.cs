using System.Text.Json;
#if ANDROID
using Android.App;
using Android.Content;
using AndroidX.Core.App;
#endif

namespace NutritionTracker.Services;

public class SocialNotificationService
{
    private const string SnapshotKey = "social_activity_snapshot_v1";
    private const string FriendPostsSnapshotKey = "social_friend_posts_snapshot_v1";
    private const string FriendPostsSnapshotInitializedKey = "social_friend_posts_snapshot_initialized_v1";
    private const string IncomingInviteSnapshotKey = "social_incoming_invites_snapshot_v1";
    private const string IncomingInviteSnapshotInitializedKey = "social_incoming_invites_snapshot_initialized_v1";
    private readonly BackendSyncService _sync;
    private bool _isPolling;
    private bool _isShowing;

    public SocialNotificationService(BackendSyncService sync)
    {
        _sync = sync;
    }

    public async Task PollAndNotifyAsync()
    {
        if (_isPolling)
            return;

        _isPolling = true;
        try
        {
            var token = Preferences.Default.Get("auth_id_token", "");
            var identityOk = await _sync.EnsureBackendIdentityAsync(token);
            if (!identityOk)
                return;

            var meUserId = Preferences.Default.Get("backend_user_id", "").Trim();
            if (string.IsNullOrWhiteSpace(meUserId))
                return;

            var feed = await _sync.GetFriendsFeedAsync(days: 14, limit: 120);
            var incomingInvites = await _sync.GetIncomingInvitesAsync();
            var mine = feed
                .Where(x => string.Equals((x.user_id ?? "").Trim(), meUserId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var friends = feed
                .Where(x => !string.Equals((x.user_id ?? "").Trim(), meUserId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var snapshot = LoadSnapshot();
            var notifications = new List<string>();

            foreach (var story in mine)
            {
                var key = (story.meal_id ?? "").Trim();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                var current = new SocialCountState
                {
                    like_count = Math.Max(0, story.like_count),
                    comment_count = Math.Max(0, story.comment_count),
                };

                if (snapshot.TryGetValue(key, out var previous))
                {
                    var deltaLikes = Math.Max(0, current.like_count - previous.like_count);
                    var deltaComments = Math.Max(0, current.comment_count - previous.comment_count);

                    if (deltaLikes > 0 || deltaComments > 0)
                    {
                        var parts = new List<string>();
                        if (deltaLikes > 0)
                            parts.Add($"+{deltaLikes} ❤️");
                        if (deltaComments > 0)
                            parts.Add($"+{deltaComments} 💬");

                        notifications.Add($"✨ {string.Join(" ", parts)}");
                    }
                }

                snapshot[key] = current;
            }

            SaveSnapshot(snapshot);

            var friendSnapshot = LoadFriendPostSnapshot();
            var hadFriendSnapshot = Preferences.Default.Get(FriendPostsSnapshotInitializedKey, false);
            var currentFriendPostIds = friends
                .Select(x => (x.meal_id ?? "").Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var newFriendPosts = friends
                .Where(x => !friendSnapshot.Contains((x.meal_id ?? "").Trim()))
                .OrderByDescending(x => x.date_utc)
                .Take(3)
                .ToList();

            if (hadFriendSnapshot && newFriendPosts.Count > 0)
            {
                foreach (var story in newFriendPosts)
                {
                    var actor = (story.display_name ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(actor))
                        actor = LocalizationService.T("story_default_author");

                    var hasPhoto = !string.IsNullOrWhiteSpace((story.photo_url ?? "").Trim());
                    notifications.Add(hasPhoto
                        ? string.Format(LocalizationService.T("friend_story_photo_notification"), actor)
                        : string.Format(LocalizationService.T("friend_story_entry_notification"), actor));
                }
            }

            SaveFriendPostSnapshot(currentFriendPostIds);
            Preferences.Default.Set(FriendPostsSnapshotInitializedKey, true);

            var inviteSnapshot = LoadInviteSnapshot();
            var hadInviteSnapshot = Preferences.Default.Get(IncomingInviteSnapshotInitializedKey, false);
            var currentInviteIds = incomingInvites
                .Select(x => (x.id ?? "").Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var newInvites = incomingInvites
                .Where(x => !inviteSnapshot.Contains((x.id ?? "").Trim()))
                .Take(3)
                .ToList();

            if (hadInviteSnapshot && newInvites.Count > 0)
            {
                foreach (var invite in newInvites)
                {
                    var who = (invite.inviter_display_name ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(who))
                        who = LocalizationService.T("friend_message");

                    notifications.Add($"📩 {string.Format(LocalizationService.T("friend_invite_notification"), who)}");
                }
            }

            SaveInviteSnapshot(currentInviteIds);
            Preferences.Default.Set(IncomingInviteSnapshotInitializedKey, true);

            if (notifications.Count == 0 || _isShowing)
                return;

            var body = string.Join("\n", notifications.Take(5).Select(x => x.StartsWith("✨") || x.StartsWith("📩") ? x : $"🔔 {x}"));
            _isShowing = true;
            try
            {
                await ShowSocialNotificationAsync($"📣 {LocalizationService.T("social_notify_title")}", body);
            }
            finally
            {
                _isShowing = false;
            }
        }
        finally
        {
            _isPolling = false;
        }
    }

    private static Dictionary<string, SocialCountState> LoadSnapshot()
    {
        var json = Preferences.Default.Get(SnapshotKey, "");
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, SocialCountState>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, SocialCountState>>(json) ?? new Dictionary<string, SocialCountState>();
        }
        catch
        {
            return new Dictionary<string, SocialCountState>();
        }
    }

    private static void SaveSnapshot(Dictionary<string, SocialCountState> snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot);
        Preferences.Default.Set(SnapshotKey, json);
    }

    private static HashSet<string> LoadFriendPostSnapshot()
    {
        var json = Preferences.Default.Get(FriendPostsSnapshotKey, "");
        if (string.IsNullOrWhiteSpace(json))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            return parsed
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveFriendPostSnapshot(HashSet<string> mealIds)
    {
        var json = JsonSerializer.Serialize(mealIds.ToList());
        Preferences.Default.Set(FriendPostsSnapshotKey, json);
    }

    private static HashSet<string> LoadInviteSnapshot()
    {
        var json = Preferences.Default.Get(IncomingInviteSnapshotKey, "");
        if (string.IsNullOrWhiteSpace(json))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            return parsed
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveInviteSnapshot(HashSet<string> inviteIds)
    {
        var json = JsonSerializer.Serialize(inviteIds.ToList());
        Preferences.Default.Set(IncomingInviteSnapshotKey, json);
    }

    private sealed class SocialCountState
    {
        public int like_count { get; set; }
        public int comment_count { get; set; }
    }

    private static async Task ShowSocialNotificationAsync(string title, string body)
    {
#if ANDROID
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var context = Android.App.Application.Context;
            if (context == null)
                return;

            const string channelId = "social_updates";
            EnsureSocialChannel(context, channelId);

            var openIntent = new Intent(context, Java.Lang.Class.FromType(typeof(MainActivity)));
            openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

            var flags = PendingIntentFlags.UpdateCurrent;
            if (OperatingSystem.IsAndroidVersionAtLeast(23))
                flags |= PendingIntentFlags.Immutable;

            var pendingOpen = PendingIntent.GetActivity(context, 8801, openIntent, flags);

            var notification = new NotificationCompat.Builder(context, channelId)
                .SetContentTitle(title)
                .SetContentText(body)
                .SetStyle(new NotificationCompat.BigTextStyle().BigText(body))
                .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)
                .SetAutoCancel(true)
                .SetPriority((int)NotificationPriority.High)
                .SetContentIntent(pendingOpen)
                .Build();

            NotificationManagerCompat.From(context).Notify(8801, notification);
        });
#else
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.Windows.Count > 0
                ? Application.Current.Windows[0].Page
                : null;
            if (page == null)
                return;

            await page.DisplayAlert(title, body, "OK");
        });
#endif
    }

#if ANDROID
    private static void EnsureSocialChannel(Context context, string channelId)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (manager == null)
            return;

        if (manager.GetNotificationChannel(channelId) != null)
            return;

        var channel = new NotificationChannel(channelId, LocalizationService.T("notif_channel_social_name"), NotificationImportance.Default)
        {
            Description = LocalizationService.T("notif_channel_social_desc")
        };

        manager.CreateNotificationChannel(channel);
    }
#endif
}
