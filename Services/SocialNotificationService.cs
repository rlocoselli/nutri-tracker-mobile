using System.Text.Json;

namespace NutritionTracker.Services;

public class SocialNotificationService
{
    private const string SnapshotKey = "social_activity_snapshot_v1";
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

                        notifications.Add(string.Join(" ", parts));
                    }
                }

                snapshot[key] = current;
            }

            SaveSnapshot(snapshot);

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

                    notifications.Add(string.Format(LocalizationService.T("friend_invite_notification"), who));
                }
            }

            SaveInviteSnapshot(currentInviteIds);
            Preferences.Default.Set(IncomingInviteSnapshotInitializedKey, true);

            if (notifications.Count == 0 || _isShowing)
                return;

            var body = string.Join("\n", notifications.Take(5));
            _isShowing = true;
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    await Application.Current!.MainPage!.DisplayAlert(
                        LocalizationService.T("social_notify_title"),
                        body,
                        "OK");
                }
                finally
                {
                    _isShowing = false;
                }
            });
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
}
