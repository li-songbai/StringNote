using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Xianpu.Desktop;

public partial class MainWindow
{
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key == Key.Space &&
            _autoScrollEngaged &&
            Keyboard.Modifiers == ModifierKeys.None &&
            Keyboard.FocusedElement is not TextBoxBase &&
            !ChordPopover.IsOpen &&
            AutoScrollToggle is not null)
        {
            AutoScrollToggle.IsChecked = !(AutoScrollToggle.IsChecked == true);
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
            (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.None)
        {
            // Keep native text undo/redo inside text inputs (TitleBox, search box, inline chord input, etc.)
            if (Keyboard.FocusedElement is TextBoxBase)
                return;

            if (e.Key == Key.Z)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    PerformRedo();
                else
                    PerformUndo();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Y)
            {
                PerformRedo();
                e.Handled = true;
                return;
            }
        }
    }

    private void ResetUndoHistory(SongModel? song)
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _currentSongSnapshot = song is null ? null : LibraryStore.SerializeSongSnapshot(song);
        UpdateUndoRedoButtons();
    }

    private void RecordUndoSnapshotIfChanged()
    {
        if (_initializing || _restoringUndo || _selectedSong is null)
            return;

        var nextSnapshot = LibraryStore.SerializeSongSnapshot(_selectedSong);
        if (_currentSongSnapshot is not null &&
            !string.Equals(nextSnapshot, _currentSongSnapshot, StringComparison.Ordinal))
        {
            _undoStack.Add(_currentSongSnapshot);
            if (_undoStack.Count > MaxUndoSteps)
                _undoStack.RemoveAt(0);
            _redoStack.Clear();
        }
        _currentSongSnapshot = nextSnapshot;
        UpdateUndoRedoButtons();
    }

    private void UpdateUndoRedoButtons()
    {
        if (UndoButton is not null)
        {
            UndoButton.IsEnabled = _undoStack.Count > 0;
            UndoButton.ToolTip = _undoStack.Count > 0
                ? $"撤销上一步谱面编辑（Ctrl+Z，可撤销 {_undoStack.Count} 步）"
                : "撤销上一步谱面编辑 (Ctrl+Z)";
        }

        if (RedoButton is not null)
        {
            RedoButton.IsEnabled = _redoStack.Count > 0;
            RedoButton.ToolTip = _redoStack.Count > 0
                ? $"重做已撤销的编辑（Ctrl+Y，可重做 {_redoStack.Count} 步）"
                : "重做已撤销的编辑 (Ctrl+Y)";
        }
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => PerformUndo();

    private void Redo_Click(object sender, RoutedEventArgs e) => PerformRedo();

    private void PerformUndo()
    {
        if (_selectedSong is null || _undoStack.Count == 0)
            return;

        var targetJson = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        if (_currentSongSnapshot is not null)
            _redoStack.Add(_currentSongSnapshot);

        ApplySongSnapshot(targetJson, "已撤销上一步谱面编辑。");
    }

    private void PerformRedo()
    {
        if (_selectedSong is null || _redoStack.Count == 0)
            return;

        var targetJson = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);

        if (_currentSongSnapshot is not null)
            _undoStack.Add(_currentSongSnapshot);

        ApplySongSnapshot(targetJson, "已重做上一步谱面编辑。");
    }

    private void ApplySongSnapshot(string snapshotJson, string statusMessage)
    {
        if (_selectedSong is null)
            return;

        var restored = LibraryStore.DeserializeSongSnapshot(snapshotJson);
        if (restored is null)
            return;

        ChordPopover.IsOpen = false;
        _restoringUndo = true;
        _initializing = true;
        try
        {
            _selectedSong.Title = restored.Title;
            _selectedSong.OriginalKey = restored.OriginalKey;
            _selectedSong.SelectedKey = restored.SelectedKey;
            _selectedSong.Capo = restored.Capo;
            _selectedSong.Bpm = restored.Bpm;
            _selectedSong.Category = restored.Category;
            _selectedSong.Favorite = restored.Favorite;
            _selectedSong.ChordDisplayMode = restored.ChordDisplayMode;
            _selectedSong.ColumnMode = restored.ColumnMode;
            _selectedSong.ShowChordGuide = restored.ShowChordGuide;
            _selectedSong.ShowTabSections = restored.ShowTabSections;

            _selectedSong.Lines.Clear();
            foreach (var line in restored.Lines)
                _selectedSong.Lines.Add(line);

            _selectedSong.TabSections.Clear();
            foreach (var section in restored.TabSections)
                _selectedSong.TabSections.Add(section);

            TitleBox.Text = _selectedSong.Title;
            OriginalKeyBox.Text = _selectedSong.OriginalKey;
            SelectedKeyBox.Text = _selectedSong.SelectedKey;
            CapoBox.Text = _selectedSong.Capo;
            if (BpmBox is not null)
                BpmBox.Text = _selectedSong.Bpm > 0 ? _selectedSong.Bpm.ToString(CultureInfo.InvariantCulture) : "";
            ColumnModeBox.SelectedValue = _selectedSong.ColumnMode.ToString(CultureInfo.InvariantCulture);
            ChordDisplayBox.SelectedValue = string.IsNullOrWhiteSpace(_selectedSong.ChordDisplayMode) ? "name" : _selectedSong.ChordDisplayMode;
            if (SongCategoryBox is not null)
                SongCategoryBox.Text = string.IsNullOrWhiteSpace(_selectedSong.Category) ? "未分类" : _selectedSong.Category;
            if (SongFavoriteToggle is not null)
                SongFavoriteToggle.IsChecked = _selectedSong.Favorite;
        }
        finally
        {
            _initializing = false;
            _restoringUndo = false;
        }

        _currentSongSnapshot = snapshotJson;
        UpdateFavoriteToggleVisual();
        UpdateCaptionTitle();
        ScoreSurface.SetSong(_selectedSong);
        UpdatePartArrangeButtonCue();
        RefreshQuickChordBar();
        RefreshCategoryOptions();
        RefreshLibraryList();
        UpdateActionStates();
        UpdateViewportScale();

        try
        {
            _selectedSong.UpdatedAt = DateTime.Now;
            _store.Save(_library);
        }
        catch
        {
            // Ignore transient background save errors
        }

        SetStatus(statusMessage);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        SettingsDialog.Show(this, _store, () => _library, ApplyReplacedLibrary);
    }

    private void ApplyReplacedLibrary(LibraryData newLibrary, string statusMessage)
    {
        ChordPopover.IsOpen = false;
        _library = newLibrary;
        _library.Categories ??= [];
        _library.Songs ??= [];
        _library.CustomChordsByKey ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        _library.CustomChordShapes ??= [];
        ChordCatalog.LoadCustomData(_library.CustomChordsByKey, _library.CustomChordShapes);

        if (_library.Songs.Count == 0)
            _library.Songs.Add(CreateDemoSong());

        RefreshCategoryOptions();
        UpdateLibraryFilterButtons();
        var nextSong = _library.Songs.OrderByDescending(x => x.UpdatedAt).First();
        SelectSong(nextSong);
        SetStatus(statusMessage);
    }
}
