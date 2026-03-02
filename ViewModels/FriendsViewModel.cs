using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel.DataTransfer;
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
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string selectedTab = "friends";
    [ObservableProperty] private string friendsSearchQuery = "";

    public ObservableCollection<FriendsInviteRow> Friends { get; } = new();
    public ObservableCollection<FriendsInviteRow> AcceptedFriends { get; } = new();
    public ObservableCollection<FriendsInviteRow> OutgoingInvites { get; } = new();
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
    public string TabFriendsText => T("friends_tab_friends");
    public string TabRequestsText => T("friends_tab_requests");
    public string TabSuggestionsText => T("friends_tab_suggestions");
    public string ShareAppText => T("share_app_button");
    public string IncomingSectionText => T("incoming_invites_title");
    public string OutgoingSectionText => T("outgoing_invites_title");
    public string SuggestionsHintText => T("suggestions_hint");
    public string FriendsSearchLocalPlaceholder => T("friends_local_search_placeholder");
    public string EmptyRequestsText => T("friends_requests_empty");
    public string EmptySuggestionsText => T("friends_suggestions_empty");
    public string EmptyFriendsHelpText => T("friends_empty_help");
    public string AcceptAllText => T("accept_all");
    public string DeclineAllText => T("decline_all");
    public string SyncContactsText => T("sync_contacts");
    public string InviteLinkText => T("invite_link");
    public string ShowQrText => T("show_qr");
    public string RequestToConfirmText => T("request_to_confirm");
    public string RequestSentStateText => T("request_sent_state");

    public bool IsFriendsTab => string.Equals(SelectedTab, "friends", StringComparison.OrdinalIgnoreCase);
    public bool IsRequestsTab => string.Equals(SelectedTab, "requests", StringComparison.OrdinalIgnoreCase);
    public bool IsSuggestionsTab => string.Equals(SelectedTab, "suggestions", StringComparison.OrdinalIgnoreCase);
    public bool ShowSuggestionsUi => false;
    public bool HasIncomingInvites => IncomingInvites.Count > 0;
    public bool HasOutgoingInvites => OutgoingInvites.Count > 0;
    public bool HasRequestItems => HasIncomingInvites || HasOutgoingInvites;
    public bool HasSuggestions => SearchResults.Count > 0;
    public bool HasAcceptedFriends => AcceptedFriends.Count > 0;

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
        OnPropertyChanged(nameof(TabFriendsText));
        OnPropertyChanged(nameof(TabRequestsText));
        OnPropertyChanged(nameof(TabSuggestionsText));
        OnPropertyChanged(nameof(ShareAppText));
        OnPropertyChanged(nameof(IncomingSectionText));
        OnPropertyChanged(nameof(OutgoingSectionText));
        OnPropertyChanged(nameof(SuggestionsHintText));
        OnPropertyChanged(nameof(FriendsSearchLocalPlaceholder));
        OnPropertyChanged(nameof(EmptyRequestsText));
        OnPropertyChanged(nameof(EmptySuggestionsText));
        OnPropertyChanged(nameof(EmptyFriendsHelpText));
        OnPropertyChanged(nameof(AcceptAllText));
        OnPropertyChanged(nameof(DeclineAllText));
        OnPropertyChanged(nameof(SyncContactsText));
        OnPropertyChanged(nameof(InviteLinkText));
        OnPropertyChanged(nameof(ShowQrText));
        OnPropertyChanged(nameof(ShowSuggestionsUi));
        OnPropertyChanged(nameof(RequestToConfirmText));
        OnPropertyChanged(nameof(RequestSentStateText));
        await ReloadAsync();
    }

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsFriendsTab));
        OnPropertyChanged(nameof(IsRequestsTab));
        OnPropertyChanged(nameof(IsSuggestionsTab));
    }

    partial void OnFriendsSearchQueryChanged(string value)
    {
        ApplyFriendsFilter();
    }

    [RelayCommand]
    private void ShowFriendsTab() => SelectedTab = "friends";

    [RelayCommand]
    private void ShowRequestsTab() => SelectedTab = "requests";

    [RelayCommand]
    private void ShowSuggestionsTab() => SelectedTab = "suggestions";

    [RelayCommand]
    private async Task ShareApp()
    {
        var shareText = T("share_app_message");
        var title = T("share_app_title");

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Text = shareText,
            Title = title,
            Subject = title,
        });
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task Invite()
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(InviteEmail) || !InviteEmail.Contains('@'))
        {
            StatusText = T("invite_invalid_email");
            return;
        }

        var email = InviteEmail.Trim();
        if (IsCurrentUserEmail(email))
        {
            StatusText = T("invite_self_not_allowed");
            return;
        }

        try
        {
            IsBusy = true;
            var backendOk = await _sync.TryInviteFriendAsync(email);
            InviteEmail = "";
            StatusText = backendOk ? T("invite_sent") : T("invite_send_failed");
            await ReloadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddBuddy()
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(InviteEmail) || !InviteEmail.Contains('@'))
        {
            StatusText = T("invite_invalid_email");
            return;
        }

        var email = InviteEmail.Trim();
        if (IsCurrentUserEmail(email))
        {
            StatusText = T("invite_self_not_allowed");
            return;
        }

        try
        {
            IsBusy = true;
            var ok = await _sync.TryInviteFriendAsync(email);
            InviteEmail = "";
            StatusText = ok ? T("invite_sent") : T("invite_send_failed");
            await ReloadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchUsers()
    {
        if (IsBusy)
            return;

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

        try
        {
            IsBusy = true;
            var knownEmails = Friends
                .Select(x => (x.Email ?? "").Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var invite in IncomingInvites)
            {
                var email = (invite.InviterEmail ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(email))
                    knownEmails.Add(email);
            }

            var users = await _sync.SearchFriendUsersAsync(query, limit: 20);
            foreach (var user in users)
            {
                var email = (user.email ?? "").Trim();
                if (string.IsNullOrWhiteSpace(email))
                    continue;

                if (IsCurrentUserEmail(email) || knownEmails.Contains(email))
                    continue;

                SearchResults.Add(new FriendSearchRow
                {
                    UserId = (user.user_id ?? "").Trim(),
                    Email = email,
                    DisplayName = string.IsNullOrWhiteSpace(user.display_name) ? email.Split('@')[0] : user.display_name,
                    Handle = BuildHandle(string.IsNullOrWhiteSpace(user.display_name) ? email.Split('@')[0] : user.display_name),
                    NutritionHint = T("friend_suggestion_nutrition_hint"),
                });
            }
        }
        finally
        {
            IsBusy = false;
        }

        StatusText = SearchResults.Count == 0 ? T("friend_search_empty") : T("friend_search_success");
        SelectedTab = "suggestions";
    }

    [RelayCommand]
    private async Task InviteUser(FriendSearchRow? row)
    {
        if (IsBusy)
            return;

        if (row == null || string.IsNullOrWhiteSpace(row.Email))
            return;

        var email = row.Email.Trim();
        if (IsCurrentUserEmail(email))
        {
            StatusText = T("invite_self_not_allowed");
            return;
        }

        try
        {
            IsBusy = true;
            var ok = await _sync.TryInviteFriendAsync(email);
            StatusText = ok ? T("invite_sent") : T("invite_send_failed");
            await ReloadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Accept(FriendsInviteRow? row)
    {
        if (row == null) return;
        StatusText = row.IsPending ? T("invite_accept_failed") : T("friend_accepted");
    }

    [RelayCommand]
    private async Task Remove(FriendsInviteRow? row)
    {
        if (row == null) return;

        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            if (row.IsPending && !string.IsNullOrWhiteSpace(row.InviteId))
            {
                var ok = await _sync.TryDeleteInviteAsync(row.InviteId);
                StatusText = ok ? T("friend_invite_cancelled") : T("friend_invite_cancel_failed");
                await ReloadAsync();
                return;
            }

            _social.Remove(row.Email);
            StatusText = T("friend_removed");
            await ReloadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AcceptIncoming(IncomingInviteRow? row)
    {
        if (IsBusy)
            return;

        if (row == null || string.IsNullOrWhiteSpace(row.InviteId))
            return;

        try
        {
            IsBusy = true;
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
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AcceptAllIncoming()
    {
        if (IsBusy || IncomingInvites.Count == 0)
            return;

        IsBusy = true;
        try
        {
            var success = 0;
            foreach (var invite in IncomingInvites.ToList())
            {
                if (string.IsNullOrWhiteSpace(invite.InviteId))
                    continue;

                if (await _sync.TryAcceptInviteAsync(invite.InviteId))
                    success++;
            }

            StatusText = success > 0 ? T("friend_accepted") : T("invite_accept_failed");
            await ReloadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeclineAllIncoming()
    {
        if (IsBusy || IncomingInvites.Count == 0)
            return;

        IsBusy = true;
        try
        {
            var success = 0;
            foreach (var invite in IncomingInvites.ToList())
            {
                if (string.IsNullOrWhiteSpace(invite.InviteId))
                    continue;

                if (await _sync.TryDeclineInviteAsync(invite.InviteId))
                    success++;
            }

            StatusText = success > 0 ? T("invite_declined") : T("invite_decline_failed");
            await ReloadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SyncContacts()
    {
        await Application.Current!.MainPage!.DisplayAlert(T("friends_title"), T("contacts_permission_explainer"), "OK");
    }

    [RelayCommand]
    private async Task InviteByLink()
    {
        await Application.Current!.MainPage!.DisplayAlert(T("friends_title"), T("invite_link_hint"), "OK");
    }

    [RelayCommand]
    private async Task ShowQr()
    {
        await Application.Current!.MainPage!.DisplayAlert(T("friends_title"), T("qr_hint"), "OK");
    }

    [RelayCommand]
    private async Task DeclineIncoming(IncomingInviteRow? row)
    {
        if (IsBusy)
            return;

        if (row == null || string.IsNullOrWhiteSpace(row.InviteId))
            return;

        try
        {
            IsBusy = true;
            var ok = await _sync.TryDeclineInviteAsync(row.InviteId);
            StatusText = ok ? T("invite_declined") : T("invite_decline_failed");
            await ReloadAsync();
        }
        finally
        {
            IsBusy = false;
        }
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

        var otherUserId = (row.UserId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(otherUserId))
            otherUserId = await ResolveOtherUserIdByEmailAsync(row.Email);

        if (string.IsNullOrWhiteSpace(otherUserId))
        {
            await Application.Current!.MainPage!.DisplayAlert(
                string.Format(T("friend_stories_title"), row.DisplayName),
                T("friend_no_stories"),
                "OK");
            return;
        }

        if (_services.GetService(typeof(Pages.StoriesPage)) is not Pages.StoriesPage storiesPage)
            return;

        if (storiesPage.BindingContext is not StoriesViewModel storiesVm)
            return;

        storiesVm.ConfigureAuthorFilter(otherUserId, row.DisplayName);
        await Shell.Current.Navigation.PushAsync(storiesPage);
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
        var outgoingByEmail = new Dictionary<string, OutgoingInviteDto>(StringComparer.OrdinalIgnoreCase);
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

            var outgoing = await _sync.GetOutgoingInvitesAsync();
            foreach (var invite in outgoing)
            {
                var email = (invite.invitee_email ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(email))
                    continue;

                if (!string.Equals((invite.status ?? "").Trim(), "pending", StringComparison.OrdinalIgnoreCase))
                    continue;

                outgoingByEmail[email] = invite;
            }
        }

        Friends.Clear();
        var knownEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in outgoingByEmail)
        {
            var pending = true;
            var email = pair.Key;
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
                InviteId = pair.Value.id,
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
                InviteId = "",
                Status = T("status_friend"),
                Badge = "🟢",
                IsPending = false,
            });
        }

        if (!identityOk)
        {
            foreach (var item in _social.GetInvites())
            {
                var pending = string.Equals(item.Status, "pending", StringComparison.OrdinalIgnoreCase);
                var email = (item.Email ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(email) || Friends.Any(x => string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var displayName = DisplayNameFromEmail(email);
                Friends.Add(new FriendsInviteRow
                {
                    Email = email,
                    DisplayName = displayName,
                    Handle = BuildHandle(displayName),
                    UserId = "",
                    InviteId = "",
                    Status = pending ? T("status_pending") : T("status_friend"),
                    Badge = pending ? "🟡" : "🟢",
                    IsPending = pending,
                });
            }
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

        ApplyFriendsFilter();
        RebuildOutgoingInvites();
        OnPropertyChanged(nameof(HasIncomingInvites));
        OnPropertyChanged(nameof(HasOutgoingInvites));
        OnPropertyChanged(nameof(HasRequestItems));
        OnPropertyChanged(nameof(HasAcceptedFriends));
        OnPropertyChanged(nameof(HasSuggestions));

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

    private static bool IsCurrentUserEmail(string email)
    {
        var mine = Preferences.Default.Get("profile_email", "").Trim().ToLowerInvariant();
        var other = (email ?? "").Trim().ToLowerInvariant();
        return !string.IsNullOrWhiteSpace(mine) && string.Equals(mine, other, StringComparison.Ordinal);
    }

    private static string T(string key) => LocalizationService.T(key);

    private void ApplyFriendsFilter()
    {
        AcceptedFriends.Clear();
        var q = (FriendsSearchQuery ?? "").Trim().ToLowerInvariant();

        foreach (var row in Friends.Where(x => !x.IsPending))
        {
            if (!string.IsNullOrWhiteSpace(q))
            {
                var inName = (row.DisplayName ?? "").ToLowerInvariant().Contains(q);
                var inHandle = (row.Handle ?? "").ToLowerInvariant().Contains(q);
                var inEmail = (row.Email ?? "").ToLowerInvariant().Contains(q);
                if (!inName && !inHandle && !inEmail)
                    continue;
            }

            AcceptedFriends.Add(row);
        }
    }

    private void RebuildOutgoingInvites()
    {
        OutgoingInvites.Clear();
        foreach (var row in Friends.Where(x => x.IsPending))
            OutgoingInvites.Add(row);
    }
}

public class FriendsInviteRow
{
    public string UserId { get; set; } = "";
    public string InviteId { get; set; } = "";
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
    public string NutritionHint { get; set; } = "";
}

public class IncomingInviteRow
{
    public string InviteId { get; set; } = "";
    public string InviterUserId { get; set; } = "";
    public string InviterEmail { get; set; } = "";
    public string InviterDisplay { get; set; } = "";
    public string InviterHandle { get; set; } = "@user";
}
