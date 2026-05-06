using NutritionTracker.Pages;
using Microsoft.Maui.Controls.Shapes;

namespace NutritionTracker.Controls;

public sealed class StravaTopBar : ContentView
{
    public static readonly BindableProperty TitleTextProperty = BindableProperty.Create(
        nameof(TitleText),
        typeof(string),
        typeof(StravaTopBar),
        defaultValue: "",
        propertyChanged: OnTitleTextChanged);

    private readonly Label _titleLabel;
    private readonly Image _profileImage;

    public string TitleText
    {
        get => (string)GetValue(TitleTextProperty);
        set => SetValue(TitleTextProperty, value);
    }

    public StravaTopBar()
    {
        _titleLabel = new Label
        {
            VerticalOptions = LayoutOptions.Center,
            Style = (Style)Application.Current!.Resources["H2"],
            LineBreakMode = LineBreakMode.TailTruncation,
        };

        _profileImage = new Image
        {
            Aspect = Aspect.AspectFill,
            Source = "ic_profile.svg"
        };

        var profileBorder = new Border
        {
            WidthRequest = 42,
            HeightRequest = 42,
            StrokeShape = new RoundRectangle { CornerRadius = 21 },
            Style = (Style)Application.Current!.Resources["Card"],
            Padding = 0,
            Content = _profileImage
        };
        profileBorder.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await SafeGoToAsync("//profile"))
        });

        // Use icons that are present in Resources/Images to avoid blank placeholders.
        var recommendationBtn = BuildIconButton("ic_goals.svg", async () => await SafeGoToAsync(nameof(RecommendationsPage)));
        var messageBtn = BuildIconButton("ic_diary.svg", async () => await SafeGoToAsync("//friends"));
        var notificationBtn = BuildIconButton("ic_stories.svg", async () => await SafeGoToAsync("//stories"));

        var homeButton = BuildIconButton("ic_home.svg", async () => await NavigateHomeAsync());
        var actionsLayout = new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { recommendationBtn, messageBtn, notificationBtn }
        };

        var layout = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 10,
        };

        Grid.SetColumn(homeButton, 0);
        Grid.SetColumn(_titleLabel, 1);
        Grid.SetColumn(profileBorder, 2);
        Grid.SetColumn(actionsLayout, 3);

        layout.Children.Add(homeButton);
        layout.Children.Add(_titleLabel);
        layout.Children.Add(profileBorder);
        layout.Children.Add(actionsLayout);

        Content = layout;

        RefreshProfilePhoto();
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        RefreshProfilePhoto();
    }

    private static void OnTitleTextChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is StravaTopBar bar)
            bar._titleLabel.Text = (newValue as string) ?? "";
    }

    private void RefreshProfilePhoto()
    {
        var raw = Preferences.Default.Get("profile_picture", "").Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            _profileImage.Source = "ic_profile.svg";
            return;
        }

        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            _profileImage.Source = ImageSource.FromUri(uri);
            return;
        }

        _profileImage.Source = "ic_profile.svg";
    }

    private static Task NavigateHomeAsync()
    {
        if (Shell.Current is not null)
            return Shell.Current.GoToAsync("//dashboard");

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        return page?.Navigation?.PopToRootAsync() ?? Task.CompletedTask;
    }

    private static Task SafeGoToAsync(string route)
    {
        if (Shell.Current is not null)
            return Shell.Current.GoToAsync(route);

        return Task.CompletedTask;
    }

    private static Button BuildIconButton(string icon, Func<Task> onClick, bool enabled = true)
    {
        var btn = new Button
        {
            Text = "",
            ImageSource = icon,
            Style = (Style)Application.Current!.Resources["IconButton"],
            IsEnabled = enabled
        };
        btn.Clicked += async (_, _) => await onClick();
        return btn;
    }
}
