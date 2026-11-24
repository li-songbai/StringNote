using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xianpu.Desktop.ViewModels;

namespace Xianpu.Desktop;

public partial class MainWindow : Window
{
    private readonly LibraryStore _store = new();
    private readonly MainViewModel _vm = new();
    private LibraryData _library { get => _vm.Library; set => _vm.Library = value; }
    private SongModel? _selectedSong { get => _vm.SelectedSong; set => _vm.SelectedSong = value; }
    private bool _initializing;
    private bool _favoritesOnly { get => _vm.FavoritesOnly; set => _vm.FavoritesOnly = value; }
    private ChordAnchor? _popoverAnchor;
    private string _popoverInitialChord = "";
    private bool _syncingPopoverPosition;
    private TrayService? _trayService;
    private double _savedLibraryWidth = 236;
    private double? _manualZoom;
    private bool _isPanning;
    private Point _panOrigin;
    private double _panStartH;
    private double _panStartV;
    private const string AllCategoriesLabel = MainViewModel.AllCategoriesLabel;
    private bool _restoringUndo;
    private readonly MetronomeService _metronome = new();
    private readonly DispatcherTimer _autoScrollTimer;
    private readonly DispatcherTimer _headerSaveDebounceTimer;
    private readonly Stopwatch _autoScrollClock = new();
    private bool _autoScrollEngaged;
    private string? _startupLoadErrorMessage;
    private string? _startupCorruptedBackupPath;

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
        DataContext = _vm;
        _vm.UndoRequested += PerformUndo;
        _vm.RedoRequested += PerformRedo;
        ApplyAdaptiveInitialWindowSize();
        LoadWindowIcon();

        _autoScrollTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _autoScrollTimer.Tick += OnAutoScrollTick;
        _headerSaveDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(550)
        };
        _headerSaveDebounceTimer.Tick += (_, _) =>
        {
            _headerSaveDebounceTimer.Stop();
            SaveLibraryQuietly();
            RefreshLibraryList();
        };
        _metronome.BeatTick += OnMetronomeBeatTick;

        SourceInitialized += (_, _) =>
        {
            if (PresentationSource.FromVisual(this) is HwndSource source)
                source.AddHook(WindowProc);
        };
        StateChanged += (_, _) => UpdateMaximizeVisual();

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
        ScoreSurface.TabSectionEditRequested += EditTabSection;
        ScoreSurface.TabSectionDuplicateRequested += DuplicateTabSection;
        ScoreSurface.TabSectionDeleteRequested += DeleteTabSection;
        ScoreSurface.TabSectionInsertRequested += InsertNewTabSection;
        ScoreSurface.AddAltLyricRequested += idx => OpenStackedLyricEditor(idx, addNewRow: true);
        ScoreSurface.PartArrangeRequested += OpenPartArrangeDialog;
        ScoreSurface.InsertPartAfterRequested += OpenInsertPartDialog;
        ScoreSurface.StatusMessageRequested += SetStatus;

        Loaded += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(App.StartupFilePath) && File.Exists(App.StartupFilePath))
            {
                LoadFromFilePath(App.StartupFilePath);
            }
            UpdateMaximizeVisual();
            UpdateViewportScale();
            ScoreSurface.Focus();
            _ = CheckForUpdatesSilentlyAsync();

            if (!string.IsNullOrWhiteSpace(_startupLoadErrorMessage))
            {
                var quarantineHint = string.IsNullOrWhiteSpace(_startupCorruptedBackupPath)
                    ? ""
                    : $"\n\n原文件已隔离保存至：\n{_startupCorruptedBackupPath}";
                AppDialogs.ShowInfo(
                    this,
                    "本地曲库读取异常",
                    $"启动时未能正常读取原曲库文件（可能是网盘同步冲突或异常关机导致）：\n{_startupLoadErrorMessage}{quarantineHint}\n\n为防止数据被覆盖，已暂时载入示例曲谱。你可以点击下方按钮从历史快照中一键恢复。",
                    actionLabel: "打开设置与备份",
                    actionCallback: () => SettingsDialog.Show(this, _store, () => _library, ApplyReplacedLibrary));
                _startupLoadErrorMessage = null;
            }
        };
    }

    private void ApplyAdaptiveInitialWindowSize()
    {
        var workArea = SystemParameters.WorkArea;
        if (workArea.Width <= 0 || workArea.Height <= 0) return;

        var targetWidth = Math.Floor(workArea.Width * 0.90);
        var targetHeight = Math.Floor(workArea.Height * 0.90);
        MinWidth = Math.Min(700, Math.Max(560, Math.Floor(workArea.Width * 0.65)));
        MinHeight = Math.Min(500, Math.Max(420, Math.Floor(workArea.Height * 0.65)));

        Width = Math.Clamp(targetWidth, MinWidth, 1240);
        Height = Math.Clamp(targetHeight, MinHeight, 780);

        if (workArea.Width < 1180 && LibraryColumn is not null)
        {
            _savedLibraryWidth = 210;
            LibraryColumn.Width = new GridLength(_savedLibraryWidth);
        }
    }

    private void LoadInitialState()
    {
        var loadedSuccessfully = true;
        try
        {
            _library = _store.Load();
        }
        catch (Exception ex)
        {
            loadedSuccessfully = false;
            LibraryStore.AppendCrashLog("LoadInitialState", ex);
            _startupCorruptedBackupPath = _store.QuarantineCorruptedLibrary();
            _startupLoadErrorMessage = ex.Message;
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
        if (loadedSuccessfully)
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
        FlushPendingHeaderSave();

        if (_selectedSong is not null)
            _selectedSong.PropertyChanged -= SelectedSong_PropertyChanged;

        _selectedSong = song;
        if (!_library.Songs.Contains(song))
            _library.Songs.Add(song);

        StopAutoScroll(clearSession: true);

        _initializing = true;
        TitleBox.Text = song.Title;
        OriginalKeyBox.Text = song.OriginalKey;
        SelectedKeyBox.Text = song.SelectedKey;
        CapoBox.Text = song.Capo;
        if (BpmBox is not null)
            BpmBox.Text = song.Bpm > 0 ? song.Bpm.ToString(CultureInfo.InvariantCulture) : "";
        ColumnModeBox.SelectedValue = song.ColumnMode.ToString(CultureInfo.InvariantCulture);
        ChordDisplayBox.SelectedValue = string.IsNullOrWhiteSpace(song.ChordDisplayMode) ? "name" : song.ChordDisplayMode;
        if (SongCategoryBox is not null)
            SongCategoryBox.Text = string.IsNullOrWhiteSpace(song.Category) ? "未分类" : song.Category;
        if (SongFavoriteToggle is not null)
            SongFavoriteToggle.IsChecked = song.Favorite;
        _initializing = false;

        if (_metronome.IsRunning && song.Bpm > 0)
            _metronome.Bpm = song.Bpm;

        UpdateFavoriteToggleVisual();
        UpdateCaptionTitle();
        ScoreSurface.SetSong(song);
        song.PropertyChanged += SelectedSong_PropertyChanged;
        UpdatePartArrangeButtonCue();
        RefreshQuickChordBar();
        RefreshCategoryOptions();
        RefreshLibraryList();
        ResetUndoHistory(song);
        UpdateActionStates();
        UpdateViewportScale();
    }

    private void FlushPendingHeaderSave()
    {
        if (_headerSaveDebounceTimer.IsEnabled)
        {
            _headerSaveDebounceTimer.Stop();
            SaveLibraryQuietly();
        }
    }

    private void UpdateCaptionTitle()
    {
        if (CaptionSongTitle is null) return;
        var title = string.IsNullOrWhiteSpace(_selectedSong?.Title) ? "吉他歌词和弦记谱" : _selectedSong!.Title.Trim();
        CaptionSongTitle.Text = $"· {title}";
    }

    private void SelectedSong_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        if (e.PropertyName is nameof(SongModel.Title)
            or nameof(SongModel.OriginalKey)
            or nameof(SongModel.SelectedKey)
            or nameof(SongModel.Capo)
            or nameof(SongModel.Bpm)
            or nameof(SongModel.ChordDisplayMode)
            or nameof(SongModel.ColumnMode)
            or nameof(SongModel.ShowChordGuide)
            or nameof(SongModel.ShowTabSections))
        {
            ScoreSurface.Rebuild();
            UpdateViewportScale();
            UpdateActionStates();
        }
        if (e.PropertyName == nameof(SongModel.Title))
        {
            UpdateCaptionTitle();
        }
        if (e.PropertyName == nameof(SongModel.SelectedKey))
        {
            RefreshQuickChordBar();
        }
        _selectedSong.UpdatedAt = DateTime.Now;

        if (e.PropertyName is nameof(SongModel.Title)
            or nameof(SongModel.OriginalKey)
            or nameof(SongModel.SelectedKey)
            or nameof(SongModel.Capo)
            or nameof(SongModel.Bpm))
        {
            _headerSaveDebounceTimer.Stop();
            _headerSaveDebounceTimer.Start();
            return;
        }

        SaveLibraryQuietly();

        if (e.PropertyName is nameof(SongModel.Category)
            or nameof(SongModel.Favorite))
        {
            RefreshLibraryList();
        }
    }

    private void ScoreViewport_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateViewportScale();

    private void UpdateViewportScale()
    {
        if (ScoreSurface is null || ScoreScrollViewer is null || ScoreScrollViewer.ActualWidth <= 0) return;
        var paperWidth = ScoreSurface.PaperWidthCurrent > 0 ? ScoreSurface.PaperWidthCurrent : ScoreSurface.Width;
        var available = Math.Max(320, ScoreScrollViewer.ActualWidth - 48);
        var fitScale = Math.Clamp(available / paperWidth, 0.50, 1.0);
        var scale = _manualZoom ?? fitScale;
        ScoreSurface.LayoutTransform = Math.Abs(scale - 1.0) < 0.01 ? Transform.Identity : new ScaleTransform(scale, scale);

        _vm.Practice.ZoomPercentText = _manualZoom is null ? $"适应 {scale * 100:0}%" : $"{scale * 100:0}%";
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => AdjustZoom(0.1);

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => AdjustZoom(-0.1);

    private void ZoomFit_Click(object sender, RoutedEventArgs e)
    {
        _manualZoom = null;
        UpdateViewportScale();
    }

    private void AdjustZoom(double delta)
    {
        if (ScoreSurface is null || ScoreScrollViewer is null) return;
        var paperWidth = ScoreSurface.PaperWidthCurrent > 0 ? ScoreSurface.PaperWidthCurrent : ScoreSurface.Width;
        var available = Math.Max(320, ScoreScrollViewer.ActualWidth - 48);
        var current = _manualZoom ?? Math.Clamp(available / paperWidth, 0.50, 1.0);
        _manualZoom = Math.Clamp(Math.Round((current + delta) * 20) / 20.0, 0.50, 1.60);
        UpdateViewportScale();
    }

    private void ScoreScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            AdjustZoom(e.Delta > 0 ? 0.1 : -0.1);
            e.Handled = true;
        }
    }

    private void ScoreScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ScoreScrollViewer is null || ScoreSurface is null) return;
        if (IsInsideScrollBar(e.OriginalSource as DependencyObject)) return;

        var isMiddle = e.ChangedButton == MouseButton.Middle;
        var isBlankLeft = e.ChangedButton == MouseButton.Left &&
                          !ScoreSurface.IsPointInteractive(e.GetPosition(ScoreSurface));

        if (isMiddle || isBlankLeft)
        {
            _isPanning = true;
            _panOrigin = e.GetPosition(ScoreScrollViewer);
            _panStartH = ScoreScrollViewer.HorizontalOffset;
            _panStartV = ScoreScrollViewer.VerticalOffset;
            ScoreScrollViewer.Cursor = Cursors.SizeAll;
            ScoreScrollViewer.CaptureMouse();
            if (isMiddle) e.Handled = true;
        }
    }

    private void ScoreScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning || ScoreScrollViewer is null) return;
        if (e.LeftButton != MouseButtonState.Pressed && e.MiddleButton != MouseButtonState.Pressed)
        {
            EndViewportPan();
            return;
        }
        var cur = e.GetPosition(ScoreScrollViewer);
        var dx = cur.X - _panOrigin.X;
        var dy = cur.Y - _panOrigin.Y;
        ScoreScrollViewer.ScrollToHorizontalOffset(_panStartH - dx);
        ScoreScrollViewer.ScrollToVerticalOffset(_panStartV - dy);
    }

    private void ScoreScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e) => EndViewportPan();

    private void ScoreScrollViewer_LostMouseCapture(object sender, MouseEventArgs e) => EndViewportPan();

    private void EndViewportPan()
    {
        if (!_isPanning || ScoreScrollViewer is null) return;
        _isPanning = false;
        if (ScoreScrollViewer.IsMouseCaptured)
            ScoreScrollViewer.ReleaseMouseCapture();
        ScoreScrollViewer.Cursor = Cursors.Arrow;
    }

    private static bool IsInsideScrollBar(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ScrollBar) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
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
        _vm.Practice.StatusMessage = message;
    }

    private string? PromptSingleLine(string title, string label, string initial = "") =>
        AppDialogs.PromptSingleLine(this, title, label, initial);

    private void SaveLibraryQuietly()
    {
        try
        {
            if (_selectedSong is not null)
                _selectedSong.UpdatedAt = DateTime.Now;
            RecordUndoSnapshotIfChanged();
            _store.Save(_library);
        }
        catch (Exception ex)
        {
            LibraryStore.AppendCrashLog("SaveLibraryQuietly", ex);
            SetStatus($"警告：本地曲库保存失败（{ex.Message}），请检查数据目录权限或导出备份。");
        }
    }
}
