using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Xianpu.Desktop.Services;

namespace Xianpu.Desktop.ViewModels;

/// <summary>
/// Core workspace ViewModel managing library filtering, active song header state, undo/redo commands,
/// library CRUD commands via IDialogService, and practice state.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    public const string AllCategoriesLabel = "全部分类";

    private IDialogService? _dialogs;
    private bool _syncingBpmText;

    public SongUndoManager UndoManager { get; } = new();
    public PracticeViewModel Practice { get; } = new();

    public ObservableCollection<SongModel> FilteredSongs { get; } = [];
    public ObservableCollection<string> CategoryFilterOptions { get; } = [AllCategoriesLabel];

    [ObservableProperty]
    private LibraryData _library = new();

    [ObservableProperty]
    private SongModel? _selectedSong;

    [ObservableProperty]
    private string _bpmText = "";

    [ObservableProperty]
    private string _captionSongTitle = "· 吉他歌词和弦记谱";

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private bool _favoritesOnly;

    [ObservableProperty]
    private string _selectedCategoryFilter = AllCategoriesLabel;

    [ObservableProperty]
    private string _libraryCountText = "0";

    [ObservableProperty]
    private string _filteredCountDisplay = "0";

    [ObservableProperty]
    private string _libraryToggleText = "曲库 (0)";

    [ObservableProperty]
    private bool _isSearchEmpty = true;

    [ObservableProperty]
    private bool _isPracticeMode;

    [ObservableProperty]
    private bool _isTheoryMode;

    [ObservableProperty]
    private string _undoToolTip = "撤销上一步谱面编辑 (Ctrl+Z)";

    [ObservableProperty]
    private string _redoToolTip = "重做已撤销的编辑 (Ctrl+Y)";

    public event Action? UndoRequested;
    public event Action? RedoRequested;
    public event Action? FilterStateChanged;
    public event Action? SaveRequested;
    public event Action? FavoriteVisualRefreshRequested;
    public event Action? CategoryOptionsRefreshRequested;
    public event Action<SongModel>? SelectSongRequested;
    public event Action<int>? BpmChangedWhileEditing;
    public event Action<bool>? PracticeModeChanged;
    public event Action<bool>? TheoryModeChanged;

    public void AttachDialogService(IDialogService dialogs) => _dialogs = dialogs;

    public bool CanUndo => UndoManager.CanUndo;
    public bool CanRedo => UndoManager.CanRedo;

    partial void OnSearchQueryChanged(string value)
    {
        IsSearchEmpty = string.IsNullOrWhiteSpace(value);
        ComputeFilteredSongs();
        FilterStateChanged?.Invoke();
    }

    partial void OnFavoritesOnlyChanged(bool value)
    {
        ComputeFilteredSongs();
        FilterStateChanged?.Invoke();
    }

    partial void OnSelectedCategoryFilterChanged(string value)
    {
        ComputeFilteredSongs();
        FilterStateChanged?.Invoke();
    }

    partial void OnBpmTextChanged(string value)
    {
        if (_syncingBpmText || SelectedSong is null)
            return;

        var trimmed = (value ?? "").Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            SelectedSong.Bpm = 0;
            return;
        }

        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
            parsed is >= 30 and <= 240)
        {
            SelectedSong.Bpm = parsed;
            BpmChangedWhileEditing?.Invoke(parsed);
        }
    }

    public void SyncBpmTextFromSelectedSong()
    {
        _syncingBpmText = true;
        try
        {
            BpmText = SelectedSong is { Bpm: > 0 } ? SelectedSong.Bpm.ToString(CultureInfo.InvariantCulture) : "";
        }
        finally
        {
            _syncingBpmText = false;
        }
    }

    public void CommitBpmText()
    {
        if (SelectedSong is null) return;
        var trimmed = (BpmText ?? "").Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            SelectedSong.Bpm = 0;
        }
        else if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            var clamped = Math.Clamp(parsed, 30, 240);
            SelectedSong.Bpm = clamped;
            SyncBpmTextFromSelectedSong();
            BpmChangedWhileEditing?.Invoke(clamped);
        }
        else
        {
            SyncBpmTextFromSelectedSong();
        }
    }

    public void UpdateCaptionTitle()
    {
        var title = string.IsNullOrWhiteSpace(SelectedSong?.Title)
            ? "吉他歌词和弦记谱"
            : SelectedSong!.Title.Trim();
        CaptionSongTitle = $"· {title}";
    }

    partial void OnIsPracticeModeChanged(bool value)
    {
        PracticeModeChanged?.Invoke(value);
    }

    partial void OnIsTheoryModeChanged(bool value)
    {
        TheoryModeChanged?.Invoke(value);
    }

    [RelayCommand]
    private void EnterEditMode()
    {
        IsTheoryMode = false;
        IsPracticeMode = false;
    }

    [RelayCommand]
    private void EnterPracticeMode()
    {
        IsTheoryMode = false;
        IsPracticeMode = true;
    }

    [RelayCommand]
    private void TogglePracticeMode()
    {
        if (IsPracticeMode)
        {
            IsPracticeMode = false;
        }
        else
        {
            IsTheoryMode = false;
            IsPracticeMode = true;
        }
    }

    [RelayCommand]
    private void EnterTheoryMode()
    {
        IsPracticeMode = false;
        IsTheoryMode = true;
    }

    [RelayCommand]
    private void ToggleTheoryMode()
    {
        if (IsTheoryMode)
        {
            IsTheoryMode = false;
        }
        else
        {
            IsPracticeMode = false;
            IsTheoryMode = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => UndoRequested?.Invoke();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => RedoRequested?.Invoke();

    [RelayCommand]
    private void ShowAllSongs() => FavoritesOnly = false;

    [RelayCommand]
    private void ShowFavoriteSongs() => FavoritesOnly = true;

    [RelayCommand]
    private void ToggleFavorite()
    {
        if (SelectedSong is null) return;
        SelectedSong.Favorite = !SelectedSong.Favorite;
        SelectedSong.UpdatedAt = DateTime.Now;
        FavoriteVisualRefreshRequested?.Invoke();
        ComputeFilteredSongs();
        FilterStateChanged?.Invoke();
        SaveRequested?.Invoke();
        Practice.StatusMessage = SelectedSong.Favorite
            ? $"已收藏《{SelectedSong.Title}》。"
            : $"已取消收藏《{SelectedSong.Title}》。";
    }

    [RelayCommand]
    private void AddCategory()
    {
        if (_dialogs is null) return;
        var name = _dialogs.PromptSingleLine("新建分类", "请输入分类名称（例如：民谣、弹唱、指弹、演出）：");
        var trimmed = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return;

        if (!Library.Categories.Any(c => string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase)))
            Library.Categories.Add(trimmed);

        if (SelectedSong is not null)
        {
            SelectedSong.Category = trimmed;
            SelectedSong.UpdatedAt = DateTime.Now;
        }

        RefreshCategoryOptions();
        CategoryOptionsRefreshRequested?.Invoke();
        ComputeFilteredSongs();
        FilterStateChanged?.Invoke();
        SaveRequested?.Invoke();
        Practice.StatusMessage = $"已添加分类“{trimmed}”。";
    }

    [RelayCommand]
    private void RenameSelectedSong()
    {
        if (_dialogs is null || SelectedSong is null) return;
        var next = _dialogs.PromptSingleLine("重命名曲谱", "请输入新的歌曲名称：", SelectedSong.Title);
        var trimmed = (next ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return;

        SelectedSong.Title = trimmed;
        SelectedSong.UpdatedAt = DateTime.Now;
        UpdateCaptionTitle();
        ComputeFilteredSongs();
        FilterStateChanged?.Invoke();
        SaveRequested?.Invoke();
        Practice.StatusMessage = $"已重命名为《{trimmed}》。";
    }

    [RelayCommand]
    private void DeleteSelectedSong()
    {
        if (_dialogs is null || SelectedSong is null) return;
        var target = SelectedSong;
        if (!_dialogs.Confirm("删除曲谱", $"确定从本地曲库删除《{target.Title}》吗？删除后无法自动恢复（可在“设置与备份”中从历史快照找回）。", "确定删除", isDanger: true))
            return;

        Library.Songs.Remove(target);
        SongModel nextSong;
        if (Library.Songs.Count == 0)
        {
            nextSong = new SongModel
            {
                Title = "新曲谱",
                OriginalKey = "C",
                SelectedKey = "C",
                Capo = "",
                Category = "未分类"
            };
            Library.Songs.Add(nextSong);
        }
        else
        {
            nextSong = Library.Songs.OrderByDescending(x => x.UpdatedAt).First();
        }

        SelectSongRequested?.Invoke(nextSong);
        ComputeFilteredSongs();
        FilterStateChanged?.Invoke();
        SaveRequested?.Invoke();
        Practice.StatusMessage = $"已删除《{target.Title}》。";
    }

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
                (song.Artist ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (song.SelectedKey ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (song.OriginalKey ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (song.Category ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                song.Lines.Any(line =>
                    (line.Text ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    line.AltLyrics.Any(alt => (alt ?? "").Contains(query, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(song => song.UpdatedAt)
            .ToList();

        FilteredSongs.Clear();
        foreach (var song in filtered)
            FilteredSongs.Add(song);

        LibraryCountText = Library.Songs.Count.ToString(CultureInfo.InvariantCulture);
        FilteredCountDisplay = filtered.Count == Library.Songs.Count
            ? $"{Library.Songs.Count}"
            : $"{filtered.Count}/{Library.Songs.Count}";
        LibraryToggleText = $"曲库 ({Library.Songs.Count})";
        return filtered;
    }

    public static SongModel CreateDuplicateSong(SongModel source) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Title = string.IsNullOrWhiteSpace(source.Title) ? "未命名歌曲 (副本)" : source.Title + " (副本)",
        Artist = source.Artist,
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
