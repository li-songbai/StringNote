using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Xianpu.Desktop;

public partial class MainWindow : Window
{
    private readonly LibraryStore _store = new();
    private LibraryData _library = new();
    private SongModel? _selectedSong;
    private bool _initializing;
    private bool _favoritesOnly;
    private ChordAnchor? _popoverAnchor;
    private string _popoverInitialChord = "";
    private bool _syncingPopoverPosition;
    private TrayService? _trayService;
    private bool _isExplicitExit;
    private const string AllCategoriesLabel = "全部分类";

    private static readonly string[] KeyNames =
    [
        "C", "C#", "Db", "D", "Eb", "E", "F", "F#", "Gb", "G", "Ab", "A", "Bb", "B",
        "Am", "Bbm", "Bm", "Cm", "C#m", "Dm", "Ebm", "Em", "Fm", "F#m", "Gm", "G#m"
    ];

    private static readonly string[] CapoOptions =
    [
        "不夹", "1 品", "2 品", "3 品", "4 品", "5 品", "6 品", "7 品"
    ];

    public MainWindow()
    {
        InitializeComponent();
        LoadWindowIcon();

        OriginalKeyBox.ItemsSource = KeyNames;
        SelectedKeyBox.ItemsSource = KeyNames;
        CapoBox.ItemsSource = CapoOptions;

        LoadInitialState();

        _trayService = new TrayService(
            RestoreFromTray,
            () =>
            {
                RestoreFromTray();
                CheckUpdate_Click(this, new RoutedEventArgs());
            },
            ExitApplication);

        ScoreSurface.Changed += () =>
        {
            SaveLibraryQuietly();
            RefreshLibraryList();
            UpdateActionStates();
            UpdateViewportScale();
        };
        ScoreSurface.SelectionChanged += UpdateActionStates;
        ScoreSurface.LineEditRequested += EditLyricLine;
        ScoreSurface.CharacterSlotClicked += OnCharacterSlotClicked;
        ScoreSurface.QuickTypeStarted += OnQuickTypeStarted;

        Loaded += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(App.StartupFilePath) && File.Exists(App.StartupFilePath))
            {
                LoadFromFilePath(App.StartupFilePath);
            }
            UpdateViewportScale();
            ScoreSurface.Focus();
            _ = CheckForUpdatesSilentlyAsync();
        };
    }

    private void LoadInitialState()
    {
        try
        {
            _library = _store.Load();
        }
        catch
        {
            _library = new LibraryData();
        }

        if (_library.Songs.Count == 0)
        {
            _library.Songs.Add(CreateDemoSong());
        }

        RefreshCategoryOptions();
        UpdateLibraryFilterButtons();

        var initialSong = _library.Songs.OrderByDescending(x => x.UpdatedAt).First();
        SelectSong(initialSong);
        SaveLibraryQuietly();
        RefreshLibraryList();
    }

    private static SongModel CreateDemoSong() => new()
    {
        Title = "示例曲谱 · 光落在窗前",
        OriginalKey = "G",
        SelectedKey = "G",
        Capo = "2 品",
        Category = "默认",
        ColumnMode = 0,
        Lines =
        [
            new LyricLine
            {
                Text = "风吹过屋檐，阳光落在肩上",
                Chords =
                [
                    new ChordPlacement { Position = 0, Name = "G", ShapeId = "G" },
                    new ChordPlacement { Position = 6, Name = "C", ShapeId = "C" },
                    new ChordPlacement { Position = 10, Name = "D", ShapeId = "D" }
                ]
            },
            new LyricLine
            {
                Text = "你说今天适合慢慢地走",
                Chords =
                [
                    new ChordPlacement { Position = 0, Name = "Em", ShapeId = "Em" },
                    new ChordPlacement { Position = 4, Name = "C", ShapeId = "C" },
                    new ChordPlacement { Position = 8, Name = "D", ShapeId = "D" }
                ]
            },
            new LyricLine
            {
                Text = "我把这一刻写成一段歌",
                Chords =
                [
                    new ChordPlacement { Position = 0, Name = "G", ShapeId = "G" },
                    new ChordPlacement { Position = 5, Name = "Am7", ShapeId = "Am7" },
                    new ChordPlacement { Position = 8, Name = "D", ShapeId = "D" },
                    new ChordPlacement { Position = 9, Name = "G", ShapeId = "G" }
                ]
            }
        ]
    };

    private void SelectSong(SongModel song)
    {
        if (_selectedSong is not null)
            _selectedSong.PropertyChanged -= SelectedSong_PropertyChanged;

        _selectedSong = song;
        if (!_library.Songs.Contains(song))
            _library.Songs.Add(song);

        _initializing = true;
        TitleBox.Text = song.Title;
        OriginalKeyBox.Text = song.OriginalKey;
        SelectedKeyBox.Text = song.SelectedKey;
        CapoBox.Text = song.Capo;
        ColumnModeBox.SelectedValue = song.ColumnMode.ToString(CultureInfo.InvariantCulture);
        ChordDisplayBox.SelectedValue = string.IsNullOrWhiteSpace(song.ChordDisplayMode) ? "name" : song.ChordDisplayMode;
        if (SongCategoryBox is not null)
            SongCategoryBox.Text = string.IsNullOrWhiteSpace(song.Category) ? "未分类" : song.Category;
        if (SongFavoriteToggle is not null)
            SongFavoriteToggle.IsChecked = song.Favorite;
        _initializing = false;

        UpdateFavoriteToggleVisual();
        ScoreSurface.SetSong(song);
        song.PropertyChanged += SelectedSong_PropertyChanged;
        RefreshQuickChordBar();
        RefreshCategoryOptions();
        RefreshLibraryList();
        UpdateActionStates();
        UpdateViewportScale();
    }

    private void SelectedSong_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        if (e.PropertyName is nameof(SongModel.Title)
            or nameof(SongModel.OriginalKey)
            or nameof(SongModel.SelectedKey)
            or nameof(SongModel.Capo)
            or nameof(SongModel.ChordDisplayMode)
            or nameof(SongModel.ColumnMode)
            or nameof(SongModel.ShowChordGuide))
        {
            ScoreSurface.Rebuild();
            UpdateViewportScale();
        }
        if (e.PropertyName == nameof(SongModel.SelectedKey))
        {
            RefreshQuickChordBar();
        }
        _selectedSong.UpdatedAt = DateTime.Now;
        SaveLibraryQuietly();

        if (e.PropertyName is nameof(SongModel.Title)
            or nameof(SongModel.OriginalKey)
            or nameof(SongModel.SelectedKey)
            or nameof(SongModel.Capo)
            or nameof(SongModel.Category)
            or nameof(SongModel.Favorite))
        {
            RefreshLibraryList();
        }
    }

    private void RefreshQuickChordBar()
    {
        if (QuickChordPanel is null) return;
        QuickChordPanel.Children.Clear();

        var key = string.IsNullOrWhiteSpace(_selectedSong?.SelectedKey)
            ? (string.IsNullOrWhiteSpace(_selectedSong?.OriginalKey) ? "C" : _selectedSong!.OriginalKey)
            : _selectedSong!.SelectedKey;

        PaletteKeyLabel.Text = $"{key} 调常用和弦：";
        var chords = ChordCatalog.GetCommonChordsForKey(key);
        var activeBrush = ScoreSurface?.ActiveChordBrush;

        foreach (var chordName in chords)
        {
            var isSelected = ScoreSurface?.ContinuousPaintMode == true &&
                             string.Equals(activeBrush, chordName, StringComparison.OrdinalIgnoreCase);

            var btn = new Button
            {
                Content = chordName,
                Tag = chordName,
                Style = (Style)FindResource("ChordChipButton"),
                Background = isSelected ? new SolidColorBrush(Color.FromRgb(47, 107, 72)) : new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                Foreground = isSelected ? Brushes.White : new SolidColorBrush(Color.FromRgb(36, 85, 56)),
                BorderBrush = isSelected ? new SolidColorBrush(Color.FromRgb(47, 107, 72)) : new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                ToolTip = ScoreSurface?.ContinuousPaintMode == true
                    ? $"选中 {chordName} 后，可连续点击歌词字位放置"
                    : $"将 {chordName} 填入当前光标字位（或点击后再点歌词字位）"
            };
            btn.Click += QuickChordChip_Click;
            QuickChordPanel.Children.Add(btn);
        }
    }

    private void QuickChordChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string chordName } || _selectedSong is null) return;

        if (ScoreSurface.ContinuousPaintMode)
        {
            if (string.Equals(ScoreSurface.ActiveChordBrush, chordName, StringComparison.OrdinalIgnoreCase))
            {
                ScoreSurface.ActiveChordBrush = null;
                SetStatus("已取消连点画笔。");
            }
            else
            {
                ScoreSurface.ActiveChordBrush = chordName;
                SetStatus($"连点填谱：已选【{chordName}】，直接依次点击歌词上的进唱字位即可放置。");
            }
            RefreshQuickChordBar();
            ScoreSurface.Focus();
            return;
        }

        if (ScoreSurface.Caret is { } caret)
        {
            ScoreSurface.SetChordAt(caret, chordName);
            SetStatus($"已在当前字位填入和弦 {chordName}（可按左右方向键移动到下一进唱字位）。");
            ScoreSurface.Focus();
        }
        else
        {
            ScoreSurface.ActiveChordBrush = chordName;
            SetStatus($"已选和弦【{chordName}】，请点击歌词上的进唱字位放置。");
            ScoreSurface.Focus();
        }
    }

    private void ContinuousPaint_Changed(object sender, RoutedEventArgs e)
    {
        if (ScoreSurface is null) return;
        var enabled = ContinuousPaintToggle.IsChecked == true;
        ScoreSurface.ContinuousPaintMode = enabled;
        ContinuousPaintToggle.Background = enabled
            ? new SolidColorBrush(Color.FromRgb(234, 243, 237))
            : Brushes.White;
        ContinuousPaintToggle.BorderBrush = enabled
            ? new SolidColorBrush(Color.FromRgb(47, 107, 72))
            : new SolidColorBrush(Color.FromRgb(203, 213, 225));
        ContinuousPaintToggle.Foreground = enabled
            ? new SolidColorBrush(Color.FromRgb(36, 85, 56))
            : new SolidColorBrush(Color.FromRgb(51, 65, 85));

        if (!enabled)
        {
            ScoreSurface.ActiveChordBrush = null;
            SetStatus("已关闭连点填谱：点击任意歌词字位可直接输入或点选和弦。");
        }
        else
        {
            var firstChord = ChordCatalog.GetCommonChordsForKey(_selectedSong?.SelectedKey).FirstOrDefault() ?? "C";
            ScoreSurface.ActiveChordBrush ??= firstChord;
            SetStatus($"已开启连点填谱：当前画笔【{ScoreSurface.ActiveChordBrush}】，点击歌词字位可连续放置，点上方和弦按钮可切换。");
        }
        RefreshQuickChordBar();
    }

    private void OnCharacterSlotClicked(ChordAnchor anchor, Rect anchorRect, string? existingChord)
    {
        OpenChordPopover(anchor, anchorRect, existingChord ?? "", selectAll: true);
    }

    private void OnQuickTypeStarted(ChordAnchor anchor, Rect anchorRect, string initialText)
    {
        OpenChordPopover(anchor, anchorRect, initialText, selectAll: false);
    }

    private void OpenChordPopover(ChordAnchor anchor, Rect anchorRect, string initialText, bool selectAll)
    {
        if (_selectedSong is null || anchor.LineIndex < 0 || anchor.LineIndex >= _selectedSong.Lines.Count)
            return;

        PopulatePopoverContent(anchor, initialText, selectAll);

        ChordPopover.PlacementRectangle = new Rect(
            anchorRect.X,
            anchorRect.Y,
            Math.Max(22, anchorRect.Width),
            Math.Max(28, anchorRect.Height));
        ChordPopover.IsOpen = true;
    }

    private void PopulatePopoverContent(ChordAnchor anchor, string initialText, bool selectAll)
    {
        if (_selectedSong is null || anchor.LineIndex < 0 || anchor.LineIndex >= _selectedSong.Lines.Count)
            return;

        _popoverAnchor = anchor;
        var currentChord = ScoreSurface.GetChordAt(anchor);
        _popoverInitialChord = currentChord ?? "";

        _syncingPopoverPosition = true;
        try
        {
            PosOnCharRadio.IsChecked = !anchor.BeforeChar;
            PosBeforeCharRadio.IsChecked = anchor.BeforeChar;
        }
        finally
        {
            _syncingPopoverPosition = false;
        }

        var lineText = _selectedSong.Lines[anchor.LineIndex].Text;
        var charLabel = GetCharacterLabelAt(lineText, anchor.Position);
        PopoverTargetLabel.Text = anchor.BeforeChar
            ? $"进唱字位：「{charLabel}」字前"
            : $"进唱字位：「{charLabel}」上方";

        DeleteInlineChordButton.Visibility = !string.IsNullOrWhiteSpace(currentChord)
            ? Visibility.Visible
            : Visibility.Collapsed;

        BuildPopoverCharStrip(anchor);

        PopoverChordWrap.Children.Clear();
        var key = string.IsNullOrWhiteSpace(_selectedSong.SelectedKey)
            ? (string.IsNullOrWhiteSpace(_selectedSong.OriginalKey) ? "C" : _selectedSong.OriginalKey)
            : _selectedSong.SelectedKey;

        foreach (var chord in ChordCatalog.GetCommonChordsForKey(key))
        {
            var isCurrent = string.Equals(currentChord, chord, StringComparison.OrdinalIgnoreCase);
            var chip = new Button
            {
                Content = chord,
                Tag = chord,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 4, 4),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Background = isCurrent ? new SolidColorBrush(Color.FromRgb(47, 107, 72)) : new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                Foreground = isCurrent ? Brushes.White : new SolidColorBrush(Color.FromRgb(36, 85, 56)),
                BorderBrush = isCurrent ? new SolidColorBrush(Color.FromRgb(47, 107, 72)) : new SolidColorBrush(Color.FromRgb(203, 213, 225))
            };
            chip.Click += PopoverChordChip_Click;
            PopoverChordWrap.Children.Add(chip);
        }

        InlineChordBox.Text = initialText;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            InlineChordBox.Focus();
            Keyboard.Focus(InlineChordBox);
            if (selectAll)
                InlineChordBox.SelectAll();
            else
                InlineChordBox.CaretIndex = InlineChordBox.Text.Length;
        });
    }

    private void PopoverPosition_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncingPopoverPosition || _selectedSong is null) return;
        var beforeChar = PosBeforeCharRadio.IsChecked == true;
        if (_popoverAnchor is not { } oldAnchor || oldAnchor.BeforeChar == beforeChar)
            return;

        var newAnchor = oldAnchor with { BeforeChar = beforeChar };
        _popoverAnchor = newAnchor;

        var existingAtOld = ScoreSurface.GetChordAt(oldAnchor);
        var typed = InlineChordBox.Text.Trim();
        var chordToPlace = !string.IsNullOrWhiteSpace(typed)
            ? ChordCatalog.NormalizeChordName(typed)
            : existingAtOld;

        if (!string.IsNullOrWhiteSpace(chordToPlace))
        {
            ScoreSurface.SetChordPlacement(oldAnchor, newAnchor, chordToPlace);
            _popoverInitialChord = chordToPlace;
            DeleteInlineChordButton.Visibility = Visibility.Visible;
        }
        else
        {
            var existingAtNew = ScoreSurface.GetChordAt(newAnchor);
            _popoverInitialChord = existingAtNew ?? "";
            if (!string.IsNullOrWhiteSpace(existingAtNew))
                InlineChordBox.Text = existingAtNew;
            DeleteInlineChordButton.Visibility = !string.IsNullOrWhiteSpace(existingAtNew)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        ScoreSurface.SelectAnchor(newAnchor);

        var lineText = newAnchor.LineIndex >= 0 && newAnchor.LineIndex < _selectedSong.Lines.Count
            ? _selectedSong.Lines[newAnchor.LineIndex].Text
            : "";
        var charLabel = GetCharacterLabelAt(lineText, newAnchor.Position);
        PopoverTargetLabel.Text = newAnchor.BeforeChar
            ? $"进唱字位：「{charLabel}」字前"
            : $"进唱字位：「{charLabel}」上方";

        BuildPopoverCharStrip(newAnchor);
    }

    private void BuildPopoverCharStrip(ChordAnchor activeAnchor)
    {
        PopoverCharStrip.Children.Clear();
        if (_selectedSong is null || activeAnchor.LineIndex < 0 || activeAnchor.LineIndex >= _selectedSong.Lines.Count)
            return;

        var line = _selectedSong.Lines[activeAnchor.LineIndex];
        var elements = new List<(int Start, string Text)>();
        var enumerator = StringInfo.GetTextElementEnumerator(line.Text);
        while (enumerator.MoveNext())
        {
            elements.Add((enumerator.ElementIndex, enumerator.GetTextElement()));
        }

        if (line.Chords.Any(c => c.Position >= line.Text.Length) || activeAnchor.Position >= line.Text.Length)
        {
            elements.Add((line.Text.Length, "尾"));
        }

        var chordsByPos = line.Chords
            .GroupBy(c => c.Position)
            .ToDictionary(g => g.Key, g => g.ToList());

        Button? activeButton = null;
        foreach (var (start, text) in elements)
        {
            chordsByPos.TryGetValue(start, out var chordsAtPos);
            var hasChord = chordsAtPos is { Count: > 0 };
            var chordDisplay = hasChord
                ? string.Join(" ", chordsAtPos!.Select(c => c.BeforeChar ? $"↗{c.Name}" : c.Name))
                : "·";
            var isActive = start == activeAnchor.Position;

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = chordDisplay,
                FontSize = 11,
                FontWeight = hasChord ? FontWeights.Bold : FontWeights.Normal,
                Foreground = hasChord
                    ? new SolidColorBrush(Color.FromRgb(47, 107, 72))
                    : new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(text) ? "␣" : text,
                FontSize = 13,
                FontWeight = (isActive || hasChord) ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isActive
                    ? new SolidColorBrush(Color.FromRgb(15, 23, 42))
                    : (hasChord ? new SolidColorBrush(Color.FromRgb(30, 41, 59)) : new SolidColorBrush(Color.FromRgb(71, 85, 105))),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            var cell = new Button
            {
                Content = stack,
                Margin = new Thickness(0, 0, 3, 0),
                Padding = new Thickness(5, 3, 5, 3),
                MinWidth = 28,
                Cursor = Cursors.Hand,
                Background = isActive
                    ? new SolidColorBrush(Color.FromRgb(234, 243, 237))
                    : (hasChord ? new SolidColorBrush(Color.FromRgb(248, 250, 252)) : Brushes.White),
                BorderBrush = isActive
                    ? new SolidColorBrush(Color.FromRgb(47, 107, 72))
                    : (hasChord ? new SolidColorBrush(Color.FromRgb(148, 191, 164)) : new SolidColorBrush(Color.FromRgb(226, 232, 240))),
                BorderThickness = isActive ? new Thickness(1.5) : new Thickness(1),
                ToolTip = hasChord ? $"字位「{text}」当前和弦：{chordDisplay}（点击切换）" : $"点击切换到字位「{text}」"
            };

            var targetBeforeChar = chordsAtPos is { Count: > 0 }
                ? (chordsAtPos.Any(c => c.BeforeChar == activeAnchor.BeforeChar)
                    ? activeAnchor.BeforeChar
                    : chordsAtPos[0].BeforeChar)
                : false;
            var targetAnchor = new ChordAnchor(activeAnchor.LineIndex, start, targetBeforeChar);
            cell.Click += (_, _) =>
            {
                ScoreSurface.SelectAnchor(targetAnchor);
                var nextChord = ScoreSurface.GetChordAt(targetAnchor) ?? "";
                PopulatePopoverContent(targetAnchor, nextChord, selectAll: true);
            };

            PopoverCharStrip.Children.Add(cell);
            if (isActive)
                activeButton = cell;
        }

        if (activeButton is not null)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => activeButton.BringIntoView());
        }
    }

    private static string GetCharacterLabelAt(string text, int position)
    {
        if (string.IsNullOrEmpty(text) || position >= text.Length)
            return "句尾";
        if (position < 0)
            position = 0;
        var info = StringInfo.GetNextTextElement(text, position);
        return string.IsNullOrWhiteSpace(info) ? "空格" : info;
    }

    private void PopoverChordChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string chordName } || _popoverAnchor is not { } anchor)
            return;

        ScoreSurface.SetChordAt(anchor, chordName);
        ChordPopover.IsOpen = false;
        SetStatus($"已填入和弦 {chordName}。");
        ScoreSurface.Focus();
    }

    private void CommitInlineChord_Click(object sender, RoutedEventArgs e)
    {
        CommitInlineChordAndClose();
    }

    private void DeleteInlineChord_Click(object sender, RoutedEventArgs e)
    {
        if (_popoverAnchor is { } anchor)
        {
            ScoreSurface.SetChordAt(anchor, null);
            SetStatus("已删除该字位上的和弦。");
        }
        ChordPopover.IsOpen = false;
        ScoreSurface.Focus();
    }

    private void CommitInlineChordAndClose()
    {
        if (_popoverAnchor is { } anchor)
        {
            var raw = InlineChordBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                if (!string.IsNullOrWhiteSpace(_popoverInitialChord))
                {
                    ScoreSurface.SetChordAt(anchor, null);
                    SetStatus("已清除该字位上的和弦。");
                }
            }
            else
            {
                var normalized = ChordCatalog.NormalizeChordName(raw);
                ScoreSurface.SetChordAt(anchor, normalized);
                SetStatus($"已在进唱字位填入和弦 {normalized}。");
            }
        }
        ChordPopover.IsOpen = false;
        ScoreSurface.Focus();
    }

    private void InlineChordBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitInlineChordAndClose();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ChordPopover.IsOpen = false;
            ScoreSurface.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            if (_popoverAnchor is { } anchor)
            {
                var raw = InlineChordBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(raw))
                    ScoreSurface.SetChordAt(anchor, ChordCatalog.NormalizeChordName(raw));
                else if (!string.IsNullOrWhiteSpace(_popoverInitialChord))
                    ScoreSurface.SetChordAt(anchor, null);
            }

            var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            if (ScoreSurface.MoveCaret(shift ? -1 : 1, 0) && ScoreSurface.Caret is { } nextCaret)
            {
                var nextChord = ScoreSurface.GetChordAt(nextCaret) ?? "";
                OpenChordPopover(nextCaret, ScoreSurface.GetAnchorRect(nextCaret), nextChord, selectAll: true);
            }
            else
            {
                ChordPopover.IsOpen = false;
                ScoreSurface.Focus();
            }
            e.Handled = true;
        }
    }

    private void ChordPopover_Closed(object sender, EventArgs e)
    {
        _popoverAnchor = null;
    }

    private void NewSong_Click(object sender, RoutedEventArgs e)
    {
        var song = new SongModel
        {
            Title = "未命名歌曲",
            OriginalKey = "C",
            SelectedKey = "C",
            Capo = "",
            ColumnMode = 0
        };
        SelectSong(song);
        TitleBox.Focus();
        TitleBox.SelectAll();
        SetStatus("已创建空白曲谱：可点「导入 LRC」或「编辑歌词」铺开歌词。");
    }

    private void ImportLrc_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "LRC 歌词与文本 (*.lrc;*.txt)|*.lrc;*.txt|所有文件 (*.*)|*.*",
            Title = "导入 LRC 歌词文件"
        };
        if (dialog.ShowDialog(this) != true) return;
        ImportLrcFile(dialog.FileName);
    }

    private void ImportLrcFile(string filePath)
    {
        try
        {
            var song = LrcImporter.Import(LrcImporter.ReadFile(filePath), filePath);
            if (string.IsNullOrWhiteSpace(song.SelectedKey) && !string.IsNullOrWhiteSpace(song.OriginalKey))
                song.SelectedKey = song.OriginalKey;
            if (string.IsNullOrWhiteSpace(song.SelectedKey))
                song.SelectedKey = "C";
            SelectSong(song);
            SetStatus($"已导入《{song.Title}》（共 {song.Lines.Count} 行歌词），点击歌词上的进唱字位即可添加左手和弦。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            AppDialogs.ShowError(this, "导入歌词失败", ex.Message);
        }
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "弦谱项目 (*.xianpu.json;*.json)|*.xianpu.json;*.json|所有文件 (*.*)|*.*",
            Title = "打开弦谱项目"
        };
        if (dialog.ShowDialog(this) != true) return;
        OpenProjectFile(dialog.FileName);
    }

    private void OpenProjectFile(string filePath)
    {
        try
        {
            var song = LibraryStore.LoadSongProject(filePath);
            song.Id = Guid.NewGuid().ToString("N");
            SelectSong(song);
            SetStatus($"已打开曲谱《{song.Title}》。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            AppDialogs.ShowError(this, "打开曲谱失败", ex.Message);
        }
    }

    private void LoadFromFilePath(string filePath)
    {
        if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            OpenProjectFile(filePath);
        else
            ImportLrcFile(filePath);
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && File.Exists(files[0]))
        {
            LoadFromFilePath(files[0]);
            e.Handled = true;
        }
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        CommitFields();
        var dialog = new SaveFileDialog
        {
            Filter = "弦谱项目 (*.xianpu.json)|*.xianpu.json|JSON 文件 (*.json)|*.json",
            FileName = SafeFileName(_selectedSong.Title) + ".xianpu.json",
            DefaultExt = ".json",
            AddExtension = false,
            Title = "保存弦谱项目"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            LibraryStore.SaveSongProject(_selectedSong, dialog.FileName);
            SetStatus($"曲谱项目已保存：{Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppDialogs.ShowError(this, "保存失败", ex.Message);
        }
    }

    private void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        CommitFields();
        var dialog = new SaveFileDialog
        {
            Filter = "PNG 图片 (*.png)|*.png",
            FileName = SafeFileName(_selectedSong.Title) + ".png",
            DefaultExt = ".png",
            AddExtension = true,
            Title = "导出曲谱图片"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var bitmap = ScoreSurface.RenderPng();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
            SetStatus($"PNG 图片已导出（{bitmap.PixelWidth} × {bitmap.PixelHeight}）：{Path.GetFileName(dialog.FileName)}");
            AppDialogs.ShowInfo(
                this,
                "导出完成",
                $"曲谱图片已导出：\n{dialog.FileName}\n尺寸：{bitmap.PixelWidth} × {bitmap.PixelHeight} px",
                "打开所在文件夹",
                () => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{dialog.FileName}\"") { UseShellExecute = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            AppDialogs.ShowError(this, "导出失败", ex.Message);
        }
    }

    private void TransposeDown_Click(object sender, RoutedEventArgs e) => TransposeBySemitones(-1);

    private void TransposeUp_Click(object sender, RoutedEventArgs e) => TransposeBySemitones(1);

    private void TransposeBySemitones(int semitones)
    {
        if (_selectedSong is null) return;
        CommitFields();
        ChordTranspose.TransposeSongInPlace(_selectedSong, semitones, updateSelectedKey: true);
        _initializing = true;
        SelectedKeyBox.Text = _selectedSong.SelectedKey;
        _initializing = false;
        RefreshQuickChordBar();
        ScoreSurface.Rebuild();
        SaveLibraryQuietly();
        SetStatus($"全谱和弦已{(semitones > 0 ? "升" : "降")}半音，当前选调：{_selectedSong.SelectedKey}。");
    }

    private void TransposeDialog_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is not { } song) return;
        CommitFields();

        var dialog = AppDialogs.CreateCardWindow(
            this,
            "批量移调",
            "指定原和弦调与目标选调，一键转换全谱和弦",
            390,
            null,
            false,
            out var root);

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var row1 = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        row1.Children.Add(new TextBlock
        {
            Text = "当前谱面和弦调：",
            Width = 122,
            FontSize = 12.5,
            Foreground = AppDialogs.BodyBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        var fromBox = new ComboBox
        {
            ItemsSource = KeyNames,
            IsEditable = true,
            Text = string.IsNullOrWhiteSpace(song.SelectedKey) ? "C" : song.SelectedKey
        };
        row1.Children.Add(fromBox);
        Grid.SetRow(row1, 0);

        var row2 = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        row2.Children.Add(new TextBlock
        {
            Text = "转换到目标选调：",
            Width = 122,
            FontSize = 12.5,
            Foreground = AppDialogs.BodyBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        var toBox = new ComboBox
        {
            ItemsSource = KeyNames,
            IsEditable = true,
            Text = string.IsNullOrWhiteSpace(song.SelectedKey) ? "G" : song.SelectedKey
        };
        row2.Children.Add(toBox);
        Grid.SetRow(row2, 1);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancelBtn = AppDialogs.CreateSecondaryButton(this, "取消", minWidth: 74);
        cancelBtn.IsCancel = true;
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);
        cancelBtn.Click += (_, _) => AppDialogs.CloseWithResult(dialog, false);

        var okBtn = AppDialogs.CreatePrimaryButton(this, "确定移调", minWidth: 88);
        okBtn.IsDefault = true;
        okBtn.Click += (_, _) => AppDialogs.CloseWithResult(dialog, true);

        buttons.Children.Add(cancelBtn);
        buttons.Children.Add(okBtn);
        Grid.SetRow(buttons, 2);

        root.Children.Add(row1);
        root.Children.Add(row2);
        root.Children.Add(buttons);

        if (dialog.ShowDialog() != true) return;
        var fromKey = fromBox.Text.Trim();
        var toKey = toBox.Text.Trim();
        if (!ChordTranspose.TransposeSongKeys(song, fromKey, toKey))
        {
            AppDialogs.ShowInfo(this, "移调提示", "请输入有效的调名（例如 C → G 或 Am → Em）。");
            return;
        }

        _initializing = true;
        SelectedKeyBox.Text = song.SelectedKey;
        _initializing = false;
        RefreshQuickChordBar();
        ScoreSurface.Rebuild();
        SaveLibraryQuietly();
        SetStatus($"已将全谱和弦从 {fromKey} 调移至 {song.SelectedKey} 调。");
    }

    private void EditAllLyrics_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        var currentText = string.Join(Environment.NewLine, _selectedSong.Lines.Select(x => x.Text));
        var updatedText = PromptLyrics("编辑或粘贴全文歌词", "每行对应一句歌词（支持直接粘贴带 [mm:ss] 时间戳的 LRC 内容；未改动的歌词行会自动保留已填和弦）：", currentText, height: 480);
        if (updatedText is null) return;

        ApplyFullLyricsEdit(_selectedSong, updatedText);
        ScoreSurface.SetSong(_selectedSong);
        UpdateViewportScale();
        SaveLibraryQuietly();
        SetStatus($"已更新歌词（共 {_selectedSong.Lines.Count} 行）。");
    }

    private static void ApplyFullLyricsEdit(SongModel song, string rawText)
    {
        if (rawText.Contains('[') && rawText.Contains(':'))
        {
            var imported = LrcImporter.ImportText(rawText, song.Title);
            if (imported.Lines.Count > 0)
            {
                MergeLyricLinesPreservingChords(song, imported.Lines.Select(x => x.Text).ToList());
                if (!string.IsNullOrWhiteSpace(imported.OriginalKey) && string.IsNullOrWhiteSpace(song.OriginalKey))
                    song.OriginalKey = imported.OriginalKey;
                if (!string.IsNullOrWhiteSpace(imported.SelectedKey) && string.IsNullOrWhiteSpace(song.SelectedKey))
                    song.SelectedKey = imported.SelectedKey;
                if (!string.IsNullOrWhiteSpace(imported.Capo) && string.IsNullOrWhiteSpace(song.Capo))
                    song.Capo = imported.Capo;
                return;
            }
        }

        var newTexts = rawText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        MergeLyricLinesPreservingChords(song, newTexts);
    }

    private static void MergeLyricLinesPreservingChords(SongModel song, List<string> newTexts)
    {
        var oldLines = song.Lines.ToList();
        var usedOldIndices = new HashSet<int>();
        var result = new List<LyricLine>(newTexts.Count);

        for (var i = 0; i < newTexts.Count; i++)
        {
            var text = newTexts[i];
            LyricLine? matched = null;

            if (i < oldLines.Count && !usedOldIndices.Contains(i) && oldLines[i].Text == text)
            {
                matched = oldLines[i];
                usedOldIndices.Add(i);
            }
            else
            {
                var foundIdx = oldLines.FindIndex(x => x.Text == text);
                if (foundIdx >= 0 && !usedOldIndices.Contains(foundIdx))
                {
                    matched = oldLines[foundIdx];
                    usedOldIndices.Add(foundIdx);
                }
                else if (i < oldLines.Count && !usedOldIndices.Contains(i) && oldLines.Count == newTexts.Count)
                {
                    matched = oldLines[i];
                    matched.Text = text;
                    foreach (var c in matched.Chords)
                        c.Position = Math.Clamp(c.Position, 0, text.Length);
                    usedOldIndices.Add(i);
                }
            }

            result.Add(matched ?? new LyricLine { Text = text });
        }

        song.Lines.Clear();
        foreach (var line in result)
            song.Lines.Add(line);
    }

    private void EditLyricLine(int index)
    {
        if (_selectedSong is null || index < 0 || index >= _selectedSong.Lines.Count) return;
        var oldLine = _selectedSong.Lines[index];
        var text = PromptLyrics("编辑单行歌词", "修改当前行歌词（留空确定可删除该行；输入多行可拆分为多句）：", oldLine.Text, height: 260);
        if (text is null) return;

        var newLines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        if (newLines.Count == 0)
        {
            _selectedSong.Lines.RemoveAt(index);
        }
        else
        {
            oldLine.Text = newLines[0];
            foreach (var chord in oldLine.Chords)
                chord.Position = Math.Clamp(chord.Position, 0, oldLine.Text.Length);

            for (var i = 1; i < newLines.Count; i++)
                _selectedSong.Lines.Insert(index + i, new LyricLine { Text = newLines[i] });
        }

        ScoreSurface.SetSong(_selectedSong);
        UpdateViewportScale();
        SaveLibraryQuietly();
    }

    private void RemoveChord_Click(object sender, RoutedEventArgs e)
    {
        ScoreSurface.RemoveSelectedChord();
        SetStatus("已删除选中的和弦。");
    }

    private void ClearAllChords_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        var total = _selectedSong.Lines.Sum(x => x.Chords.Count);
        if (total == 0) return;
        if (!AppDialogs.Confirm(this, "清空和弦", $"确定清除当前曲谱上的全部 {total} 个和弦标记吗？（歌词内容会保留）", "确定清空", isDanger: true))
            return;

        foreach (var line in _selectedSong.Lines)
            line.Chords.Clear();

        ScoreSurface.Rebuild();
        UpdateActionStates();
        SaveLibraryQuietly();
        SetStatus("已清空整首歌上的所有和弦标记。");
    }

    private void UpdateActionStates()
    {
        if (RemoveChordButton is not null)
            RemoveChordButton.IsEnabled = ScoreSurface.HasSelectedChord;
    }

    private void SongField_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        if (ReferenceEquals(sender, TitleBox))
            _selectedSong.Title = TitleBox.Text;
    }

    private void OriginalKeyBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        if (OriginalKeyBox.SelectedItem is string val)
            _selectedSong.OriginalKey = val.Trim();
    }

    private void OriginalKeyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        _selectedSong.OriginalKey = OriginalKeyBox.Text.Trim();
    }

    private void SelectedKeyBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        if (SelectedKeyBox.SelectedItem is string val)
            _selectedSong.SelectedKey = val.Trim();
    }

    private void SelectedKeyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        _selectedSong.SelectedKey = SelectedKeyBox.Text.Trim();
    }

    private void CapoBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        if (CapoBox.SelectedItem is string val)
            _selectedSong.Capo = val == "不夹" ? "" : val.Trim();
    }

    private void CapoBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        var text = CapoBox.Text.Trim();
        _selectedSong.Capo = text == "不夹" ? "" : text;
    }

    private void KeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        CommitFields();
    }

    private void CommitFields()
    {
        if (_selectedSong is null) return;
        _selectedSong.Title = TitleBox.Text.Trim();
        _selectedSong.OriginalKey = OriginalKeyBox.Text.Trim();
        _selectedSong.SelectedKey = SelectedKeyBox.Text.Trim();
        var capo = CapoBox.Text.Trim();
        _selectedSong.Capo = capo == "不夹" ? "" : capo;
        _selectedSong.UpdatedAt = DateTime.Now;
        SaveLibraryQuietly();
        RefreshLibraryList();
    }

    private void ColumnMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null || ColumnModeBox.SelectedValue is not string raw) return;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mode))
        {
            _selectedSong.ColumnMode = mode;
            ScoreSurface.Rebuild();
            UpdateViewportScale();
            SaveLibraryQuietly();
        }
    }

    private void ChordDisplay_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null || ChordDisplayBox.SelectedValue is not string mode) return;
        _selectedSong.ChordDisplayMode = mode;
        ScoreSurface.Rebuild();
        UpdateViewportScale();
        SaveLibraryQuietly();
    }

    private void ScoreViewport_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateViewportScale();

    private void UpdateViewportScale()
    {
        if (ScoreSurface is null || ScoreScrollViewer is null || ScoreScrollViewer.ActualWidth <= 0) return;
        var paperWidth = ScoreSurface.PaperWidthCurrent > 0 ? ScoreSurface.PaperWidthCurrent : ScoreSurface.Width;
        var available = ScoreScrollViewer.ActualWidth - 56;
        var scale = Math.Clamp(available / paperWidth, 0.55, 1.0);
        ScoreSurface.LayoutTransform = Math.Abs(scale - 1.0) < 0.01 ? Transform.Identity : new ScaleTransform(scale, scale);
    }

    private string? PromptLyrics(string title, string label, string initial = "", double height = 360) =>
        AppDialogs.PromptLyrics(this, title, label, initial, height);

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "未命名歌曲" : safe;
    }

    private void SetStatus(string message)
    {
        if (StatusText is not null)
            StatusText.Text = message;
    }

    private void LibraryToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (LibraryDrawer is null || LibraryColumn is null || LibraryToggle is null) return;
        var open = LibraryToggle.IsChecked == true;
        LibraryDrawer.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        LibraryColumn.Width = open ? new GridLength(252) : new GridLength(0);

        LibraryToggle.Background = open
            ? new SolidColorBrush(Color.FromRgb(234, 243, 237))
            : Brushes.White;
        LibraryToggle.BorderBrush = open
            ? new SolidColorBrush(Color.FromRgb(148, 191, 164))
            : new SolidColorBrush(Color.FromRgb(203, 213, 225));
        LibraryToggle.Foreground = open
            ? new SolidColorBrush(Color.FromRgb(36, 85, 56))
            : new SolidColorBrush(Color.FromRgb(51, 65, 85));

        UpdateViewportScale();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, UpdateViewportScale);
    }

    private void RefreshCategoryOptions()
    {
        if (LibraryCategoryFilter is null || SongCategoryBox is null) return;

        if (_library.Categories.Count == 0)
        {
            _library.Categories.Add("民谣");
            _library.Categories.Add("练习");
        }

        foreach (var song in _library.Songs)
        {
            var cat = song.Category?.Trim();
            if (!string.IsNullOrWhiteSpace(cat) && cat != "未分类" && !_library.Categories.Contains(cat))
                _library.Categories.Add(cat);
        }

        var prevFilter = LibraryCategoryFilter.SelectedItem as string;
        var filterItems = new List<string> { AllCategoriesLabel };
        filterItems.AddRange(_library.Categories);

        var prevInit = _initializing;
        _initializing = true;
        LibraryCategoryFilter.ItemsSource = filterItems;
        LibraryCategoryFilter.SelectedItem = prevFilter is not null && filterItems.Contains(prevFilter)
            ? prevFilter
            : AllCategoriesLabel;

        var songCats = new List<string> { "未分类" };
        foreach (var c in _library.Categories)
        {
            if (!songCats.Contains(c))
                songCats.Add(c);
        }
        SongCategoryBox.ItemsSource = songCats;
        if (_selectedSong is not null)
            SongCategoryBox.Text = string.IsNullOrWhiteSpace(_selectedSong.Category) ? "未分类" : _selectedSong.Category;
        _initializing = prevInit;
    }

    private void RefreshLibraryList()
    {
        if (LibrarySongList is null || LibraryToggle is null || LibraryCountText is null) return;

        var query = LibrarySearchBox?.Text?.Trim() ?? "";
        if (LibrarySearchPlaceholder is not null)
            LibrarySearchPlaceholder.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;

        var selectedCategory = LibraryCategoryFilter?.SelectedItem as string;

        IEnumerable<SongModel> items = _library.Songs;

        if (_favoritesOnly)
            items = items.Where(s => s.Favorite);

        if (!string.IsNullOrWhiteSpace(selectedCategory) && selectedCategory != AllCategoriesLabel)
            items = items.Where(s => string.Equals(s.Category, selectedCategory, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query))
        {
            items = items.Where(s =>
                s.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.OriginalKey.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.SelectedKey.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Lines.Any(l => l.Text.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        var filtered = items.OrderByDescending(s => s.UpdatedAt).ToList();

        var prevInit = _initializing;
        _initializing = true;
        LibrarySongList.ItemsSource = filtered;
        LibrarySongList.SelectedItem = _selectedSong is not null && filtered.Contains(_selectedSong) ? _selectedSong : null;
        _initializing = prevInit;

        LibraryToggle.Content = $"曲库 ({_library.Songs.Count})";
        LibraryCountText.Text = filtered.Count == _library.Songs.Count
            ? $"{_library.Songs.Count}"
            : $"{filtered.Count}/{_library.Songs.Count}";
    }

    private void LibrarySongList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (LibrarySongList.SelectedItem is SongModel song && !ReferenceEquals(song, _selectedSong))
        {
            SelectSong(song);
            SetStatus($"已切换到曲谱《{song.Title}》。");
        }
    }

    private void LibraryFilter_Changed(object sender, EventArgs e)
    {
        if (_initializing) return;
        RefreshLibraryList();
    }

    private void LibraryAllFilter_Click(object sender, RoutedEventArgs e)
    {
        _favoritesOnly = false;
        UpdateLibraryFilterButtons();
        RefreshLibraryList();
    }

    private void LibraryFavFilter_Click(object sender, RoutedEventArgs e)
    {
        _favoritesOnly = true;
        UpdateLibraryFilterButtons();
        RefreshLibraryList();
    }

    private void UpdateLibraryFilterButtons()
    {
        if (LibraryAllFilterButton is null || LibraryFavFilterButton is null) return;

        LibraryAllFilterButton.Background = !_favoritesOnly ? new SolidColorBrush(Color.FromRgb(234, 243, 237)) : Brushes.White;
        LibraryAllFilterButton.BorderBrush = !_favoritesOnly ? new SolidColorBrush(Color.FromRgb(47, 107, 72)) : new SolidColorBrush(Color.FromRgb(203, 213, 225));
        LibraryAllFilterButton.Foreground = !_favoritesOnly ? new SolidColorBrush(Color.FromRgb(36, 85, 56)) : new SolidColorBrush(Color.FromRgb(51, 65, 85));

        LibraryFavFilterButton.Background = _favoritesOnly ? new SolidColorBrush(Color.FromRgb(254, 243, 199)) : Brushes.White;
        LibraryFavFilterButton.BorderBrush = _favoritesOnly ? new SolidColorBrush(Color.FromRgb(217, 119, 6)) : new SolidColorBrush(Color.FromRgb(203, 213, 225));
        LibraryFavFilterButton.Foreground = _favoritesOnly ? new SolidColorBrush(Color.FromRgb(180, 83, 9)) : new SolidColorBrush(Color.FromRgb(51, 65, 85));
    }

    private void SaveToLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        CommitFields();
        if (!_library.Songs.Contains(_selectedSong))
            _library.Songs.Add(_selectedSong);
        SaveLibraryQuietly();
        RefreshCategoryOptions();
        RefreshLibraryList();
        SetStatus("已保存到本地曲库。");
    }

    private void DuplicateSong_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        CommitFields();

        var clone = new SongModel
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = string.IsNullOrWhiteSpace(_selectedSong.Title) ? "未命名歌曲 (副本)" : _selectedSong.Title + " (副本)",
            OriginalKey = _selectedSong.OriginalKey,
            SelectedKey = _selectedSong.SelectedKey,
            Capo = _selectedSong.Capo,
            Category = _selectedSong.Category,
            Favorite = _selectedSong.Favorite,
            ChordDisplayMode = _selectedSong.ChordDisplayMode,
            ColumnMode = _selectedSong.ColumnMode,
            ShowChordGuide = _selectedSong.ShowChordGuide,
            UpdatedAt = DateTime.Now,
            Lines = new System.Collections.ObjectModel.ObservableCollection<LyricLine>(
                _selectedSong.Lines.Select(line => new LyricLine
                {
                    Text = line.Text,
                    Chords = line.Chords.Select(c => new ChordPlacement
                    {
                        Position = c.Position,
                        Name = c.Name,
                        ShapeId = c.ShapeId
                    }).ToList()
                }))
        };

        _library.Songs.Add(clone);
        SelectSong(clone);
        SaveLibraryQuietly();
        RefreshLibraryList();
        SetStatus($"已创建副本《{clone.Title}》。");
    }

    private void DeleteSong_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        var title = string.IsNullOrWhiteSpace(_selectedSong.Title) ? "未命名歌曲" : _selectedSong.Title;
        if (!AppDialogs.Confirm(this, "删除曲谱", $"确定从本地曲库删除《{title}》吗？", "确定删除", isDanger: true))
            return;

        var toRemove = _selectedSong;
        _library.Songs.Remove(toRemove);

        var next = _library.Songs.OrderByDescending(x => x.UpdatedAt).FirstOrDefault();
        if (next is null)
        {
            next = new SongModel
            {
                Title = "未命名歌曲",
                OriginalKey = "C",
                SelectedKey = "C",
                Capo = "",
                ColumnMode = 0
            };
            _library.Songs.Add(next);
        }

        SelectSong(next);
        SaveLibraryQuietly();
        RefreshLibraryList();
        SetStatus($"已从本地曲库删除《{title}》。");
    }

    private void AddCategory_Click(object sender, RoutedEventArgs e)
    {
        var name = PromptSingleLine("新增曲库分类", "请输入分类名称（例如：指弹、弹唱、摇滚）：");
        if (string.IsNullOrWhiteSpace(name)) return;
        var trimmed = name.Trim();
        if (trimmed != "未分类" && !_library.Categories.Contains(trimmed))
            _library.Categories.Add(trimmed);

        if (_selectedSong is not null)
        {
            _selectedSong.Category = trimmed;
            _selectedSong.UpdatedAt = DateTime.Now;
        }

        RefreshCategoryOptions();
        SaveLibraryQuietly();
        RefreshLibraryList();
        SetStatus($"已添加并应用分类「{trimmed}」。");
    }

    private void SongCategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        if (SongCategoryBox.SelectedItem is string val)
            ApplySongCategory(val);
    }

    private void SongCategoryBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        ApplySongCategory(SongCategoryBox.Text);
    }

    private void SongCategoryBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _selectedSong is not null)
        {
            ApplySongCategory(SongCategoryBox.Text);
            ScoreSurface.Focus();
            e.Handled = true;
        }
    }

    private void ApplySongCategory(string? rawCategory)
    {
        if (_selectedSong is null) return;
        var cat = string.IsNullOrWhiteSpace(rawCategory) ? "未分类" : rawCategory.Trim();
        if (_selectedSong.Category == cat) return;

        if (cat != "未分类" && !_library.Categories.Contains(cat))
            _library.Categories.Add(cat);

        _selectedSong.Category = cat;
        _selectedSong.UpdatedAt = DateTime.Now;
        RefreshCategoryOptions();
        SaveLibraryQuietly();
        RefreshLibraryList();
    }

    private void SongFavoriteToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing || _selectedSong is null || SongFavoriteToggle is null) return;
        _selectedSong.Favorite = SongFavoriteToggle.IsChecked == true;
        UpdateFavoriteToggleVisual();
        _selectedSong.UpdatedAt = DateTime.Now;
        SaveLibraryQuietly();
        RefreshLibraryList();
    }

    private void ToggleSelectedFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        _selectedSong.Favorite = !_selectedSong.Favorite;
        var prevInit = _initializing;
        _initializing = true;
        if (SongFavoriteToggle is not null)
            SongFavoriteToggle.IsChecked = _selectedSong.Favorite;
        _initializing = prevInit;
        UpdateFavoriteToggleVisual();
        _selectedSong.UpdatedAt = DateTime.Now;
        SaveLibraryQuietly();
        RefreshLibraryList();
        SetStatus(_selectedSong.Favorite ? $"已收藏《{_selectedSong.Title}》。" : $"已取消收藏《{_selectedSong.Title}》。");
    }

    private void UpdateFavoriteToggleVisual()
    {
        if (SongFavoriteToggle is null) return;
        var fav = SongFavoriteToggle.IsChecked == true;
        SongFavoriteToggle.Content = fav ? "★ 已收藏" : "☆ 收藏";
        SongFavoriteToggle.Background = fav ? new SolidColorBrush(Color.FromRgb(254, 243, 199)) : Brushes.White;
        SongFavoriteToggle.BorderBrush = fav ? new SolidColorBrush(Color.FromRgb(217, 119, 6)) : new SolidColorBrush(Color.FromRgb(203, 213, 225));
        SongFavoriteToggle.Foreground = fav ? new SolidColorBrush(Color.FromRgb(180, 83, 9)) : new SolidColorBrush(Color.FromRgb(71, 85, 105));
    }

    private void RenameSong_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        var updated = PromptSingleLine("重命名曲谱", "请输入新的歌曲名称：", _selectedSong.Title);
        if (string.IsNullOrWhiteSpace(updated)) return;
        var trimmed = updated.Trim();
        _initializing = true;
        TitleBox.Text = trimmed;
        _initializing = false;
        _selectedSong.Title = trimmed;
        _selectedSong.UpdatedAt = DateTime.Now;
        SaveLibraryQuietly();
        RefreshLibraryList();
        SetStatus($"已重命名为《{trimmed}》。");
    }

    private string? PromptSingleLine(string title, string label, string initial = "") =>
        AppDialogs.PromptSingleLine(this, title, label, initial);

    private void SaveLibraryQuietly()
    {
        try
        {
            if (_selectedSong is not null)
                _selectedSong.UpdatedAt = DateTime.Now;
            _store.Save(_library);
        }
        catch
        {
            // Ignore transient background save errors; explicit file save is always available.
        }
    }

    private async Task CheckForUpdatesSilentlyAsync()
    {
        if (!UpdateService.IsRepositoryConfigured && !UpdateService.HasCustomUpdateUrl)
            return;

        try
        {
            var result = await UpdateService.CheckForUpdatesAsync();
            if (result is { HasUpdate: true })
            {
                CheckUpdateButton.Content = $"发现新版 v{result.LatestVersion}";
                CheckUpdateButton.Style = (Style)FindResource("AccentOutlineButton");
                CheckUpdateButton.ToolTip = $"发现新版本 v{result.LatestVersion}（当前 v{UpdateService.CurrentVersion}），点击查看详情";
                SetStatus($"发现弦笺新版本 v{result.LatestVersion}，可点击右上角「发现新版」更新。");
            }
        }
        catch
        {
            // Background update checks must never interrupt editing.
        }
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (!UpdateService.IsRepositoryConfigured && !UpdateService.HasCustomUpdateUrl)
        {
            ShowUnconfiguredUpdateDialog();
            return;
        }

        CheckUpdateButton.IsEnabled = false;
        var previousContent = CheckUpdateButton.Content;
        CheckUpdateButton.Content = "检查中...";
        try
        {
            var result = await UpdateService.CheckForUpdatesAsync();
            if (result is { HasUpdate: true })
            {
                CheckUpdateButton.Content = $"发现新版 v{result.LatestVersion}";
                CheckUpdateButton.Style = (Style)FindResource("AccentOutlineButton");
            }
            else
            {
                CheckUpdateButton.Content = previousContent;
            }

            ShowUpdateDialog(result);
        }
        catch (Exception ex)
        {
            CheckUpdateButton.Content = previousContent;
            ShowUpdateDialog(null, ex.Message);
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void ShowUnconfiguredUpdateDialog()
    {
        var dialog = AppDialogs.CreateCardWindow(
            this,
            "检查更新",
            "弦笺 StringNote · 版本信息",
            450,
            null,
            false,
            out var layout);

        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var versionPill = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = AppDialogs.AccentLightBrush,
            Padding = new Thickness(10, 5, 10, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 12),
            Child = new TextBlock
            {
                Text = $"当前版本：v{UpdateService.CurrentVersion}",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = AppDialogs.AccentBrush
            }
        };
        Grid.SetRow(versionPill, 0);
        layout.Children.Add(versionPill);

        var bodyBox = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = AppDialogs.SubtleSurfaceBrush,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 10, 12, 10),
            Child = new TextBlock
            {
                Text = "GitHub 自动更新仓库尚未绑定。\n在公开仓库创建后，只需将 UpdateService.cs 中的 DefaultOwner / DefaultRepo 配置为对应的 GitHub 用户名与仓库名，即可启用一键检查更新与自动安装。",
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = AppDialogs.BodyBrush,
                LineHeight = 20
            }
        };
        Grid.SetRow(bodyBox, 1);
        layout.Children.Add(bodyBox);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var ok = AppDialogs.CreatePrimaryButton(this, "知道了", minWidth: 88);
        ok.IsDefault = true;
        ok.IsCancel = true;
        ok.Click += (_, _) => AppDialogs.CloseWithResult(dialog, true);
        footer.Children.Add(ok);
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);

        dialog.ShowDialog();
    }

    private void ShowUpdateDialog(UpdateCheckResult? result, string? errorMessage = null)
    {
        var cts = new CancellationTokenSource();
        var titleText = result switch
        {
            { HasUpdate: true } => $"发现新版本 v{result.LatestVersion}",
            { HasUpdate: false } => "当前已是最新版本",
            _ => "暂时无法获取更新信息"
        };
        var dialog = AppDialogs.CreateCardWindow(
            this,
            titleText,
            "软件更新 · 弦笺 StringNote",
            510,
            420,
            false,
            out var layout);
        dialog.Closed += (_, _) => cts.Cancel();

        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var versionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 10)
        };
        versionRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
            Padding = new Thickness(9, 4, 9, 4),
            Child = new TextBlock
            {
                Text = $"当前 v{UpdateService.CurrentVersion}",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = AppDialogs.BodyBrush
            }
        });

        if (result is not null)
        {
            versionRow.Children.Add(new TextBlock
            {
                Text = "→",
                FontSize = 12,
                Foreground = AppDialogs.MutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0)
            });
            versionRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = AppDialogs.AccentLightBrush,
                Padding = new Thickness(9, 4, 9, 4),
                Child = new TextBlock
                {
                    Text = $"最新 v{result.LatestVersion}",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = AppDialogs.AccentBrush
                }
            });
        }
        else if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            versionRow.Children.Add(new TextBlock
            {
                Text = errorMessage,
                FontSize = 12,
                Foreground = AppDialogs.MutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        Grid.SetRow(versionRow, 0);
        layout.Children.Add(versionRow);

        var notesText = result switch
        {
            not null when !string.IsNullOrWhiteSpace(result.ReleaseNotes) => result.ReleaseNotes,
            { HasUpdate: true } => "本次更新包含功能优化与稳定性改进。",
            { HasUpdate: false } => "您正在使用最新发布的弦笺版本。",
            _ => "请检查网络连接，或点击下方「问题反馈 (GitHub)」前往项目页面查看。"
        };
        var notesContainer = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = AppDialogs.SubtleSurfaceBrush,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            Margin = new Thickness(0, 0, 0, 12),
            Child = new TextBox
            {
                Text = notesText,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalContentAlignment = VerticalAlignment.Top,
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 12.5,
                Foreground = AppDialogs.BodyBrush,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            }
        };
        Grid.SetRow(notesContainer, 1);
        layout.Children.Add(notesContainer);

        var progressPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12), Visibility = Visibility.Collapsed };
        var progressStatus = new TextBlock
        {
            Text = "准备下载...",
            FontSize = 12,
            Foreground = AppDialogs.BodyBrush,
            Margin = new Thickness(0, 0, 0, 6)
        };
        var progressBar = new ProgressBar
        {
            Height = 8,
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Foreground = AppDialogs.AccentBrush,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        var progressTrack = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = AppDialogs.SubtleBorderBrush,
            ClipToBounds = true,
            Child = progressBar
        };
        progressPanel.Children.Add(progressStatus);
        progressPanel.Children.Add(progressTrack);
        Grid.SetRow(progressPanel, 2);
        layout.Children.Add(progressPanel);

        var footer = new DockPanel { LastChildFill = false };
        var feedbackBtn = AppDialogs.CreateSecondaryButton(this, "问题反馈 (GitHub)", minWidth: 120);
        feedbackBtn.Click += (_, _) => OpenExternalUrl(UpdateService.IssuesPageUrl);
        DockPanel.SetDock(feedbackBtn, Dock.Left);
        footer.Children.Add(feedbackBtn);

        var rightButtons = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(rightButtons, Dock.Right);

        var closeBtn = AppDialogs.CreateSecondaryButton(this, "关闭", minWidth: 74);
        closeBtn.IsCancel = true;
        closeBtn.Click += (_, _) => AppDialogs.CloseWithResult(dialog, false);

        if (result is { HasUpdate: true })
        {
            var installBtn = AppDialogs.CreatePrimaryButton(this, "下载并安装更新", minWidth: 128);
            installBtn.Margin = new Thickness(0, 0, 8, 0);
            installBtn.Click += async (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(result.DownloadUrl))
                {
                    OpenExternalUrl(string.IsNullOrWhiteSpace(result.HtmlUrl) ? UpdateService.ReleasesPageUrl : result.HtmlUrl);
                    return;
                }

                installBtn.IsEnabled = false;
                progressPanel.Visibility = Visibility.Visible;
                progressBar.Value = 0;
                progressStatus.Text = $"正在下载 {result.AssetName}...";

                try
                {
                    var progress = new Progress<double>(p =>
                    {
                        progressBar.Value = p;
                        progressStatus.Text = $"正在下载 {result.AssetName}... {p * 100:0}%";
                    });
                    var filePath = await UpdateService.DownloadAssetAsync(result.DownloadUrl, result.AssetName, progress, cts.Token);
                    progressStatus.Text = "下载完成，正在启动安装程序...";
                    UpdateService.LaunchInstallerAndExit(filePath);
                    dialog.Close();
                }
                catch (OperationCanceledException)
                {
                    // Dialog closed by user.
                }
                catch (Exception ex)
                {
                    progressStatus.Text = $"下载失败：{ex.Message}";
                    installBtn.IsEnabled = true;
                }
            };
            rightButtons.Children.Add(installBtn);
        }

        rightButtons.Children.Add(closeBtn);
        footer.Children.Add(rightButtons);
        Grid.SetRow(footer, 3);
        layout.Children.Add(footer);

        dialog.ShowDialog();
    }

    private void LoadWindowIcon()
    {
        try
        {
            using var iconStream = typeof(MainWindow).Assembly.GetManifestResourceStream("Xianpu.Desktop.Assets.StringNote.ico");
            if (iconStream is not null)
            {
                Icon = BitmapFrame.Create(iconStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            }
        }
        catch
        {
            // Ignore icon load failures; Win32 ApplicationIcon remains active.
        }
    }

    private void RestoreFromTray()
    {
        if (!IsVisible)
            Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void ExitApplication()
    {
        _isExplicitExit = true;
        _trayService?.Dispose();
        _trayService = null;
        Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            e.Cancel = true;
            Hide();
            _trayService?.ShowMinimizedTipOnce();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _trayService?.Dispose();
        _trayService = null;
        base.OnClosed(e);
    }

    private static void OpenExternalUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Ignore browser launch failures.
        }
    }
}
