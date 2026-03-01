using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class StoriesViewModel : ObservableObject
{
    private readonly BackendSyncService _sync;

    [ObservableProperty] private bool isBusy;
    public ObservableCollection<StoryFeedItem> Items { get; } = new();

    public bool HasItems => Items.Count > 0;
    public bool IsEmpty => !IsBusy && Items.Count == 0;

    public string PageTitle => T("stories_tab_title");
    public string HeaderTitle => T("stories_header_title");
    public string HeaderSubtitle => T("stories_header_subtitle");
    public string RefreshText => T("refresh");
    public string EmptyText => T("stories_empty");

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
        OnPropertyChanged(nameof(EmptyText));
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
                Items.Add(new StoryFeedItem
                {
                    Author = ResolveAuthor(row, meUserId, myProfileName),
                    PostedAtText = row.date_utc.ToLocalTime().ToString("dd/MM HH:mm"),
                    Caption = string.IsNullOrWhiteSpace(row.raw_text) ? T("story_meal") : row.raw_text,
                    NutritionText = $"{Math.Round(row.total_calories)} kcal · P {Math.Round(row.total_protein_g)}g · C {Math.Round(row.total_carbs_g)}g",
                    QualityText = string.IsNullOrWhiteSpace(row.quality_label) ? "" : row.quality_label,
                    PhotoSource = photo,
                    AvatarSource = avatar,
                });
            }
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(IsEmpty));
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
            "stories_empty" => L(lang, "Aucune story photo pour le moment.", "No photo stories yet.", "Nenhuma story com foto ainda.", "Aún no hay stories con foto."),
            "refresh" => L(lang, "Rafraîchir", "Refresh", "Atualizar", "Actualizar"),
            "story_default_author" => L(lang, "Utilisateur", "User", "Usuário", "Usuario"),
            "story_meal" => L(lang, "Repas", "Meal", "Refeição", "Comida"),
            _ => key,
        };
    }
}

public class StoryFeedItem
{
    public string Author { get; set; } = "";
    public string PostedAtText { get; set; } = "";
    public string Caption { get; set; } = "";
    public string NutritionText { get; set; } = "";
    public string QualityText { get; set; } = "";
    public ImageSource PhotoSource { get; set; } = ImageSource.FromFile("ic_profile.svg");
    public ImageSource AvatarSource { get; set; } = ImageSource.FromFile("ic_profile.svg");
    public bool HasPhoto => PhotoSource != null;
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
