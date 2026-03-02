using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class FriendsViewModel : ObservableObject
{
    private readonly SocialService _social;
    private readonly BackendSyncService _sync;
    private readonly PointsService _points;
    private readonly IServiceProvider _services;

    [ObservableProperty] private string inviteEmail = "";
    [ObservableProperty] private string searchQuery = "";
    [ObservableProperty] private string statusText = "";

    public ObservableCollection<FriendsInviteRow> Friends { get; } = new();
    public ObservableCollection<FriendsRankRow> League { get; } = new();
    public ObservableCollection<FriendSearchRow> SearchResults { get; } = new();
    public ObservableCollection<IncomingInviteRow> IncomingInvites { get; } = new();

    public string TitleText => T("friends_title");
    public string InvitePlaceholder => T("invite_email_placeholder");
    public string InviteSectionTitleText => T("friends_invite_section_title");
    public string FriendsSectionTitleText => T("friends_contacts_section_title");
    public string SearchSectionTitleText => T("friends_search_section_title");
    public string EmptyFriendsText => T("friends_empty");
    public string SearchPlaceholder => T("friend_search_placeholder");
    public string InviteText => T("invite_friend");
    public string AddBuddyText => T("add_buddy");
    public string SearchText => T("search");
    public string AcceptText => T("accept");
    public string DeclineText => T("decline");
    public string RemoveText => T("remove");
    public string LeagueTitleText => T("friends_league_title");
    public string IncomingInvitesTitleText => T("incoming_invites_title");
    public string SearchResultsTitleText => T("search_results_title");
    public string RefreshText => T("refresh");
    public string StoriesText => T("friend_stories");
    public string MessageText => T("friend_message");
    public string ViewMessagesText => T("story_view_messages");
    public string ChatText => T("friend_chat_open");

    public FriendsViewModel(SocialService social, BackendSyncService sync, PointsService points, IServiceProvider services)
    {
        _social = social;
        _sync = sync;
        _points = points;
        _services = services;
    }

    public async Task LoadAsync()
    {
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(InvitePlaceholder));
        OnPropertyChanged(nameof(InviteSectionTitleText));
        OnPropertyChanged(nameof(FriendsSectionTitleText));
        OnPropertyChanged(nameof(SearchSectionTitleText));
        OnPropertyChanged(nameof(EmptyFriendsText));
        OnPropertyChanged(nameof(SearchPlaceholder));
        OnPropertyChanged(nameof(InviteText));
        OnPropertyChanged(nameof(AddBuddyText));
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(AcceptText));
        OnPropertyChanged(nameof(DeclineText));
        OnPropertyChanged(nameof(RemoveText));
        OnPropertyChanged(nameof(LeagueTitleText));
        OnPropertyChanged(nameof(IncomingInvitesTitleText));
        OnPropertyChanged(nameof(SearchResultsTitleText));
        OnPropertyChanged(nameof(RefreshText));
        OnPropertyChanged(nameof(StoriesText));
        OnPropertyChanged(nameof(MessageText));
        OnPropertyChanged(nameof(ViewMessagesText));
        OnPropertyChanged(nameof(ChatText));
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task Invite()
    {
        if (string.IsNullOrWhiteSpace(InviteEmail) || !InviteEmail.Contains('@'))
        {
            StatusText = T("invite_invalid_email");
            return;
        }

        var email = InviteEmail.Trim();
        var added = _social.Invite(email);
        if (!added)
        {
            StatusText = T("invite_already_exists");
            return;
        }

        var backendOk = await _sync.TryInviteFriendAsync(email);
        InviteEmail = "";
        StatusText = backendOk ? T("invite_sent") : T("invite_send_failed");
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task AddBuddy()
    {
        if (string.IsNullOrWhiteSpace(InviteEmail) || !InviteEmail.Contains('@'))
        {
            StatusText = T("invite_invalid_email");
            return;
        }

        var email = InviteEmail.Trim();
        var ok = _social.AddFriend(email);
        if (!ok)
        {
            StatusText = T("invite_already_exists");
            return;
        }

        _ = await _sync.TryInviteFriendAsync(email);
        InviteEmail = "";
        StatusText = T("buddy_added");
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task SearchUsers()
    {
        var query = (SearchQuery ?? "").Trim();
        SearchResults.Clear();

        if (query.Length < 2)
        {
            StatusText = T("friend_search_hint");
            return;
        }

        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        if (!identityOk)
        {
            StatusText = T("friend_action_signin_needed");
            return;
        }

        var users = await _sync.SearchFriendUsersAsync(query, limit: 20);
        foreach (var user in users)
        {
            var email = (user.email ?? "").Trim();
            if (string.IsNullOrWhiteSpace(email))
                continue;

            SearchResults.Add(new FriendSearchRow
            {
                UserId = (user.user_id ?? "").Trim(),
                Email = email,
                DisplayName = string.IsNullOrWhiteSpace(user.display_name) ? email.Split('@')[0] : user.display_name,
                Handle = BuildHandle(string.IsNullOrWhiteSpace(user.display_name) ? email.Split('@')[0] : user.display_name),
            });
        }

        StatusText = SearchResults.Count == 0 ? T("friend_search_empty") : T("friend_search_success");
    }

    [RelayCommand]
    private async Task InviteUser(FriendSearchRow? row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.Email))
            return;

        var email = row.Email.Trim();
        _social.Invite(email);
        var ok = await _sync.TryInviteFriendAsync(email);
        StatusText = ok ? T("invite_sent") : T("invite_send_failed");
        await ReloadAsync();
    }

    [RelayCommand]
    private void Accept(FriendsInviteRow? row)
    {
        if (row == null) return;
        _social.Accept(row.Email);
        StatusText = T("friend_accepted");
        _ = ReloadAsync();
    }

    [RelayCommand]
    private void Remove(FriendsInviteRow? row)
    {
        if (row == null) return;
        _social.Remove(row.Email);
        StatusText = T("friend_removed");
        _ = ReloadAsync();
    }

    [RelayCommand]
    private async Task AcceptIncoming(IncomingInviteRow? row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.InviteId))
            return;

        var ok = await _sync.TryAcceptInviteAsync(row.InviteId);
        if (!ok)
        {
            StatusText = T("invite_accept_failed");
            return;
        }

        if (!string.IsNullOrWhiteSpace(row.InviterEmail))
            _social.AddFriend(row.InviterEmail);

        StatusText = T("friend_accepted");
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task DeclineIncoming(IncomingInviteRow? row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.InviteId))
            return;

        var ok = await _sync.TryDeclineInviteAsync(row.InviteId);
        StatusText = ok ? T("invite_declined") : T("invite_decline_failed");
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task ViewStories(FriendsInviteRow? row)
    {
        if (row == null || row.IsPending || string.IsNullOrWhiteSpace(row.Email))
            return;

        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        if (!identityOk)
        {
            await Application.Current!.MainPage!.DisplayAlert(T("friends_title"), T("friend_action_signin_needed"), "OK");
            return;
        }

        var feed = await _sync.GetFriendsFeedAsync(days: 14, limit: 120);
        var stories = feed
            .Where(x => !string.IsNullOrWhiteSpace(x.photo_url)
                && string.Equals((x.author_email ?? "").Trim(), row.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.date_utc)
            .Take(8)
            .ToList();

        if (stories.Count == 0)
        {
            await Application.Current!.MainPage!.DisplayAlert(
                string.Format(T("friend_stories_title"), row.DisplayName),
                T("friend_no_stories"),
                "OK");
            return;
        }

        var lines = stories.Select(x =>
        {
            var when = x.date_utc.ToLocalTime().ToString("dd/MM HH:mm");
            var caption = string.IsNullOrWhiteSpace(x.raw_text) ? T("story_meal") : x.raw_text.Trim();
            return $"• {when} · {Math.Round(x.total_calories)} kcal · {caption}";
        });

        var body = string.Join("\n", lines);
        await Application.Current!.MainPage!.DisplayAlert(
                string.Format(T("friend_stories_title"), row.DisplayName),
            body,
            "OK");
    }

    [RelayCommand]
    private async Task SendMessage(FriendsInviteRow? row)
    {
        await OpenChat(row);
    }

    [RelayCommand]
    private async Task ViewMessages(FriendsInviteRow? row)
    {
        await OpenChat(row);
    }

    [RelayCommand]
    private async Task OpenChat(FriendsInviteRow? row)
    {
        if (row == null || row.IsPending || string.IsNullOrWhiteSpace(row.Email))
            return;

        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        if (!identityOk)
        {
            await Application.Current!.MainPage!.DisplayAlert(T("friends_title"), T("friend_action_signin_needed"), "OK");
            return;
        }

        var otherUserId = (row.UserId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(otherUserId))
            otherUserId = await ResolveOtherUserIdByEmailAsync(row.Email);

        if (string.IsNullOrWhiteSpace(otherUserId))
        {
            await Application.Current!.MainPage!.DisplayAlert(T("friend_message"), T("friend_message_unavailable"), "OK");
            return;
        }

        if (_services.GetService(typeof(Pages.FriendChatPage)) is not Pages.FriendChatPage chatPage)
            return;

        if (chatPage.BindingContext is not FriendChatViewModel chatVm)
            return;

        await chatVm.InitializeAsync(otherUserId, row.DisplayName, row.Email);
        await Shell.Current.Navigation.PushAsync(chatPage);
    }

    private async Task<string> ResolveOtherUserIdByEmailAsync(string email)
    {
        var normalized = (email ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return "";

        var directory = await _sync.GetFriendDirectoryAsync();
        var userId = directory
            .Where(x => string.Equals((x.email ?? "").Trim(), normalized, StringComparison.OrdinalIgnoreCase))
            .Select(x => (x.user_id ?? "").Trim())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        if (!string.IsNullOrWhiteSpace(userId))
            return userId;

        return "";
    }

    private async Task ReloadAsync()
    {
        var displayByEmail = new Dictionary<string, (string Name, string UserId)>(StringComparer.OrdinalIgnoreCase);
        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        if (identityOk)
        {
            var directory = await _sync.GetFriendDirectoryAsync();
            foreach (var user in directory)
            {
                var email = (user.email ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(email))
                    continue;

                var name = (user.display_name ?? "").Trim();
                if (string.IsNullOrWhiteSpace(name))
                    name = DisplayNameFromEmail(email);

                displayByEmail[email] = (name, (user.user_id ?? "").Trim());
            }
        }

        Friends.Clear();
        var knownEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _social.GetInvites())
        {
            var pending = string.Equals(item.Status, "pending", StringComparison.OrdinalIgnoreCase);
            var email = (item.Email ?? "").Trim().ToLowerInvariant();
            knownEmails.Add(email);

            var displayName = displayByEmail.TryGetValue(email, out var mapped)
                ? mapped.Name
                : DisplayNameFromEmail(email);

            Friends.Add(new FriendsInviteRow
            {
                Email = email,
                DisplayName = displayName,
                Handle = BuildHandle(displayName),
                UserId = displayByEmail.TryGetValue(email, out var info) ? info.UserId : "",
                Status = pending ? T("status_pending") : T("status_friend"),
                Badge = pending ? "🟡" : "🟢",
                IsPending = pending,
            });
        }

        foreach (var kv in displayByEmail)
        {
            if (knownEmails.Contains(kv.Key))
                continue;

            Friends.Add(new FriendsInviteRow
            {
                Email = kv.Key,
                DisplayName = kv.Value.Name,
                Handle = BuildHandle(kv.Value.Name),
                UserId = kv.Value.UserId,
                Status = T("status_friend"),
                Badge = "🟢",
                IsPending = false,
            });
        }

        IncomingInvites.Clear();
        if (identityOk)
        {
            var incoming = await _sync.GetIncomingInvitesAsync();
            foreach (var invite in incoming)
            {
                var email = (invite.inviter_email ?? "").Trim();
                var display = (invite.inviter_display_name ?? "").Trim();
                if (string.IsNullOrWhiteSpace(display))
                    display = !string.IsNullOrWhiteSpace(email) && email.Contains('@') ? email.Split('@')[0] : "User";

                IncomingInvites.Add(new IncomingInviteRow
                {
                    InviteId = invite.id,
                    InviterUserId = invite.inviter_user_id,
                    InviterEmail = email,
                    InviterDisplay = display,
                    InviterHandle = BuildHandle(display),
                });
            }
        }

        League.Clear();
        var selfEmail = Preferences.Default.Get("profile_email", "");
        var selfName = Preferences.Default.Get("profile_name", "");
        var rows = _social.GetLeaderboard(selfEmail, selfName, _points.GetBalance());

        var rank = 1;
        foreach (var row in rows)
        {
            var medal = rank switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => $"#{rank}",
            };

            var me = row.IsMe ? $" ({T("you")})" : "";
            League.Add(new FriendsRankRow
            {
                Rank = medal,
                Name = $"{row.DisplayName}{me}",
                Detail = $"XP: {row.WeeklyXp} · 🔥 {row.StreakDays}",
            });
            rank++;
        }
    }

    private static string DisplayNameFromEmail(string email)
    {
        var normalized = (email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || !normalized.Contains('@'))
            return "User";

        var local = normalized.Split('@')[0].Trim();
        if (string.IsNullOrWhiteSpace(local))
            return "User";

        var compact = local.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ').Trim();
        if (string.IsNullOrWhiteSpace(compact))
            compact = local;

        return char.ToUpper(compact[0]) + compact[1..];
    }

    private static string BuildHandle(string displayName)
    {
        var source = (displayName ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(source))
            return "@user";

        var chars = source
            .Where(ch => char.IsLetterOrDigit(ch) || ch == '.' || ch == '_')
            .ToArray();

        var normalized = new string(chars).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = "user";

        return $"@{normalized}";
    }

    private static string T(string key) => LocalizationService.T(key);
}

public class FriendsInviteRow
{
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Handle { get; set; } = "@user";
    public string Email { get; set; } = "";
    public string Status { get; set; } = "";
    public string Badge { get; set; } = "";
    public bool IsPending { get; set; }
    public bool IsFriend => !IsPending;
}

public class FriendsRankRow
{
    public string Rank { get; set; } = "";
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";
}

public class FriendSearchRow
{
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Handle { get; set; } = "@user";
}

public class IncomingInviteRow
{
    public string InviteId { get; set; } = "";
    public string InviterUserId { get; set; } = "";
    public string InviterEmail { get; set; } = "";
    public string InviterDisplay { get; set; } = "";
    public string InviterHandle { get; set; } = "@user";
}
