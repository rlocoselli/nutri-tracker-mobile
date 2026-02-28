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

    private void Save(List<FriendInvite> list)
    {
        var json = JsonSerializer.Serialize(list.OrderByDescending(x => x.CreatedUtc).ToList());
        Preferences.Default.Set(InvitesKey, json);
    }

    private static string NormalizeEmail(string email)
        => (email ?? "").Trim().ToLowerInvariant();
}
