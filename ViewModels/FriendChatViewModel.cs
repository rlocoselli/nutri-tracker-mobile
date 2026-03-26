using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class FriendChatViewModel : ObservableObject
{
    private readonly BackendSyncService _sync;

    [ObservableProperty] private string friendUserId = "";
    [ObservableProperty] private string friendDisplayName = "";
    [ObservableProperty] private string friendEmail = "";
    [ObservableProperty] private string messageText = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusText = "";

    public ObservableCollection<ChatBubbleRow> Messages { get; } = new();

    public string PageTitle => string.IsNullOrWhiteSpace(FriendDisplayName) ? LocalizationService.T("friend_chat_title") : FriendDisplayName;
    public string InputPlaceholder => LocalizationService.T("friend_chat_placeholder");
    public string SendText => LocalizationService.T("send");
    public string RefreshText => LocalizationService.T("refresh");
    public string SubtitleText => string.IsNullOrWhiteSpace(FriendDisplayName)
        ? LocalizationService.T("friend_chat_subtitle_default")
        : string.Format(LocalizationService.T("friend_chat_subtitle"), FriendDisplayName);

    public FriendChatViewModel(BackendSyncService sync)
    {
        _sync = sync;
    }

    public async Task InitializeAsync(string otherUserId, string displayName, string email)
    {
        FriendUserId = (otherUserId ?? "").Trim();
        FriendDisplayName = (displayName ?? "").Trim();
        FriendEmail = (email ?? "").Trim();
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(InputPlaceholder));
        OnPropertyChanged(nameof(SendText));
        OnPropertyChanged(nameof(RefreshText));
        OnPropertyChanged(nameof(SubtitleText));
        await ReloadMessagesAsync();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await ReloadMessagesAsync();
    }

    [RelayCommand]
    private async Task Send()
    {
        if (IsBusy)
            return;

        var text = (MessageText ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(FriendUserId))
            return;

        IsBusy = true;
        try
        {
            var ok = await _sync.SendPrivateMessageAsync(FriendUserId, text);
            if (!ok)
            {
                StatusText = LocalizationService.T("friend_message_failed");
                return;
            }

            MessageText = "";
            StatusText = "";
            await ReloadMessagesAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ReloadMessagesAsync()
    {
        if (string.IsNullOrWhiteSpace(FriendUserId))
            return;

        var token = Preferences.Default.Get("auth_id_token", "");
        var identityOk = await _sync.EnsureBackendIdentityAsync(token);
        if (!identityOk)
        {
            StatusText = LocalizationService.T("friend_action_signin_needed");
            return;
        }

        var meUserId = Preferences.Default.Get("backend_user_id", "").Trim();
        var messages = await _sync.GetPrivateMessagesAsync(FriendUserId, limit: 120);

        Messages.Clear();
        foreach (var message in messages.OrderBy(x => x.created_at_utc))
        {
            var isMine = string.Equals((message.sender_user_id ?? "").Trim(), meUserId, StringComparison.OrdinalIgnoreCase);
            Messages.Add(new ChatBubbleRow
            {
                IsMine = isMine,
                MineVisible = isMine,
                OtherVisible = !isMine,
                Text = message.text,
                TimeText = message.created_at_utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                SenderText = isMine ? LocalizationService.T("you") : (string.IsNullOrWhiteSpace(FriendDisplayName) ? LocalizationService.T("friend_message") : FriendDisplayName),
            });
        }

        if (Messages.Count == 0)
            StatusText = LocalizationService.T("friend_chat_empty");
        else
            StatusText = "";

        // Track unread state for the friends list badge
        var latestFromFriend = messages
            .Where(m => !string.Equals((m.sender_user_id ?? "").Trim(), meUserId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.created_at_utc)
            .FirstOrDefault();
        if (latestFromFriend != null)
            Preferences.Default.Set($"chat_last_msg_{FriendUserId}", latestFromFriend.created_at_utc.Ticks);

        // Mark conversation as read using message timeline to avoid clock skew issues.
        var latestConversationTick = messages
            .OrderByDescending(m => m.created_at_utc)
            .Select(m => m.created_at_utc.Ticks)
            .FirstOrDefault();
        var readTick = latestConversationTick > 0 ? latestConversationTick : DateTime.UtcNow.Ticks;
        Preferences.Default.Set($"chat_last_read_{FriendUserId}", readTick);
    }
}

public class ChatBubbleRow
{
    public bool IsMine { get; set; }
    public bool MineVisible { get; set; }
    public bool OtherVisible { get; set; }
    public string SenderText { get; set; } = "";
    public string Text { get; set; } = "";
    public string TimeText { get; set; } = "";
}
