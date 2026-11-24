using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Xianpu.Desktop.Services;

namespace Xianpu.Desktop.ViewModels;

/// <summary>
/// Core workspace ViewModel managing library filtering, active song state, undo/redo commands, and practice state.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    public const string AllCategoriesLabel = "全部分类";

    public SongUndoManager UndoManager { get; } = new();
    public PracticeViewModel Practice { get; } = new();

    public ObservableCollection<SongModel> FilteredSongs { get; } = [];
    public ObservableCollection<string> CategoryFilterOptions { get; } = [AllCategoriesLabel];

    [ObservableProperty]
    private LibraryData _library = new();

    [ObservableProperty]
    private SongModel? _selectedSong;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private bool _favoritesOnly;

    [ObservableProperty]
    private string _selectedCategoryFilter = AllCategoriesLabel;

    [ObservableProperty]
    private string _libraryCountText = "0";

    [ObservableProperty]
    private string _libraryToggleText = "曲库 (0)";

    [ObservableProperty]
    private string _undoToolTip = "撤销上一步谱面编辑 (Ctrl+Z)";

    [ObservableProperty]
    private string _redoToolTip = "重做已撤销的编辑 (Ctrl+Y)";

    public event Action? UndoRequested;
    public event Action? RedoRequested;

    public bool CanUndo => UndoManager.CanUndo;
    public bool CanRedo => UndoManager.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => UndoRequested?.Invoke();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => RedoRequested?.Invoke();

    public void RefreshUndoState()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoToolTip = UndoManager.UndoCount > 0
            ? $"撤销上一步谱面编辑（Ctrl+Z，可撤销 {UndoManager.UndoCount} 步）"
            : "撤销上一步谱面编辑 (Ctrl+Z)";
        RedoToolTip = UndoManager.RedoCount > 0
            ? $"重做已撤销的编辑（Ctrl+Y，可重做 {UndoManager.RedoCount} 步）"
            : "重做已撤销的编辑 (Ctrl+Y)";
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    public void RefreshCategoryOptions()
    {
        var categories = new List<string> { AllCategoriesLabel };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AllCategoriesLabel };

        foreach (var cat in Library.Categories)
        {
            var trimmed = (cat ?? "").Trim();
            if (!string.IsNullOrEmpty(trimmed) && seen.Add(trimmed))
                categories.Add(trimmed);
        }

        foreach (var song in Library.Songs)
        {
            var trimmed = (song.Category ?? "").Trim();
            if (!string.IsNullOrEmpty(trimmed) && seen.Add(trimmed))
                categories.Add(trimmed);
        }

        CategoryFilterOptions.Clear();
        foreach (var item in categories)
            CategoryFilterOptions.Add(item);

        if (!seen.Contains(SelectedCategoryFilter))
            SelectedCategoryFilter = AllCategoriesLabel;
    }

    public List<SongModel> ComputeFilteredSongs()
    {
        var query = (SearchQuery ?? "").Trim();
        var category = SelectedCategoryFilter;

        var filtered = Library.Songs
            .Where(song => !FavoritesOnly || song.Favorite)
            .Where(song =>
                string.IsNullOrWhiteSpace(category) ||
                string.Equals(category, AllCategoriesLabel, StringComparison.Ordinal) ||
                string.Equals((song.Category ?? "").Trim(), category, StringComparison.OrdinalIgnoreCase))
            .Where(song =>
                string.IsNullOrWhiteSpace(query) ||
                (song.Title ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (song.SelectedKey ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (song.OriginalKey ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (song.Category ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                song.Lines.Any(line =>
                    (line.Text ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    line.AltLyrics.Any(alt => (alt ?? "").Contains(query, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(song => song.Favorite)
            .ThenByDescending(song => song.UpdatedAt)
            .ToList();

        FilteredSongs.Clear();
        foreach (var song in filtered)
            FilteredSongs.Add(song);

        LibraryCountText = Library.Songs.Count.ToString();
        LibraryToggleText = $"曲库 ({Library.Songs.Count})";
        return filtered;
    }

    public static SongModel CreateDuplicateSong(SongModel source) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Title = string.IsNullOrWhiteSpace(source.Title) ? "未命名歌曲 (副本)" : source.Title + " (副本)",
        OriginalKey = source.OriginalKey,
        SelectedKey = source.SelectedKey,
        Capo = source.Capo,
        Bpm = source.Bpm,
        Category = source.Category,
        Favorite = source.Favorite,
        ChordDisplayMode = source.ChordDisplayMode,
        ColumnMode = source.ColumnMode,
        ShowChordGuide = source.ShowChordGuide,
        ShowTabSections = source.ShowTabSections,
        UpdatedAt = DateTime.Now,
        Lines = new ObservableCollection<LyricLine>(source.Lines.Select(line => line.Clone())),
        TabSections = new ObservableCollection<TabSection>(source.TabSections.Select(t => t.Clone()))
    };
}
