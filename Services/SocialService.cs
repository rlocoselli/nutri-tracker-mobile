using System.Text.Json;
using NutritionTracker.Models;

namespace NutritionTracker.Services;

public class SocialService
{
    private const string InvitesKey = "social_friend_invites_v1";

    public List<FriendInvite> GetInvites()
    {
        var json = Preferences.Default.Get(InvitesKey, "");
        if (string.IsNullOrWhiteSpace(json))
            return new List<FriendInvite>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<FriendInvite>>(json);
            return parsed?
                .OrderByDescending(x => x.CreatedUtc)
                .ToList() ?? new List<FriendInvite>();
        }
        catch
        {
            return new List<FriendInvite>();
        }
    }

    public bool Invite(string email)
    {
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        var list = GetInvites();
        var existing = list.FirstOrDefault(x => string.Equals(x.Email, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return false;

        list.Add(new FriendInvite
        {
            Email = normalized,
            Status = "pending",
            CreatedUtc = DateTime.UtcNow,
        });

        Save(list);
        return true;
    }

    public bool AddFriend(string email)
    {
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        var list = GetInvites();
        var existing = list.FirstOrDefault(x => string.Equals(x.Email, normalized, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.Status = "accepted";
            Save(list);
            return true;
        }

        list.Add(new FriendInvite
        {
            Email = normalized,
            Status = "accepted",
            CreatedUtc = DateTime.UtcNow,
        });

        Save(list);
        return true;
    }

    public bool Accept(string email)
    {
        var normalized = NormalizeEmail(email);
        var list = GetInvites();
        var existing = list.FirstOrDefault(x => string.Equals(x.Email, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
            return false;

        existing.Status = "accepted";
        Save(list);
        return true;
    }

    public bool Remove(string email)
    {
        var normalized = NormalizeEmail(email);
        var list = GetInvites();
        var removed = list.RemoveAll(x => string.Equals(x.Email, normalized, StringComparison.OrdinalIgnoreCase));
        if (removed <= 0)
            return false;

        Save(list);
        return true;
    }

    public List<SocialLeaderboardEntry> GetLeaderboard(string selfEmail, string selfName, int selfCoins)
    {
        var entries = new List<SocialLeaderboardEntry>();

        var selfDisplay = !string.IsNullOrWhiteSpace(selfName)
            ? selfName.Trim()
            : EmailToDisplayName(selfEmail);

        entries.Add(new SocialLeaderboardEntry
        {
            Email = NormalizeEmail(selfEmail),
            DisplayName = string.IsNullOrWhiteSpace(selfDisplay) ? "You" : selfDisplay,
            WeeklyXp = Math.Max(30, selfCoins * 3),
            StreakDays = Math.Max(1, selfCoins / 25),
            IsMe = true,
        });

        foreach (var friend in GetInvites().Where(x => string.Equals(x.Status, "accepted", StringComparison.OrdinalIgnoreCase)))
        {
            var hash = StableHash(friend.Email);
            entries.Add(new SocialLeaderboardEntry
            {
                Email = friend.Email,
                DisplayName = EmailToDisplayName(friend.Email),
                WeeklyXp = 80 + (hash % 700),
                StreakDays = 1 + ((hash / 7) % 60),
                IsMe = false,
            });
        }

        return entries
            .OrderByDescending(x => x.WeeklyXp)
            .ThenByDescending(x => x.StreakDays)
            .ThenBy(x => x.DisplayName)
            .ToList();
    }

    private void Save(List<FriendInvite> list)
    {
        var json = JsonSerializer.Serialize(list.OrderByDescending(x => x.CreatedUtc).ToList());
        Preferences.Default.Set(InvitesKey, json);
    }

    private static string NormalizeEmail(string email)
        => (email ?? "").Trim().ToLowerInvariant();

    private static string EmailToDisplayName(string email)
    {
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalized))
            return "Friend";

        var local = normalized.Split('@')[0];
        if (string.IsNullOrWhiteSpace(local))
            return "Friend";

        var compact = local.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ').Trim();
        if (string.IsNullOrWhiteSpace(compact))
            compact = local;

        return char.ToUpper(compact[0]) + compact[1..];
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261;
            foreach (var ch in value ?? string.Empty)
            {
                hash ^= ch;
                hash *= 16777619;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
    }
}

public class SocialLeaderboardEntry
{
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int WeeklyXp { get; set; }
    public int StreakDays { get; set; }
    public bool IsMe { get; set; }
}
