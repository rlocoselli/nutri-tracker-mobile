using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class StoriesViewModel : ObservableObject
{
    private readonly BackendSyncService _sync;
    private readonly List<StoryFeedItem> _allItems = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string searchQuery = "";
    [ObservableProperty] private string activeAuthorUserId = "";
    public ObservableCollection<StoryFeedItem> Items { get; } = new();
    public ObservableCollection<StoryBubbleItem> StoryBubbles { get; } = new();

    public bool HasItems => Items.Count > 0;
    public bool HasBubbles => StoryBubbles.Count > 0;
    public bool IsEmpty => !IsBusy && Items.Count == 0;
    public bool HasActiveAuthorFilter => !string.IsNullOrWhiteSpace(ActiveAuthorUserId);

    public string PageTitle => T("stories_tab_title");
    public string HeaderTitle => T("stories_header_title");
    public string HeaderSubtitle => T("stories_header_subtitle");
    public string RefreshText => T("refresh");
    public string SearchPlaceholder => T("stories_search_placeholder");
    public string ClearFilterText => T("stories_clear_filter");
    public string EmptyText => T("stories_empty");
    public string LikeText => T("story_like");
    public string UnlikeText => T("story_unlike");
    public string CommentText => T("story_comment");
    public string CommentPlaceholder => T("story_comment_placeholder");
    public string SendText => T("send");
    public string ViewMessagesText => T("story_view_messages");
    public string ViewAllCommentsText => T("story_view_all_comments");

    public StoriesViewModel(BackendSyncService sync)
    {
        _sync = sync;
    }

    public async Task LoadAsync()
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderSubtitle));
        OnPropertyChanged(nameof(RefreshText));
        OnPropertyChanged(nameof(SearchPlaceholder));
        OnPropertyChanged(nameof(ClearFilterText));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(LikeText));
        OnPropertyChanged(nameof(UnlikeText));
        OnPropertyChanged(nameof(CommentText));
        OnPropertyChanged(nameof(CommentPlaceholder));
        OnPropertyChanged(nameof(SendText));
        OnPropertyChanged(nameof(ViewMessagesText));
        OnPropertyChanged(nameof(ViewAllCommentsText));
        OnPropertyChanged(nameof(HasBubbles));
        OnPropertyChanged(nameof(HasActiveAuthorFilter));
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        OnPropertyChanged(nameof(IsEmpty));

        try
        {
            Items.Clear();
            StoryBubbles.Clear();
            _allItems.Clear();

            var token = Preferences.Default.Get("auth_id_token", "");
            var identityOk = await _sync.EnsureBackendIdentityAsync(token);
            if (!identityOk)
                return;

            var meUserId = Preferences.Default.Get("backend_user_id", "").Trim();
            var myProfileName = Preferences.Default.Get("profile_name", "").Trim();
            var myProfilePicture = Preferences.Default.Get("profile_picture", "").Trim();

            var feed = await _sync.GetFriendsFeedAsync(days: 14, limit: 120);
            var ordered = feed
                .Where(x => !string.IsNullOrWhiteSpace(x.photo_url))
                .OrderByDescending(x => x.date_utc)
                .ToList();

            foreach (var row in ordered)
            {
                var photo = StoriesPhotoSourceHelper.Build(row.photo_url);
                if (photo == null)
                    continue;

                var avatar = ResolveAvatar(row, meUserId, myProfilePicture);
                var item = new StoryFeedItem
                {
                    MealId = row.meal_id,
                    AuthorUserId = row.user_id,
                    Author = ResolveAuthor(row, meUserId, myProfileName),
                    PostedAtText = row.date_utc.ToLocalTime().ToString("dd/MM HH:mm"),
                    Caption = string.IsNullOrWhiteSpace(row.raw_text) ? T("story_meal") : row.raw_text,
                    NutritionText = $"{Math.Round(row.total_calories)} kcal · P {Math.Round(row.total_protein_g)}g · C {Math.Round(row.total_carbs_g)}g",
                    QualityText = string.IsNullOrWhiteSpace(row.quality_label) ? "" : row.quality_label,
                    LikeCount = row.like_count,
                    CommentCount = row.comment_count,
                    IsLiked = row.liked_by_me,
                    PhotoSource = photo,
                    AvatarSource = avatar,
                };

                var preview = await _sync.GetStoryCommentsAsync(row.meal_id, limit: 3);
                foreach (var comment in preview)
                {
                    item.Comments.Add(new StoryCommentLine
                    {
                        AuthorName = NormalizeAuthor(comment.author_name),
                        Text = comment.text?.Trim() ?? "",
                    });
                }

                item.HasMoreComments = row.comment_count > item.Comments.Count;
                _allItems.Add(item);
            }

            ApplyFilters();
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasBubbles));
        }
    }

    [RelayCommand]
    private async Task ToggleLike(StoryFeedItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.MealId) || IsBusy)
            return;

        var (liked, likeCount) = await _sync.ToggleStoryLikeAsync(item.MealId);
        item.IsLiked = liked;
        item.LikeCount = likeCount;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task AddComment(StoryFeedItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.MealId) || IsBusy)
            return;

        var text = (item.CommentDraft ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        var created = await _sync.AddStoryCommentAsync(item.MealId, text);
        if (created == null)
            return;

        item.CommentDraft = "";
        item.Comments.Add(new StoryCommentLine
        {
            AuthorName = NormalizeAuthor(created.author_name),
            Text = created.text?.Trim() ?? "",
        });
        item.CommentCount++;
        item.HasMoreComments = false;
    }

    [RelayCommand]
    private async Task ViewComments(StoryFeedItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.MealId) || IsBusy)
            return;

        var comments = await _sync.GetStoryCommentsAsync(item.MealId, limit: 80);
        item.Comments.Clear();
        foreach (var comment in comments.OrderBy(x => x.created_at_utc))
        {
            item.Comments.Add(new StoryCommentLine
            {
                AuthorName = NormalizeAuthor(comment.author_name),
                Text = comment.text?.Trim() ?? "",
            });
        }

        item.HasMoreComments = false;
    }

    [RelayCommand]
    private void SelectBubble(StoryBubbleItem? bubble)
    {
        if (bubble == null)
            return;

        ActiveAuthorUserId = string.Equals(ActiveAuthorUserId, bubble.AuthorUserId, StringComparison.OrdinalIgnoreCase)
            ? ""
            : bubble.AuthorUserId;

        ApplyFilters();
    }

    [RelayCommand]
    private void ClearFilter()
    {
        SearchQuery = "";
        ActiveAuthorUserId = "";
        ApplyFilters();
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var query = (SearchQuery ?? "").Trim().ToLowerInvariant();
        var authorFilter = (ActiveAuthorUserId ?? "").Trim();

        var filtered = _allItems
            .Where(item =>
            {
                var matchAuthor = string.IsNullOrWhiteSpace(authorFilter) ||
                                  string.Equals((item.AuthorUserId ?? "").Trim(), authorFilter, StringComparison.OrdinalIgnoreCase);

                if (!matchAuthor)
                    return false;

                if (string.IsNullOrWhiteSpace(query))
                    return true;

                return (item.Author ?? "").ToLowerInvariant().Contains(query)
                       || (item.Caption ?? "").ToLowerInvariant().Contains(query)
                       || (item.NutritionText ?? "").ToLowerInvariant().Contains(query);
            })
            .ToList();

        Items.Clear();
        foreach (var item in filtered)
            Items.Add(item);

        BuildStoryBubbles();

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasBubbles));
        OnPropertyChanged(nameof(HasActiveAuthorFilter));
    }

    private void BuildStoryBubbles()
    {
        StoryBubbles.Clear();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _allItems)
        {
            var key = !string.IsNullOrWhiteSpace(item.AuthorUserId)
                ? item.AuthorUserId.Trim()
                : item.Author.Trim();

            if (string.IsNullOrWhiteSpace(key) || seen.Contains(key))
                continue;

            seen.Add(key);
            StoryBubbles.Add(new StoryBubbleItem
            {
                AuthorUserId = key,
                Author = item.Author,
                AvatarSource = item.AvatarSource,
                IsActive = string.Equals((ActiveAuthorUserId ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase),
            });
        }
    }

    private string ResolveAuthor(BackendStory story, string meUserId, string myProfileName)
    {
        if (!string.IsNullOrWhiteSpace(meUserId) && string.Equals(story.user_id?.Trim(), meUserId, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(myProfileName))
                return myProfileName;
        }

        var name = (story.display_name ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, "new user", StringComparison.OrdinalIgnoreCase))
            return name;

        var email = (story.author_email ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(email) && email.Contains('@'))
        {
            var localPart = email.Split('@')[0].Trim();
            if (!string.IsNullOrWhiteSpace(localPart))
                return localPart;
        }

        return T("story_default_author");
    }

    private string NormalizeAuthor(string? raw)
    {
        var name = (raw ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, "new user", StringComparison.OrdinalIgnoreCase))
            return name;

        if (!string.IsNullOrWhiteSpace(name) && name.Contains('@'))
        {
            var local = name.Split('@')[0].Trim();
            if (!string.IsNullOrWhiteSpace(local))
                return local;
        }

        return T("story_default_author");
    }

    private ImageSource ResolveAvatar(BackendStory story, string meUserId, string myProfilePicture)
    {
        if (!string.IsNullOrWhiteSpace(meUserId) && string.Equals(story.user_id?.Trim(), meUserId, StringComparison.OrdinalIgnoreCase))
        {
            var mine = StoriesPhotoSourceHelper.Build(myProfilePicture);
            if (mine != null)
                return mine;
        }

        return StoriesPhotoSourceHelper.Build(story.picture_url) ?? ImageSource.FromFile("ic_profile.svg");
    }

    private static string T(string key)
    {
        var lang = Preferences.Default.Get("app_lang", "fr");
        static string L(string lang, string fr, string en, string pt, string es) => lang switch
        {
            "en" => en,
            "pt" => pt,
            "es" => es,
            _ => fr,
        };

        return key switch
        {
            "stories_tab_title" => L(lang, "Stories", "Stories", "Stories", "Stories"),
            "stories_header_title" => L(lang, "Stories nutrition", "Nutrition stories", "Stories de nutrição", "Stories de nutrición"),
            "stories_header_subtitle" => L(lang, "Photos de vos repas et de vos amis, du plus récent au plus ancien.", "Your meal photos and friends' photos, newest first.", "Fotos das suas refeições e dos amigos, do mais recente ao mais antigo.", "Fotos de tus comidas y de tus amigos, de más reciente a más antigua."),
            "stories_search_placeholder" => L(lang, "Rechercher des amis, repas, calories...", "Search friends, meals, calories...", "Buscar amigos, refeições, calorias...", "Buscar amigos, comidas, calorías..."),
            "stories_clear_filter" => L(lang, "Effacer", "Clear", "Limpar", "Limpiar"),
            "stories_empty" => L(lang, "Aucune story photo pour le moment.", "No photo stories yet.", "Nenhuma story com foto ainda.", "Aún no hay stories con foto."),
            "refresh" => L(lang, "Rafraîchir", "Refresh", "Atualizar", "Actualizar"),
            "story_default_author" => L(lang, "Utilisateur", "User", "Usuário", "Usuario"),
            "story_meal" => L(lang, "Repas", "Meal", "Refeição", "Comida"),
            "story_like" => L(lang, "J'aime", "Like", "Curtir", "Me gusta"),
            "story_unlike" => L(lang, "Aimé", "Liked", "Curtido", "Te gusta"),
            "story_comment" => L(lang, "Commenter", "Comment", "Comentar", "Comentar"),
            "story_comment_placeholder" => L(lang, "Écrire un commentaire", "Write a comment", "Escreva um comentário", "Escribe un comentario"),
            "story_view_messages" => L(lang, "Voir messages", "View messages", "Ver mensagens", "Ver mensajes"),
            "story_view_all_comments" => L(lang, "Voir tous les commentaires", "View all comments", "Ver todos comentários", "Ver todos los comentarios"),
            "story_no_messages" => L(lang, "Aucun message pour cette photo.", "No messages for this photo.", "Nenhuma mensagem para esta foto.", "No hay mensajes para esta foto."),
            "saved_title" => L(lang, "Enregistré", "Saved", "Salvo", "Guardado"),
            "send" => L(lang, "Envoyer", "Send", "Enviar", "Enviar"),
            "cancel" => L(lang, "Annuler", "Cancel", "Cancelar", "Cancelar"),
            _ => key,
        };
    }
}

public class StoryFeedItem
{
    public string MealId { get; set; } = "";
    public string AuthorUserId { get; set; } = "";
    public string Author { get; set; } = "";
    public string PostedAtText { get; set; } = "";
    public string Caption { get; set; } = "";
    public string NutritionText { get; set; } = "";
    public string QualityText { get; set; } = "";
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public bool IsLiked { get; set; }
    public string CommentDraft { get; set; } = "";
    public ObservableCollection<StoryCommentLine> Comments { get; } = new();
    public bool HasMoreComments { get; set; }
    public bool HasComments => Comments.Count > 0;
    public ImageSource PhotoSource { get; set; } = ImageSource.FromFile("ic_profile.svg");
    public ImageSource AvatarSource { get; set; } = ImageSource.FromFile("ic_profile.svg");
    public bool HasPhoto => PhotoSource != null;
}

public class StoryCommentLine
{
    public string AuthorName { get; set; } = "";
    public string Text { get; set; } = "";
}

public class StoryBubbleItem
{
    public string AuthorUserId { get; set; } = "";
    public string Author { get; set; } = "";
    public ImageSource AvatarSource { get; set; } = ImageSource.FromFile("ic_profile.svg");
    public bool IsActive { get; set; }
}

internal static class StoriesPhotoSourceHelper
{
    public static ImageSource? Build(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var value = raw.Trim();

        if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = value.IndexOf(',');
            if (commaIndex > 0 && commaIndex < value.Length - 1)
            {
                var base64 = value[(commaIndex + 1)..];
                try
                {
                    var bytes = Convert.FromBase64String(base64);
                    return ImageSource.FromStream(() => new MemoryStream(bytes));
                }
                catch
                {
                    return null;
                }
            }
        }

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return ImageSource.FromUri(uri);
        }

        if (File.Exists(value))
            return ImageSource.FromFile(value);

        return null;
    }
}
