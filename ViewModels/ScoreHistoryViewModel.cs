using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NutritionTracker.Services;

namespace NutritionTracker.ViewModels;

public partial class ScoreHistoryViewModel : ObservableObject
{
    private readonly BackendSyncService _sync;

    [ObservableProperty] private bool isLoading;

    public ObservableCollection<ScoreHistoryItem> Items { get; } = new();

    public string TitleText => LocalizationService.T("score_history_title");
    public string SubtitleText => LocalizationService.T("score_history_subtitle");
    public string EmptyText => LocalizationService.T("score_history_empty");
    public string LoadingText => LocalizationService.T("main_loading");
    public string RefreshText => LocalizationService.T("refresh");
    public bool HasItems => Items.Count > 0;
    public bool NoItems => !HasItems;

    public ScoreHistoryViewModel(BackendSyncService sync)
    {
        _sync = sync;
    }

    public async Task LoadAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        try
        {
            OnPropertyChanged(nameof(TitleText));
            OnPropertyChanged(nameof(SubtitleText));
            OnPropertyChanged(nameof(EmptyText));
            OnPropertyChanged(nameof(LoadingText));
            OnPropertyChanged(nameof(RefreshText));

            var rows = await _sync.GetGamificationEventsAsync(limit: 120);
            var explanations = rows
                .Where(x => string.Equals((x.event_type ?? "").Trim(), "meal_score_explanation", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.created_at_utc)
                .ToList();

            Items.Clear();
            foreach (var row in explanations)
            {
                var date = row.created_at_utc.ToLocalTime();
                var title = string.IsNullOrWhiteSpace(row.title)
                    ? LocalizationService.T("score_history_item_default_title")
                    : row.title.Trim();

                Items.Add(new ScoreHistoryItem
                {
                    Title = title,
                    Message = (row.message ?? "").Trim(),
                    DateText = date.ToString("dd/MM/yyyy HH:mm"),
                });
            }

            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(NoItems));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync();
}

public sealed class ScoreHistoryItem
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string DateText { get; set; } = "";
}
