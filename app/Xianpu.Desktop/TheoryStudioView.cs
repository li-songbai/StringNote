using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Xianpu.Desktop;

/// <summary>
/// Redesigned Modern Instrument-Grade Guitar Music Theory &amp; CAGED Fretboard Studio:
/// - Warm paper-grey canvas + soft-elevated white cards + segmented pill controls (no cluttered WinForms toolbars)
/// - Native 5-color CAGED pill bar synced with clickable badges directly on the panoramic fretboard
/// - Animated note-by-note fretboard highlights synced with audio playback (scale / CAGED / arpeggio / beat LEDs)
/// - Click-to-audition Chord Anatomy with clickable 6-string decoder columns
/// - DAW-style Progression timeline with 1-click diatonic builder, beat LEDs, and auditionable Coach cards
/// - Auto-saving Journal-style Personal Music Theory Notebook with starter templates
/// </summary>
public sealed class TheoryStudioView : UserControl, IDisposable
{
    private static readonly Brush StudioBg = Freeze(new SolidColorBrush(Color.FromRgb(242, 244, 241)));
    private static readonly Brush CardBorder = Freeze(new SolidColorBrush(Color.FromRgb(220, 228, 223)));
    private static readonly Brush SoftSurface = Freeze(new SolidColorBrush(Color.FromRgb(246, 248, 246)));
    private static readonly Brush SegTrackBg = Freeze(new SolidColorBrush(Color.FromRgb(236, 240, 238)));
    private static readonly Brush WarmPaperBg = Freeze(new SolidColorBrush(Color.FromRgb(253, 251, 246)));
    private static readonly Brush WarmPaperBorder = Freeze(new SolidColorBrush(Color.FromRgb(232, 226, 211)));

    private static readonly Dictionary<string, string> CagedColorMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"] = "#7C9A46",
        ["A"] = "#D96B8A",
        ["G"] = "#8B74C4",
        ["E"] = "#4A8FD2",
        ["D"] = "#39A089"
    };

    private static readonly Dictionary<string, (string Bg, string Fg)> NoteTagColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CAGED"] = ("#ECF5EE", "#245538"),
        ["音阶"] = ("#EFF4FF", "#1D4ED8"),
        ["和弦"] = ("#FEF3E7", "#C2410C"),
        ["即兴"] = ("#F3EEFF", "#6D28D9"),
        ["调弦"] = ("#ECFEFF", "#0E7490"),
        ["笔记"] = ("#F1F5F9", "#475569")
    };

    private readonly GuitarAudioService _audio = new();
    private readonly FretboardCanvas _fretboard = new();
    private readonly DispatcherTimer _progBeatTimer = new();
    private readonly DispatcherTimer _noteAutoSaveTimer = new();

    private Func<SongModel?>? _getCurrentSong;
    private Func<LibraryData>? _getLibrary;
    private Action? _onLibraryMutated;
    private Action? _onSongMutated;
    private Action<string>? _setStatus;

    // Top fretboard bar controls
    private TextBlock _summaryKeyText = null!;
    private TextBlock _summarySubText = null!;
    private StackPanel _legendPanel = null!;
    private ComboBox _tonicBox = null!;
    private ComboBox _scaleBox = null!;
    private ComboBox _tuningBox = null!;
    private ComboBox _capoBox = null!;
    private readonly Dictionary<FretboardLabelMode, Button> _labelModeBtns = [];
    private Button _coordCagedBtn = null!;
    private Button _coord3NpsBtn = null!;
    private Border _cagedTrackBorder = null!;
    private Border _threeNpsTrackBorder = null!;
    private readonly Dictionary<string, Button> _cagedFilterBtns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, Button> _threeNpsBtns = [];
    private Button _capsulePillBtn = null!;
    private Button _mapleWoodBtn = null!;
    private Button _roseWoodBtn = null!;
    private Button _muteBtn = null!;

    // Bottom 5-tab underline navigation
    private readonly Button[] _tabButtons = new Button[5];
    private readonly Border[] _tabIndicators = new Border[5];
    private readonly UIElement[] _tabPages = new UIElement[5];
    private ScaleDrillStageView _drillStage = null!;
    private int _activeTab;

    // Tab 0: Lessons (8 Units · 24 Interactive Practical Guitar Theory Lessons)
    private TextBlock _lessonProgressText = null!;
    private ProgressBar _lessonProgressBar = null!;
    private ComboBox _lessonUnitFilterBox = null!;
    private string _lessonUnitFilter = "";
    private StackPanel _lessonListPanel = null!;
    private WrapPanel _lessonStepPills = null!;
    private TextBlock _lessonStepCounter = null!;
    private TextBlock _lessonUnitBadge = null!;
    private Button _lessonMasteredBtn = null!;
    private TextBlock _lessonTitleText = null!;
    private TextBlock _lessonSummaryText = null!;
    private TextBlock _lessonBodyText = null!;
    private Border _lessonFormulaCard = null!;
    private TextBlock _lessonFormulaText = null!;
    private Border _lessonCompareCard = null!;
    private WrapPanel _lessonCompareWrap = null!;
    private TextBlock _lessonCompareHintText = null!;
    private int _activeCompareIdx = -1;
    private Border _lessonQuizCard = null!;
    private TextBlock _lessonQuizPromptText = null!;
    private Border _lessonQuizFeedbackBorder = null!;
    private TextBlock _lessonQuizFeedbackText = null!;
    private bool _quizSolvedInCurrentStep;
    private WrapPanel _lessonScalePillsWrap = null!;
    private StackPanel _lessonCagedSection = null!;
    private WrapPanel _lessonCagedCardsWrap = null!;
    private WrapPanel _lessonPortalWrap = null!;
    private Button _lessonPrevBtn = null!;
    private Button _lessonNextBtn = null!;
    private int _activeLessonIndex; // Start at Lesson 01
    private int _activeStepIndex;

    // Tab 1: Chord Anatomy
    private bool _chordDiatonicMode = true;
    private bool _chordExploreMode;
    private bool _showChordFingers = true;
    private Button _chordDiatonicBtn = null!;
    private Button _chordFreeBtn = null!;
    private Button _chordBasicBtn = null!;
    private Button _chordExploreBtn = null!;
    private WrapPanel _chordRootWrap = null!;
    private WrapPanel _chordQualityWrap = null!;
    private TextBox _chordCustomSearchBox = null!;
    private TextBlock _chordFunctionText = null!;
    private WrapPanel _chordVariationsWrap = null!;
    private TextBlock _chordAnatomyTitle = null!;
    private TextBlock _chordAnatomySub = null!;
    private TextBlock _chordVoicingCounter = null!;
    private ChordDiagramView _chordDiagramView = null!;
    private Grid _chordDecodeGrid = null!;
    private WrapPanel _chordGalleryWrap = null!;
    private TextBlock _chordGalleryHeader = null!;
    private int _selectedChordRootPitch;
    private string _selectedChordQuality = "";
    private string _activeChordSymbol = "C";
    private IReadOnlyList<CagedChordVoicing> _chordVoicings = [];
    private int _chordCandidateIdx;

    // Tab 2: Progression & Jam Target Notes
    private WrapPanel _progPresetPillsWrap = null!;
    private string _activePresetId = "pop_1564";
    private Button _progPlayBtn = null!;
    private Button _progLoopBtn = null!;
    private Button _progStrumBtn = null!;
    private Button _progArpBtn = null!;
    private Button _progJamColorsBtn = null!;
    private TextBlock _progBpmText = null!;
    private TextBlock _progActiveBanner = null!;
    private WrapPanel _progQuickAddWrap = null!;
    private WrapPanel _progTimelineWrap = null!;
    private ChordDiagramView _progLiveDiagram = null!;
    private TextBlock _progLiveChordTitle = null!;
    private TextBlock _progLiveChordTones = null!;
    private StackPanel _progLiveTargetPanel = null!;
    private Border _progSuggestionCard = null!;
    private StackPanel _progSuggestionList = null!;
    private List<ProgressionStep> _progressionSteps = [];
    private int _progPlayIndex = -1;
    private int _progCurrentBeat; // 0..beats-1
    private int _progBpm = 90;
    private bool _progLoop = true;
    private bool _progArpeggio;
    private bool _progJamColors = true;
    private bool _progPlaying;

    // Tab 3: Personal Theory Notebook
    private string _noteTagFilter = "全部";
    private WrapPanel _noteTagFilterWrap = null!;
    private StackPanel _noteListPanel = null!;
    private TextBox _noteTitleBox = null!;
    private ComboBox _noteTagBox = null!;
    private TextBlock _noteSnapshotBadge = null!;
    private TextBlock _noteSaveStateText = null!;
    private TextBox _noteContentBox = null!;
    private Button _noteApplySnapshotBtn = null!;
    private Button _noteUpdateSnapBtn = null!;
    private Button _noteDeleteBtn = null!;
    private TheoryNoteItem? _selectedNote;
    private bool _populatingNoteEditor;

    private bool _syncingControls;
    private bool _disposed;

    public TheoryStudioView()
    {
        Background = StudioBg;
        Content = BuildLayout();

        _fretboard.FretClicked += (stringIdx, fret, midi) =>
        {
            _audio.PlayNote(midi);
            var preferFlats = MusicTheoryCatalog.PreferFlatsForTonic(_fretboard.TonicPitch);
            var note = MusicTheoryCatalog.MidiToNoteWithOctave(midi, preferFlats);
            var deg = MusicTheoryCatalog.GetDegreeLabel(midi % 12, _fretboard.TonicPitch);
            _setStatus?.Invoke($"指板发声：{note}（{6 - stringIdx}弦 {fret}品 · 当前调内音级：{deg}）");
            if (_activeTab == 0)
                HandleLessonQuizFretClick(stringIdx, fret, midi);
        };

        _fretboard.CagedBadgeClicked += letter =>
        {
            var isAlreadyFocused = _fretboard.ShowCagedBoxes &&
                                   string.Equals(_fretboard.ActiveCagedFilter, letter, StringComparison.OrdinalIgnoreCase);
            _fretboard.ShowCagedBoxes = true;
            _fretboard.ActiveCagedFilter = isAlreadyFocused ? "ALL" : letter;
            _fretboard.HighlightedChord = null;
            SyncTopControlsFromFretboard();
            _fretboard.Refresh();

            if (!isAlreadyFocused)
            {
                PlayAnimatedFretboardTriad();
                _setStatus?.Invoke($"已聚焦 CAGED「{letter}形」把位（再次点击徽章可恢复全指板 5 把位）。");
            }
            else
            {
                _setStatus?.Invoke("已恢复全指板 CAGED 五色咬合全景。");
            }
        };

        _fretboard.ThreeNpsBadgeClicked += npsPos =>
        {
            var isAlready = _fretboard.ShowThreeNpsBoxes && _fretboard.ActiveThreeNpsPosition == npsPos;
            _fretboard.ShowThreeNpsBoxes = true;
            _fretboard.ActiveThreeNpsPosition = isAlready ? 0 : npsPos;
            _fretboard.HighlightedChord = null;
            SyncTopControlsFromFretboard();
            _fretboard.Refresh();

            if (!isAlready)
            {
                PlayAnimatedFretboardSequence();
                _setStatus?.Invoke($"已聚焦 3NPS（每弦三音）第 {npsPos} 把位（再次点击恢复全部 7 把位）。");
            }
            else
            {
                _setStatus?.Invoke("已恢复全指板 3NPS 七把位全景。");
            }
        };

        _progBeatTimer.Tick += OnProgressionBeatTick;
        _noteAutoSaveTimer.Interval = TimeSpan.FromMilliseconds(550);
        _noteAutoSaveTimer.Tick += (_, _) =>
        {
            _noteAutoSaveTimer.Stop();
            SaveSelectedNote(silent: true);
        };

        SyncTopControlsFromFretboard();
        LoadLesson(0, 0, applyToFretboard: false);
        SelectChordBySymbol("C", syncToFretboard: false, autoAudition: false);
        LoadProgressionPreset(MusicTheoryCatalog.ProgressionPresets[0]);
        SwitchTab(0);
    }

    public void AttachHost(
        Func<SongModel?> getCurrentSong,
        Func<LibraryData> getLibrary,
        Action onLibraryMutated,
        Action<string> setStatus,
        Action? onSongMutated = null)
    {
        _getCurrentSong = getCurrentSong;
        _getLibrary = getLibrary;
        _onLibraryMutated = onLibraryMutated;
        _onSongMutated = onSongMutated;
        _setStatus = setStatus;
        RebuildLessonList();
        UpdateLessonMasteredButton();
        RefreshNotesList();
    }

    public void OnEnteredStudio()
    {
        RebuildLessonList();
        UpdateLessonMasteredButton();
        RefreshNotesList();
        SyncWithCurrentSong(silent: true);
    }

    public void OnLeftStudio()
    {
        FlushPendingNoteSave();
        StopProgressionPlayback();
        _drillStage?.StopPlayback();
        _fretboard.StopAnimations();
        _audio.Stop();
    }

    public void HandleSpaceKey()
    {
        if (_activeTab == 3)
            _drillStage?.TogglePlaybackFromShortcut();
        else if (_activeTab == 2)
            ToggleProgressionPlayback();
        else if (_activeTab == 1)
            PlayActiveChord(arpeggio: false);
        else if (_activeTab == 0)
            PlayAnimatedFretboardSequence();
    }

    public void SyncWithCurrentSong(bool silent = false)
    {
        var song = _getCurrentSong?.Invoke();
        if (song is null) return;

        var keyStr = !string.IsNullOrWhiteSpace(song.SelectedKey) ? song.SelectedKey : song.OriginalKey;
        var parsedKey = ChordTranspose.ReadKey(keyStr);
        if (parsedKey is { } pk)
        {
            _fretboard.TonicPitch = pk.Pitch;
            if (pk.Minor && !_fretboard.Scale.IsMinorFamily)
                _fretboard.Scale = MusicTheoryCatalog.GetScale("natural_minor");
            else if (!pk.Minor && _fretboard.Scale.IsMinorFamily)
                _fretboard.Scale = MusicTheoryCatalog.GetScale("major");
        }

        _fretboard.Capo = MusicTheoryCatalog.ParseCapoNumber(song.Capo);
        _fretboard.HighlightedChord = null;
        _fretboard.JamTargetColorMode = false;
        SyncTopControlsFromFretboard();
        RebuildChordRootButtons();
        RebuildProgressionQuickAddBar();
        _fretboard.Refresh();

        if (!silent)
        {
            var capoHint = _fretboard.Capo > 0 ? $" · Capo {_fretboard.Capo}" : "";
            _setStatus?.Invoke($"已同步当前曲谱《{song.Title}》调性：{MusicTheoryCatalog.NoteName(_fretboard.TonicPitch, MusicTheoryCatalog.PreferFlatsForTonic(_fretboard.TonicPitch))} {_fretboard.Scale.Name}{capoHint}。");
        }
    }

    // ─── Top-Level Visual Architecture ────────────────────────────────────────────────

    private UIElement BuildLayout()
    {
        var root = new Grid { Margin = new Thickness(18, 12, 18, 12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ─── Row 0: Panoramic Fretboard Hero Stage ────────────────────────────────────
        var boardCard = CreateElevatedCard(padding: new Thickness(16, 12, 16, 12));
        boardCard.Margin = new Thickness(0, 0, 0, 12);

        var boardStack = new StackPanel();

        // Top minimal status + legend + audio pills
        var topStrip = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

        var rightActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var playShapeBtn = CreatePillButton("▶ 弹指型", isPrimary: true, toolTip: "按音阶/CAGED 把位顺序逐音试听并在指板同步发光");
        playShapeBtn.Margin = new Thickness(0, 0, 6, 0);
        playShapeBtn.Click += (_, _) => PlayAnimatedFretboardSequence();

        var playTriadBtn = CreatePillButton("▶ 听和弦", isPrimary: false, toolTip: "扫弦试听当前把位内置核心和弦");
        playTriadBtn.Margin = new Thickness(0, 0, 6, 0);
        playTriadBtn.Click += (_, _) => PlayAnimatedFretboardTriad();

        var syncSongBtn = CreatePillButton("🔗 同步曲谱", isPrimary: false, toolTip: "读取当前正在编辑曲谱的调号与变调夹");
        syncSongBtn.Margin = new Thickness(0, 0, 6, 0);
        syncSongBtn.Click += (_, _) => SyncWithCurrentSong(silent: false);

        _muteBtn = CreatePillButton("🔊", isPrimary: false, toolTip: "静音 / 取消静音");
        _muteBtn.Padding = new Thickness(9, 4, 9, 4);
        _muteBtn.Click += (_, _) =>
        {
            _audio.IsMuted = !_audio.IsMuted;
            _muteBtn.Content = _audio.IsMuted ? "🔇" : "🔊";
            StyleTogglePill(_muteBtn, _audio.IsMuted, "#DC2626");
        };

        rightActions.Children.Add(playShapeBtn);
        rightActions.Children.Add(playTriadBtn);
        rightActions.Children.Add(syncSongBtn);
        rightActions.Children.Add(_muteBtn);
        DockPanel.SetDock(rightActions, Dock.Right);
        topStrip.Children.Add(rightActions);

        var leftInfo = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        var keyPill = new Border
        {
            Background = AppDialogs.AccentLightBrush,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 3, 10, 3),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _summaryKeyText = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = AppDialogs.AccentHoverBrush
        };
        keyPill.Child = _summaryKeyText;

        _summarySubText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = AppDialogs.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0)
        };
        _legendPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        leftInfo.Children.Add(keyPill);
        leftInfo.Children.Add(_summarySubText);
        leftInfo.Children.Add(_legendPanel);
        topStrip.Children.Add(leftInfo);

        boardStack.Children.Add(topStrip);
        boardStack.Children.Add(_fretboard);
        boardStack.Children.Add(BuildSleekControlDock());

        boardCard.Child = boardStack;
        Grid.SetRow(boardCard, 0);
        root.Children.Add(boardCard);

        // ─── Row 1: Bottom 4-Tab Modern Workspace ─────────────────────────────────────
        var studioCard = CreateElevatedCard(padding: new Thickness(16, 10, 16, 14));
        var studioGrid = new Grid();
        studioGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        studioGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Modern Underline Tab Navigation Bar + Right-Side Quick Snapshot CTA
        var navBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(230, 235, 232)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var navDock = new DockPanel();

        var quickSaveSnapBtn = CreatePillButton("★ 收藏当前指板", isPrimary: false, toolTip: "随时把上方指板的调号、CAGED 把位与和弦拍成快照存入「我的乐理收集本」");
        quickSaveSnapBtn.Margin = new Thickness(0, 0, 0, 6);
        quickSaveSnapBtn.Click += (_, _) =>
        {
            SwitchTab(4);
            CreateTheoryNoteFromCurrentFretboard();
        };
        DockPanel.SetDock(quickSaveSnapBtn, Dock.Right);
        navDock.Children.Add(quickSaveSnapBtn);

        var tabBar = new StackPanel { Orientation = Orientation.Horizontal };
        string[] tabLabels = ["📖 互动乐理与 CAGED 课", "🎸 和弦解剖库", "🎵 和弦进行 & 靶向音", "⚡ 指板练功房", "🗂 我的乐理收集本"];
        for (var i = 0; i < 5; i++)
        {
            var idx = i;
            var wrapper = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
            var btn = new Button
            {
                Content = tabLabels[i],
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 4, 4, 8),
                FontSize = 13,
                Cursor = Cursors.Hand
            };
            btn.Click += (_, _) => SwitchTab(idx);
            var indicator = new Border
            {
                Height = 2.5,
                CornerRadius = new CornerRadius(1.5),
                Background = AppDialogs.AccentBrush,
                Visibility = Visibility.Hidden
            };
            _tabButtons[i] = btn;
            _tabIndicators[i] = indicator;
            wrapper.Children.Add(btn);
            wrapper.Children.Add(indicator);
            tabBar.Children.Add(wrapper);
        }
        navDock.Children.Add(tabBar);
        navBorder.Child = navDock;
        Grid.SetRow(navBorder, 0);
        studioGrid.Children.Add(navBorder);

        _drillStage = new ScaleDrillStageView(
            _fretboard,
            _audio,
            SyncTopControlsFromFretboard,
            () => _getCurrentSong?.Invoke(),
            () => _getLibrary?.Invoke(),
            () => _onLibraryMutated?.Invoke(),
            () =>
            {
                _onSongMutated?.Invoke();
                _onLibraryMutated?.Invoke();
            },
            msg => _setStatus?.Invoke(msg));

        var tabHost = new Grid();
        _tabPages[0] = BuildLessonsTab();
        _tabPages[1] = BuildChordAnatomyTab();
        _tabPages[2] = BuildProgressionTab();
        _tabPages[3] = _drillStage;
        _tabPages[4] = BuildNotebookTab();
        foreach (var page in _tabPages)
            tabHost.Children.Add(page);

        Grid.SetRow(tabHost, 1);
        studioGrid.Children.Add(tabHost);

        studioCard.Child = studioGrid;
        Grid.SetRow(studioCard, 1);
        root.Children.Add(studioCard);

        return root;
    }

    private UIElement BuildSleekControlDock()
    {
        var dock = new Border
        {
            Background = SoftSurface,
            BorderBrush = new SolidColorBrush(Color.FromRgb(228, 234, 230)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 8, 0, 0)
        };

        var wrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };

        // 1. Key + Scale
        wrap.Children.Add(MakeDockLabel("调号"));
        _tonicBox = new ComboBox { Width = 54, Height = 26, Margin = new Thickness(0, 2, 8, 2) };
        for (var i = 0; i < 12; i++)
            _tonicBox.Items.Add(new ComboBoxItem { Content = MusicTheoryCatalog.TonicLabels[i], Tag = i });
        _tonicBox.SelectionChanged += (_, _) =>
        {
            if (_syncingControls || _tonicBox.SelectedItem is not ComboBoxItem { Tag: int pc }) return;
            _fretboard.TonicPitch = pc;
            _fretboard.HighlightedChord = null;
            NotifyFretboardManualChange();
        };
        wrap.Children.Add(_tonicBox);

        _scaleBox = new ComboBox { Width = 172, Height = 26, Margin = new Thickness(0, 2, 10, 2) };
        foreach (var sc in MusicTheoryCatalog.Scales)
            _scaleBox.Items.Add(new ComboBoxItem { Content = $"{sc.Name}", Tag = sc.Id });
        _scaleBox.SelectionChanged += (_, _) =>
        {
            if (_syncingControls || _scaleBox.SelectedItem is not ComboBoxItem { Tag: string id }) return;
            _fretboard.Scale = MusicTheoryCatalog.GetScale(id);
            NotifyFretboardManualChange();
        };
        wrap.Children.Add(_scaleBox);

        // 2. Segmented Label Mode Switcher (音级 | 音名 | 音程)
        var labelSeg = CreateSegmentedContainer();
        labelSeg.Margin = new Thickness(0, 2, 10, 2);
        var labelSegStack = (StackPanel)labelSeg.Child;
        (FretboardLabelMode Mode, string Text)[] labelItems =
        [
            (FretboardLabelMode.ScaleDegree, "音级"),
            (FretboardLabelMode.NoteName, "音名"),
            (FretboardLabelMode.Interval, "音程")
        ];
        foreach (var (mode, text) in labelItems)
        {
            var m = mode;
            var b = CreateSegButton(text);
            b.Click += (_, _) =>
            {
                _fretboard.LabelMode = m;
                UpdateLabelSegStyles();
                _fretboard.Refresh();
            };
            _labelModeBtns[m] = b;
            labelSegStack.Children.Add(b);
        }
        wrap.Children.Add(labelSeg);

        // 3. Coordinate System Switcher (CAGED | 3NPS) + Dynamic Position Track
        var coordSeg = CreateSegmentedContainer();
        coordSeg.Margin = new Thickness(0, 2, 6, 2);
        var coordStack = (StackPanel)coordSeg.Child;
        _coordCagedBtn = CreateSegButton("CAGED");
        _coordCagedBtn.ToolTip = "五把位盒子坐标系（最适合和弦内音定位与弹唱加花）";
        _coordCagedBtn.Click += (_, _) =>
        {
            _fretboard.ShowThreeNpsBoxes = false;
            _fretboard.ShowCagedBoxes = true;
            _drillStage?.SyncPositionModeFromFretboard();
            SyncTopControlsFromFretboard();
            _fretboard.Refresh();
        };
        _coord3NpsBtn = CreateSegButton("3NPS");
        _coord3NpsBtn.ToolTip = "每弦三音斜向七把位坐标系（最适合音阶模进、跨把与高速连奏）";
        _coord3NpsBtn.Click += (_, _) =>
        {
            _fretboard.ShowThreeNpsBoxes = true;
            _fretboard.ShowCapsules = false;
            _drillStage?.SyncPositionModeFromFretboard();
            SyncTopControlsFromFretboard();
            _fretboard.Refresh();
        };
        coordStack.Children.Add(_coordCagedBtn);
        coordStack.Children.Add(_coord3NpsBtn);
        wrap.Children.Add(coordSeg);

        _cagedTrackBorder = CreateSegmentedContainer();
        _cagedTrackBorder.Margin = new Thickness(0, 2, 8, 2);
        var cagedStack = (StackPanel)_cagedTrackBorder.Child;
        (string Code, string Label, string Tip)[] cagedItems =
        [
            ("OFF", "关", "隐藏 CAGED 把位框"),
            ("ALL", "全", "显示全指板 5 个咬合把位"),
            ("C", "C", "聚焦 C 指型把位（橄榄绿）"),
            ("A", "A", "聚焦 A 指型把位（樱桃粉）"),
            ("G", "G", "聚焦 G 指型把位（薰衣草紫）"),
            ("E", "E", "聚焦 E 指型把位（钴蓝）"),
            ("D", "D", "聚焦 D 指型把位（青碧）")
        ];
        foreach (var (code, label, tip) in cagedItems)
        {
            var c = code;
            var btn = CreateSegButton(label, minWidth: c.Length == 1 ? 24 : 28);
            btn.ToolTip = tip;
            btn.Click += (_, _) =>
            {
                _fretboard.ShowThreeNpsBoxes = false;
                if (c == "OFF")
                {
                    _fretboard.ShowCagedBoxes = false;
                }
                else
                {
                    _fretboard.ShowCagedBoxes = true;
                    _fretboard.ActiveCagedFilter = c;
                    _fretboard.HighlightedChord = null;
                }
                SyncTopControlsFromFretboard();
                _fretboard.Refresh();
            };
            _cagedFilterBtns[c] = btn;
            cagedStack.Children.Add(btn);
        }
        wrap.Children.Add(_cagedTrackBorder);

        _threeNpsTrackBorder = CreateSegmentedContainer();
        _threeNpsTrackBorder.Visibility = Visibility.Collapsed;
        _threeNpsTrackBorder.Margin = new Thickness(0, 2, 8, 2);
        var npsStack = (StackPanel)_threeNpsTrackBorder.Child;
        for (var p = 0; p <= 7; p++)
        {
            var posNum = p;
            var lbl = posNum == 0 ? "全" : $"{posNum}把";
            var nb = CreateSegButton(lbl, minWidth: 26);
            nb.ToolTip = posNum == 0 ? "显示全部 7 个每弦三音把位" : $"聚焦 3NPS 第 {posNum} 把位";
            nb.Click += (_, _) =>
            {
                _fretboard.ShowThreeNpsBoxes = true;
                _fretboard.ActiveThreeNpsPosition = posNum;
                _fretboard.HighlightedChord = null;
                SyncTopControlsFromFretboard();
                _fretboard.Refresh();
            };
            _threeNpsBtns[posNum] = nb;
            npsStack.Children.Add(nb);
        }
        wrap.Children.Add(_threeNpsTrackBorder);

        _capsulePillBtn = CreatePillButton("双音胶囊", isPrimary: false, toolTip: "用柔和胶囊圈出每根弦上的五声音阶双音对");
        _capsulePillBtn.Padding = new Thickness(9, 3, 9, 3);
        _capsulePillBtn.Margin = new Thickness(0, 2, 10, 2);
        _capsulePillBtn.Click += (_, _) =>
        {
            _fretboard.ShowCapsules = !_fretboard.ShowCapsules;
            if (_fretboard.ShowCapsules) _fretboard.ShowCagedBoxes = true;
            SyncTopControlsFromFretboard();
            _fretboard.Refresh();
        };
        wrap.Children.Add(_capsulePillBtn);

        // 4. Tuning + Capo + Wood Segmented Switch
        _tuningBox = new ComboBox { Width = 132, Height = 26, Margin = new Thickness(0, 2, 6, 2), ToolTip = "切换吉他调弦（Standard / Drop D / DADGAD / Open G 等）" };
        foreach (var t in MusicTheoryCatalog.Tunings)
            _tuningBox.Items.Add(new ComboBoxItem { Content = t.Name, Tag = t.Id });
        _tuningBox.SelectionChanged += (_, _) =>
        {
            if (_syncingControls || _tuningBox.SelectedItem is not ComboBoxItem { Tag: string tid }) return;
            _fretboard.Tuning = MusicTheoryCatalog.GetTuning(tid);
            NotifyFretboardManualChange();
        };
        wrap.Children.Add(_tuningBox);

        _capoBox = new ComboBox { Width = 68, Height = 26, Margin = new Thickness(0, 2, 8, 2), ToolTip = "变调夹品位" };
        _capoBox.Items.Add(new ComboBoxItem { Content = "不夹", Tag = 0 });
        for (var c = 1; c <= 11; c++)
            _capoBox.Items.Add(new ComboBoxItem { Content = $"Capo {c}", Tag = c });
        _capoBox.SelectionChanged += (_, _) =>
        {
            if (_syncingControls || _capoBox.SelectedItem is not ComboBoxItem { Tag: int capo }) return;
            _fretboard.Capo = capo;
            NotifyFretboardManualChange();
        };
        wrap.Children.Add(_capoBox);

        var woodSeg = CreateSegmentedContainer();
        woodSeg.Margin = new Thickness(0, 2, 0, 2);
        var woodStack = (StackPanel)woodSeg.Child;
        _mapleWoodBtn = CreateSegButton("枫木");
        _mapleWoodBtn.Click += (_, _) => { _fretboard.DarkRosewood = false; UpdateWoodSegStyles(); _fretboard.Refresh(); };
        _roseWoodBtn = CreateSegButton("玫瑰木");
        _roseWoodBtn.Click += (_, _) => { _fretboard.DarkRosewood = true; UpdateWoodSegStyles(); _fretboard.Refresh(); };
        woodStack.Children.Add(_mapleWoodBtn);
        woodStack.Children.Add(_roseWoodBtn);
        wrap.Children.Add(woodSeg);

        dock.Child = wrap;
        return dock;
    }

    // ─── Tab 0: Interactive Theory & CAGED Academy (8 Units · 24 Lessons) ───────────

    private UIElement BuildLessonsTab()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(288) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left: Course Progress Header + Unit Filter + 24-Lesson Syllabus Rail
        var leftGrid = new Grid();
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var progCard = new Border
        {
            Background = WarmPaperBg,
            BorderBrush = WarmPaperBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var progStack = new StackPanel();
        _lessonProgressText = new TextBlock
        {
            Text = "🎓 吉他实用乐理全课 · 已掌握 0 / 24 课",
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            Foreground = AppDialogs.AccentHoverBrush,
            Margin = new Thickness(0, 0, 0, 5)
        };
        progStack.Children.Add(_lessonProgressText);

        _lessonProgressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = Math.Max(1, MusicTheoryCatalog.Lessons.Count),
            Value = 0,
            Height = 5,
            Foreground = AppDialogs.AccentBrush,
            Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 7)
        };
        progStack.Children.Add(_lessonProgressBar);

        _lessonUnitFilterBox = new ComboBox { Height = 25, FontSize = 11.5 };
        _lessonUnitFilterBox.Items.Add(new ComboBoxItem { Content = $"全部 8 大单元（共 {MusicTheoryCatalog.Lessons.Count} 课）", Tag = "" });
        foreach (var unit in MusicTheoryCatalog.Lessons.Select(l => l.UnitName).Distinct())
        {
            var cnt = MusicTheoryCatalog.Lessons.Count(l => l.UnitName == unit);
            _lessonUnitFilterBox.Items.Add(new ComboBoxItem { Content = $"{unit}（{cnt}课）", Tag = unit });
        }
        _lessonUnitFilterBox.SelectedIndex = 0;
        _lessonUnitFilterBox.SelectionChanged += (_, _) =>
        {
            if (_lessonUnitFilterBox.SelectedItem is ComboBoxItem { Tag: string u })
            {
                _lessonUnitFilter = u;
                RebuildLessonList();
            }
        };
        progStack.Children.Add(_lessonUnitFilterBox);
        progCard.Child = progStack;
        Grid.SetRow(progCard, 0);
        leftGrid.Children.Add(progCard);

        _lessonListPanel = new StackPanel();
        var listScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _lessonListPanel
        };
        var listPanel = new Border
        {
            Background = SoftSurface,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(7, 7, 7, 7),
            Child = listScroll
        };
        Grid.SetRow(listPanel, 1);
        leftGrid.Children.Add(listPanel);

        Grid.SetColumn(leftGrid, 0);
        grid.Children.Add(leftGrid);

        // Right: Warm Editorial Interactive Lesson Stage
        var rightCard = new Border
        {
            Background = WarmPaperBg,
            BorderBrush = WarmPaperBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(20, 14, 20, 14)
        };
        var rightGrid = new Grid();
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Meta + Mastery pill
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Step pills
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title + Summary
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Rich Interactive Body
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Action & Portal bar

        var metaRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var rightMetaStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _lessonStepCounter = new TextBlock
        {
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        rightMetaStack.Children.Add(_lessonStepCounter);

        _lessonMasteredBtn = CreatePillButton("○ 标记已掌握", isPrimary: false, toolTip: "手动标记或通过指板随堂闯关自动点亮已掌握进度");
        _lessonMasteredBtn.FontSize = 11;
        _lessonMasteredBtn.Padding = new Thickness(9, 2.5, 9, 2.5);
        _lessonMasteredBtn.Click += (_, _) => ToggleCurrentLessonMastered();
        rightMetaStack.Children.Add(_lessonMasteredBtn);
        DockPanel.SetDock(rightMetaStack, Dock.Right);
        metaRow.Children.Add(rightMetaStack);

        var unitPill = new Border
        {
            Background = AppDialogs.AccentLightBrush,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(9, 2.5, 9, 2.5),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _lessonUnitBadge = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = AppDialogs.AccentHoverBrush };
        unitPill.Child = _lessonUnitBadge;
        metaRow.Children.Add(unitPill);
        Grid.SetRow(metaRow, 0);
        rightGrid.Children.Add(metaRow);

        _lessonStepPills = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(_lessonStepPills, 1);
        rightGrid.Children.Add(_lessonStepPills);

        var titleStack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        _lessonTitleText = new TextBlock
        {
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = AppDialogs.TitleBrush,
            TextWrapping = TextWrapping.Wrap
        };
        _lessonSummaryText = new TextBlock
        {
            FontSize = 12,
            Foreground = AppDialogs.MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0)
        };
        titleStack.Children.Add(_lessonTitleText);
        titleStack.Children.Add(_lessonSummaryText);
        Grid.SetRow(titleStack, 2);
        rightGrid.Children.Add(titleStack);

        var bodyStack = new StackPanel();
        _lessonBodyText = new TextBlock
        {
            FontSize = 13.5,
            Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 24,
            Margin = new Thickness(0, 0, 0, 12)
        };
        bodyStack.Children.Add(_lessonBodyText);

        // 1. Key Formula / Mnemonic Callout Card
        _lessonFormulaCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(238, 247, 241)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(47, 107, 72)),
            BorderThickness = new Thickness(4, 1, 1, 1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 10),
            Visibility = Visibility.Collapsed
        };
        var formulaStack = new StackPanel();
        formulaStack.Children.Add(new TextBlock
        {
            Text = "📌 核心口诀与推导公式",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = AppDialogs.AccentHoverBrush,
            Margin = new Thickness(0, 0, 0, 3)
        });
        _lessonFormulaText = new TextBlock
        {
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.TitleBrush,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20
        };
        formulaStack.Children.Add(_lessonFormulaText);
        _lessonFormulaCard.Child = formulaStack;
        bodyStack.Children.Add(_lessonFormulaCard);

        // 2. Interactive AB Ear-Comparison Bench
        _lessonCompareCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(0, 0, 0, 10),
            Visibility = Visibility.Collapsed
        };
        var cmpStack = new StackPanel();
        cmpStack.Children.Add(new TextBlock
        {
            Text = "🎧 AB 听感对比试验台 · 点击任意胶囊，上方指板同步变形并试听差异",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(29, 78, 216)),
            Margin = new Thickness(0, 0, 0, 6)
        });
        _lessonCompareWrap = new WrapPanel();
        cmpStack.Children.Add(_lessonCompareWrap);
        _lessonCompareHintText = new TextBlock
        {
            FontSize = 12,
            Foreground = AppDialogs.BodyBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 4, 0, 0),
            Visibility = Visibility.Collapsed
        };
        cmpStack.Children.Add(_lessonCompareHintText);
        _lessonCompareCard.Child = cmpStack;
        bodyStack.Children.Add(_lessonCompareCard);

        // 3. Interactive Fretboard Point-and-Click Quiz Card
        _lessonQuizCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(255, 251, 235)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
            BorderThickness = new Thickness(4, 1, 1, 1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(0, 0, 0, 10),
            Visibility = Visibility.Collapsed
        };
        var quizStack = new StackPanel();
        var quizHead = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var hintBtn = CreatePillButton("💡 闪烁提示", isPrimary: false, toolTip: "在上方指板短暂闪烁目标音位置");
        hintBtn.FontSize = 10.5;
        hintBtn.Padding = new Thickness(8, 2, 8, 2);
        hintBtn.Click += (_, _) => FlashCurrentLessonQuizHint();
        DockPanel.SetDock(hintBtn, Dock.Right);
        quizHead.Children.Add(hintBtn);
        quizHead.Children.Add(new TextBlock
        {
            Text = "🎯 随堂指板点按闯关（直接在上方全景指板点击对应品格验证）",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 83, 9)),
            VerticalAlignment = VerticalAlignment.Center
        });
        quizStack.Children.Add(quizHead);

        _lessonQuizPromptText = new TextBlock
        {
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.TitleBrush,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
            Margin = new Thickness(0, 0, 0, 6)
        };
        quizStack.Children.Add(_lessonQuizPromptText);

        _lessonQuizFeedbackBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(9, 5, 9, 5)
        };
        _lessonQuizFeedbackText = new TextBlock
        {
            Text = "👆 请点击上方全景指板中的目标音品格作答…",
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14)),
            TextWrapping = TextWrapping.Wrap
        };
        _lessonQuizFeedbackBorder.Child = _lessonQuizFeedbackText;
        quizStack.Children.Add(_lessonQuizFeedbackBorder);
        _lessonQuizCard.Child = quizStack;
        bodyStack.Children.Add(_lessonQuizCard);

        // 4. Interactive Scale Formula Pills (Click any degree -> flashes all occurrences on fretboard & plays note!)
        var scaleFormulaCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var scaleFormulaStack = new StackPanel();
        scaleFormulaStack.Children.Add(new TextBlock
        {
            Text = "本步音阶配方 · 点击任意音级，全指板同名音同步闪烁发声",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.MutedBrush,
            Margin = new Thickness(0, 0, 0, 6)
        });
        _lessonScalePillsWrap = new WrapPanel();
        scaleFormulaStack.Children.Add(_lessonScalePillsWrap);
        scaleFormulaCard.Child = scaleFormulaStack;
        bodyStack.Children.Add(scaleFormulaCard);

        // 5. Interactive CAGED 5-Box Quick-Jump Cards
        _lessonCagedSection = new StackPanel();
        _lessonCagedSection.Children.Add(new TextBlock
        {
            Text = "本调 CAGED 五把位直切速览（点哪张卡，上方指板立即聚焦哪块并试听）",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.MutedBrush,
            Margin = new Thickness(2, 0, 0, 6)
        });
        _lessonCagedCardsWrap = new WrapPanel();
        _lessonCagedSection.Children.Add(_lessonCagedCardsWrap);
        bodyStack.Children.Add(_lessonCagedSection);

        var bodyScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = bodyStack
        };
        Grid.SetRow(bodyScroll, 3);
        rightGrid.Children.Add(bodyScroll);

        var bottomBar = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var navBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _lessonPrevBtn = CreatePillButton("← 上一步", isPrimary: false);
        _lessonPrevBtn.Margin = new Thickness(0, 0, 8, 0);
        _lessonPrevBtn.Click += (_, _) => StepLesson(-1);

        _lessonNextBtn = CreatePillButton("下一步 →", isPrimary: true);
        _lessonNextBtn.Click += (_, _) => StepLesson(1);

        navBtns.Children.Add(_lessonPrevBtn);
        navBtns.Children.Add(_lessonNextBtn);
        DockPanel.SetDock(navBtns, Dock.Right);
        bottomBar.Children.Add(navBtns);

        var leftDemoWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        var playShapeBtn = CreatePillButton("▶ 动画弹音阶", isPrimary: true, toolTip: "逐音拨弦试听当前把位/音阶，上方指板同步点亮对应圆点");
        playShapeBtn.Margin = new Thickness(0, 2, 6, 2);
        playShapeBtn.Click += (_, _) => PlayAnimatedFretboardSequence();

        var playChordBtn = CreatePillButton("▶ 听核心和弦", isPrimary: false, toolTip: "扫弦试听藏在当前把位里的核心和弦");
        playChordBtn.Margin = new Thickness(0, 2, 8, 2);
        playChordBtn.Click += (_, _) => PlayAnimatedFretboardTriad();

        _lessonPortalWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        leftDemoWrap.Children.Add(playShapeBtn);
        leftDemoWrap.Children.Add(playChordBtn);
        leftDemoWrap.Children.Add(_lessonPortalWrap);
        bottomBar.Children.Add(leftDemoWrap);

        Grid.SetRow(bottomBar, 4);
        rightGrid.Children.Add(bottomBar);

        rightCard.Child = rightGrid;
        Grid.SetColumn(rightCard, 2);
        grid.Children.Add(rightCard);

        RebuildLessonList();
        return grid;
    }

    // ─── Tab 1: Chord Anatomy Workspace (Audition + Clickable 6-String Decoder) ───────

    private UIElement BuildChordAnatomyTab()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.84, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.46, GridUnitType.Star) });

        // Left: Tactile Chord Picker
        var leftScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var leftStack = new StackPanel { Margin = new Thickness(0, 0, 4, 0) };

        var filterBar = new WrapPanel { Margin = new Thickness(0, 0, 0, 10), VerticalAlignment = VerticalAlignment.Center };

        var modeSeg = CreateSegmentedContainer();
        modeSeg.Margin = new Thickness(0, 2, 10, 2);
        var modeStack = (StackPanel)modeSeg.Child;
        _chordDiatonicBtn = CreateSegButton("调内 1–7级", minWidth: 74);
        _chordDiatonicBtn.Click += (_, _) => { _chordDiatonicMode = true; RebuildChordRootButtons(); };
        _chordFreeBtn = CreateSegButton("自由 12音", minWidth: 68);
        _chordFreeBtn.Click += (_, _) => { _chordDiatonicMode = false; RebuildChordRootButtons(); };
        modeStack.Children.Add(_chordDiatonicBtn);
        modeStack.Children.Add(_chordFreeBtn);
        filterBar.Children.Add(modeSeg);

        var qualSeg = CreateSegmentedContainer();
        qualSeg.Margin = new Thickness(0, 2, 0, 2);
        var qualStack = (StackPanel)qualSeg.Child;
        _chordBasicBtn = CreateSegButton("常用和弦", minWidth: 64);
        _chordBasicBtn.Click += (_, _) => { _chordExploreMode = false; RebuildChordQualityButtons(); };
        _chordExploreBtn = CreateSegButton("全色彩探索", minWidth: 74);
        _chordExploreBtn.Click += (_, _) => { _chordExploreMode = true; RebuildChordQualityButtons(); };
        qualStack.Children.Add(_chordBasicBtn);
        qualStack.Children.Add(_chordExploreBtn);
        filterBar.Children.Add(qualSeg);

        leftStack.Children.Add(filterBar);

        leftStack.Children.Add(new TextBlock { Text = "根音（点击自动试听并高亮指板）", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = AppDialogs.MutedBrush, Margin = new Thickness(0, 0, 0, 5) });
        _chordRootWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        leftStack.Children.Add(_chordRootWrap);

        leftStack.Children.Add(new TextBlock { Text = "性质与色彩", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = AppDialogs.MutedBrush, Margin = new Thickness(0, 0, 0, 5) });
        _chordQualityWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        leftStack.Children.Add(_chordQualityWrap);

        var searchRow = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
        var searchBtn = CreatePillButton("解剖", isPrimary: true);
        searchBtn.Margin = new Thickness(6, 0, 0, 0);
        searchBtn.Click += (_, _) =>
        {
            var sym = ChordCatalog.NormalizeChordName(_chordCustomSearchBox.Text);
            if (!string.IsNullOrWhiteSpace(sym))
                SelectChordBySymbol(sym, syncToFretboard: true, autoAudition: true);
        };
        DockPanel.SetDock(searchBtn, Dock.Right);
        searchRow.Children.Add(searchBtn);

        _chordCustomSearchBox = new TextBox
        {
            FontSize = 12.5,
            Padding = new Thickness(10, 5, 10, 5),
            ToolTip = "直接键入任意和弦记号（如 C9、Am7sus4、Dm7/G、E7#9）回车立即推导指法并解剖"
        };
        _chordCustomSearchBox.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Return)
            {
                var sym = ChordCatalog.NormalizeChordName(_chordCustomSearchBox.Text);
                if (!string.IsNullOrWhiteSpace(sym))
                    SelectChordBySymbol(sym, syncToFretboard: true, autoAudition: true);
                e.Handled = true;
            }
        };
        searchRow.Children.Add(_chordCustomSearchBox);
        leftStack.Children.Add(new TextBlock { Text = "自由输入任意延伸 / 转位和弦（回车即查）", FontSize = 11, Foreground = AppDialogs.MutedBrush, Margin = new Thickness(0, 2, 0, 4) });
        leftStack.Children.Add(searchRow);

        // Lower-left fill: Harmonic Function Card + Same-Root Color Variations
        var funcCard = new Border
        {
            Background = WarmPaperBg,
            BorderBrush = WarmPaperBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(11, 9, 11, 9),
            Margin = new Thickness(0, 12, 0, 10)
        };
        var funcStack = new StackPanel();
        funcStack.Children.Add(new TextBlock
        {
            Text = "本调和声功能速览",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = AppDialogs.AccentHoverBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        _chordFunctionText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = AppDialogs.BodyBrush,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 19
        };
        funcStack.Children.Add(_chordFunctionText);
        funcCard.Child = funcStack;
        leftStack.Children.Add(funcCard);

        leftStack.Children.Add(new TextBlock
        {
            Text = "同根音色彩变体 · 一键切听对比",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.MutedBrush,
            Margin = new Thickness(0, 0, 0, 5)
        });
        _chordVariationsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        leftStack.Children.Add(_chordVariationsWrap);

        leftScroll.Content = leftStack;
        Grid.SetColumn(leftScroll, 0);
        grid.Children.Add(leftScroll);

        // Right: Sleek Chord Anatomy Card
        var rightCard = new Border
        {
            Background = SoftSurface,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 14, 16, 14)
        };
        var rightScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var rightStack = new StackPanel();

        var headDock = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };

        var voicingCtrl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var fingerCheck = new CheckBox
        {
            Content = "显示指法",
            IsChecked = true,
            FontSize = 11.5,
            Foreground = AppDialogs.BodyBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        fingerCheck.Checked += (_, _) => { _showChordFingers = true; RefreshChordAnatomyDisplay(); };
        fingerCheck.Unchecked += (_, _) => { _showChordFingers = false; RefreshChordAnatomyDisplay(); };

        var pagerBox = CreateSegmentedContainer();
        var pagerStack = (StackPanel)pagerBox.Child;
        var prevV = CreateSegButton("‹", minWidth: 24);
        prevV.Click += (_, _) => CycleChordCandidate(-1);
        _chordVoicingCounter = new TextBlock { Text = "1 / 1", FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = AppDialogs.TitleBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
        var nextV = CreateSegButton("›", minWidth: 24);
        nextV.Click += (_, _) => CycleChordCandidate(1);
        pagerStack.Children.Add(prevV);
        pagerStack.Children.Add(_chordVoicingCounter);
        pagerStack.Children.Add(nextV);

        voicingCtrl.Children.Add(fingerCheck);
        voicingCtrl.Children.Add(pagerBox);
        DockPanel.SetDock(voicingCtrl, Dock.Right);
        headDock.Children.Add(voicingCtrl);

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _chordAnatomyTitle = new TextBlock { Text = "C", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = AppDialogs.TitleBrush, VerticalAlignment = VerticalAlignment.Center };
        _chordAnatomySub = new TextBlock { Text = "开放和弦", FontSize = 12, Foreground = AppDialogs.MutedBrush, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(8, 0, 0, 2) };
        titleStack.Children.Add(_chordAnatomyTitle);
        titleStack.Children.Add(_chordAnatomySub);
        headDock.Children.Add(titleStack);
        rightStack.Children.Add(headDock);

        // Diagram + Clickable 6-String Decoder Card
        var bodyDock = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        _chordDiagramView = new ChordDiagramView { Width = 114, Height = 138, VerticalAlignment = VerticalAlignment.Center };
        var diagramBox = new Border
        {
            Background = Brushes.White,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 14, 0),
            Cursor = Cursors.Hand,
            ToolTip = "点击和弦图扫弦试听",
            Child = _chordDiagramView
        };
        diagramBox.MouseLeftButtonDown += (_, _) => PlayActiveChord(arpeggio: false);
        DockPanel.SetDock(diagramBox, Dock.Left);
        bodyDock.Children.Add(diagramBox);

        var tableCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 8, 10, 8),
            VerticalAlignment = VerticalAlignment.Center
        };
        var tableWrapStack = new StackPanel();
        _chordDecodeGrid = new Grid();
        tableWrapStack.Children.Add(_chordDecodeGrid);
        tableWrapStack.Children.Add(new TextBlock
        {
            Text = "提示：点击任意一列弦位，可单独拨响该弦并在上方指板闪烁定位",
            FontSize = 10.5,
            Foreground = AppDialogs.MutedBrush,
            Margin = new Thickness(2, 6, 0, 0)
        });
        tableCard.Child = tableWrapStack;
        bodyDock.Children.Add(tableCard);
        rightStack.Children.Add(bodyDock);

        // Action Pills
        var actionWrap = new WrapPanel();
        var strumBtn = CreatePillButton("▶ 扫弦试听", isPrimary: true);
        strumBtn.Margin = new Thickness(0, 2, 8, 2);
        strumBtn.Click += (_, _) => PlayActiveChord(arpeggio: false);

        var arpBtn = CreatePillButton("▶ 分解试听", isPrimary: false);
        arpBtn.Margin = new Thickness(0, 2, 8, 2);
        arpBtn.Click += (_, _) => PlayActiveChord(arpeggio: true);

        var addToProgBtn = CreatePillButton("+ 加入和弦进行", isPrimary: false);
        addToProgBtn.Margin = new Thickness(0, 2, 8, 2);
        addToProgBtn.Click += (_, _) =>
        {
            var deg = ChordTranspose.ToDegree(_activeChordSymbol, MusicTheoryCatalog.NoteName(_fretboard.TonicPitch));
            _progressionSteps.Add(new ProgressionStep(_activeChordSymbol, deg, 4));
            RebuildProgressionTimeline();
            _setStatus?.Invoke($"已将「{_activeChordSymbol}」加入和弦进行末尾。");
        };

        var clearHiBtn = CreatePillButton("显示全指板音阶", isPrimary: false);
        clearHiBtn.Margin = new Thickness(0, 2, 8, 2);
        clearHiBtn.Click += (_, _) =>
        {
            _fretboard.HighlightedChord = null;
            UpdateSummaryAndLegend();
            _fretboard.Refresh();
        };

        var pinCustomBtn = CreatePillButton("📌 收录到曲谱常用和弦", isPrimary: false, toolTip: "将当前选中的和弦指法直接收录到主曲谱本调常用和弦栏，回「编辑制谱」马上就能点选使用");
        pinCustomBtn.Margin = new Thickness(0, 2, 0, 2);
        pinCustomBtn.Click += (_, _) => PinActiveChordToLibraryCustomChords();

        actionWrap.Children.Add(strumBtn);
        actionWrap.Children.Add(arpBtn);
        actionWrap.Children.Add(addToProgBtn);
        actionWrap.Children.Add(clearHiBtn);
        actionWrap.Children.Add(pinCustomBtn);
        rightStack.Children.Add(actionWrap);

        // Visual CAGED & Multi-Position Hand-Shape Gallery (fills lower workspace!)
        var gallerySep = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(226, 232, 228)),
            Margin = new Thickness(0, 12, 0, 10)
        };
        rightStack.Children.Add(gallerySep);

        _chordGalleryHeader = new TextBlock
        {
            Text = "CAGED 全指板五手型（点击任意手型卡片，上方指板同步高亮并发声）",
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.BodyBrush,
            Margin = new Thickness(2, 0, 0, 8)
        };
        rightStack.Children.Add(_chordGalleryHeader);

        _chordGalleryWrap = new WrapPanel();
        rightStack.Children.Add(_chordGalleryWrap);

        rightScroll.Content = rightStack;
        rightCard.Child = rightScroll;
        Grid.SetColumn(rightCard, 2);
        grid.Children.Add(rightCard);

        RebuildChordRootButtons();
        RebuildChordQualityButtons();
        return grid;
    }

    // ─── Tab 2: DAW-Style Chord Progression & 4-Color Jam Target Notes ────────────────

    private UIElement BuildProgressionTab()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var stack = new StackPanel();

        // Row 1: Transport + Presets + Coach
        var controlBar = new Border
        {
            Background = SoftSurface,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var row1 = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };

        _progPlayBtn = CreatePillButton("▶ 播放进行", isPrimary: true);
        _progPlayBtn.Margin = new Thickness(0, 2, 8, 2);
        _progPlayBtn.Click += (_, _) => ToggleProgressionPlayback();
        row1.Children.Add(_progPlayBtn);

        _progLoopBtn = CreatePillButton("↻ 循环", isPrimary: false);
        _progLoopBtn.Margin = new Thickness(0, 2, 8, 2);
        _progLoopBtn.Click += (_, _) => { _progLoop = !_progLoop; StyleTogglePill(_progLoopBtn, _progLoop); };
        StyleTogglePill(_progLoopBtn, _progLoop);
        row1.Children.Add(_progLoopBtn);

        var playModeSeg = CreateSegmentedContainer();
        playModeSeg.Margin = new Thickness(0, 2, 10, 2);
        var playModeStack = (StackPanel)playModeSeg.Child;
        _progStrumBtn = CreateSegButton("扫弦");
        _progStrumBtn.Click += (_, _) => { _progArpeggio = false; UpdateProgModeSeg(); };
        _progArpBtn = CreateSegButton("分解");
        _progArpBtn.Click += (_, _) => { _progArpeggio = true; UpdateProgModeSeg(); };
        playModeStack.Children.Add(_progStrumBtn);
        playModeStack.Children.Add(_progArpBtn);
        UpdateProgModeSeg();
        row1.Children.Add(playModeSeg);

        // Tactile BPM Stepper
        var bpmSeg = CreateSegmentedContainer();
        bpmSeg.Margin = new Thickness(0, 2, 12, 2);
        var bpmStack = (StackPanel)bpmSeg.Child;
        var bpmMinus = CreateSegButton("−", minWidth: 22);
        bpmMinus.Click += (_, _) => AdjustProgBpm(-5);
        _progBpmText = new TextBlock { Text = "90 BPM", FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = AppDialogs.TitleBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
        var bpmPlus = CreateSegButton("+", minWidth: 22);
        bpmPlus.Click += (_, _) => AdjustProgBpm(5);
        bpmStack.Children.Add(bpmMinus);
        bpmStack.Children.Add(_progBpmText);
        bpmStack.Children.Add(bpmPlus);
        row1.Children.Add(bpmSeg);

        var importSongBtn = CreatePillButton("📥 读取当前曲谱", isPrimary: false, toolTip: "提取当前打开曲谱里的和弦走向直接练习即兴");
        importSongBtn.Margin = new Thickness(0, 2, 8, 2);
        importSongBtn.Click += (_, _) => ImportProgressionFromCurrentSong();
        row1.Children.Add(importSongBtn);

        var smootherBtn = CreatePillButton("✨ 更顺滑", isPrimary: false, toolTip: "推荐转位低音斜杠连接");
        smootherBtn.Margin = new Thickness(0, 2, 6, 2);
        smootherBtn.Click += (_, _) => ShowProgressionSuggestions(isRicher: false);
        row1.Children.Add(smootherBtn);

        var richerBtn = CreatePillButton("✨ 更丰富", isPrimary: false, toolTip: "推荐 7 / maj7 / add9 / 6 色彩升级");
        richerBtn.Margin = new Thickness(0, 2, 8, 2);
        richerBtn.Click += (_, _) => ShowProgressionSuggestions(isRicher: true);
        row1.Children.Add(richerBtn);

        _progJamColorsBtn = CreatePillButton("🎯 四色靶向音", isPrimary: false, toolTip: "播放时在指板上用红/金/蓝/灰四色标注当前和弦落点");
        _progJamColorsBtn.Margin = new Thickness(0, 2, 0, 2);
        _progJamColorsBtn.Click += (_, _) =>
        {
            _progJamColors = !_progJamColors;
            StyleTogglePill(_progJamColorsBtn, _progJamColors);
            UpdateJamTargetVisual();
            UpdateSummaryAndLegend();
        };
        StyleTogglePill(_progJamColorsBtn, _progJamColors);
        row1.Children.Add(_progJamColorsBtn);

        controlBar.Child = row1;
        stack.Children.Add(controlBar);

        // Row 1.5: 1-Click Classic Preset Style Pills (no more buried dropdown!)
        _progPresetPillsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 8), VerticalAlignment = VerticalAlignment.Center };
        RebuildProgressionPresetPills();
        stack.Children.Add(_progPresetPillsWrap);

        // Row 2: 1-Click Diatonic Builder Strip
        var quickAddRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var clearProgBtn = CreatePillButton("清空", isPrimary: false);
        clearProgBtn.Padding = new Thickness(8, 2, 8, 2);
        clearProgBtn.FontSize = 11;
        clearProgBtn.Click += (_, _) =>
        {
            StopProgressionPlayback();
            _progressionSteps.Clear();
            RebuildProgressionTimeline();
            UpdateProgressionLivePanel(null);
        };
        DockPanel.SetDock(clearProgBtn, Dock.Right);
        quickAddRow.Children.Add(clearProgBtn);

        _progQuickAddWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        quickAddRow.Children.Add(_progQuickAddWrap);
        stack.Children.Add(quickAddRow);

        // Row 3: Live Banner + DAW Measure Timeline Cards
        _progActiveBanner = new TextBlock
        {
            Text = "点击任意和弦小节试听，或点击「▶ 播放进行」跟着四色靶向音练习即兴",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.AccentHoverBrush,
            Margin = new Thickness(2, 0, 0, 8)
        };
        stack.Children.Add(_progActiveBanner);

        _progTimelineWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        stack.Children.Add(_progTimelineWrap);

        // Row 4: Coach Suggestions Card
        _progSuggestionList = new StackPanel();
        _progSuggestionCard = new Border
        {
            Visibility = Visibility.Collapsed,
            Background = WarmPaperBg,
            BorderBrush = WarmPaperBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 10),
            Child = _progSuggestionList
        };
        stack.Children.Add(_progSuggestionCard);

        // Row 5: Live Active-Chord Diagram + Clickable Jam Target Notes Split Panel (fills lower 55%!)
        var liveGrid = new Grid();
        liveGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.92, GridUnitType.Star) });
        liveGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        liveGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.38, GridUnitType.Star) });

        // Left live card: Real-time Chord Diagram following playback
        var liveChordCard = new Border
        {
            Background = SoftSurface,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(14, 10, 14, 10)
        };
        var liveChordDock = new DockPanel();
        _progLiveDiagram = new ChordDiagramView { Width = 96, Height = 114, VerticalAlignment = VerticalAlignment.Center };
        var liveDiagBox = new Border
        {
            Background = Brushes.White,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 12, 0),
            Cursor = Cursors.Hand,
            ToolTip = "点击试听当前小节和弦",
            Child = _progLiveDiagram
        };
        liveDiagBox.MouseLeftButtonDown += (_, _) =>
        {
            if (_progLiveDiagram.Shape is { } sh)
            {
                _audio.PlayChord(MusicTheoryCatalog.GetChordMidiNotes(sh, _fretboard.Tuning, _fretboard.Capo), _progArpeggio);
                _fretboard.FlashChordShape(sh, _progArpeggio);
            }
        };
        DockPanel.SetDock(liveDiagBox, Dock.Left);
        liveChordDock.Children.Add(liveDiagBox);

        var liveMetaStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        liveMetaStack.Children.Add(new TextBlock { Text = "当前小节和弦实时同步", FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = AppDialogs.MutedBrush });
        _progLiveChordTitle = new TextBlock { Text = "C", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = AppDialogs.TitleBrush, Margin = new Thickness(0, 2, 0, 2) };
        _progLiveChordTones = new TextBlock { Text = "构成音：C · E · G", FontSize = 11.5, Foreground = AppDialogs.BodyBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        var inspectBtn = CreatePillButton("去解剖库看 CAGED 五把位 →", isPrimary: false);
        inspectBtn.HorizontalAlignment = HorizontalAlignment.Left;
        inspectBtn.Click += (_, _) =>
        {
            var sym = _progLiveDiagram.Shape?.Name ?? _progressionSteps.FirstOrDefault()?.ChordName;
            if (!string.IsNullOrWhiteSpace(sym))
            {
                SelectChordBySymbol(sym, syncToFretboard: true, autoAudition: false);
                SwitchTab(1);
            }
        };
        liveMetaStack.Children.Add(_progLiveChordTitle);
        liveMetaStack.Children.Add(_progLiveChordTones);
        liveMetaStack.Children.Add(inspectBtn);
        liveChordDock.Children.Add(liveMetaStack);
        liveChordCard.Child = liveChordDock;
        Grid.SetColumn(liveChordCard, 0);
        liveGrid.Children.Add(liveChordCard);

        // Right live card: Clickable Red / Gold / Blue Jam Target Notes Guide
        var liveTargetCard = new Border
        {
            Background = WarmPaperBg,
            BorderBrush = WarmPaperBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(14, 10, 14, 10)
        };
        var targetOuter = new StackPanel();
        targetOuter.Children.Add(new TextBlock
        {
            Text = "🎯 即兴靶向音落点攻略 · 点击任意音胶囊，全指板该音立即闪烁发声",
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            Foreground = AppDialogs.TitleBrush,
            Margin = new Thickness(0, 0, 0, 8)
        });
        _progLiveTargetPanel = new StackPanel();
        targetOuter.Children.Add(_progLiveTargetPanel);
        liveTargetCard.Child = targetOuter;
        Grid.SetColumn(liveTargetCard, 2);
        liveGrid.Children.Add(liveTargetCard);

        stack.Children.Add(liveGrid);

        RebuildProgressionQuickAddBar();
        scroll.Content = stack;
        return scroll;
    }

    // ─── Tab 3: Journal-Style Personal Theory Notebook ────────────────────────────────

    private UIElement BuildNotebookTab()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left: Saved Journal Cards
        var leftGrid = new Grid();
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var addCardBtn = CreatePillButton("+ 拍下当前指板存入收集本", isPrimary: true);
        addCardBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
        addCardBtn.Padding = new Thickness(12, 7, 12, 7);
        addCardBtn.Margin = new Thickness(0, 0, 0, 8);
        addCardBtn.Click += (_, _) => CreateTheoryNoteFromCurrentFretboard();
        Grid.SetRow(addCardBtn, 0);
        leftGrid.Children.Add(addCardBtn);

        _noteTagFilterWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        string[] tags = ["全部", "CAGED", "音阶", "和弦", "即兴", "调弦"];
        foreach (var tag in tags)
        {
            var t = tag;
            var chip = CreatePillButton(t, isPrimary: false);
            chip.Padding = new Thickness(8, 2, 8, 2);
            chip.FontSize = 11;
            chip.Margin = new Thickness(0, 2, 4, 2);
            chip.Click += (_, _) => { _noteTagFilter = t; RefreshNotesList(); };
            _noteTagFilterWrap.Children.Add(chip);
        }
        Grid.SetRow(_noteTagFilterWrap, 1);
        leftGrid.Children.Add(_noteTagFilterWrap);

        _noteListPanel = new StackPanel();
        var noteScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _noteListPanel
        };
        var listBorder = new Border
        {
            Background = SoftSurface,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8),
            Child = noteScroll
        };
        Grid.SetRow(listBorder, 2);
        leftGrid.Children.Add(listBorder);

        Grid.SetColumn(leftGrid, 0);
        grid.Children.Add(leftGrid);

        // Right: Warm Auto-Saving Journal Editor
        var editorCard = new Border
        {
            Background = WarmPaperBg,
            BorderBrush = WarmPaperBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(18, 14, 18, 14)
        };
        var edGrid = new Grid();
        edGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        edGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        edGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        edGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        _noteTagBox = new ComboBox { Width = 88, Height = 28, Margin = new Thickness(8, 0, 0, 0) };
        foreach (var t in new[] { "CAGED", "音阶", "和弦", "即兴", "调弦", "笔记" })
            _noteTagBox.Items.Add(t);
        _noteTagBox.SelectedIndex = 0;
        _noteTagBox.SelectionChanged += (_, _) => ScheduleNoteAutoSave();
        DockPanel.SetDock(_noteTagBox, Dock.Right);
        titleRow.Children.Add(_noteTagBox);

        _noteTitleBox = new TextBox
        {
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Padding = new Thickness(10, 5, 10, 5),
            Background = Brushes.White
        };
        _noteTitleBox.TextChanged += (_, _) => ScheduleNoteAutoSave();
        titleRow.Children.Add(_noteTitleBox);
        Grid.SetRow(titleRow, 0);
        edGrid.Children.Add(titleRow);

        var snapBanner = new Border
        {
            Background = Brushes.White,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var snapDock = new DockPanel();
        _noteApplySnapshotBtn = CreatePillButton("🎯 还原到上方指板", isPrimary: true);
        _noteApplySnapshotBtn.Click += (_, _) =>
        {
            if (_selectedNote is not null) ApplyNoteSnapshotToFretboard(_selectedNote);
        };
        DockPanel.SetDock(_noteApplySnapshotBtn, Dock.Right);
        snapDock.Children.Add(_noteApplySnapshotBtn);

        _noteSnapshotBadge = new TextBlock
        {
            Text = "选中任意乐理卡片即可一键还原完整指板快照",
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.BodyBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 10, 0)
        };
        snapDock.Children.Add(_noteSnapshotBadge);
        snapBanner.Child = snapDock;
        Grid.SetRow(snapBanner, 1);
        edGrid.Children.Add(snapBanner);

        var contentGrid = new Grid();
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var insertChips = new WrapPanel { Margin = new Thickness(0, 0, 0, 6), VerticalAlignment = VerticalAlignment.Center };
        insertChips.Children.Add(new TextBlock { Text = "快速插入：", FontSize = 11, Foreground = AppDialogs.MutedBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 5, 0) });

        var insScaleBtn = CreatePillButton("+ 当前音阶音名", isPrimary: false);
        insScaleBtn.FontSize = 10.5;
        insScaleBtn.Padding = new Thickness(7, 2, 7, 2);
        insScaleBtn.Margin = new Thickness(0, 0, 5, 0);
        insScaleBtn.Click += (_, _) =>
        {
            var pf = MusicTheoryCatalog.PreferFlatsForTonic(_fretboard.TonicPitch);
            var notes = _fretboard.Scale.Intervals.Select(iv => MusicTheoryCatalog.NoteName((_fretboard.TonicPitch + iv) % 12, pf));
            AppendNoteSnippet($"【{MusicTheoryCatalog.NoteName(_fretboard.TonicPitch, pf)} {_fretboard.Scale.Name}】{string.Join(" - ", notes)}");
        };

        var insChordBtn = CreatePillButton("+ 当前和弦指法", isPrimary: false);
        insChordBtn.FontSize = 10.5;
        insChordBtn.Padding = new Thickness(7, 2, 7, 2);
        insChordBtn.Margin = new Thickness(0, 0, 5, 0);
        insChordBtn.Click += (_, _) =>
        {
            var sh = _fretboard.HighlightedChord ?? (_chordVoicings.Count > 0 ? _chordVoicings[Math.Clamp(_chordCandidateIdx, 0, _chordVoicings.Count - 1)].Shape : null);
            if (sh is null) return;
            var tab = string.Join("", sh.Frets.Select(f => f.HasValue ? (f.Value >= 10 ? $"({f.Value})" : f.Value.ToString()) : "x"));
            AppendNoteSnippet($"【{sh.Name} · {sh.Label}】6→1弦指法：{tab}");
        };

        var insProgBtn = CreatePillButton("+ 当前和弦进行", isPrimary: false);
        insProgBtn.FontSize = 10.5;
        insProgBtn.Padding = new Thickness(7, 2, 7, 2);
        insProgBtn.Click += (_, _) =>
        {
            if (_progressionSteps.Count == 0) return;
            var line = string.Join(" → ", _progressionSteps.Select(s => $"{s.ChordName}({s.Roman})"));
            AppendNoteSnippet($"【和弦进行】{line}");
        };

        insertChips.Children.Add(insScaleBtn);
        insertChips.Children.Add(insChordBtn);
        insertChips.Children.Add(insProgBtn);
        Grid.SetRow(insertChips, 0);
        contentGrid.Children.Add(insertChips);

        _noteContentBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalContentAlignment = VerticalAlignment.Top,
            Padding = new Thickness(12, 10, 12, 10),
            FontSize = 13,
            Background = Brushes.White
        };
        _noteContentBox.TextChanged += (_, _) => ScheduleNoteAutoSave();
        Grid.SetRow(_noteContentBox, 1);
        contentGrid.Children.Add(_noteContentBox);

        Grid.SetRow(contentGrid, 2);
        edGrid.Children.Add(contentGrid);

        var footerDock = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        var footerRight = new StackPanel { Orientation = Orientation.Horizontal };
        _noteUpdateSnapBtn = CreatePillButton("↻ 覆盖为当前指板状态", isPrimary: false);
        _noteUpdateSnapBtn.Margin = new Thickness(0, 0, 8, 0);
        _noteUpdateSnapBtn.Click += (_, _) =>
        {
            if (_selectedNote is null) return;
            CaptureFretboardIntoNote(_selectedNote);
            SaveSelectedNote(silent: false);
        };

        _noteDeleteBtn = CreatePillButton("删除", isPrimary: false);
        _noteDeleteBtn.Foreground = AppDialogs.DangerBrush;
        _noteDeleteBtn.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242));
        _noteDeleteBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(254, 202, 202));
        _noteDeleteBtn.Click += (_, _) => DeleteSelectedNote();

        footerRight.Children.Add(_noteUpdateSnapBtn);
        footerRight.Children.Add(_noteDeleteBtn);
        DockPanel.SetDock(footerRight, Dock.Right);
        footerDock.Children.Add(footerRight);

        _noteSaveStateText = new TextBlock
        {
            Text = "内容自动保存至本地曲库",
            FontSize = 11,
            Foreground = AppDialogs.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        footerDock.Children.Add(_noteSaveStateText);
        Grid.SetRow(footerDock, 3);
        edGrid.Children.Add(footerDock);

        editorCard.Child = edGrid;
        Grid.SetColumn(editorCard, 2);
        grid.Children.Add(editorCard);

        return grid;
    }

    // ─── Animated Audio-Visual Helpers ────────────────────────────────────────────────

    private void PlayAnimatedFretboardSequence()
    {
        var spots = _fretboard.GetActiveCagedOrScaleSpots();
        if (spots.Count == 0) return;
        const int stepMs = 195;
        _audio.PlayScaleSequence(spots.Select(x => x.Midi).ToList(), stepMs);
        _fretboard.AnimateSpotSequence(spots.Select(x => (x.StringIdx, x.Fret)).ToList(), stepMs);
    }

    private void PlayAnimatedFretboardTriad()
    {
        var spots = _fretboard.GetEmbeddedCagedTriadSpots();
        if (spots.Count > 0)
        {
            _audio.PlayChord(spots.Select(x => x.Midi).ToList(), arpeggio: false);
            _fretboard.AnimateSpotSequence(spots.Select(x => (x.StringIdx, x.Fret)).ToList(), 34);
        }
        else
        {
            _audio.PlayChord(_fretboard.GetEmbeddedCagedTriadMidi(), arpeggio: false);
        }
    }

    // ─── State & Tab Synchronization ──────────────────────────────────────────────────

    private void SwitchTab(int idx)
    {
        _activeTab = Math.Clamp(idx, 0, 4);
        if (_activeTab != 2)
            StopProgressionPlayback();
        if (_activeTab != 3)
            _drillStage?.StopPlayback();

        for (var i = 0; i < 5; i++)
        {
            var sel = i == _activeTab;
            _tabPages[i].Visibility = sel ? Visibility.Visible : Visibility.Collapsed;
            _tabIndicators[i].Visibility = sel ? Visibility.Visible : Visibility.Hidden;
            _tabButtons[i].Foreground = sel ? AppDialogs.AccentHoverBrush : AppDialogs.MutedBrush;
            _tabButtons[i].FontWeight = sel ? FontWeights.Bold : FontWeights.Normal;
        }

        if (_activeTab == 0)
            LoadLesson(_activeLessonIndex, _activeStepIndex, applyToFretboard: true);
        else if (_activeTab == 1)
            RefreshChordAnatomyDisplay(syncToFretboard: true);
        else if (_activeTab == 2)
            RebuildProgressionQuickAddBar();
        else if (_activeTab == 3)
            _drillStage?.OnTabActivated();
        else if (_activeTab == 4)
            RefreshNotesList();
    }

    private void NotifyFretboardManualChange()
    {
        UpdateSummaryAndLegend();
        RebuildLessonPracticePanels();
        RebuildChordRootButtons();
        RefreshChordAnatomyDisplay(syncToFretboard: _activeTab == 1);
        RebuildProgressionQuickAddBar();
        if (_activeTab == 3)
            _drillStage?.OnFretboardParametersChanged();
        _fretboard.Refresh();
    }

    private void SyncTopControlsFromFretboard()
    {
        _syncingControls = true;
        try
        {
            _tonicBox.SelectedIndex = Math.Clamp(_fretboard.TonicPitch, 0, 11);

            for (var i = 0; i < _scaleBox.Items.Count; i++)
            {
                if (_scaleBox.Items[i] is ComboBoxItem { Tag: string id } && id == _fretboard.Scale.Id)
                {
                    _scaleBox.SelectedIndex = i;
                    break;
                }
            }

            for (var i = 0; i < _tuningBox.Items.Count; i++)
            {
                if (_tuningBox.Items[i] is ComboBoxItem { Tag: string id } && id == _fretboard.Tuning.Id)
                {
                    _tuningBox.SelectedIndex = i;
                    break;
                }
            }

            _capoBox.SelectedIndex = Math.Clamp(_fretboard.Capo, 0, 11);
            UpdateLabelSegStyles();
            UpdateCagedChipStyles();
            UpdateWoodSegStyles();
            StyleTogglePill(_capsulePillBtn, _fretboard.ShowCapsules);
            UpdateSummaryAndLegend();
            RebuildLessonPracticePanels();
            if (_activeTab == 3)
            {
                _drillStage?.SyncPositionModeFromFretboard();
                _drillStage?.OnFretboardParametersChanged();
            }
        }
        finally
        {
            _syncingControls = false;
        }
    }

    private void UpdateLabelSegStyles()
    {
        foreach (var (mode, btn) in _labelModeBtns)
            StyleSegButton(btn, mode == _fretboard.LabelMode);
    }

    private void UpdateWoodSegStyles()
    {
        StyleSegButton(_mapleWoodBtn, !_fretboard.DarkRosewood);
        StyleSegButton(_roseWoodBtn, _fretboard.DarkRosewood);
    }

    private void UpdateCagedChipStyles()
    {
        var is3Nps = _fretboard.ShowThreeNpsBoxes;
        StyleSegButton(_coordCagedBtn, !is3Nps);
        StyleSegButton(_coord3NpsBtn, is3Nps);
        _cagedTrackBorder.Visibility = is3Nps ? Visibility.Collapsed : Visibility.Visible;
        _threeNpsTrackBorder.Visibility = is3Nps ? Visibility.Visible : Visibility.Collapsed;
        _capsulePillBtn.Visibility = is3Nps ? Visibility.Collapsed : Visibility.Visible;

        foreach (var (posNum, nb) in _threeNpsBtns)
            StyleSegButton(nb, is3Nps && _fretboard.ActiveThreeNpsPosition == posNum);

        var activeCode = !_fretboard.ShowCagedBoxes
            ? "OFF"
            : (string.IsNullOrWhiteSpace(_fretboard.ActiveCagedFilter) ? "ALL" : _fretboard.ActiveCagedFilter);

        foreach (var (code, btn) in _cagedFilterBtns)
        {
            var isActive = string.Equals(code, activeCode, StringComparison.OrdinalIgnoreCase);
            if (isActive && CagedColorMap.TryGetValue(code, out var hex))
            {
                btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                btn.Foreground = Brushes.White;
                btn.FontWeight = FontWeights.Bold;
            }
            else if (isActive)
            {
                btn.Background = code == "OFF" ? new SolidColorBrush(Color.FromRgb(100, 116, 139)) : AppDialogs.AccentBrush;
                btn.Foreground = Brushes.White;
                btn.FontWeight = FontWeights.Bold;
            }
            else
            {
                btn.Background = Brushes.Transparent;
                btn.Foreground = CagedColorMap.TryGetValue(code, out var tint)
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(tint))
                    : AppDialogs.MutedBrush;
                btn.FontWeight = CagedColorMap.ContainsKey(code) ? FontWeights.Bold : FontWeights.Normal;
            }
        }
    }

    private void UpdateSummaryAndLegend()
    {
        var tonicName = MusicTheoryCatalog.NoteName(_fretboard.TonicPitch, MusicTheoryCatalog.PreferFlatsForTonic(_fretboard.TonicPitch));
        _summaryKeyText.Text = $"{tonicName} · {_fretboard.Scale.Name}";

        var subParts = new List<string>();
        if (_fretboard.Capo > 0) subParts.Add($"Capo {_fretboard.Capo}");
        if (_fretboard.ShowThreeNpsBoxes)
            subParts.Add(_fretboard.ActiveThreeNpsPosition == 0 ? "3NPS 每弦三音全 7 把位" : $"3NPS 第 {_fretboard.ActiveThreeNpsPosition} 把位");
        else if (_fretboard.ShowCagedBoxes)
            subParts.Add(_fretboard.ActiveCagedFilter == "ALL" ? "CAGED 全指板（点顶标可聚焦）" : $"聚焦 {_fretboard.ActiveCagedFilter} 形");
        if (_fretboard.HighlightedChord is not null)
            subParts.Add($"和弦 {_fretboard.HighlightedChord.Name}");
        _summarySubText.Text = string.Join(" · ", subParts);

        _legendPanel.Children.Clear();
        if (_fretboard.JamTargetColorMode && !string.IsNullOrWhiteSpace(_fretboard.JamActiveChordName))
        {
            AddLegendDot("#DC2626", "根音");
            AddLegendDot("#D97706", "和弦音");
            AddLegendDot("#2563EB", "色彩音");
            AddLegendDot("#94A3B8", "经过音");
        }
        else
        {
            AddLegendDot("#E26224", "根音 1");
            AddLegendDot("#7C4DE0", "三音 3");
            AddLegendDot("#267E55", "五音 5");
            AddLegendDot("#FFFFFF", "音阶音", borderHex: "#64748B");
        }
    }

    private void AddLegendDot(string fillHex, string label, string? borderHex = null)
    {
        var dot = new Border
        {
            Width = 9,
            Height = 9,
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fillHex)),
            BorderBrush = borderHex is not null ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderHex)) : null,
            BorderThickness = borderHex is not null ? new Thickness(1.1) : new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        var txt = new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = AppDialogs.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        _legendPanel.Children.Add(dot);
        _legendPanel.Children.Add(txt);
    }

    // ─── Lessons Logic (Syllabus, AB Ear Bench, Fretboard Quiz & Portals) ──────────────

    private HashSet<string> GetCompletedLessonSet()
    {
        var list = _getLibrary?.Invoke()?.CompletedTheoryLessons;
        return list is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
    }

    private void MarkLessonCompleted(string lessonId, bool completed)
    {
        var lib = _getLibrary?.Invoke();
        if (lib is null) return;
        lib.CompletedTheoryLessons ??= [];
        var existingIdx = lib.CompletedTheoryLessons.FindIndex(x => string.Equals(x, lessonId, StringComparison.OrdinalIgnoreCase));
        if (completed && existingIdx < 0)
        {
            lib.CompletedTheoryLessons.Add(lessonId);
            _onLibraryMutated?.Invoke();
        }
        else if (!completed && existingIdx >= 0)
        {
            lib.CompletedTheoryLessons.RemoveAt(existingIdx);
            _onLibraryMutated?.Invoke();
        }
        RebuildLessonList();
        UpdateLessonMasteredButton();
    }

    private void ToggleCurrentLessonMastered()
    {
        var lesson = MusicTheoryCatalog.Lessons[_activeLessonIndex];
        var done = GetCompletedLessonSet().Contains(lesson.Id);
        MarkLessonCompleted(lesson.Id, !done);
        _setStatus?.Invoke(!done
            ? $"已标记《{lesson.CodeBadge} {lesson.Title}》为已掌握！"
            : $"已取消《{lesson.CodeBadge} {lesson.Title}》的已掌握标记。");
    }

    private void UpdateLessonMasteredButton()
    {
        if (_lessonMasteredBtn is null) return;
        var lesson = MusicTheoryCatalog.Lessons[Math.Clamp(_activeLessonIndex, 0, MusicTheoryCatalog.Lessons.Count - 1)];
        var isDone = GetCompletedLessonSet().Contains(lesson.Id);
        _lessonMasteredBtn.Content = isDone ? "✓ 已掌握" : "○ 标记已掌握";
        StyleTogglePill(_lessonMasteredBtn, isDone);
    }

    private void RebuildLessonList()
    {
        if (_lessonListPanel is null) return;
        _lessonListPanel.Children.Clear();

        var completedSet = GetCompletedLessonSet();
        var totalCount = MusicTheoryCatalog.Lessons.Count;
        var doneCount = MusicTheoryCatalog.Lessons.Count(l => completedSet.Contains(l.Id));
        if (_lessonProgressText is not null)
            _lessonProgressText.Text = $"🎓 实用乐理实战课 · 已掌握 {doneCount} / {totalCount} 课";
        if (_lessonProgressBar is not null)
        {
            _lessonProgressBar.Maximum = Math.Max(1, totalCount);
            _lessonProgressBar.Value = doneCount;
        }

        string? lastUnit = null;
        for (var i = 0; i < MusicTheoryCatalog.Lessons.Count; i++)
        {
            var lesson = MusicTheoryCatalog.Lessons[i];
            if (!string.IsNullOrEmpty(_lessonUnitFilter) && !string.Equals(lesson.UnitName, _lessonUnitFilter, StringComparison.Ordinal))
                continue;

            if (lesson.UnitName != lastUnit)
            {
                lastUnit = lesson.UnitName;
                _lessonListPanel.Children.Add(new TextBlock
                {
                    Text = lesson.UnitName,
                    FontSize = 10.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = AppDialogs.AccentHoverBrush,
                    Margin = new Thickness(6, _lessonListPanel.Children.Count == 0 ? 2 : 10, 4, 4)
                });
            }

            var idx = i;
            var isSel = idx == _activeLessonIndex;
            var isDone = completedSet.Contains(lesson.Id);

            var itemBorder = new Border
            {
                Background = isSel ? Brushes.White : Brushes.Transparent,
                BorderBrush = isSel ? AppDialogs.AccentBrush : Brushes.Transparent,
                BorderThickness = new Thickness(isSel ? 1.4 : 1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 5, 6, 5),
                Margin = new Thickness(0, 1, 0, 2),
                Cursor = Cursors.Hand,
                ToolTip = $"{lesson.CodeBadge} · {lesson.Title}\n{lesson.Summary}"
            };

            var dock = new DockPanel();
            var rightBadge = new TextBlock
            {
                Text = isDone ? "✓" : $"{lesson.Steps.Count}步",
                FontSize = isDone ? 11 : 9.5,
                FontWeight = isDone ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isDone ? AppDialogs.AccentHoverBrush : AppDialogs.MutedBrush,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4, 2, 2, 0)
            };
            DockPanel.SetDock(rightBadge, Dock.Right);
            dock.Children.Add(rightBadge);

            var codePill = new Border
            {
                Background = isSel
                    ? AppDialogs.AccentBrush
                    : (isDone ? AppDialogs.AccentLightBrush : new SolidColorBrush(Color.FromRgb(226, 232, 240))),
                CornerRadius = new CornerRadius(5),
                Width = 24,
                Height = 17,
                Margin = new Thickness(0, 1, 7, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = lesson.CodeBadge,
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = isSel ? Brushes.White : (isDone ? AppDialogs.AccentHoverBrush : AppDialogs.BodyBrush),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            DockPanel.SetDock(codePill, Dock.Left);
            dock.Children.Add(codePill);

            var textCol = new StackPanel();
            textCol.Children.Add(new TextBlock
            {
                Text = lesson.Title,
                FontSize = 11.5,
                FontWeight = isSel ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isSel ? AppDialogs.TitleBrush : AppDialogs.BodyBrush,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            if (isSel)
            {
                textCol.Children.Add(new TextBlock
                {
                    Text = lesson.Summary,
                    FontSize = 10,
                    Foreground = AppDialogs.MutedBrush,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            dock.Children.Add(textCol);

            itemBorder.Child = dock;
            itemBorder.MouseLeftButtonDown += (_, _) => LoadLesson(idx, 0, applyToFretboard: true);
            _lessonListPanel.Children.Add(itemBorder);
        }
    }

    private void LoadLesson(int lessonIdx, int stepIdx, bool applyToFretboard)
    {
        _activeLessonIndex = Math.Clamp(lessonIdx, 0, MusicTheoryCatalog.Lessons.Count - 1);
        var lesson = MusicTheoryCatalog.Lessons[_activeLessonIndex];
        _activeStepIndex = Math.Clamp(stepIdx, 0, lesson.Steps.Count - 1);
        var step = lesson.Steps[_activeStepIndex];

        _activeCompareIdx = -1;
        _quizSolvedInCurrentStep = false;

        _lessonUnitBadge.Text = $"{lesson.UnitName} · 第 {lesson.CodeBadge} 课";
        _lessonStepCounter.Text = $"步骤 {_activeStepIndex + 1} / {lesson.Steps.Count}";
        _lessonTitleText.Text = $"{lesson.CodeBadge}. {lesson.Title} — {step.StepTitle}";
        _lessonSummaryText.Text = lesson.Summary;
        _lessonBodyText.Text = step.Body;

        if (!string.IsNullOrWhiteSpace(step.KeyFormula))
        {
            _lessonFormulaCard.Visibility = Visibility.Visible;
            _lessonFormulaText.Text = step.KeyFormula;
        }
        else
        {
            _lessonFormulaCard.Visibility = Visibility.Collapsed;
        }

        RebuildLessonCompareBench(step);
        RebuildLessonQuizCard(step);
        RebuildLessonPortals(lesson);
        UpdateLessonMasteredButton();

        _lessonStepPills.Children.Clear();
        for (var i = 0; i < lesson.Steps.Count; i++)
        {
            var si = i;
            var isCurrent = i == _activeStepIndex;
            var hasQuizTag = lesson.Steps[i].Quiz is not null ? " 🎯" : "";
            var pill = CreatePillButton($"{i + 1}. {lesson.Steps[i].StepTitle}{hasQuizTag}", isPrimary: isCurrent);
            pill.FontSize = 11;
            pill.Padding = new Thickness(9, 3, 9, 3);
            pill.Margin = new Thickness(0, 0, 6, 4);
            pill.Click += (_, _) => LoadLesson(_activeLessonIndex, si, applyToFretboard: true);
            _lessonStepPills.Children.Add(pill);
        }

        _lessonPrevBtn.IsEnabled = _activeLessonIndex > 0 || _activeStepIndex > 0;
        var isLastStep = _activeStepIndex == lesson.Steps.Count - 1;
        _lessonNextBtn.Content = isLastStep
            ? (_activeLessonIndex < MusicTheoryCatalog.Lessons.Count - 1 ? "✓ 学完进入下一课 →" : "✓ 完成全套课程")
            : "下一步 →";
        _lessonNextBtn.IsEnabled = true;

        RebuildLessonList();

        if (applyToFretboard)
        {
            _fretboard.TonicPitch = step.TonicPitch;
            _fretboard.Scale = MusicTheoryCatalog.GetScale(step.ScaleId);
            _fretboard.Tuning = MusicTheoryCatalog.GetTuning(step.TuningId);
            _fretboard.Capo = step.Capo;
            if (step.PreferredLabelMode is { } prefLabel)
                _fretboard.LabelMode = prefLabel;
            _fretboard.ShowThreeNpsBoxes = step.ShowThreeNps;
            _fretboard.ActiveThreeNpsPosition = step.ThreeNpsPos;
            _fretboard.ShowCagedBoxes = !step.ShowThreeNps && step.ShowCaged;
            _fretboard.ActiveCagedFilter = string.IsNullOrEmpty(step.CagedFilter) ? "ALL" : step.CagedFilter;
            _fretboard.ShowCapsules = !step.ShowThreeNps && step.ShowCapsules;
            _fretboard.JamTargetColorMode = false;
            _fretboard.HighlightedChord = string.IsNullOrWhiteSpace(step.HighlightChord)
                ? null
                : ChordCatalog.ByName(step.HighlightChord);
            SyncTopControlsFromFretboard();
            _fretboard.Refresh();
        }

        RebuildLessonPracticePanels();
    }

    private void RebuildLessonCompareBench(TheoryLessonStep step)
    {
        if (_lessonCompareCard is null) return;
        _lessonCompareWrap.Children.Clear();
        if (step.CompareItems is not { Count: > 0 } items)
        {
            _lessonCompareCard.Visibility = Visibility.Collapsed;
            return;
        }

        _lessonCompareCard.Visibility = Visibility.Visible;
        _lessonCompareHintText.Visibility = Visibility.Collapsed;

        for (var i = 0; i < items.Count; i++)
        {
            var idx = i;
            var opt = items[i];
            var isSel = idx == _activeCompareIdx;
            var btn = CreatePillButton($"🔊 {opt.Label}", isPrimary: isSel);
            btn.FontSize = 11.5;
            btn.Padding = new Thickness(10, 4, 10, 4);
            btn.Margin = new Thickness(0, 2, 7, 4);
            btn.Click += (_, _) =>
            {
                _activeCompareIdx = idx;
                _lessonCompareHintText.Text = $"👂 听感解析：{opt.Hint}";
                _lessonCompareHintText.Visibility = Visibility.Visible;

                if (opt.TonicPitch.HasValue)
                    _fretboard.TonicPitch = opt.TonicPitch.Value;
                if (!string.IsNullOrWhiteSpace(opt.ScaleId))
                    _fretboard.Scale = MusicTheoryCatalog.GetScale(opt.ScaleId);

                if (!string.IsNullOrWhiteSpace(opt.ChordSymbol))
                {
                    var shape = ChordCatalog.ByName(opt.ChordSymbol);
                    _fretboard.HighlightedChord = shape;
                    SyncTopControlsFromFretboard();
                    _fretboard.Refresh();
                    if (shape is not null)
                    {
                        var midiNotes = MusicTheoryCatalog.GetChordMidiNotes(shape, _fretboard.Tuning, _fretboard.Capo);
                        _audio.PlayChord(midiNotes, arpeggio: false);
                        _fretboard.FlashChordShape(shape, arpeggio: false);
                    }
                }
                else
                {
                    _fretboard.HighlightedChord = null;
                    SyncTopControlsFromFretboard();
                    _fretboard.Refresh();
                    PlayAnimatedFretboardSequence();
                }

                // Refresh button highlight states while keeping hint visible
                var savedHint = _lessonCompareHintText.Text;
                RebuildLessonCompareBench(step);
                _lessonCompareHintText.Text = savedHint;
                _lessonCompareHintText.Visibility = Visibility.Visible;
            };
            _lessonCompareWrap.Children.Add(btn);
        }
    }

    private void RebuildLessonQuizCard(TheoryLessonStep step)
    {
        if (_lessonQuizCard is null) return;
        if (step.Quiz is not { } quiz)
        {
            _lessonQuizCard.Visibility = Visibility.Collapsed;
            return;
        }

        _lessonQuizCard.Visibility = Visibility.Visible;
        _lessonQuizPromptText.Text = quiz.Prompt;
        SetQuizVisualState(solved: false, feedback: "👆 请直接在上方全景指板中点击对应品格验证答案…", isError: false);
    }

    private void SetQuizVisualState(bool solved, string feedback, bool isError)
    {
        if (_lessonQuizCard is null) return;
        if (solved)
        {
            _lessonQuizCard.Background = new SolidColorBrush(Color.FromRgb(236, 253, 245));
            _lessonQuizCard.BorderBrush = AppDialogs.AccentBrush;
            _lessonQuizFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(209, 250, 229));
            _lessonQuizFeedbackText.Foreground = AppDialogs.AccentHoverBrush;
            _lessonQuizFeedbackText.FontWeight = FontWeights.SemiBold;
        }
        else if (isError)
        {
            _lessonQuizCard.Background = new SolidColorBrush(Color.FromRgb(255, 251, 235));
            _lessonQuizCard.BorderBrush = new SolidColorBrush(Color.FromRgb(217, 119, 6));
            _lessonQuizFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
            _lessonQuizFeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(180, 83, 9));
            _lessonQuizFeedbackText.FontWeight = FontWeights.Normal;
        }
        else
        {
            _lessonQuizCard.Background = new SolidColorBrush(Color.FromRgb(255, 251, 235));
            _lessonQuizCard.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            _lessonQuizFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
            _lessonQuizFeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14));
            _lessonQuizFeedbackText.FontWeight = FontWeights.Normal;
        }
        _lessonQuizFeedbackText.Text = feedback;
    }

    private void FlashCurrentLessonQuizHint()
    {
        var lesson = MusicTheoryCatalog.Lessons[_activeLessonIndex];
        var step = lesson.Steps[_activeStepIndex];
        if (step.Quiz is not { } quiz) return;
        _fretboard.FlashPitchClass(quiz.TargetPitchClass, 750);
        var targetName = MusicTheoryCatalog.NoteName(quiz.TargetPitchClass, MusicTheoryCatalog.PreferFlatsForTonic(_fretboard.TonicPitch));
        _setStatus?.Invoke($"已闪烁提示目标音「{targetName}」在指板上的位置，快点一下完成闯关吧！");
    }

    private void HandleLessonQuizFretClick(int stringIdx, int fret, int midi)
    {
        var lesson = MusicTheoryCatalog.Lessons[_activeLessonIndex];
        var step = lesson.Steps[_activeStepIndex];
        if (step.Quiz is not { } quiz) return;

        var clickedPc = ((midi % 12) + 12) % 12;
        var preferFlats = MusicTheoryCatalog.PreferFlatsForTonic(_fretboard.TonicPitch);
        var clickedNote = MusicTheoryCatalog.NoteName(clickedPc, preferFlats);
        var humanString = 6 - stringIdx;

        if (quiz.RequiredStringIdx is { } reqS && reqS != stringIdx)
        {
            var reqHuman = 6 - reqS;
            if (!_quizSolvedInCurrentStep)
                SetQuizVisualState(false, $"↺ 你点到了 {humanString} 弦 {fret} 品（{clickedNote}）—— 本题限定在「{reqHuman} 弦」上找哦，再试一下！", isError: true);
            return;
        }

        if ((quiz.MinFret is { } minF && fret < minF) || (quiz.MaxFret is { } maxF && fret > maxF))
        {
            if (!_quizSolvedInCurrentStep)
                SetQuizVisualState(false, $"↺ 你点到了 {humanString} 弦 {fret} 品（{clickedNote}）—— 请在题目指定的把位品格范围内寻找！", isError: true);
            return;
        }

        if (clickedPc != ((quiz.TargetPitchClass % 12) + 12) % 12)
        {
            if (!_quizSolvedInCurrentStep)
                SetQuizVisualState(false, $"↺ 刚才点到的是 {humanString} 弦 {fret} 品「{clickedNote}」，还差一点点！可点右上角「💡 闪烁提示」观察音位。", isError: true);
            return;
        }

        _quizSolvedInCurrentStep = true;
        _fretboard.FlashPitchClass(clickedPc, 650);
        MarkLessonCompleted(lesson.Id, completed: true);
        SetQuizVisualState(true, $"✅ {quiz.SuccessMessage}（命中 {humanString} 弦 {fret} 品 {clickedNote} · 本课已自动点亮「✓ 已掌握」！）", isError: false);
    }

    private void RebuildLessonPortals(TheoryLesson lesson)
    {
        if (_lessonPortalWrap is null) return;
        _lessonPortalWrap.Children.Clear();

        if (!string.IsNullOrWhiteSpace(lesson.PracticeDrillId))
        {
            var drillId = lesson.PracticeDrillId;
            var btn = CreatePillButton("⚡ 去练功房实操", isPrimary: false, toolTip: "跳转到「指板练功房」直接跟练本课配套机能/把位科目");
            btn.Margin = new Thickness(0, 2, 6, 2);
            btn.Click += (_, _) =>
            {
                SwitchTab(3);
                _drillStage.SelectPresetById(drillId);
            };
            _lessonPortalWrap.Children.Add(btn);
        }

        if (!string.IsNullOrWhiteSpace(lesson.PracticeChordSymbol))
        {
            var sym = lesson.PracticeChordSymbol;
            var btn = CreatePillButton($"🎸 解剖 {sym}", isPrimary: false, toolTip: $"在「和弦解剖库」查看 {sym} 的六弦组成音与 CAGED 全把位按法");
            btn.Margin = new Thickness(0, 2, 6, 2);
            btn.Click += (_, _) =>
            {
                SelectChordBySymbol(sym, syncToFretboard: true, autoAudition: true);
                SwitchTab(1);
            };
            _lessonPortalWrap.Children.Add(btn);
        }

        if (!string.IsNullOrWhiteSpace(lesson.CustomProgressionChords))
        {
            var csv = lesson.CustomProgressionChords;
            var btn = CreatePillButton("🎵 送入进行台试听", isPrimary: false, toolTip: "将本课示范和弦进行载入「和弦进行 & 靶向音」开启四色靶向跟弹");
            btn.Margin = new Thickness(0, 2, 6, 2);
            btn.Click += (_, _) => OpenCustomProgressionFromLesson(csv, lesson.Steps[0].TonicPitch);
            _lessonPortalWrap.Children.Add(btn);
        }
        else if (!string.IsNullOrWhiteSpace(lesson.PracticeProgressionId))
        {
            var pid = lesson.PracticeProgressionId;
            var preset = MusicTheoryCatalog.ProgressionPresets.FirstOrDefault(p => string.Equals(p.Id, pid, StringComparison.OrdinalIgnoreCase));
            if (preset is not null)
            {
                var btn = CreatePillButton("🎵 送入进行台试听", isPrimary: false, toolTip: $"载入「{preset.Name}」体验靶向音染色跟弹");
                btn.Margin = new Thickness(0, 2, 6, 2);
                btn.Click += (_, _) =>
                {
                    LoadProgressionPreset(preset);
                    SwitchTab(2);
                    ActivateProgressionStep(0, playSound: true);
                };
                _lessonPortalWrap.Children.Add(btn);
            }
        }
    }

    private void OpenCustomProgressionFromLesson(string csvChords, int tonicPitch)
    {
        var chords = csvChords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (chords.Length == 0) return;

        StopProgressionPlayback();
        _activePresetId = "";
        var keyName = MusicTheoryCatalog.NoteName(tonicPitch, MusicTheoryCatalog.PreferFlatsForTonic(tonicPitch));
        _progressionSteps = chords.Select(c => new ProgressionStep(c, ChordTranspose.ToDegree(c, keyName), 4)).ToList();
        _fretboard.TonicPitch = tonicPitch;
        _fretboard.Scale = MusicTheoryCatalog.GetScale("major");
        SyncTopControlsFromFretboard();
        RebuildProgressionPresetPills();
        RebuildProgressionTimeline();
        SwitchTab(2);
        ActivateProgressionStep(0, playSound: true);
        _setStatus?.Invoke($"已载入本课示范进行：{string.Join(" → ", chords)}（点击「▶ 循环播放」可观察四色靶向内音流动）。");
    }

    private void RebuildLessonPracticePanels()
    {
        if (_lessonScalePillsWrap is null || _lessonCagedCardsWrap is null) return;

        // 1. Scale formula interactive pills
        _lessonScalePillsWrap.Children.Clear();
        var preferFlats = MusicTheoryCatalog.PreferFlatsForTonic(_fretboard.TonicPitch);
        var triadSet = new HashSet<int>(_fretboard.Scale.TriadIntervals);

        foreach (var iv in _fretboard.Scale.Intervals)
        {
            var pc = (_fretboard.TonicPitch + iv) % 12;
            var deg = MusicTheoryCatalog.GetDegreeLabel(pc, _fretboard.TonicPitch);
            var noteName = MusicTheoryCatalog.NoteName(pc, preferFlats);
            var isRoot = iv == 0;
            var isThird = triadSet.Contains(iv) && iv is 3 or 4;
            var isFifth = triadSet.Contains(iv) && iv is 6 or 7 or 8;

            var bg = isRoot ? "#E26224" : isThird ? "#7C4DE0" : isFifth ? "#267E55" : "#F8FAFC";
            var fg = (isRoot || isThird || isFifth) ? Brushes.White : AppDialogs.TitleBrush;

            var pill = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg)),
                BorderBrush = (isRoot || isThird || isFifth) ? null : CardBorder,
                BorderThickness = (isRoot || isThird || isFifth) ? new Thickness(0) : new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 2, 6, 4),
                Cursor = Cursors.Hand,
                ToolTip = $"点击全指板闪烁所有「{noteName} ({deg}级)」并发声"
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = deg, FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = fg, Margin = new Thickness(0, 0, 4, 0) });
            row.Children.Add(new TextBlock { Text = noteName, FontSize = 11, Foreground = fg, Opacity = 0.92 });
            pill.Child = row;
            pill.MouseLeftButtonDown += (_, _) =>
            {
                _audio.PlayNote(48 + pc);
                _fretboard.FlashPitchClass(pc, 600);
                _setStatus?.Invoke($"全指板高亮音级：{deg}（{noteName}）");
            };
            _lessonScalePillsWrap.Children.Add(pill);
        }

        // 2. CAGED 5-box fret-range quick-jump cards (hidden when teaching 3NPS mode)
        _lessonCagedCardsWrap.Children.Clear();
        if (_fretboard.ShowThreeNpsBoxes)
        {
            _lessonCagedSection.Visibility = Visibility.Collapsed;
            return;
        }

        var boxes = MusicTheoryCatalog.ComputeCagedBoxes(_fretboard.TonicPitch, _fretboard.Scale.IsMinorFamily, _fretboard.Tuning, FretboardCanvas.TotalFrets);
        if (boxes.Count == 0)
        {
            _lessonCagedSection.Visibility = Visibility.Collapsed;
            return;
        }
        _lessonCagedSection.Visibility = Visibility.Visible;

        var seenLetters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var box in boxes)
        {
            if (box.MaxFret < _fretboard.Capo || !seenLetters.Add(box.ShapeLetter))
                continue;

            var letter = box.ShapeLetter;
            var color = (Color)ColorConverter.ConvertFromString(box.ColorHex);
            var isFocused = _fretboard.ShowCagedBoxes &&
                            string.Equals(_fretboard.ActiveCagedFilter, letter, StringComparison.OrdinalIgnoreCase);

            var card = new Border
            {
                Background = isFocused ? new SolidColorBrush(Color.FromArgb(26, color.R, color.G, color.B)) : Brushes.White,
                BorderBrush = isFocused ? new SolidColorBrush(color) : CardBorder,
                BorderThickness = new Thickness(isFocused ? 1.8 : 1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 2, 7, 4),
                MinWidth = 88,
                Cursor = Cursors.Hand,
                ToolTip = $"点击聚焦 {letter} 形把位（{box.MinFret}–{box.MaxFret}品）并试听"
            };
            var st = new StackPanel();
            var top = new StackPanel { Orientation = Orientation.Horizontal };
            top.Children.Add(new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            });
            top.Children.Add(new TextBlock
            {
                Text = $"{letter} 形把位",
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color)
            });
            st.Children.Add(top);
            st.Children.Add(new TextBlock
            {
                Text = $"{box.MinFret}–{box.MaxFret} 品",
                FontSize = 10.5,
                Foreground = AppDialogs.MutedBrush,
                Margin = new Thickness(13, 2, 0, 0)
            });
            card.Child = st;
            card.MouseLeftButtonDown += (_, _) =>
            {
                var already = _fretboard.ShowCagedBoxes &&
                              string.Equals(_fretboard.ActiveCagedFilter, letter, StringComparison.OrdinalIgnoreCase);
                _fretboard.ShowCagedBoxes = true;
                _fretboard.ActiveCagedFilter = already ? "ALL" : letter;
                _fretboard.HighlightedChord = null;
                SyncTopControlsFromFretboard();
                _fretboard.Refresh();
                RebuildLessonPracticePanels();
                if (!already) PlayAnimatedFretboardTriad();
            };
            _lessonCagedCardsWrap.Children.Add(card);
        }
    }

    private void StepLesson(int delta)
    {
        var lesson = MusicTheoryCatalog.Lessons[_activeLessonIndex];
        var nextStep = _activeStepIndex + delta;
        if (nextStep >= 0 && nextStep < lesson.Steps.Count)
        {
            LoadLesson(_activeLessonIndex, nextStep, applyToFretboard: true);
            return;
        }
        if (delta > 0)
        {
            // Finishing the final step marks the current lesson mastered and advances if possible
            MarkLessonCompleted(lesson.Id, completed: true);
            if (_activeLessonIndex + 1 < MusicTheoryCatalog.Lessons.Count)
                LoadLesson(_activeLessonIndex + 1, 0, applyToFretboard: true);
            else
                _setStatus?.Invoke("🎉 恭喜！你已完成全部 8 大单元 24 节实用乐理实战课程！");
        }
        else if (delta < 0 && _activeLessonIndex > 0)
        {
            var prevLesson = MusicTheoryCatalog.Lessons[_activeLessonIndex - 1];
            LoadLesson(_activeLessonIndex - 1, prevLesson.Steps.Count - 1, applyToFretboard: true);
        }
    }

    // ─── Chord Anatomy Logic ──────────────────────────────────────────────────────────

    private void RebuildChordRootButtons()
    {
        _chordRootWrap.Children.Clear();
        StyleSegButton(_chordDiatonicBtn, _chordDiatonicMode);
        StyleSegButton(_chordFreeBtn, !_chordDiatonicMode);

        var preferFlats = MusicTheoryCatalog.PreferFlatsForTonic(_fretboard.TonicPitch);
        if (_chordDiatonicMode)
        {
            var diatonics = MusicTheoryCatalog.GetDiatonicChords(_fretboard.TonicPitch, _fretboard.Scale.IsMinorFamily, preferFlats);
            foreach (var d in diatonics)
            {
                var item = d;
                var isSel = _selectedChordRootPitch == item.RootPitch;
                var card = new Border
                {
                    Background = isSel ? AppDialogs.AccentLightBrush : Brushes.White,
                    BorderBrush = isSel ? AppDialogs.AccentBrush : CardBorder,
                    BorderThickness = new Thickness(isSel ? 1.6 : 1),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 2, 6, 4),
                    MinWidth = 54,
                    Cursor = Cursors.Hand
                };
                var st = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                st.Children.Add(new TextBlock
                {
                    Text = item.TriadSymbol,
                    FontSize = 13.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = isSel ? AppDialogs.AccentHoverBrush : AppDialogs.TitleBrush,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                st.Children.Add(new TextBlock
                {
                    Text = $"{item.RomanNumeral}",
                    FontSize = 10,
                    Foreground = AppDialogs.MutedBrush,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                card.Child = st;
                card.MouseLeftButtonDown += (_, _) =>
                {
                    _selectedChordRootPitch = item.RootPitch;
                    SelectChordBySymbol(item.TriadSymbol, syncToFretboard: true, autoAudition: true);
                    RebuildChordRootButtons();
                    RebuildChordQualityButtons();
                };
                _chordRootWrap.Children.Add(card);
            }
        }
        else
        {
            for (var pc = 0; pc < 12; pc++)
            {
                var pitch = pc;
                var name = MusicTheoryCatalog.TonicLabels[pc];
                var btn = CreatePillButton(name, isPrimary: _selectedChordRootPitch == pitch);
                btn.MinWidth = 40;
                btn.Margin = new Thickness(0, 2, 5, 4);
                btn.Click += (_, _) =>
                {
                    _selectedChordRootPitch = pitch;
                    SelectChordBySymbol(name + _selectedChordQuality, syncToFretboard: true, autoAudition: true);
                    RebuildChordRootButtons();
                };
                _chordRootWrap.Children.Add(btn);
            }
        }
    }

    private void RebuildChordQualityButtons()
    {
        _chordQualityWrap.Children.Clear();
        StyleSegButton(_chordBasicBtn, !_chordExploreMode);
        StyleSegButton(_chordExploreBtn, _chordExploreMode);

        (string Label, string Suffix)[] qualities = _chordExploreMode
            ? [("大三", ""), ("小三", "m"), ("属7", "7"), ("大7", "maj7"), ("小7", "m7"), ("sus2", "sus2"), ("sus4", "sus4"), ("add9", "add9"), ("9", "9"), ("m9", "m9"), ("6", "6"), ("m7-5", "m7-5"), ("dim7", "dim7"), ("aug", "aug"), ("7sus4", "7sus4"), ("强力5", "5")]
            : [("大三", ""), ("小三", "m"), ("属7", "7"), ("大7", "maj7"), ("小7", "m7"), ("add9", "add9"), ("sus4", "sus4")];

        var preferFlats = MusicTheoryCatalog.PreferFlatsForTonic(_fretboard.TonicPitch);
        foreach (var (label, suffix) in qualities)
        {
            var q = suffix;
            var btn = CreatePillButton(label, isPrimary: string.Equals(_selectedChordQuality, q, StringComparison.OrdinalIgnoreCase));
            btn.Margin = new Thickness(0, 2, 5, 4);
            btn.Click += (_, _) =>
            {
                _selectedChordQuality = q;
                var rootName = MusicTheoryCatalog.NoteName(_selectedChordRootPitch, preferFlats);
                SelectChordBySymbol(rootName + q, syncToFretboard: true, autoAudition: true);
                RebuildChordQualityButtons();
            };
            _chordQualityWrap.Children.Add(btn);
        }
    }

    private void SelectChordBySymbol(string chordSymbol, bool syncToFretboard, bool autoAudition)
    {
        var normalized = ChordCatalog.NormalizeChordName(chordSymbol);
        if (string.IsNullOrWhiteSpace(normalized)) return;

        _activeChordSymbol = normalized;
        _chordCustomSearchBox.Text = normalized;
        if (ChordVoicingEngine.TryParseChord(normalized, out var parsed))
        {
            _selectedChordRootPitch = parsed.RootPitch;
            _selectedChordQuality = parsed.CanonicalQuality;
        }

        _chordVoicings = MusicTheoryCatalog.GetCagedChordVoicings(normalized);
        _chordCandidateIdx = 0;
        RefreshChordAnatomyDisplay(syncToFretboard);
        if (autoAudition)
            PlayActiveChord(arpeggio: false);
    }

    private void CycleChordCandidate(int delta)
    {
        if (_chordVoicings.Count <= 1) return;
        _chordCandidateIdx = (_chordCandidateIdx + delta + _chordVoicings.Count) % _chordVoicings.Count;
        RefreshChordAnatomyDisplay(syncToFretboard: true);
        PlayActiveChord(arpeggio: false);
    }

    private void RefreshChordAnatomyDisplay(bool syncToFretboard = true)
    {
        var activeVoicing = _chordVoicings.Count > 0
            ? _chordVoicings[Math.Clamp(_chordCandidateIdx, 0, _chordVoicings.Count - 1)]
            : null;
        var shape = activeVoicing?.Shape ?? ChordCatalog.ByName(_activeChordSymbol);
        _chordAnatomyTitle.Text = shape is not null ? shape.Name : _activeChordSymbol;
        _chordAnatomySub.Text = shape is not null ? shape.Label : "";
        _chordVoicingCounter.Text = _chordVoicings.Count > 0
            ? $"{_chordCandidateIdx + 1} / {_chordVoicings.Count}"
            : "0 / 0";

        RebuildChordVoicingGallery();
        RebuildChordFunctionAndVariations();

        if (shape is null)
        {
            _chordDiagramView.Shape = null;
            _chordDecodeGrid.Children.Clear();
            return;
        }

        _chordDiagramView.Shape = PrepareDiagramShape(shape);
        BuildChordDecodeTable(shape);

        if (syncToFretboard && _activeTab == 1)
        {
            _fretboard.JamTargetColorMode = false;
            _fretboard.HighlightedChord = shape;

            // If clicking a CAGED voicing of the current key's tonic triad/7th, also focus its matching CAGED box!
            if (activeVoicing?.CagedLetter is { } letter &&
                ChordVoicingEngine.TryParseChord(shape.Name, out var parsed) &&
                parsed.RootPitch == _fretboard.TonicPitch &&
                ((parsed.CanonicalQuality is "m" or "m7") == _fretboard.Scale.IsMinorFamily))
            {
                _fretboard.ShowCagedBoxes = true;
                _fretboard.ActiveCagedFilter = letter;
                SyncTopControlsFromFretboard();
            }
            else
            {
                UpdateSummaryAndLegend();
            }
            _fretboard.Refresh();
        }
    }

    private ChordShape PrepareDiagramShape(ChordShape shape) =>
        _showChordFingers
            ? shape
            : new ChordShape
            {
                Id = shape.Id,
                Name = shape.Name,
                Type = shape.Type,
                Frets = shape.Frets,
                Fingers = new int[6],
                BaseFret = shape.BaseFret,
                Barre = shape.Barre,
                Label = shape.Label
            };

    private void RebuildChordVoicingGallery()
    {
        if (_chordGalleryWrap is null) return;
        _chordGalleryWrap.Children.Clear();

        var hasCaged = _chordVoicings.Any(v => v.CagedLetter is not null);
        _chordGalleryHeader.Text = hasCaged
            ? $"「{_activeChordSymbol}」CAGED 全指板把位手型（共 {_chordVoicings.Count} 个把位 · 点哪张手型卡，上方指板亮哪块）"
            : $"「{_activeChordSymbol}」可用指法把位一览（共 {_chordVoicings.Count} 个把位 · 点击切换并在指板高亮）";

        for (var i = 0; i < _chordVoicings.Count; i++)
        {
            var idx = i;
            var v = _chordVoicings[i];
            var isSel = idx == _chordCandidateIdx;
            var accentColor = (Color)ColorConverter.ConvertFromString(v.ColorHex);
            var accentBrush = new SolidColorBrush(accentColor);

            var card = new Border
            {
                Width = 104,
                Background = isSel ? new SolidColorBrush(Color.FromArgb(20, accentColor.R, accentColor.G, accentColor.B)) : Brushes.White,
                BorderBrush = isSel ? accentBrush : CardBorder,
                BorderThickness = new Thickness(isSel ? 2.0 : 1.0),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6, 6, 6, 6),
                Margin = new Thickness(0, 0, 8, 8),
                Cursor = Cursors.Hand,
                ToolTip = $"点击切换到 {v.Shape.Label} 并在上方指板高亮试听"
            };

            card.MouseEnter += (_, _) =>
            {
                if (idx != _chordCandidateIdx)
                    card.BorderBrush = new SolidColorBrush(Color.FromArgb(140, accentColor.R, accentColor.G, accentColor.B));
            };
            card.MouseLeave += (_, _) =>
            {
                if (idx != _chordCandidateIdx)
                    card.BorderBrush = CardBorder;
            };

            var stack = new StackPanel();

            // Top row: CAGED colored pill + fret position hint
            var topDock = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var posTxt = new TextBlock
            {
                Text = v.PositionHint,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = AppDialogs.MutedBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(posTxt, Dock.Right);
            topDock.Children.Add(posTxt);

            var badge = new Border
            {
                Background = v.CagedLetter is not null ? accentBrush : (isSel ? accentBrush : SoftSurface),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(6, 1.5, 6, 1.5),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = v.BadgeText,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = (v.CagedLetter is not null || isSel) ? Brushes.White : AppDialogs.BodyBrush
                }
            };
            topDock.Children.Add(badge);
            stack.Children.Add(topDock);

            // Mini live ChordDiagramView
            var miniDiagram = new ChordDiagramView
            {
                Width = 84,
                Height = 96,
                HorizontalAlignment = HorizontalAlignment.Center,
                Shape = PrepareDiagramShape(v.Shape),
                IsHitTestVisible = false
            };
            stack.Children.Add(miniDiagram);

            card.Child = stack;
            card.MouseLeftButtonDown += (_, _) =>
            {
                _chordCandidateIdx = idx;
                RefreshChordAnatomyDisplay(syncToFretboard: true);
                PlayActiveChord(arpeggio: false);
            };

            _chordGalleryWrap.Children.Add(card);
        }
    }

    private void RebuildChordFunctionAndVariations()
    {
        if (_chordFunctionText is null || _chordVariationsWrap is null) return;
        var preferFlats = MusicTheoryCatalog.PreferFlatsForTonic(_fretboard.TonicPitch);
        _chordFunctionText.Text = MusicTheoryCatalog.DescribeDiatonicFunction(
            _activeChordSymbol,
            _fretboard.TonicPitch,
            _fretboard.Scale.IsMinorFamily);

        _chordVariationsWrap.Children.Clear();
        var variations = MusicTheoryCatalog.GetChordColorVariations(_activeChordSymbol, preferFlats);
        foreach (var (label, sym) in variations)
        {
            var s = sym;
            var chip = CreatePillButton($"{sym} · {label}", isPrimary: false, toolTip: $"点击立即切换到 {sym} 试听对比");
            chip.FontSize = 11;
            chip.Padding = new Thickness(8, 3, 8, 3);
            chip.Margin = new Thickness(0, 2, 5, 4);
            chip.Click += (_, _) =>
            {
                SelectChordBySymbol(s, syncToFretboard: true, autoAudition: true);
                RebuildChordRootButtons();
                RebuildChordQualityButtons();
            };
            _chordVariationsWrap.Children.Add(chip);
        }
    }

    private void PinActiveChordToLibraryCustomChords()
    {
        var lib = _getLibrary?.Invoke();
        var shape = _chordVoicings.Count > 0
            ? _chordVoicings[Math.Clamp(_chordCandidateIdx, 0, _chordVoicings.Count - 1)].Shape
            : ChordCatalog.ByName(_activeChordSymbol);
        if (lib is null || shape is null) return;

        var tonicName = MusicTheoryCatalog.NoteName(_fretboard.TonicPitch, MusicTheoryCatalog.PreferFlatsForTonic(_fretboard.TonicPitch));
        var keyBucket = ChordCatalog.NormalizeKeyBucket(_fretboard.Scale.IsMinorFamily ? tonicName + "m" : tonicName);

        if (!lib.CustomChordsByKey.TryGetValue(keyBucket, out var list))
        {
            list = [];
            lib.CustomChordsByKey[keyBucket] = list;
        }
        if (!list.Contains(shape.Name, StringComparer.OrdinalIgnoreCase))
            list.Add(shape.Name);

        lib.CustomChordShapes.RemoveAll(c => string.Equals(c.Name, shape.Name, StringComparison.OrdinalIgnoreCase));
        lib.CustomChordShapes.Add(new CustomChordDefinition
        {
            Name = shape.Name,
            BaseFret = shape.BaseFret,
            Frets = shape.Frets.ToArray(),
            Fingers = shape.Fingers.ToArray(),
            Label = shape.Label
        });

        ChordCatalog.LoadCustomData(lib.CustomChordsByKey, lib.CustomChordShapes);
        _onLibraryMutated?.Invoke();
        _setStatus?.Invoke($"已将「{shape.Name}（{shape.Label}）」收录至 {keyBucket} 调常用和弦栏，切回「编辑制谱」即可一键点用！");
    }

    private void BuildChordDecodeTable(ChordShape shape)
    {
        _chordDecodeGrid.Children.Clear();
        _chordDecodeGrid.RowDefinitions.Clear();
        _chordDecodeGrid.ColumnDefinitions.Clear();

        _chordDecodeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(66) });
        for (var s = 0; s < 6; s++)
            _chordDecodeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (var r = 0; r < 4; r++)
            _chordDecodeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Subtle zebra row stripes
        for (var r = 1; r < 4; r += 2)
        {
            var stripe = new Border { Background = SoftSurface, CornerRadius = new CornerRadius(5) };
            Grid.SetRow(stripe, r);
            Grid.SetColumnSpan(stripe, 7);
            _chordDecodeGrid.Children.Add(stripe);
        }

        var preferFlats = MusicTheoryCatalog.PreferFlatsForTonic(_fretboard.TonicPitch);
        var decoded = MusicTheoryCatalog.DecodeChordStrings(shape, _fretboard.TonicPitch, _fretboard.Tuning, _fretboard.Capo, preferFlats);

        string[] rowHeaders = ["弦位", "和弦音程", "调内音级", "实际音名"];
        for (var r = 0; r < 4; r++)
        {
            var hdr = new TextBlock
            {
                Text = rowHeaders[r],
                FontSize = 11,
                FontWeight = r == 0 ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = AppDialogs.MutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 5, 6, 5)
            };
            Grid.SetRow(hdr, r);
            Grid.SetColumn(hdr, 0);
            _chordDecodeGrid.Children.Add(hdr);
        }

        string[] stringNames = ["6E", "5A", "4D", "3G", "2B", "1e"];
        for (var s = 0; s < 6; s++)
        {
            var strIdx = s;
            var d = decoded[s];

            // Interactive column click target overlay
            var colHit = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(6),
                Cursor = d.Fret.HasValue ? Cursors.Hand : Cursors.Arrow,
                ToolTip = d.Fret.HasValue ? $"点击单弦拨响 {stringNames[s]} ({d.NoteWithOctave})" : "哑音弦 (×)"
            };
            if (d.Fret is { } relFret)
            {
                colHit.MouseEnter += (_, _) => colHit.Background = new SolidColorBrush(Color.FromArgb(26, 47, 107, 72));
                colHit.MouseLeave += (_, _) => colHit.Background = Brushes.Transparent;
                colHit.MouseLeftButtonDown += (_, _) =>
                {
                    var absFret = _fretboard.Capo + relFret;
                    var midi = _fretboard.Tuning.OpenMidi[strIdx] + absFret;
                    _audio.PlayNote(midi);
                    if (absFret <= FretboardCanvas.TotalFrets)
                        _fretboard.FlashNote(strIdx, absFret, 420);
                };
            }
            Grid.SetRow(colHit, 0);
            Grid.SetRowSpan(colHit, 4);
            Grid.SetColumn(colHit, s + 1);
            _chordDecodeGrid.Children.Add(colHit);

            // Row 0: String header
            var sh = new TextBlock { Text = stringNames[s], FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = AppDialogs.MutedBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 4), IsHitTestVisible = false };
            Grid.SetRow(sh, 0);
            Grid.SetColumn(sh, s + 1);
            _chordDecodeGrid.Children.Add(sh);

            // Row 1: Interval pill
            UIElement ivCell;
            if (d.IntervalRole == "mute")
            {
                ivCell = new TextBlock { Text = "—", FontSize = 11.5, Foreground = AppDialogs.MutedBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            }
            else
            {
                var bgHex = d.IntervalRole switch
                {
                    "root" => "#E26224",
                    "third" => "#7C4DE0",
                    "fifth" => "#267E55",
                    _ => "#2563EB"
                };
                ivCell = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)),
                    CornerRadius = new CornerRadius(10),
                    MinWidth = 24,
                    Height = 20,
                    Padding = new Thickness(6, 0, 6, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 3, 0, 3),
                    IsHitTestVisible = false,
                    Child = new TextBlock { Text = d.IntervalLabel, FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                };
            }
            Grid.SetRow(ivCell, 1);
            Grid.SetColumn(ivCell, s + 1);
            _chordDecodeGrid.Children.Add(ivCell);

            // Row 2: Scale Degree
            var degCell = new TextBlock
            {
                Text = d.ScaleDegreeLabel,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = d.Fret.HasValue ? AppDialogs.TitleBrush : AppDialogs.MutedBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4),
                IsHitTestVisible = false
            };
            Grid.SetRow(degCell, 2);
            Grid.SetColumn(degCell, s + 1);
            _chordDecodeGrid.Children.Add(degCell);

            // Row 3: Note + Octave
            var noteCell = new TextBlock
            {
                Text = d.NoteWithOctave,
                FontSize = 11.5,
                Foreground = d.Fret.HasValue ? AppDialogs.BodyBrush : AppDialogs.MutedBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4),
                IsHitTestVisible = false
            };
            Grid.SetRow(noteCell, 3);
            Grid.SetColumn(noteCell, s + 1);
            _chordDecodeGrid.Children.Add(noteCell);
        }
    }

    private void PlayActiveChord(bool arpeggio)
    {
        var shape = _chordVoicings.Count > 0
            ? _chordVoicings[Math.Clamp(_chordCandidateIdx, 0, _chordVoicings.Count - 1)].Shape
            : ChordCatalog.ByName(_activeChordSymbol);
        if (shape is null) return;
        var notes = MusicTheoryCatalog.GetChordMidiNotes(shape, _fretboard.Tuning, _fretboard.Capo);
        _audio.PlayChord(notes, arpeggio);
        _fretboard.FlashChordShape(shape, arpeggio);
    }

    // ─── Progression & Beat LED Logic ─────────────────────────────────────────────────

    private void UpdateProgModeSeg()
    {
        StyleSegButton(_progStrumBtn, !_progArpeggio);
        StyleSegButton(_progArpBtn, _progArpeggio);
    }

    private void AdjustProgBpm(int delta)
    {
        _progBpm = Math.Clamp(_progBpm + delta, 45, 190);
        _progBpmText.Text = $"{_progBpm} BPM";
        if (_progPlaying)
            _progBeatTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(180, (int)Math.Round(60000.0 / _progBpm)));
    }

    private void RebuildProgressionQuickAddBar()
    {
        if (_progQuickAddWrap is null) return;
        _progQuickAddWrap.Children.Clear();

        _progQuickAddWrap.Children.Add(new TextBlock
        {
            Text = "快拼本调和弦：",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 6, 0)
        });

        var preferFlats = MusicTheoryCatalog.PreferFlatsForTonic(_fretboard.TonicPitch);
        var diatonics = MusicTheoryCatalog.GetDiatonicChords(_fretboard.TonicPitch, _fretboard.Scale.IsMinorFamily, preferFlats);
        foreach (var d in diatonics)
        {
            var item = d;
            var chip = CreatePillButton($"+ {item.RomanNumeral} {item.TriadSymbol}", isPrimary: false);
            chip.FontSize = 11;
            chip.Padding = new Thickness(8, 2, 8, 2);
            chip.Margin = new Thickness(0, 2, 5, 2);
            chip.Click += (_, _) =>
            {
                _progressionSteps.Add(new ProgressionStep(item.TriadSymbol, item.RomanNumeral, 4));
                RebuildProgressionTimeline();
                ActivateProgressionStep(_progressionSteps.Count - 1, playSound: true);
            };
            _progQuickAddWrap.Children.Add(chip);
        }
    }

    private void ImportProgressionFromCurrentSong()
    {
        var song = _getCurrentSong?.Invoke();
        var extracted = MusicTheoryCatalog.ExtractSongProgression(song);
        if (extracted.Count == 0)
        {
            _setStatus?.Invoke("当前曲谱还没有标记和弦，可先在「编辑制谱」填入和弦，或点下方快拼按钮自由组合。");
            return;
        }
        StopProgressionPlayback();
        _progressionSteps = extracted;
        if (song is { Bpm: >= 45 and <= 190 })
        {
            _progBpm = song.Bpm;
            _progBpmText.Text = $"{_progBpm} BPM";
        }
        SyncWithCurrentSong(silent: true);
        RebuildProgressionTimeline();
        UpdateProgressionLivePanel(_progressionSteps.FirstOrDefault());
        _progSuggestionCard.Visibility = Visibility.Collapsed;
        _setStatus?.Invoke($"已从《{song!.Title}》导入 {extracted.Count} 个和弦进行。");
    }

    private void RebuildProgressionPresetPills()
    {
        if (_progPresetPillsWrap is null) return;
        _progPresetPillsWrap.Children.Clear();
        _progPresetPillsWrap.Children.Add(new TextBlock
        {
            Text = "经典套路：",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 6, 0)
        });

        foreach (var preset in MusicTheoryCatalog.ProgressionPresets)
        {
            var p = preset;
            var shortTitle = p.Name.Contains('·') ? p.Name.Split('·')[1].Trim() : p.Name;
            var isSel = string.Equals(p.Id, _activePresetId, StringComparison.OrdinalIgnoreCase);
            var chip = CreatePillButton(shortTitle, isPrimary: false, toolTip: p.Name);
            chip.FontSize = 11;
            chip.Padding = new Thickness(9, 2.5, 9, 2.5);
            chip.Margin = new Thickness(0, 2, 5, 2);
            StyleTogglePill(chip, isSel);
            chip.Click += (_, _) => LoadProgressionPreset(p);
            _progPresetPillsWrap.Children.Add(chip);
        }
    }

    private void LoadProgressionPreset(ProgressionPreset preset)
    {
        StopProgressionPlayback();
        _activePresetId = preset.Id;
        _progressionSteps = preset.Steps.Select(s => s with { }).ToList();
        _progBpm = Math.Clamp(preset.Bpm, 45, 190);
        _progBpmText.Text = $"{_progBpm} BPM";
        if (ChordTranspose.ReadKey(preset.KeyName) is { } pk)
        {
            _fretboard.TonicPitch = pk.Pitch;
            _fretboard.Scale = MusicTheoryCatalog.GetScale(pk.Minor ? "natural_minor" : "major");
            SyncTopControlsFromFretboard();
            RebuildProgressionQuickAddBar();
            _fretboard.Refresh();
        }
        _progSuggestionCard.Visibility = Visibility.Collapsed;
        RebuildProgressionPresetPills();
        RebuildProgressionTimeline();
        UpdateProgressionLivePanel(_progressionSteps.FirstOrDefault());
    }

    private void RebuildProgressionTimeline()
    {
        _progTimelineWrap.Children.Clear();
        if (_progressionSteps.Count == 0)
        {
            _progTimelineWrap.Children.Add(new TextBlock
            {
                Text = "进行为空 — 点击上方「快拼本调和弦」或选择一个经典进行预设即可开始。",
                FontSize = 12,
                Foreground = AppDialogs.MutedBrush,
                Margin = new Thickness(4, 10, 4, 10)
            });
            return;
        }

        for (var i = 0; i < _progressionSteps.Count; i++)
        {
            var idx = i;
            var step = _progressionSteps[i];
            var isCurrent = idx == _progPlayIndex;

            var card = new Border
            {
                Background = isCurrent ? AppDialogs.AccentLightBrush : Brushes.White,
                BorderBrush = isCurrent ? AppDialogs.AccentBrush : CardBorder,
                BorderThickness = new Thickness(isCurrent ? 2.0 : 1),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 2, 10, 8),
                MinWidth = 104,
                Cursor = Cursors.Hand
            };

            var st = new StackPanel();

            // Header row: Roman numeral + delete button
            var topRow = new DockPanel();
            var delBtn = new Button
            {
                Content = "×",
                Padding = new Thickness(3, 0, 3, 0),
                FontSize = 11,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = AppDialogs.MutedBrush,
                ToolTip = "移除此小节"
            };
            delBtn.Click += (_, e) =>
            {
                e.Handled = true;
                _progressionSteps.RemoveAt(idx);
                if (_progPlayIndex >= _progressionSteps.Count) _progPlayIndex = -1;
                RebuildProgressionTimeline();
            };
            DockPanel.SetDock(delBtn, Dock.Right);
            topRow.Children.Add(delBtn);
            topRow.Children.Add(new TextBlock { Text = step.Roman, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = isCurrent ? AppDialogs.AccentHoverBrush : AppDialogs.MutedBrush });
            st.Children.Add(topRow);

            // Big Chord Symbol
            st.Children.Add(new TextBlock
            {
                Text = step.ChordName,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = isCurrent ? AppDialogs.AccentHoverBrush : AppDialogs.TitleBrush,
                Margin = new Thickness(0, 2, 0, 6)
            });

            // Bottom row: Animated Beat LEDs + clickable 2/4 beats toggle
            var botDock = new DockPanel();
            var beatToggle = new Button
            {
                Content = $"{step.Beats}拍",
                FontSize = 10,
                Padding = new Thickness(5, 1, 5, 1),
                Background = SoftSurface,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                Foreground = AppDialogs.MutedBrush,
                ToolTip = "点击切换 2 拍 / 4 拍"
            };
            beatToggle.Click += (_, e) =>
            {
                e.Handled = true;
                _progressionSteps[idx] = step with { Beats = step.Beats == 4 ? 2 : 4 };
                RebuildProgressionTimeline();
            };
            DockPanel.SetDock(beatToggle, Dock.Right);
            botDock.Children.Add(beatToggle);

            var leds = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            for (var b = 0; b < step.Beats; b++)
            {
                var lit = isCurrent && _progPlaying && b == _progCurrentBeat;
                var passed = isCurrent && _progPlaying && b < _progCurrentBeat;
                leds.Children.Add(new Border
                {
                    Width = lit ? 8 : 6,
                    Height = lit ? 8 : 6,
                    CornerRadius = new CornerRadius(4),
                    Background = lit ? AppDialogs.AccentBrush
                        : passed ? new SolidColorBrush(Color.FromRgb(148, 191, 164))
                        : new SolidColorBrush(Color.FromRgb(218, 224, 228)),
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            botDock.Children.Add(leds);
            st.Children.Add(botDock);

            card.Child = st;
            card.MouseLeftButtonDown += (_, _) =>
            {
                _progCurrentBeat = 0;
                ActivateProgressionStep(idx, playSound: true);
            };
            _progTimelineWrap.Children.Add(card);
        }
    }

    private void ActivateProgressionStep(int index, bool playSound)
    {
        if (index < 0 || index >= _progressionSteps.Count) return;
        _progPlayIndex = index;
        var step = _progressionSteps[index];
        var shape = ChordCatalog.ByName(step.ChordName);

        _progActiveBanner.Text = $"正在高亮：{step.Roman}  {step.ChordName}  ·  红=根音（拍头稳）  金=和弦音（停住稳）  蓝=色彩音（加味道）";
        _fretboard.JamActiveChordName = step.ChordName;
        UpdateJamTargetVisual();
        _fretboard.HighlightedChord = _progJamColors ? null : shape;
        UpdateSummaryAndLegend();
        _fretboard.Refresh();
        RebuildProgressionTimeline();
        UpdateProgressionLivePanel(step);

        if (playSound && shape is not null)
        {
            var notes = MusicTheoryCatalog.GetChordMidiNotes(shape, _fretboard.Tuning, _fretboard.Capo);
            _audio.PlayChord(notes, arpeggio: _progArpeggio);
            if (!_progJamColors)
                _fretboard.FlashChordShape(shape, _progArpeggio);
        }
    }

    private void UpdateProgressionLivePanel(ProgressionStep? step)
    {
        if (_progLiveDiagram is null || _progLiveTargetPanel is null) return;
        _progLiveTargetPanel.Children.Clear();

        if (step is null)
        {
            _progLiveDiagram.Shape = null;
            _progLiveChordTitle.Text = "—";
            _progLiveChordTones.Text = "点选上方任意和弦小节查看实时指法与靶向音";
            return;
        }

        var shape = ChordCatalog.ByName(step.ChordName);
        _progLiveDiagram.Shape = shape;
        _progLiveChordTitle.Text = $"{step.ChordName}  ({step.Roman})";

        var preferFlats = MusicTheoryCatalog.PreferFlatsForTonic(_fretboard.TonicPitch);
        var (roots, chordTones, colorTones) = MusicTheoryCatalog.GetJamTargetPitchClasses(
            step.ChordName,
            _fretboard.TonicPitch,
            _fretboard.Scale);

        var allTones = roots.Concat(chordTones).Select(pc => MusicTheoryCatalog.NoteName(pc, preferFlats)).Distinct();
        _progLiveChordTones.Text = $"和弦组成音：{string.Join(" · ", allTones)}";

        void AddTargetRow(string title, string hex, List<int> pcs)
        {
            if (pcs.Count == 0) return;
            var col = (Color)ColorConverter.ConvertFromString(hex);
            var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 6), VerticalAlignment = VerticalAlignment.Center };
            var labelRow = new StackPanel { Orientation = Orientation.Horizontal, Width = 108, VerticalAlignment = VerticalAlignment.Center };
            labelRow.Children.Add(new Border
            {
                Width = 9,
                Height = 9,
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(col),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            labelRow.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = AppDialogs.BodyBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(labelRow);
            foreach (var pc in pcs)
            {
                var pitch = pc;
                var name = MusicTheoryCatalog.NoteName(pitch, preferFlats);
                var deg = MusicTheoryCatalog.GetDegreeLabel(pitch, _fretboard.TonicPitch);
                var pill = new Border
                {
                    Background = new SolidColorBrush(col),
                    CornerRadius = new CornerRadius(11),
                    Padding = new Thickness(9, 2.5, 9, 2.5),
                    Margin = new Thickness(0, 1, 6, 1),
                    Cursor = Cursors.Hand,
                    ToolTip = $"点击全指板闪烁所有 {name} 并发声",
                    Child = new TextBlock { Text = $"{name} ({deg}级)", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White }
                };
                pill.MouseLeftButtonDown += (_, _) =>
                {
                    _audio.PlayNote(48 + pitch);
                    _fretboard.FlashPitchClass(pitch, 600);
                };
                row.Children.Add(pill);
            }
            _progLiveTargetPanel.Children.Add(row);
        }

        AddTargetRow("拍头最稳根音", "#DC2626", roots);
        AddTargetRow("停住好听和弦音", "#D97706", chordTones);
        AddTargetRow("高级感色彩音", "#2563EB", colorTones);
    }

    private void UpdateJamTargetVisual()
    {
        _fretboard.JamTargetColorMode = _progJamColors && !string.IsNullOrWhiteSpace(_fretboard.JamActiveChordName);
        _fretboard.Refresh();
    }

    private void ToggleProgressionPlayback()
    {
        if (_progPlaying)
        {
            StopProgressionPlayback();
            return;
        }
        if (_progressionSteps.Count == 0) return;

        _progPlaying = true;
        _progPlayIndex = 0;
        _progCurrentBeat = 0;
        _progPlayBtn.Content = "■ 停止播放";
        ActivateProgressionStep(0, playSound: true);
        _progBeatTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(180, (int)Math.Round(60000.0 / _progBpm)));
        _progBeatTimer.Start();
    }

    private void OnProgressionBeatTick(object? sender, EventArgs e)
    {
        if (!_progPlaying || _progressionSteps.Count == 0)
        {
            StopProgressionPlayback();
            return;
        }

        _progCurrentBeat++;
        var currentStep = _progressionSteps[Math.Clamp(_progPlayIndex, 0, _progressionSteps.Count - 1)];
        if (_progCurrentBeat < currentStep.Beats)
        {
            RebuildProgressionTimeline();
            return;
        }

        _progCurrentBeat = 0;
        var nextIdx = _progPlayIndex + 1;
        if (nextIdx >= _progressionSteps.Count)
        {
            if (_progLoop)
                nextIdx = 0;
            else
            {
                StopProgressionPlayback();
                return;
            }
        }
        ActivateProgressionStep(nextIdx, playSound: true);
    }

    private void StopProgressionPlayback()
    {
        _progPlaying = false;
        _progCurrentBeat = 0;
        _progBeatTimer.Stop();
        if (_progPlayBtn is not null) _progPlayBtn.Content = "▶ 播放进行";
        _fretboard.JamTargetColorMode = false;
        _fretboard.JamActiveChordName = null;
        UpdateSummaryAndLegend();
        _fretboard.Refresh();
        if (_progTimelineWrap is not null && _progressionSteps.Count > 0)
            RebuildProgressionTimeline();
    }

    private void ShowProgressionSuggestions(bool isRicher)
    {
        var rawChords = _progressionSteps.Select(s => s.ChordName).ToList();
        var preferFlats = MusicTheoryCatalog.PreferFlatsForTonic(_fretboard.TonicPitch);
        var suggestions = isRicher
            ? MusicTheoryCatalog.SuggestRicherProgressions(rawChords, _fretboard.TonicPitch, _fretboard.Scale.IsMinorFamily)
            : MusicTheoryCatalog.SuggestSmootherProgressions(rawChords, preferFlats);

        _progSuggestionList.Children.Clear();
        var hdr = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var closeBtn = CreatePillButton("收起", isPrimary: false);
        closeBtn.Padding = new Thickness(8, 2, 8, 2);
        closeBtn.Click += (_, _) => _progSuggestionCard.Visibility = Visibility.Collapsed;
        DockPanel.SetDock(closeBtn, Dock.Right);
        hdr.Children.Add(closeBtn);
        hdr.Children.Add(new TextBlock
        {
            Text = isRicher ? "✨ 编曲教练 · 色彩升级方案" : "✨ 编曲教练 · 转位低音顺滑连接方案",
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = AppDialogs.AccentHoverBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        _progSuggestionList.Children.Add(hdr);

        foreach (var sug in suggestions)
        {
            var item = sug;
            var row = new Border
            {
                Background = Brushes.White,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var dock = new DockPanel();
            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var applyBtn = CreatePillButton("✓ 应用并试听", isPrimary: true);
            applyBtn.Click += (_, _) =>
            {
                var keyStr = MusicTheoryCatalog.NoteName(_fretboard.TonicPitch, preferFlats);
                _progressionSteps = item.Chords.Select(c => new ProgressionStep(c, ChordTranspose.ToDegree(c, keyStr), 4)).ToList();
                RebuildProgressionTimeline();
                _progSuggestionCard.Visibility = Visibility.Collapsed;
                ActivateProgressionStep(0, playSound: true);
                _setStatus?.Invoke($"已应用「{item.Label}」：{string.Join(" → ", item.Chords)}");
            };
            btns.Children.Add(applyBtn);
            DockPanel.SetDock(btns, Dock.Right);
            dock.Children.Add(btns);

            var info = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            info.Children.Add(new TextBlock { Text = $"{item.Label}   {string.Join("  →  ", item.Chords)}", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = AppDialogs.TitleBrush });
            info.Children.Add(new TextBlock { Text = item.Reason, FontSize = 11.5, Foreground = AppDialogs.MutedBrush, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap });
            dock.Children.Add(info);

            row.Child = dock;
            _progSuggestionList.Children.Add(row);
        }

        _progSuggestionCard.Visibility = Visibility.Visible;
    }

    // ─── Personal Theory Notebook Logic (Auto-Save + Starter Templates) ───────────────

    private void CreateTheoryNoteFromCurrentFretboard()
    {
        var lib = _getLibrary?.Invoke();
        if (lib is null) return;

        var tonicName = MusicTheoryCatalog.NoteName(_fretboard.TonicPitch, MusicTheoryCatalog.PreferFlatsForTonic(_fretboard.TonicPitch));
        var shapeSuffix = _fretboard.ShowThreeNpsBoxes
            ? (_fretboard.ActiveThreeNpsPosition == 0 ? "3NPS 全指板" : $"3NPS 第{_fretboard.ActiveThreeNpsPosition}把位")
            : (_fretboard.ShowCagedBoxes
                ? (_fretboard.ActiveCagedFilter == "ALL" ? "CAGED 全指板" : $"CAGED {_fretboard.ActiveCagedFilter}形")
                : _fretboard.Scale.Name);

        var item = new TheoryNoteItem
        {
            Title = $"{tonicName} 调 · {shapeSuffix}",
            Tag = (_fretboard.ShowCagedBoxes || _fretboard.ShowThreeNpsBoxes) ? "CAGED" : "音阶",
            Content = $"记录于 {DateTime.Now:yyyy-MM-dd HH:mm}\n• 记忆要点：\n• 常用乐句 / 和弦连接："
        };
        CaptureFretboardIntoNote(item);

        lib.TheoryNotes.Insert(0, item);
        _selectedNote = item;
        _onLibraryMutated?.Invoke();
        RefreshNotesList();
        _noteTitleBox.Focus();
        _noteTitleBox.SelectAll();
        _setStatus?.Invoke($"已将当前指板拍成快照存入收集本：「{item.Title}」。");
    }

    private void SeedStarterTheoryNotes()
    {
        var lib = _getLibrary?.Invoke();
        if (lib is null) return;

        var n1 = new TheoryNoteItem
        {
            Title = "C 调 CAGED 五把位全景速查",
            Tag = "CAGED",
            TonicPitch = 0,
            ScaleId = "major",
            TuningId = "standard",
            Capo = 0,
            ShowCaged = true,
            ActiveCagedFilter = "ALL",
            ShowCapsules = true,
            Content = "【核心口诀】C → A → G → E → D 首尾相连铺满 12 品！\n• 练法：先看每根弦上的“双音胶囊”，记住五声音阶的 2 音框架，再补上 4、7 两个半音过渡音。\n• 小调秒换算：Am 关系小调和 C 大调共用完全相同的 5 个指型，只需把主音落点换成 6（A）。"
        };
        var n2 = new TheoryNoteItem
        {
            Title = "G 调 E 指型（3品）即兴黄金把位",
            Tag = "即兴",
            TonicPitch = 7,
            ScaleId = "major_pent",
            TuningId = "standard",
            Capo = 0,
            ShowCaged = true,
            ActiveCagedFilter = "E",
            ShowCapsules = true,
            HighlightChord = "",
            Content = "民谣弹唱最常用的 G 调高把位 Solo 入口：\n• 食指立在 6 弦 3 品（G 根音），就是 E 指型大横按框架。\n• 1–3 弦上的 3 品和 5 品推弦/滑音最出味道，顺手就能向右滑进 5–7 品的 D 指型！"
        };

        lib.TheoryNotes.Insert(0, n2);
        lib.TheoryNotes.Insert(0, n1);
        _selectedNote = n1;
        _onLibraryMutated?.Invoke();
        RefreshNotesList();
        ApplyNoteSnapshotToFretboard(n1);
    }

    private void CaptureFretboardIntoNote(TheoryNoteItem item)
    {
        item.TonicPitch = _fretboard.TonicPitch;
        item.ScaleId = _fretboard.Scale.Id;
        item.TuningId = _fretboard.Tuning.Id;
        item.Capo = _fretboard.Capo;
        item.ShowThreeNps = _fretboard.ShowThreeNpsBoxes;
        item.ActiveThreeNpsPosition = _fretboard.ActiveThreeNpsPosition;
        item.ShowCaged = _fretboard.ShowCagedBoxes;
        item.ActiveCagedFilter = _fretboard.ActiveCagedFilter;
        item.ShowCapsules = _fretboard.ShowCapsules;
        item.HighlightChord = _fretboard.HighlightedChord?.Name ?? "";
        item.UpdatedAt = DateTime.Now;
    }

    private void ApplyNoteSnapshotToFretboard(TheoryNoteItem item)
    {
        _fretboard.TonicPitch = Math.Clamp(item.TonicPitch, 0, 11);
        _fretboard.Scale = MusicTheoryCatalog.GetScale(item.ScaleId);
        _fretboard.Tuning = MusicTheoryCatalog.GetTuning(item.TuningId);
        _fretboard.Capo = Math.Clamp(item.Capo, 0, 11);
        _fretboard.ShowThreeNpsBoxes = item.ShowThreeNps;
        _fretboard.ActiveThreeNpsPosition = Math.Clamp(item.ActiveThreeNpsPosition, 0, 7);
        _fretboard.ShowCagedBoxes = !item.ShowThreeNps && item.ShowCaged;
        _fretboard.ActiveCagedFilter = string.IsNullOrWhiteSpace(item.ActiveCagedFilter) ? "ALL" : item.ActiveCagedFilter;
        _fretboard.ShowCapsules = !item.ShowThreeNps && item.ShowCapsules;
        _fretboard.JamTargetColorMode = false;
        _fretboard.HighlightedChord = string.IsNullOrWhiteSpace(item.HighlightChord) ? null : ChordCatalog.ByName(item.HighlightChord);
        SyncTopControlsFromFretboard();
        _fretboard.Refresh();
        _setStatus?.Invoke($"已将「{item.Title}」的指板状态完整还原到上方。");
    }

    private void RefreshNotesList()
    {
        if (_noteListPanel is null) return;
        _noteListPanel.Children.Clear();

        foreach (var child in _noteTagFilterWrap.Children)
        {
            if (child is Button b)
                StyleTogglePill(b, string.Equals(b.Content?.ToString(), _noteTagFilter, StringComparison.Ordinal));
        }

        var lib = _getLibrary?.Invoke();
        var notes = lib?.TheoryNotes ?? [];
        var filtered = _noteTagFilter == "全部"
            ? notes
            : notes.Where(n => string.Equals(n.Tag, _noteTagFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        if (filtered.Count == 0)
        {
            var emptyBox = new StackPanel { Margin = new Thickness(8, 14, 8, 8) };
            emptyBox.Children.Add(new TextBlock
            {
                Text = notes.Count == 0
                    ? "还没有收藏乐理卡片。\n调好上方指板后点「+ 拍下当前指板」，就能把你的 CAGED 口诀、音阶指型和即兴心得做成活笔记。"
                    : $"「{_noteTagFilter}」分类下暂时还没有卡片。",
                FontSize = 11.5,
                Foreground = AppDialogs.MutedBrush,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            });
            if (notes.Count == 0)
            {
                var seedBtn = CreatePillButton("✨ 载入 2 张 CAGED 精选范例", isPrimary: false);
                seedBtn.Margin = new Thickness(0, 10, 0, 0);
                seedBtn.HorizontalAlignment = HorizontalAlignment.Left;
                seedBtn.Click += (_, _) => SeedStarterTheoryNotes();
                emptyBox.Children.Add(seedBtn);
            }
            _noteListPanel.Children.Add(emptyBox);
            _selectedNote = null;
            PopulateNoteEditor(null);
            return;
        }

        if (_selectedNote is null || !filtered.Contains(_selectedNote))
            _selectedNote = filtered[0];

        foreach (var note in filtered)
        {
            var n = note;
            var isSel = ReferenceEquals(n, _selectedNote);
            var (tagBg, tagFg) = NoteTagColors.TryGetValue(n.Tag, out var tc) ? tc : ("#F1F5F9", "#475569");

            var card = new Border
            {
                Background = isSel ? Brushes.White : new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                BorderBrush = isSel ? AppDialogs.AccentBrush : CardBorder,
                BorderThickness = new Thickness(isSel ? 1.6 : 1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };
            var st = new StackPanel();
            var row1 = new DockPanel();
            var tagBadge = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tagBg)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 1.5, 6, 1.5),
                Margin = new Thickness(6, 0, 0, 0),
                Child = new TextBlock { Text = n.Tag, FontSize = 10, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tagFg)), FontWeight = FontWeights.SemiBold }
            };
            DockPanel.SetDock(tagBadge, Dock.Right);
            row1.Children.Add(tagBadge);
            row1.Children.Add(new TextBlock { Text = n.Title, FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = AppDialogs.TitleBrush, TextTrimming = TextTrimming.CharacterEllipsis });
            st.Children.Add(row1);
            st.Children.Add(new TextBlock
            {
                Text = FormatNoteSnapshotSummary(n),
                FontSize = 10.5,
                Foreground = AppDialogs.MutedBrush,
                Margin = new Thickness(0, 4, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            card.Child = st;
            card.MouseLeftButtonDown += (_, _) =>
            {
                FlushPendingNoteSave();
                _selectedNote = n;
                RefreshNotesList();
                ApplyNoteSnapshotToFretboard(n);
            };
            _noteListPanel.Children.Add(card);
        }

        PopulateNoteEditor(_selectedNote);
    }

    private void PopulateNoteEditor(TheoryNoteItem? note)
    {
        _populatingNoteEditor = true;
        try
        {
            var enabled = note is not null;
            _noteTitleBox.IsEnabled = enabled;
            _noteTagBox.IsEnabled = enabled;
            _noteContentBox.IsEnabled = enabled;
            _noteApplySnapshotBtn.IsEnabled = enabled;
            _noteUpdateSnapBtn.IsEnabled = enabled;
            _noteDeleteBtn.IsEnabled = enabled;

            if (note is null)
            {
                _noteTitleBox.Text = "";
                _noteContentBox.Text = "";
                _noteSnapshotBadge.Text = "点左侧「+ 拍下当前指板」或「载入精选范例」开始你的个人乐理手账";
                _noteSaveStateText.Text = "";
                return;
            }

            _noteTitleBox.Text = note.Title;
            _noteTagBox.SelectedItem = note.Tag;
            if (_noteTagBox.SelectedIndex < 0) _noteTagBox.SelectedIndex = 0;
            _noteContentBox.Text = note.Content;
            _noteSnapshotBadge.Text = $"绑定指板：{FormatNoteSnapshotSummary(note)}";
            _noteSaveStateText.Text = $"上次更新：{note.UpdatedAt:MM-dd HH:mm} · 自动保存";
        }
        finally
        {
            _populatingNoteEditor = false;
        }
    }

    private void ScheduleNoteAutoSave()
    {
        if (_populatingNoteEditor || _selectedNote is null) return;
        _noteSaveStateText.Text = "编辑中…";
        _noteAutoSaveTimer.Stop();
        _noteAutoSaveTimer.Start();
    }

    private void FlushPendingNoteSave()
    {
        if (_noteAutoSaveTimer.IsEnabled)
        {
            _noteAutoSaveTimer.Stop();
            SaveSelectedNote(silent: true);
        }
    }

    private void SaveSelectedNote(bool silent)
    {
        if (_selectedNote is null) return;
        _selectedNote.Title = string.IsNullOrWhiteSpace(_noteTitleBox.Text) ? "未命名乐理卡片" : _noteTitleBox.Text.Trim();
        _selectedNote.Tag = _noteTagBox.SelectedItem as string ?? "笔记";
        _selectedNote.Content = _noteContentBox.Text ?? "";
        _selectedNote.UpdatedAt = DateTime.Now;
        _onLibraryMutated?.Invoke();
        _noteSnapshotBadge.Text = $"绑定指板：{FormatNoteSnapshotSummary(_selectedNote)}";
        _noteSaveStateText.Text = $"已自动保存 · {_selectedNote.UpdatedAt:HH:mm:ss}";
        if (!silent)
        {
            RefreshNotesList();
            _setStatus?.Invoke($"已更新乐理卡片「{_selectedNote.Title}」的指板快照。");
        }
    }

    private void DeleteSelectedNote()
    {
        var lib = _getLibrary?.Invoke();
        if (lib is null || _selectedNote is null) return;
        _noteAutoSaveTimer.Stop();
        var title = _selectedNote.Title;
        lib.TheoryNotes.Remove(_selectedNote);
        _selectedNote = null;
        _onLibraryMutated?.Invoke();
        RefreshNotesList();
        _setStatus?.Invoke($"已删除乐理卡片「{title}」。");
    }

    private void AppendNoteSnippet(string snippet)
    {
        if (_selectedNote is null)
            CreateTheoryNoteFromCurrentFretboard();
        if (_selectedNote is null) return;

        var existing = _noteContentBox.Text ?? "";
        var sep = string.IsNullOrEmpty(existing) || existing.EndsWith('\n') ? "" : "\n";
        _noteContentBox.Text = existing + sep + snippet + "\n";
        _noteContentBox.CaretIndex = _noteContentBox.Text.Length;
        _noteContentBox.Focus();
    }

    private static string FormatNoteSnapshotSummary(TheoryNoteItem n)
    {
        var tonic = MusicTheoryCatalog.NoteName(n.TonicPitch, MusicTheoryCatalog.PreferFlatsForTonic(n.TonicPitch));
        var scale = MusicTheoryCatalog.GetScale(n.ScaleId).Name;
        var parts = new List<string> { $"{tonic} 调", scale };
        if (n.ShowThreeNps) parts.Add(n.ActiveThreeNpsPosition == 0 ? "3NPS 全指板" : $"3NPS 第{n.ActiveThreeNpsPosition}把位");
        else if (n.ShowCaged) parts.Add(n.ActiveCagedFilter == "ALL" ? "CAGED 全指板" : $"CAGED {n.ActiveCagedFilter}形");
        if (n.Capo > 0) parts.Add($"Capo {n.Capo}");
        if (!string.IsNullOrWhiteSpace(n.HighlightChord)) parts.Add($"和弦 {n.HighlightChord}");
        return string.Join(" · ", parts);
    }

    // ─── Modern Design System Factories ───────────────────────────────────────────────

    private static Border CreateElevatedCard(Thickness padding) => new()
    {
        Background = Brushes.White,
        BorderBrush = CardBorder,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(14),
        Padding = padding,
        Effect = new DropShadowEffect
        {
            BlurRadius = 18,
            ShadowDepth = 2,
            Opacity = 0.055,
            Color = Color.FromRgb(15, 23, 42)
        }
    };

    private static Border CreateSegmentedContainer() => new()
    {
        Background = SegTrackBg,
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(2),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new StackPanel { Orientation = Orientation.Horizontal }
    };

    private static Button CreateSegButton(string text, double minWidth = 0)
    {
        var btn = new Button
        {
            Content = text,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = AppDialogs.MutedBrush,
            Padding = new Thickness(8, 3, 8, 3),
            FontSize = 11.5,
            Cursor = Cursors.Hand
        };
        if (minWidth > 0) btn.MinWidth = minWidth;
        return btn;
    }

    private static void StyleSegButton(Button btn, bool active)
    {
        btn.Background = active ? Brushes.White : Brushes.Transparent;
        btn.Foreground = active ? AppDialogs.TitleBrush : AppDialogs.MutedBrush;
        btn.FontWeight = active ? FontWeights.Bold : FontWeights.Normal;
    }

    private static Button CreatePillButton(string text, bool isPrimary, string? toolTip = null)
    {
        var btn = new Button
        {
            Content = text,
            Padding = new Thickness(11, 4, 11, 4),
            FontSize = 11.5,
            Cursor = Cursors.Hand,
            ToolTip = toolTip
        };
        if (isPrimary && Application.Current?.TryFindResource("PrimaryButton") is Style prim)
            btn.Style = prim;
        return btn;
    }

    private static void StyleTogglePill(Button btn, bool active, string? customHex = null)
    {
        if (active)
        {
            var col = customHex is not null
                ? (Color)ColorConverter.ConvertFromString(customHex)
                : Color.FromRgb(47, 107, 72);
            btn.Background = new SolidColorBrush(Color.FromArgb(26, col.R, col.G, col.B));
            btn.BorderBrush = new SolidColorBrush(col);
            btn.Foreground = new SolidColorBrush(col);
            btn.FontWeight = FontWeights.SemiBold;
        }
        else
        {
            btn.ClearValue(BackgroundProperty);
            btn.ClearValue(BorderBrushProperty);
            btn.ClearValue(ForegroundProperty);
            btn.FontWeight = FontWeights.Normal;
        }
    }

    private static TextBlock MakeDockLabel(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = AppDialogs.MutedBrush,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 2, 5, 2)
    };

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _progBeatTimer.Stop();
        _noteAutoSaveTimer.Stop();
        _drillStage?.Dispose();
        _fretboard.StopAnimations();
        _audio.Dispose();
    }
}
