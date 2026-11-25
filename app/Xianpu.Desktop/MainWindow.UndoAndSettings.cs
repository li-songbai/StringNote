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

        if (e.Key == Key.F2 && Keyboard.Modifiers == ModifierKeys.None && _selectedSong is not null && !_vm.IsPracticeMode && !_vm.IsTheoryMode)
        {
            if (!IsInlineLyricEditorOpen)
            {
                var focused = Keyboard.FocusedElement as DependencyObject;
                var inChordPopover = ChordPopover.IsOpen && ReferenceEquals(focused, InlineChordBox);
                if (focused is not TextBoxBase || inChordPopover)
                {
                    if (TriggerInlineLyricEditForCurrentSelection())
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None && Keyboard.FocusedElement is not TextBoxBase)
        {
            _vm.TogglePracticeModeCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F6 && Keyboard.Modifiers == ModifierKeys.None && Keyboard.FocusedElement is not TextBoxBase)
        {
            _vm.ToggleTheoryModeCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape &&
            Keyboard.Modifiers == ModifierKeys.None &&
            !IsInlineLyricEditorOpen &&
            !ChordPopover.IsOpen &&
            Keyboard.FocusedElement is not TextBoxBase)
        {
            var clearedMode = false;
            if (ContinuousPaintToggle?.IsChecked == true)
            {
                ContinuousPaintToggle.IsChecked = false;
                clearedMode = true;
            }
            if (LyricEditModeToggle?.IsChecked == true)
            {
                LyricEditModeToggle.IsChecked = false;
                clearedMode = true;
            }
            if (clearedMode)
            {
                ScoreSurface?.Focus();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Space &&
            !_vm.IsTheoryMode &&
            (_autoScrollEngaged || _vm.IsPracticeMode) &&
            Keyboard.Modifiers == ModifierKeys.None &&
            Keyboard.FocusedElement is not TextBoxBase &&
            !ChordPopover.IsOpen &&
            AutoScrollToggle is not null)
        {
            AutoScrollToggle.IsChecked = !(AutoScrollToggle.IsChecked == true);
            e.Handled = true;
            return;
        }

        if (!_vm.IsTheoryMode &&
            (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
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
        _vm.UndoManager.Reset(song);
        _vm.RefreshUndoState();
    }

    private void RecordUndoSnapshotIfChanged()
    {
        if (_initializing || _restoringUndo || _selectedSong is null)
            return;

        _vm.UndoManager.RecordIfChanged(_selectedSong);
        _vm.RefreshUndoState();
    }

    private void UpdateUndoRedoButtons()
    {
        _vm.RefreshUndoState();
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => PerformUndo();

    private void Redo_Click(object sender, RoutedEventArgs e) => PerformRedo();

    private void PerformUndo()
    {
        if (_selectedSong is null || !_vm.UndoManager.TryPopUndo(out var targetJson) || targetJson is null)
            return;

        ApplySongSnapshot(targetJson, "已撤销上一步谱面编辑。");
    }

    private void PerformRedo()
    {
        if (_selectedSong is null || !_vm.UndoManager.TryPopRedo(out var targetJson) || targetJson is null)
            return;

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
        HideInlineLyricOverlay();
        _restoringUndo = true;
        _initializing = true;
        try
        {
            _selectedSong.Title = restored.Title;
            _selectedSong.Artist = restored.Artist;
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

            _vm.SyncBpmTextFromSelectedSong();
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
        _library.TheoryNotes ??= [];
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
