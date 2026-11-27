using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Xianpu.Desktop;

/// <summary>
/// Full Phase 1–3 Guitar Scale Drill &amp; Fretboard Mastery Stage ("⚡ 指板练功房"):
/// - 32 progressive drills (Spider, Diagonal Slide Highways, Sequences, 3-against-4 &amp; offbeat Accent Displacement,
///   3rd–6th Skips, Diatonic Triad/7th Alternating Arpeggios, Custom-Window 5/12-Key Circle of Fifths)
/// - Daily 15-Minute Auto-Workout Runner (multi-station countdown + 8s wrist rest buffer + daily streak &amp; minutes tracker)
/// - BPM Progression Sparkline &amp; Historical Check-in Badges
/// - Vector 12-Key Circle-of-Fifths Live Dial HUD + custom fret window (2–5 / 5–8 / 7–10 / 9–12) &amp; bars-per-key selector
/// - Multi-Drill A4 Print / PDF Workbook &amp; Single-Drill PNG / Song Insert
/// </summary>
public sealed class ScaleDrillStageView : UserControl, IDisposable
{
    private static readonly Brush SoftSurface = Freeze(new SolidColorBrush(Color.FromRgb(246, 248, 246)));
    private static readonly Brush CardBorder = Freeze(new SolidColorBrush(Color.FromRgb(220, 228, 223)));
    private static readonly Brush SegTrackBg = Freeze(new SolidColorBrush(Color.FromRgb(236, 240, 238)));
    private static readonly Brush WarmPaperBg = Freeze(new SolidColorBrush(Color.FromRgb(253, 251, 246)));
    private static readonly Brush WarmPaperBorder = Freeze(new SolidColorBrush(Color.FromRgb(232, 226, 211)));

    private sealed record WorkoutStation(string PresetId, int DurationSeconds);
    private sealed record WorkoutPlan(string Id, string Name, WorkoutStation[] Stations);

    private static readonly IReadOnlyList<WorkoutPlan> WorkoutPlans =
    [
        new("all_round_15", "均衡全能 15m",
        [
            new("u1_spider_1234", 180),
            new("u2_28_disp_beat", 240),
            new("u3_45_skip_4th_2", 240),
            new("u5_58_cof_pent", 240)
        ]),
        new("groove_seq_12", "模进与跨拍律动 12m",
        [
            new("u1_slide_mpent", 180),
            new("u2_29_mdisp_cross", 240),
            new("u2_33_pent_seq4", 180),
            new("u2_38_comp_332", 120)
        ]),
        new("harmony_cof_15", "顺阶琶音与五度圈 15m",
        [
            new("u1_3nps_major", 180),
            new("u4_52_triad_alt", 210),
            new("u4_56_7th_alt_up", 240),
            new("u5_59_cof_major", 270)
        ])
    ];

    private readonly FretboardCanvas _fretboard;
    private readonly GuitarAudioService _audio;
    private readonly Action _syncHostTopBar;
    private readonly Func<SongModel?> _getCurrentSong;
    private readonly Func<LibraryData?> _getLibrary;
    private readonly Action _onLibraryMutated;
    private readonly Action _onSongMutated;
    private readonly Action<string> _setStatus;

    private readonly DispatcherTimer _tickTimer = new(DispatcherPriority.Render);
    private readonly DispatcherTimer _workoutSecondTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DrillTabSurface _tabSurface = new();
    private readonly CircleOfFifthsDial _cofDial = new();

    private TextBlock _streakBadgeText = null!;
    private ComboBox _workoutPlanBox = null!;
    private WrapPanel _workoutStationChipsWrap = null!;
    private Button _startWorkoutBtn = null!;
    private Border _workoutActiveBar = null!;
    private TextBlock _workoutProgressText = null!;
    private ProgressBar _workoutProgressBar = null!;

    private StackPanel _presetListPanel = null!;
    private ScrollViewer _tabScroll = null!;
    private TextBlock _drillTitleText = null!;
    private TextBlock _coachingTipText = null!;
    private StackPanel _bpmHistoryPanel = null!;
    private TextBlock _liveKeyHudText = null!;
    private Border _cofOptionsBar = null!;

    private Button _playBtn = null!;
    private Button _countInBtn = null!;
    private Button _guitarSoundBtn = null!;
    private readonly Button[] _metroModeBtns = new Button[3];
    private TextBlock _bpmText = null!;
    private Button _speedTrainerBtn = null!;
    private Button _stampBpmBtn = null!;
    private Button _clearLoopBtn = null!;

    private readonly Dictionary<DrillPositionMode, Button> _posModeBtns = [];
    private readonly Dictionary<DrillSubdivision, Button> _subdivBtns = [];
    private readonly Dictionary<DrillAccentMode, Button> _accentBtns = [];
    private readonly Dictionary<int, Button> _cofWinBtns = [];
    private readonly Dictionary<int, Button> _cofKeyCountBtns = [];
    private readonly Dictionary<int, Button> _cofBarsBtns = [];

    private ScaleDrillPreset _activePreset = ScaleDrillCatalog.Presets.First(p => p.Id == "u2_28_disp_beat");
    private DrillPositionMode _posMode;
    private DrillSubdivision _subdivision;
    private DrillAccentMode _accentMode;
    private int _fixedWindowStartFret = 5; // 2, 5, 7, 9
    private int _cofKeyCount = 5;          // 5 or 12
    private int _cofBarsPerKey = 2;        // 1, 2, or 4
    private GeneratedScaleDrill? _currentDrill;

    private int _bpm = 76;
    private bool _useCountIn = true;
    private bool _playGuitarDemo = true;
    private int _metronomeMode = 1; // 0=Off, 1=1-2-3-4, 2=2&4 Backbeat
    private bool _speedTrainer;
    private int _loopStartBar = -1;
    private int _loopEndBar = -1;

    private bool _isPlaying;
    private int _countInRemaining;
    private int _playEventIdx;
    private int _savedTonicBeforeCof;
    private string _savedCagedBeforeCof = "ALL";

    // Daily Workout Runner state
    private bool _workoutActive;
    private WorkoutPlan _activePlan = WorkoutPlans[0];
    private int _workoutStationIdx;
    private int _workoutSecondsLeft;
    private int _workoutRestSecondsLeft;
    private int _workoutElapsedTotalSeconds;

    private bool _disposed;

    public ScaleDrillStageView(
        FretboardCanvas fretboard,
        GuitarAudioService audio,
        Action syncHostTopBar,
        Func<SongModel?> getCurrentSong,
        Func<LibraryData?> getLibrary,
        Action onLibraryMutated,
        Action onSongMutated,
        Action<string> setStatus)
    {
        _fretboard = fretboard;
        _audio = audio;
        _syncHostTopBar = syncHostTopBar;
        _getCurrentSong = getCurrentSong;
        _getLibrary = getLibrary;
        _onLibraryMutated = onLibraryMutated;
        _onSongMutated = onSongMutated;
        _setStatus = setStatus;

        _tickTimer.Tick += OnDrillTick;
        _workoutSecondTimer.Tick += OnWorkoutSecondTick;
        _tabSurface.BarClicked += OnTabBarClicked;

        Content = BuildLayout();
        RebuildWorkoutStationChips();
        ApplyPreset(_activePreset, applyRecommendedScale: false);
    }

    public void OnTabActivated()
    {
        UpdateStreakBadge();
        RebuildPresetList();
        RebuildWorkoutStationChips();
        RebuildDrill();
    }

    public void SelectPresetById(string presetId)
    {
        var found = ScaleDrillCatalog.Presets.FirstOrDefault(p => string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase));
        if (found is null) return;
        if (_workoutActive) StopWorkoutRunner(recordPartial: true);
        ApplyPreset(found, applyRecommendedScale: true);
    }

    public void TogglePlaybackFromShortcut() => TogglePlayback();

    public void SyncPositionModeFromFretboard()
    {
        if (IsCircleOfFifthsPattern(_activePreset.Pattern)) return;
        if (_fretboard.ShowThreeNpsBoxes && _posMode != DrillPositionMode.ThreeNotesPerString)
        {
            _posMode = DrillPositionMode.ThreeNotesPerString;
            UpdateRecipeSegStyles();
        }
        else if (!_fretboard.ShowThreeNpsBoxes && _posMode == DrillPositionMode.ThreeNotesPerString)
        {
            _posMode = DrillPositionMode.FollowCaged;
            UpdateRecipeSegStyles();
        }
    }

    public void OnFretboardParametersChanged()
    {
        if (_isPlaying && IsCircleOfFifthsPattern(_activePreset.Pattern))
            return;
        RebuildDrill();
    }

    public void StopPlayback()
    {
        StopWorkoutRunner(recordPartial: true);
        StopDrillEngineOnly();
    }

    private void StopDrillEngineOnly()
    {
        if (!_isPlaying) return;
        _isPlaying = false;
        _tickTimer.Stop();
        _playBtn.Content = "▶ 开始跟练";
        _fretboard.SetDrillLookahead(null, null);
        _liveKeyHudText.Visibility = Visibility.Collapsed;

        if (IsCircleOfFifthsPattern(_activePreset.Pattern))
        {
            _fretboard.TonicPitch = _savedTonicBeforeCof;
            _fretboard.ActiveCagedFilter = _savedCagedBeforeCof;
            _syncHostTopBar();
            _fretboard.Refresh();
            _cofDial.Reset(_fretboard.TonicPitch);
        }

        _tabSurface.SetPlayhead(new TabPlayheadState(-1, -1, _loopStartBar, _loopEndBar));
    }

    private UIElement BuildLayout()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(278) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // ─── Left: Daily Streak Card + 32-Drill Progressive Curriculum ────────
        var leftGrid = new Grid();
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var workoutCard = new Border
        {
            Background = WarmPaperBg,
            BorderBrush = WarmPaperBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var wkStack = new StackPanel();
        _streakBadgeText = new TextBlock
        {
            Text = "🔥 每日练琴日课 · 0 天连签",
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            Foreground = AppDialogs.AccentHoverBrush,
            Margin = new Thickness(0, 0, 0, 6)
        };
        wkStack.Children.Add(_streakBadgeText);

        var wkRow = new DockPanel();
        _startWorkoutBtn = CreatePillButton("▶ 开练", isPrimary: true, toolTip: "一键开启多科目自动倒计时热身日课（带组间 8 秒放松换手倒数）");
        _startWorkoutBtn.Margin = new Thickness(6, 0, 0, 0);
        _startWorkoutBtn.Click += (_, _) => ToggleDailyWorkout();
        DockPanel.SetDock(_startWorkoutBtn, Dock.Right);
        wkRow.Children.Add(_startWorkoutBtn);

        _workoutPlanBox = new ComboBox { Height = 26, FontSize = 11.5 };
        foreach (var plan in WorkoutPlans)
            _workoutPlanBox.Items.Add(new ComboBoxItem { Content = plan.Name, Tag = plan.Id });
        _workoutPlanBox.SelectedIndex = 0;
        _workoutPlanBox.SelectionChanged += (_, _) => OnWorkoutPlanSelectionChanged();
        wkRow.Children.Add(_workoutPlanBox);
        wkStack.Children.Add(wkRow);

        _workoutStationChipsWrap = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        wkStack.Children.Add(_workoutStationChipsWrap);

        workoutCard.Child = wkStack;
        Grid.SetRow(workoutCard, 0);
        leftGrid.Children.Add(workoutCard);

        _presetListPanel = new StackPanel();
        var presetScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _presetListPanel
        };
        var leftListCard = new Border
        {
            Background = SoftSurface,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8),
            Child = presetScroll
        };
        Grid.SetRow(leftListCard, 1);
        leftGrid.Children.Add(leftListCard);

        Grid.SetColumn(leftGrid, 0);
        grid.Children.Add(leftGrid);

        // ─── Right: Workout Bar + Coaching/CoF Banner + Cockpit + Live TAB ────
        var rightGrid = new Grid();
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 0: Active Workout Bar
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 1: Coaching + CoF Dial + Sparkline
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 2: Transport & Recipe Cockpit
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Row 3: Live TAB

        // Row 0: Collapsible Active Daily Workout Runner Strip
        _workoutActiveBar = new Border
        {
            Visibility = Visibility.Collapsed,
            Background = AppDialogs.AccentLightBrush,
            BorderBrush = AppDialogs.AccentBrush,
            BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var wkActiveDock = new DockPanel();
        var wkBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        var skipStBtn = CreatePillButton("下一站 →", isPrimary: false);
        skipStBtn.Padding = new Thickness(8, 2, 8, 2);
        skipStBtn.Margin = new Thickness(0, 0, 6, 0);
        skipStBtn.Click += (_, _) => AdvanceWorkoutStation();
        var stopWkBtn = CreatePillButton("结束日课", isPrimary: false);
        stopWkBtn.Padding = new Thickness(8, 2, 8, 2);
        stopWkBtn.Click += (_, _) => StopWorkoutRunner(recordPartial: true);
        wkBtns.Children.Add(skipStBtn);
        wkBtns.Children.Add(stopWkBtn);
        DockPanel.SetDock(wkBtns, Dock.Right);
        wkActiveDock.Children.Add(wkBtns);

        var wkProgStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _workoutProgressText = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = AppDialogs.AccentHoverBrush, Margin = new Thickness(0, 0, 0, 4) };
        _workoutProgressBar = new ProgressBar { Height = 5, Minimum = 0, Maximum = 100, Value = 0, Foreground = AppDialogs.AccentBrush, Background = Brushes.White, BorderThickness = new Thickness(0) };
        wkProgStack.Children.Add(_workoutProgressText);
        wkProgStack.Children.Add(_workoutProgressBar);
        wkActiveDock.Children.Add(wkProgStack);
        _workoutActiveBar.Child = wkActiveDock;
        Grid.SetRow(_workoutActiveBar, 0);
        rightGrid.Children.Add(_workoutActiveBar);

        // Row 1: Coaching Tip + BPM Sparkline + CoF 12-Key Dial + Export/Workbook Buttons
        var bannerCard = new Border
        {
            Background = WarmPaperBg,
            BorderBrush = WarmPaperBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var bannerStackOuter = new StackPanel();
        var bannerDock = new DockPanel();

        var rightOutBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };

        var workbookBtn = CreatePillButton("📚 练习册打印/PDF", isPrimary: false, toolTip: "多选勾选多条练习，一键排版输出防断行 A4 练习册 PDF 或合集长图");
        workbookBtn.Margin = new Thickness(0, 0, 6, 0);
        workbookBtn.Click += (_, _) => OpenWorkbookDialog();

        var exportPngBtn = CreatePillButton("🖼 单图PNG", isPrimary: false, toolTip: "导出当前单条练习的高清 PNG");
        exportPngBtn.Margin = new Thickness(0, 0, 6, 0);
        exportPngBtn.Click += (_, _) => ExportDrillPng();

        var insertSongBtn = CreatePillButton("📥 插入曲谱", isPrimary: false, toolTip: "将本条练习作为六线谱段落插入当前曲谱末尾");
        insertSongBtn.Click += (_, _) => InsertDrillIntoCurrentSong();

        rightOutBtns.Children.Add(workbookBtn);
        rightOutBtns.Children.Add(exportPngBtn);
        rightOutBtns.Children.Add(insertSongBtn);
        DockPanel.SetDock(rightOutBtns, Dock.Right);
        bannerDock.Children.Add(rightOutBtns);

        _cofDial.Visibility = Visibility.Collapsed;
        _cofDial.Margin = new Thickness(0, 0, 12, 0);
        DockPanel.SetDock(_cofDial, Dock.Left);
        bannerDock.Children.Add(_cofDial);

        var bannerInfoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _drillTitleText = new TextBlock { FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = AppDialogs.TitleBrush, TextTrimming = TextTrimming.CharacterEllipsis };
        _coachingTipText = new TextBlock { FontSize = 11.5, Foreground = AppDialogs.BodyBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
        _bpmHistoryPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
        _liveKeyHudText = new TextBlock
        {
            Visibility = Visibility.Collapsed,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(194, 65, 12)),
            Margin = new Thickness(0, 4, 0, 0)
        };
        bannerInfoStack.Children.Add(_drillTitleText);
        bannerInfoStack.Children.Add(_coachingTipText);
        bannerInfoStack.Children.Add(_bpmHistoryPanel);
        bannerInfoStack.Children.Add(_liveKeyHudText);
        bannerDock.Children.Add(bannerInfoStack);
        bannerStackOuter.Children.Add(bannerDock);

        // Circle-of-Fifths dedicated customizer row (Window 2-5 / 5-8 / 7-10 / 9-12 + 5/12 keys + 1/2/4 bars)
        _cofOptionsBar = new Border
        {
            Visibility = Visibility.Collapsed,
            BorderBrush = WarmPaperBorder,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(0, 7, 0, 0),
            Padding = new Thickness(0, 6, 0, 0)
        };
        var cofWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        cofWrap.Children.Add(MakeMiniLabel("锁死品位窗"));
        var winSeg = CreateSegTrack();
        winSeg.Margin = new Thickness(0, 1, 10, 1);
        foreach (var (wf, wlbl) in new[] { (2, "2–5品"), (5, "5–8品"), (7, "7–10品"), (9, "9–12品") })
        {
            var startF = wf;
            var wb = CreateSegBtn(wlbl);
            wb.Click += (_, _) => { _fixedWindowStartFret = startF; UpdateCofOptionStyles(); RebuildDrill(); };
            _cofWinBtns[startF] = wb;
            ((StackPanel)winSeg.Child).Children.Add(wb);
        }
        cofWrap.Children.Add(winSeg);

        cofWrap.Children.Add(MakeMiniLabel("五度圈数"));
        var kcSeg = CreateSegTrack();
        kcSeg.Margin = new Thickness(0, 1, 10, 1);
        foreach (var (kc, klbl) in new[] { (5, "精华 5调 (五形全满)"), (12, "大满贯 12调闭环") })
        {
            var count = kc;
            var kb = CreateSegBtn(klbl);
            kb.Click += (_, _) => { _cofKeyCount = count; UpdateCofOptionStyles(); RebuildDrill(); };
            _cofKeyCountBtns[count] = kb;
            ((StackPanel)kcSeg.Child).Children.Add(kb);
        }
        cofWrap.Children.Add(kcSeg);

        cofWrap.Children.Add(MakeMiniLabel("每调停留"));
        var bkSeg = CreateSegTrack();
        foreach (var (bk, blbl) in new[] { (1, "1小节"), (2, "2小节"), (4, "4小节") })
        {
            var bars = bk;
            var bb = CreateSegBtn(blbl);
            bb.Click += (_, _) => { _cofBarsPerKey = bars; UpdateCofOptionStyles(); RebuildDrill(); };
            _cofBarsBtns[bars] = bb;
            ((StackPanel)bkSeg.Child).Children.Add(bb);
        }
        cofWrap.Children.Add(bkSeg);
        _cofOptionsBar.Child = cofWrap;
        bannerStackOuter.Children.Add(_cofOptionsBar);

        bannerCard.Child = bannerStackOuter;
        Grid.SetRow(bannerCard, 1);
        rightGrid.Children.Add(bannerCard);

        // Row 2: Transport + Recipe Cockpit
        var ctrlCard = new Border
        {
            Background = SoftSurface,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var ctrlStack = new StackPanel();

        var rowA = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 5) };

        _playBtn = CreatePillButton("▶ 开始跟练", isPrimary: true, toolTip: "启动指板预判箭头与下方六线谱动态游标同步跟练");
        _playBtn.Margin = new Thickness(0, 2, 8, 2);
        _playBtn.Click += (_, _) => TogglePlayback();
        rowA.Children.Add(_playBtn);

        _countInBtn = CreatePillButton("预备 4拍", isPrimary: false, toolTip: "开启后先打 4 拍木鱼倒数再进入第 1 音");
        _countInBtn.Margin = new Thickness(0, 2, 6, 2);
        _countInBtn.Click += (_, _) => { _useCountIn = !_useCountIn; StyleTogglePill(_countInBtn, _useCountIn); };
        StyleTogglePill(_countInBtn, _useCountIn);
        rowA.Children.Add(_countInBtn);

        _guitarSoundBtn = CreatePillButton("🎸 示范音", isPrimary: false, toolTip: "可随时关闭示范音，仅留节拍器与指板光点跟弹");
        _guitarSoundBtn.Margin = new Thickness(0, 2, 8, 2);
        _guitarSoundBtn.Click += (_, _) => { _playGuitarDemo = !_playGuitarDemo; StyleTogglePill(_guitarSoundBtn, _playGuitarDemo); };
        StyleTogglePill(_guitarSoundBtn, _playGuitarDemo);
        rowA.Children.Add(_guitarSoundBtn);

        var metroSeg = CreateSegTrack();
        metroSeg.Margin = new Thickness(0, 2, 8, 2);
        string[] metroLabels = ["节拍:关", "♩ 正拍", "♩ 2&4反拍"];
        for (var m = 0; m < 3; m++)
        {
            var mode = m;
            var mb = CreateSegBtn(metroLabels[m]);
            mb.Click += (_, _) => { _metronomeMode = mode; UpdateMetroSegStyles(); };
            _metroModeBtns[m] = mb;
            ((StackPanel)metroSeg.Child).Children.Add(mb);
        }
        UpdateMetroSegStyles();
        rowA.Children.Add(metroSeg);

        var bpmSeg = CreateSegTrack();
        bpmSeg.Margin = new Thickness(0, 2, 8, 2);
        var minusBtn = CreateSegBtn("−", 22);
        minusBtn.Click += (_, _) => AdjustBpm(-4);
        _bpmText = new TextBlock { Text = "76 BPM", FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = AppDialogs.TitleBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
        var plusBtn = CreateSegBtn("+", 22);
        plusBtn.Click += (_, _) => AdjustBpm(4);
        ((StackPanel)bpmSeg.Child).Children.Add(minusBtn);
        ((StackPanel)bpmSeg.Child).Children.Add(_bpmText);
        ((StackPanel)bpmSeg.Child).Children.Add(plusBtn);
        rowA.Children.Add(bpmSeg);

        _speedTrainerBtn = CreatePillButton("🚀 阶梯提速", isPrimary: false, toolTip: "每完整弹完一轮自动 +4 BPM");
        _speedTrainerBtn.Margin = new Thickness(0, 2, 6, 2);
        _speedTrainerBtn.Click += (_, _) => { _speedTrainer = !_speedTrainer; StyleTogglePill(_speedTrainerBtn, _speedTrainer, "#D97706"); };
        rowA.Children.Add(_speedTrainerBtn);

        _stampBpmBtn = CreatePillButton("★ 打卡当前速度", isPrimary: false, toolTip: "记录当前 BPM 并生成速度成长折线轨迹");
        _stampBpmBtn.Margin = new Thickness(0, 2, 0, 2);
        _stampBpmBtn.Click += (_, _) => StampMasteredBpm();
        rowA.Children.Add(_stampBpmBtn);

        ctrlStack.Children.Add(rowA);

        var rowB = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        rowB.Children.Add(MakeMiniLabel("把位"));
        var posSeg = CreateSegTrack();
        posSeg.Margin = new Thickness(0, 2, 10, 2);
        foreach (var (pm, lbl) in new[] { (DrillPositionMode.FollowCaged, "跟随CAGED"), (DrillPositionMode.FixedWindow5To8, "固定品窗"), (DrillPositionMode.ThreeNotesPerString, "每弦三音(3NPS)") })
        {
            var m = pm;
            var b = CreateSegBtn(lbl);
            b.Click += (_, _) =>
            {
                _posMode = m;
                if (m == DrillPositionMode.ThreeNotesPerString)
                {
                    _fretboard.ShowThreeNpsBoxes = true;
                    if (_fretboard.ActiveThreeNpsPosition == 0) _fretboard.ActiveThreeNpsPosition = 1;
                }
                else if (_fretboard.ShowThreeNpsBoxes)
                {
                    _fretboard.ShowThreeNpsBoxes = false;
                    _fretboard.ShowCagedBoxes = true;
                }
                _syncHostTopBar();
                _fretboard.Refresh();
                UpdateRecipeSegStyles();
                RebuildDrill();
            };
            _posModeBtns[m] = b;
            ((StackPanel)posSeg.Child).Children.Add(b);
        }
        rowB.Children.Add(posSeg);

        rowB.Children.Add(MakeMiniLabel("细分"));
        var subSeg = CreateSegTrack();
        subSeg.Margin = new Thickness(0, 2, 10, 2);
        foreach (var (sd, lbl) in new[] { (DrillSubdivision.Eighth, "八分"), (DrillSubdivision.Triplet, "三连音"), (DrillSubdivision.Sixteenth, "十六分") })
        {
            var s = sd;
            var b = CreateSegBtn(lbl);
            b.Click += (_, _) => { _subdivision = s; UpdateTimerIntervalIfPlaying(); UpdateRecipeSegStyles(); RebuildDrill(); };
            _subdivBtns[s] = b;
            ((StackPanel)subSeg.Child).Children.Add(b);
        }
        rowB.Children.Add(subSeg);

        rowB.Children.Add(MakeMiniLabel("重音移位"));
        var accSeg = CreateSegTrack();
        accSeg.Margin = new Thickness(0, 2, 8, 2);
        foreach (var (am, lbl, tip) in new[]
        {
            (DrillAccentMode.None, "无", "均匀音量"),
            (DrillAccentMode.BeatDownbeat, "拍头错位", "每拍拍头重音（三音模进遇十六分自动组内移位）"),
            (DrillAccentMode.GroupOf3CrossBeat, "3对4跨拍", "每隔 3 音重弹一次"),
            (DrillAccentMode.OffbeatSecond16th, "第2音后移", "每拍第 2 个十六分重音"),
            (DrillAccentMode.OffbeatUpbeat, "反拍(&)", "正反拍重音"),
            (DrillAccentMode.Compound332, "3+3+2", "Tresillo 复合重音")
        })
        {
            var a = am;
            var b = CreateSegBtn(lbl);
            b.ToolTip = tip;
            b.Click += (_, _) => { _accentMode = a; UpdateRecipeSegStyles(); RebuildDrill(); };
            _accentBtns[a] = b;
            ((StackPanel)accSeg.Child).Children.Add(b);
        }
        rowB.Children.Add(accSeg);

        _clearLoopBtn = CreatePillButton("✕ 取消选段循环", isPrimary: false);
        _clearLoopBtn.Visibility = Visibility.Collapsed;
        _clearLoopBtn.Padding = new Thickness(8, 2, 8, 2);
        _clearLoopBtn.FontSize = 11;
        _clearLoopBtn.Click += (_, _) =>
        {
            _loopStartBar = -1;
            _loopEndBar = -1;
            _clearLoopBtn.Visibility = Visibility.Collapsed;
            _tabSurface.SetPlayhead(new TabPlayheadState(-1, -1, -1, -1));
        };
        rowB.Children.Add(_clearLoopBtn);

        ctrlStack.Children.Add(rowB);
        ctrlCard.Child = ctrlStack;
        Grid.SetRow(ctrlCard, 2);
        rightGrid.Children.Add(ctrlCard);

        // Row 3: Live Interactive TAB Sheet
        _tabScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _tabSurface
        };
        var tabCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(12, 8, 12, 8),
            Child = _tabScroll
        };
        Grid.SetRow(tabCard, 3);
        rightGrid.Children.Add(tabCard);

        Grid.SetColumn(rightGrid, 2);
        grid.Children.Add(rightGrid);

        UpdateCofOptionStyles();
        return grid;
    }

    // ─── Daily 15-Minute Auto-Workout Runner & Streak Engine ──────────────────────

    private void OnWorkoutPlanSelectionChanged()
    {
        if (_workoutPlanBox?.SelectedItem is not ComboBoxItem { Tag: string pid })
            return;

        var chosen = WorkoutPlans.FirstOrDefault(p => p.Id == pid);
        if (chosen is null || string.Equals(chosen.Id, _activePlan.Id, StringComparison.Ordinal))
            return;

        _activePlan = chosen;
        RebuildWorkoutStationChips();

        if (_workoutActive)
        {
            // Live switch to Station 1 of the newly selected workout plan
            _workoutStationIdx = 0;
            _workoutRestSecondsLeft = 0;
            LaunchCurrentWorkoutStation();
            _setStatus($"已实时切换日课方案为「{_activePlan.Name}」，从第 1 站开始连播！");
        }
        else if (_activePlan.Stations.Length > 0)
        {
            var firstPreset = ScaleDrillCatalog.Presets.FirstOrDefault(p => p.Id == _activePlan.Stations[0].PresetId);
            if (firstPreset is not null)
                ApplyPreset(firstPreset, applyRecommendedScale: true);

            var codes = string.Join(" → ", _activePlan.Stations
                .Select(s => ScaleDrillCatalog.Presets.FirstOrDefault(p => p.Id == s.PresetId)?.CodeBadge ?? s.PresetId));
            _setStatus($"已选择日课「{_activePlan.Name}」（共 {_activePlan.Stations.Length} 站：{codes}），已预览第 1 站，点击「▶ 开练」开始自动连播计时。");
        }
    }

    private void RebuildWorkoutStationChips()
    {
        if (_workoutStationChipsWrap is null) return;
        _workoutStationChipsWrap.Children.Clear();

        for (var i = 0; i < _activePlan.Stations.Length; i++)
        {
            var stIdx = i;
            var st = _activePlan.Stations[i];
            var preset = ScaleDrillCatalog.Presets.FirstOrDefault(p => p.Id == st.PresetId);
            if (preset is null) continue;

            var isCurrent = (_workoutActive && _workoutStationIdx == stIdx) ||
                            (!_workoutActive && string.Equals(_activePreset.Id, preset.Id, StringComparison.Ordinal));
            var mins = Math.Max(1, st.DurationSeconds / 60);

            var chip = new Border
            {
                Background = isCurrent ? AppDialogs.AccentLightBrush : Brushes.White,
                BorderBrush = isCurrent ? AppDialogs.AccentBrush : CardBorder,
                BorderThickness = new Thickness(isCurrent ? 1.3 : 1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 4, 3),
                Cursor = Cursors.Hand,
                ToolTip = $"第 {stIdx + 1} 站：[{preset.CodeBadge}] {preset.Title}（{mins} 分钟）\n点击可预览或直接跳到本站"
            };
            chip.Child = new TextBlock
            {
                Text = $"{stIdx + 1}.{preset.CodeBadge}课({mins}m)",
                FontSize = 10,
                FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isCurrent ? AppDialogs.AccentHoverBrush : AppDialogs.MutedBrush
            };
            chip.MouseLeftButtonDown += (_, _) =>
            {
                if (_workoutActive)
                {
                    _workoutStationIdx = stIdx;
                    _workoutRestSecondsLeft = 0;
                    LaunchCurrentWorkoutStation();
                }
                else
                {
                    ApplyPreset(preset, applyRecommendedScale: true);
                }
            };
            _workoutStationChipsWrap.Children.Add(chip);
        }
    }

    private void ToggleDailyWorkout()
    {
        if (_workoutActive)
        {
            StopWorkoutRunner(recordPartial: true);
            return;
        }

        if (_workoutPlanBox.SelectedItem is ComboBoxItem { Tag: string pid })
            _activePlan = WorkoutPlans.FirstOrDefault(p => p.Id == pid) ?? WorkoutPlans[0];

        _workoutActive = true;
        _workoutStationIdx = 0;
        _workoutRestSecondsLeft = 0;
        _workoutElapsedTotalSeconds = 0;
        _startWorkoutBtn.Content = "■ 结束";
        _workoutActiveBar.Visibility = Visibility.Visible;

        LaunchCurrentWorkoutStation();
        _workoutSecondTimer.Start();
    }

    private void LaunchCurrentWorkoutStation()
    {
        if (!_workoutActive || _workoutStationIdx >= _activePlan.Stations.Length)
        {
            CompleteWorkoutCelebration();
            return;
        }

        var st = _activePlan.Stations[_workoutStationIdx];
        var preset = ScaleDrillCatalog.Presets.FirstOrDefault(p => p.Id == st.PresetId) ?? ScaleDrillCatalog.Presets[0];
        _workoutSecondsLeft = st.DurationSeconds;
        _workoutRestSecondsLeft = 0;

        StopDrillEngineOnly();
        ApplyPreset(preset, applyRecommendedScale: true);
        UpdateWorkoutProgressUi();
        StartDrillEngine();
    }

    private void AdvanceWorkoutStation()
    {
        if (!_workoutActive) return;
        _workoutStationIdx++;
        if (_workoutStationIdx >= _activePlan.Stations.Length)
        {
            CompleteWorkoutCelebration();
            return;
        }

        // 8-second wrist-relax transition buffer before next station
        StopDrillEngineOnly();
        _workoutRestSecondsLeft = 8;
        UpdateWorkoutProgressUi();
    }

    private void OnWorkoutSecondTick(object? sender, EventArgs e)
    {
        if (!_workoutActive)
        {
            _workoutSecondTimer.Stop();
            return;
        }

        if (_workoutRestSecondsLeft > 0)
        {
            _workoutRestSecondsLeft--;
            _audio.PlayDrillStep(null, false, playClick: _workoutRestSecondsLeft <= 3, isDownbeatClick: _workoutRestSecondsLeft == 0, durationMs: 140);
            if (_workoutRestSecondsLeft <= 0)
            {
                LaunchCurrentWorkoutStation();
            }
            else
            {
                UpdateWorkoutProgressUi();
            }
            return;
        }

        _workoutElapsedTotalSeconds++;
        _workoutSecondsLeft--;
        UpdateWorkoutProgressUi();

        if (_workoutSecondsLeft <= 0)
            AdvanceWorkoutStation();
    }

    private void UpdateWorkoutProgressUi()
    {
        if (!_workoutActive) return;
        if (_workoutRestSecondsLeft > 0 && _workoutStationIdx < _activePlan.Stations.Length)
        {
            var nextPreset = ScaleDrillCatalog.Presets.FirstOrDefault(p => p.Id == _activePlan.Stations[_workoutStationIdx].PresetId);
            _workoutProgressText.Text = $"☕ 组间放松手腕 {_workoutRestSecondsLeft} 秒…  即将进入第 {_workoutStationIdx + 1}/{_activePlan.Stations.Length} 站：{nextPreset?.Title}";
            _workoutProgressBar.Value = (8 - _workoutRestSecondsLeft) * 100.0 / 8.0;
            return;
        }

        var st = _activePlan.Stations[Math.Clamp(_workoutStationIdx, 0, _activePlan.Stations.Length - 1)];
        var mins = _workoutSecondsLeft / 60;
        var secs = _workoutSecondsLeft % 60;
        _workoutProgressText.Text = $"🔥 {_activePlan.Name} · 第 {_workoutStationIdx + 1}/{_activePlan.Stations.Length} 站 [{_activePreset.CodeBadge} {_activePreset.Title}]   本项剩余 {mins:00}:{secs:00}";
        _workoutProgressBar.Value = Math.Clamp((st.DurationSeconds - _workoutSecondsLeft) * 100.0 / Math.Max(1, st.DurationSeconds), 0, 100);
    }

    private void CompleteWorkoutCelebration()
    {
        RecordWorkoutMinutes(Math.Max(1, (int)Math.Ceiling(_workoutElapsedTotalSeconds / 60.0)), _activePlan.Stations.Length);
        StopWorkoutRunner(recordPartial: false);
        _setStatus($"🎉 太棒了！已完成今日「{_activePlan.Name}」全部 {_activePlan.Stations.Length} 站训练，打卡连签已更新！");
    }

    private void StopWorkoutRunner(bool recordPartial)
    {
        if (!_workoutActive) return;
        if (recordPartial && _workoutElapsedTotalSeconds >= 45)
            RecordWorkoutMinutes(Math.Max(1, _workoutElapsedTotalSeconds / 60), Math.Max(1, _workoutStationIdx));

        _workoutActive = false;
        _workoutSecondTimer.Stop();
        if (_startWorkoutBtn is not null) _startWorkoutBtn.Content = "▶ 开练";
        if (_workoutActiveBar is not null) _workoutActiveBar.Visibility = Visibility.Collapsed;
        StopDrillEngineOnly();
    }

    private void RecordWorkoutMinutes(int minutes, int completedDrills)
    {
        var lib = _getLibrary?.Invoke();
        if (lib is null) return;
        var today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var entry = lib.WorkoutLogs.FirstOrDefault(x => x.DateKey == today);
        if (entry is null)
        {
            entry = new WorkoutLogEntry { DateKey = today };
            lib.WorkoutLogs.Add(entry);
        }
        entry.Minutes += Math.Max(1, minutes);
        entry.CompletedDrills += Math.Max(1, completedDrills);
        _onLibraryMutated();
        UpdateStreakBadge();
    }

    private void UpdateStreakBadge()
    {
        if (_streakBadgeText is null) return;
        var logs = _getLibrary?.Invoke()?.WorkoutLogs ?? [];
        var totalMins = logs.Sum(x => x.Minutes);
        var dateSet = new HashSet<string>(logs.Where(x => x.Minutes > 0).Select(x => x.DateKey), StringComparer.Ordinal);

        var streak = 0;
        var cursor = DateTime.Today;
        if (!dateSet.Contains(cursor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
            cursor = cursor.AddDays(-1);
        while (dateSet.Contains(cursor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        _streakBadgeText.Text = totalMins > 0
            ? $"🔥 连续打卡 {streak} 天 · 累计练功 {totalMins} 分钟"
            : "🔥 每日 15m 热身日课 · 一键连播打卡";
    }

    // ─── Curriculum, Sparkline & Generation ───────────────────────────────────────

    private void RebuildPresetList()
    {
        if (_presetListPanel is null) return;
        _presetListPanel.Children.Clear();

        var bestMap = _getLibrary?.Invoke()?.DrillBestBpms;
        string? lastUnit = null;

        foreach (var preset in ScaleDrillCatalog.Presets)
        {
            var p = preset;
            if (p.UnitTitle != lastUnit)
            {
                lastUnit = p.UnitTitle;
                _presetListPanel.Children.Add(new TextBlock
                {
                    Text = p.UnitTitle,
                    FontSize = 10.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = AppDialogs.AccentHoverBrush,
                    Margin = new Thickness(6, _presetListPanel.Children.Count == 0 ? 2 : 10, 4, 4)
                });
            }

            var isSel = string.Equals(p.Id, _activePreset.Id, StringComparison.Ordinal);
            var rowBorder = new Border
            {
                Background = isSel ? Brushes.White : Brushes.Transparent,
                BorderBrush = isSel ? AppDialogs.AccentBrush : Brushes.Transparent,
                BorderThickness = new Thickness(isSel ? 1.4 : 1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(7, 5, 7, 5),
                Margin = new Thickness(0, 1, 0, 2),
                Cursor = Cursors.Hand,
                ToolTip = p.CoachingTip
            };

            var dock = new DockPanel();
            if (bestMap is not null && bestMap.TryGetValue(p.Id, out var bestBpm) && bestBpm > 0)
            {
                var badge = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(5, 1, 5, 1),
                    Margin = new Thickness(4, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = $"★{bestBpm}", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(180, 83, 9)) }
                };
                DockPanel.SetDock(badge, Dock.Right);
                dock.Children.Add(badge);
            }

            var codePill = new Border
            {
                Background = isSel ? AppDialogs.AccentBrush : new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                CornerRadius = new CornerRadius(5),
                Width = 24,
                Height = 17,
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = p.CodeBadge,
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = isSel ? Brushes.White : AppDialogs.BodyBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            DockPanel.SetDock(codePill, Dock.Left);
            dock.Children.Add(codePill);

            dock.Children.Add(new TextBlock
            {
                Text = p.Title,
                FontSize = 11.5,
                FontWeight = isSel ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isSel ? AppDialogs.TitleBrush : AppDialogs.BodyBrush,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            rowBorder.Child = dock;
            rowBorder.MouseLeftButtonDown += (_, _) =>
            {
                if (_workoutActive) StopWorkoutRunner(recordPartial: true);
                ApplyPreset(p, applyRecommendedScale: true);
            };
            _presetListPanel.Children.Add(rowBorder);
        }
    }

    private void ApplyPreset(ScaleDrillPreset preset, bool applyRecommendedScale)
    {
        StopDrillEngineOnly();
        _activePreset = preset;
        _posMode = preset.DefaultPosMode;
        _subdivision = preset.Subdivision;
        _accentMode = preset.Accent;
        _bpm = preset.SuggestedBpm;
        _bpmText.Text = $"{_bpm} BPM";
        _loopStartBar = -1;
        _loopEndBar = -1;
        if (_clearLoopBtn is not null)
            _clearLoopBtn.Visibility = Visibility.Collapsed;

        var isCof = IsCircleOfFifthsPattern(preset.Pattern);
        if (_cofOptionsBar is not null)
            _cofOptionsBar.Visibility = isCof ? Visibility.Visible : Visibility.Collapsed;
        if (_cofDial is not null)
        {
            _cofDial.Visibility = isCof ? Visibility.Visible : Visibility.Collapsed;
            if (isCof) _cofDial.Reset(_fretboard.TonicPitch);
        }

        if (preset.Pattern is DrillPatternKind.Spider1234 or DrillPatternKind.Spider1324)
        {
            if (_fretboard.ShowThreeNpsBoxes)
            {
                _fretboard.ShowThreeNpsBoxes = false;
                _syncHostTopBar();
                _fretboard.Refresh();
            }
        }
        else if (applyRecommendedScale)
        {
            _fretboard.Scale = MusicTheoryCatalog.GetScale(preset.RecommendedScaleId);
            _fretboard.HighlightedChord = null;
            _fretboard.JamTargetColorMode = false;
            if (preset.DefaultPosMode == DrillPositionMode.ThreeNotesPerString)
            {
                _fretboard.ShowThreeNpsBoxes = true;
                if (_fretboard.ActiveThreeNpsPosition == 0) _fretboard.ActiveThreeNpsPosition = 1;
            }
            else if (_fretboard.ShowThreeNpsBoxes)
            {
                _fretboard.ShowThreeNpsBoxes = false;
                _fretboard.ShowCagedBoxes = true;
            }
            _syncHostTopBar();
            _fretboard.Refresh();
        }

        UpdateRecipeSegStyles();
        RebuildPresetList();
        RebuildWorkoutStationChips();
        RebuildBpmSparkline();
        RebuildDrill();
    }

    private void RebuildBpmSparkline()
    {
        if (_bpmHistoryPanel is null) return;
        _bpmHistoryPanel.Children.Clear();

        var lib = _getLibrary?.Invoke();
        if (lib is null || !lib.DrillBpmHistory.TryGetValue(_activePreset.Id, out var hist) || hist.Count == 0)
        {
            _bpmHistoryPanel.Visibility = Visibility.Collapsed;
            return;
        }

        _bpmHistoryPanel.Visibility = Visibility.Visible;
        var recent = hist.TakeLast(7).ToList();

        // Draw crisp mini sparkline canvas when >= 2 records exist
        if (recent.Count >= 2)
        {
            var canvas = new Canvas { Width = 68, Height = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var minB = recent.Min(x => x.Bpm) - 4;
            var maxB = Math.Max(minB + 8, recent.Max(x => x.Bpm) + 4);
            var poly = new Polyline
            {
                Stroke = AppDialogs.AccentBrush,
                StrokeThickness = 1.8,
                StrokeLineJoin = PenLineJoin.Round
            };
            for (var i = 0; i < recent.Count; i++)
            {
                var x = i * (64.0 / (recent.Count - 1)) + 2;
                var y = 14 - ((recent[i].Bpm - minB) / (double)(maxB - minB)) * 12;
                poly.Points.Add(new Point(x, y));
            }
            canvas.Children.Add(poly);
            _bpmHistoryPanel.Children.Add(canvas);
        }

        var trail = string.Join(" → ", recent.Select(x => x.Bpm));
        var best = lib.DrillBestBpms.TryGetValue(_activePreset.Id, out var b) ? b : recent.Max(x => x.Bpm);
        _bpmHistoryPanel.Children.Add(new TextBlock
        {
            Text = $"速度成长档案：{trail} BPM  （最高 ★{best} BPM）",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.AccentHoverBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
    }

    private void RebuildDrill()
    {
        var npsPos = _fretboard.ActiveThreeNpsPosition > 0 ? _fretboard.ActiveThreeNpsPosition : 1;
        _currentDrill = ScaleDrillCatalog.Generate(
            _fretboard.TonicPitch,
            _fretboard.Scale,
            _fretboard.Tuning,
            _fretboard.Capo,
            _fretboard.ActiveCagedFilter,
            _posMode,
            _activePreset.Pattern,
            _subdivision,
            _accentMode,
            fixedWindowStartFret: _fixedWindowStartFret,
            cofKeyCount: _cofKeyCount,
            cofBarsPerKey: _cofBarsPerKey,
            threeNpsPosition: npsPos);

        _drillTitleText.Text = $"[{_activePreset.CodeBadge}] {_activePreset.Title}  ·  {_currentDrill.Subtitle}（共 {_currentDrill.Section.Bars.Count} 小节 · 点小节可循环死磕）";
        _coachingTipText.Text = _activePreset.CoachingTip;
        _tabSurface.SetSection(_currentDrill.Section, new TabPlayheadState(-1, -1, _loopStartBar, _loopEndBar));
        if (IsCircleOfFifthsPattern(_activePreset.Pattern))
            _cofDial.Reset(_fretboard.TonicPitch);
    }

    // ─── Playback Engine ──────────────────────────────────────────────────────────

    private void TogglePlayback()
    {
        if (_isPlaying)
        {
            if (_workoutActive) StopWorkoutRunner(recordPartial: true);
            else StopDrillEngineOnly();
            return;
        }
        StartDrillEngine();
    }

    private void StartDrillEngine()
    {
        if (_currentDrill is null || _currentDrill.Events.Count == 0)
            RebuildDrill();
        if (_currentDrill is null || _currentDrill.Events.Count == 0)
            return;

        _savedTonicBeforeCof = _fretboard.TonicPitch;
        _savedCagedBeforeCof = _fretboard.ActiveCagedFilter;
        _fretboard.HighlightedChord = null;
        _fretboard.JamTargetColorMode = false;

        _isPlaying = true;
        _playBtn.Content = "■ 停止跟练";
        _playEventIdx = FindStartEventIndex();

        if (_useCountIn)
        {
            _countInRemaining = 4;
            _tickTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(160, 60000.0 / _bpm));
            TriggerCountInBeat();
            _tickTimer.Start();
        }
        else
        {
            _countInRemaining = 0;
            UpdateTimerIntervalIfPlaying();
            ExecuteDrillStep();
            _tickTimer.Start();
        }
    }

    private void TriggerCountInBeat()
    {
        var beatNum = 5 - _countInRemaining;
        _liveKeyHudText.Visibility = Visibility.Visible;
        _liveKeyHudText.Text = $"⏳ 预备拍倒数：{beatNum} / 4  —  请将左手就位！";

        if (_currentDrill is { Events.Count: > 0 })
        {
            var first = _currentDrill.Events[Math.Clamp(_playEventIdx, 0, _currentDrill.Events.Count - 1)];
            _fretboard.SetDrillLookahead(null, (first.StringIdx, first.Fret));
        }

        _audio.PlayDrillStep(midiNote: null, isNoteAccent: false, playClick: true, isDownbeatClick: beatNum == 1, durationMs: 180);
    }

    private void OnDrillTick(object? sender, EventArgs e)
    {
        if (!_isPlaying || _currentDrill is null || _currentDrill.Events.Count == 0)
        {
            StopDrillEngineOnly();
            return;
        }

        if (_countInRemaining > 1)
        {
            _countInRemaining--;
            TriggerCountInBeat();
            return;
        }
        if (_countInRemaining == 1)
        {
            _countInRemaining = 0;
            UpdateTimerIntervalIfPlaying();
        }

        ExecuteDrillStep();
    }

    private void ExecuteDrillStep()
    {
        if (_currentDrill is null || _currentDrill.Events.Count == 0) return;

        var startIdx = FindStartEventIndex();
        var endIdx = FindEndEventIndex();
        if (_playEventIdx > endIdx || _playEventIdx >= _currentDrill.Events.Count)
        {
            if (_speedTrainer && _bpm < 160)
            {
                _bpm = Math.Min(160, _bpm + 4);
                _bpmText.Text = $"{_bpm} BPM";
                UpdateTimerIntervalIfPlaying();
                _setStatus($"🚀 阶梯提速：已完成一轮，速度自动提升至 {_bpm} BPM！");
            }
            _playEventIdx = startIdx;
        }

        var ev = _currentDrill.Events[_playEventIdx];
        var nextIdx = _playEventIdx + 1 <= endIdx && _playEventIdx + 1 < _currentDrill.Events.Count
            ? _playEventIdx + 1
            : startIdx;
        var nxt = _currentDrill.Events[nextIdx];

        if (IsCircleOfFifthsPattern(_activePreset.Pattern))
        {
            if (_fretboard.TonicPitch != ev.EffectiveTonicPitch ||
                !string.Equals(_fretboard.ActiveCagedFilter, ev.EffectiveCagedLetter, StringComparison.OrdinalIgnoreCase))
            {
                _fretboard.ShowThreeNpsBoxes = false;
                _fretboard.TonicPitch = ev.EffectiveTonicPitch;
                _fretboard.ShowCagedBoxes = true;
                _fretboard.ActiveCagedFilter = ev.EffectiveCagedLetter;
                _syncHostTopBar();
            }
            var stepSemi = _activePreset.Pattern == DrillPatternKind.CircleOf5thsPentatonic ? 7 : 5;
            var nextTonic = (ev.EffectiveTonicPitch + stepSemi) % 12;
            _cofDial.UpdateState(ev.EffectiveTonicPitch, nextTonic, ev.EffectiveCagedLetter);
            _liveKeyHudText.Visibility = Visibility.Visible;
            _liveKeyHudText.Text = $"{ev.KeyBannerText}    ➔    {ev.NextKeyPreviewText}";
        }
        else
        {
            _liveKeyHudText.Visibility = Visibility.Collapsed;
        }

        _fretboard.SetDrillLookahead((ev.StringIdx, ev.Fret, ev.IsAccent), (nxt.StringIdx, nxt.Fret));
        _tabSurface.SetPlayhead(new TabPlayheadState(ev.BarIndex, ev.Column, _loopStartBar, _loopEndBar));

        var barY = _tabSurface.GetBarVerticalOffset(ev.BarIndex);
        if (barY >= 0 && (barY < _tabScroll.VerticalOffset || barY > _tabScroll.VerticalOffset + _tabScroll.ViewportHeight - 110))
            _tabScroll.ScrollToVerticalOffset(Math.Max(0, barY - 24));

        var beatInBar = ev.Column / Math.Max(1, _currentDrill.NotesPerBeat);
        var shouldClick = false;
        var isAccentClick = false;
        if (ev.IsBeatStart)
        {
            if (_metronomeMode == 1)
            {
                shouldClick = true;
                isAccentClick = beatInBar == 0;
            }
            else if (_metronomeMode == 2 && beatInBar is 1 or 3)
            {
                shouldClick = true;
                isAccentClick = true;
            }
        }

        var noteDurationMs = Math.Clamp((int)(GetStepIntervalMs() * 1.35), 130, 620);
        _audio.PlayDrillStep(
            midiNote: _playGuitarDemo ? ev.Midi : null,
            isNoteAccent: ev.IsAccent,
            playClick: shouldClick,
            isDownbeatClick: isAccentClick,
            durationMs: noteDurationMs);

        _playEventIdx++;
    }

    private int FindStartEventIndex()
    {
        if (_currentDrill is null || _loopStartBar < 0) return 0;
        var idx = _currentDrill.Events.FindIndex(e => e.BarIndex >= _loopStartBar);
        return idx >= 0 ? idx : 0;
    }

    private int FindEndEventIndex()
    {
        if (_currentDrill is null || _currentDrill.Events.Count == 0) return 0;
        if (_loopStartBar < 0) return _currentDrill.Events.Count - 1;
        var maxBar = Math.Max(_loopStartBar, _loopEndBar);
        var idx = _currentDrill.Events.FindLastIndex(e => e.BarIndex <= maxBar);
        return idx >= 0 ? idx : _currentDrill.Events.Count - 1;
    }

    private double GetStepIntervalMs()
    {
        var notesPerBeat = _currentDrill?.NotesPerBeat ?? (int)_subdivision;
        return Math.Max(55.0, 60000.0 / _bpm / Math.Max(1, notesPerBeat));
    }

    private void UpdateTimerIntervalIfPlaying()
    {
        if (_isPlaying && _countInRemaining == 0)
            _tickTimer.Interval = TimeSpan.FromMilliseconds(GetStepIntervalMs());
    }

    private void AdjustBpm(int delta)
    {
        _bpm = Math.Clamp(_bpm + delta, 40, 180);
        _bpmText.Text = $"{_bpm} BPM";
        UpdateTimerIntervalIfPlaying();
    }

    private void OnTabBarClicked(int barIndex, bool shiftHeld)
    {
        if (_currentDrill is null) return;

        if (shiftHeld && _loopStartBar >= 0)
        {
            _loopStartBar = Math.Min(_loopStartBar, barIndex);
            _loopEndBar = Math.Max(_loopStartBar, barIndex);
            _setStatus($"🔁 已设置 A–B 选段循环：第 {_loopStartBar + 1} ～ {_loopEndBar + 1} 小节。");
        }
        else if (_loopStartBar == barIndex && _loopEndBar == barIndex)
        {
            _loopStartBar = -1;
            _loopEndBar = -1;
            _setStatus("已取消单小节循环，恢复全曲练习。");
        }
        else
        {
            _loopStartBar = barIndex;
            _loopEndBar = barIndex;
            _setStatus($"🔁 已锁定死磕第 {barIndex + 1} 小节（按住 Shift 点另一小节可框选多小节，再点一次取消）。");
        }

        _clearLoopBtn.Visibility = _loopStartBar >= 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_isPlaying)
            _playEventIdx = FindStartEventIndex();

        _tabSurface.SetPlayhead(new TabPlayheadState(
            _isPlaying && _playEventIdx < _currentDrill.Events.Count ? _currentDrill.Events[_playEventIdx].BarIndex : -1,
            -1,
            _loopStartBar,
            _loopEndBar));
    }

    // ─── Progress Sparkline Check-in & Workbook / PNG / Song Export ───────────────

    private void StampMasteredBpm()
    {
        var lib = _getLibrary?.Invoke();
        if (lib is null) return;

        var prevBest = lib.DrillBestBpms.TryGetValue(_activePreset.Id, out var pb) ? pb : 0;
        if (_bpm > prevBest)
            lib.DrillBestBpms[_activePreset.Id] = _bpm;

        if (!lib.DrillBpmHistory.TryGetValue(_activePreset.Id, out var hist))
        {
            hist = [];
            lib.DrillBpmHistory[_activePreset.Id] = hist;
        }
        if (hist.Count == 0 || hist[^1].Bpm != _bpm)
            hist.Add(new DrillBpmHistoryEntry { RecordedAt = DateTime.Now, Bpm = _bpm });

        RecordWorkoutMinutes(1, 1);
        _onLibraryMutated();
        RebuildPresetList();
        RebuildBpmSparkline();
        _setStatus($"★ 已打卡记录「[{_activePreset.CodeBadge}] {_activePreset.Title}」速度：{_bpm} BPM！");
    }

    private void OpenWorkbookDialog()
    {
        StopPlayback();
        var npsPos = _fretboard.ActiveThreeNpsPosition > 0 ? _fretboard.ActiveThreeNpsPosition : 1;
        DrillWorkbookDialog.Show(
            Window.GetWindow(this),
            _fretboard.TonicPitch,
            _fretboard.Scale,
            _fretboard.Tuning,
            _fretboard.Capo,
            _fretboard.ActiveCagedFilter,
            _fixedWindowStartFret,
            _cofKeyCount,
            _cofBarsPerKey,
            npsPos,
            _getLibrary?.Invoke()?.DrillBestBpms,
            _setStatus);
    }

    private void InsertDrillIntoCurrentSong()
    {
        var song = _getCurrentSong?.Invoke();
        if (song is null || _currentDrill is null) return;

        var cloned = _currentDrill.Section.Clone();
        cloned.Title = $"指板练习 · [{_activePreset.CodeBadge}] {_activePreset.Title}";
        cloned.Kind = TabSectionKind.Outro;
        cloned.InsertAfterLineIndex = Math.Max(-1, song.Lines.Count - 1);
        song.TabSections.Add(cloned);
        song.ShowTabSections = true;
        song.UpdatedAt = DateTime.Now;
        _onSongMutated();
        _setStatus($"已将练习「{cloned.Title}」插入曲谱《{song.Title}》，切回「编辑制谱」即可查看或打印！");
    }

    private void ExportDrillPng()
    {
        if (_currentDrill is null) return;

        var dlg = new SaveFileDialog
        {
            Title = "导出指板练习高清六线谱 PNG",
            Filter = "PNG 图片 (*.png)|*.png",
            FileName = $"弦笺指板练习_{_activePreset.CodeBadge}_{_activePreset.Title}.png"
        };
        if (dlg.ShowDialog() != true) return;

        const double exportWidth = 980;
        const double pad = 32;
        var secHeight = TabPainter.MeasureSectionHeight(_currentDrill.Section, exportWidth - pad * 2);
        var totalHeight = pad * 2 + 52 + secHeight;
        const double dpiScale = 2.0;

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, exportWidth, totalHeight));
            var titleFt = new FormattedText(
                $"弦笺指板练功房 · [{_activePreset.CodeBadge}] {_activePreset.Title}",
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                18,
                AppDialogs.TitleBrush,
                dpiScale);
            dc.DrawText(titleFt, new Point(pad, pad));

            var subFt = new FormattedText(
                $"{_currentDrill.Title}  |  建议拍速 {_bpm} BPM  |  {_activePreset.CoachingTip}",
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                11.5,
                AppDialogs.MutedBrush,
                dpiScale);
            dc.DrawText(subFt, new Point(pad, pad + 26));

            TabPainter.DrawSection(dc, _currentDrill.Section, pad, pad + 52, exportWidth - pad * 2, Brushes.White, dpiScale);
        }

        var rtb = new RenderTargetBitmap((int)(exportWidth * dpiScale), (int)(totalHeight * dpiScale), 96 * dpiScale, 96 * dpiScale, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using (var fs = File.Create(dlg.FileName))
            encoder.Save(fs);

        _setStatus($"已导出高清练习谱图片：{dlg.FileName}");
    }

    // ─── Style Helpers ────────────────────────────────────────────────────────────

    private void UpdateMetroSegStyles()
    {
        for (var i = 0; i < 3; i++)
            StyleSegBtn(_metroModeBtns[i], i == _metronomeMode);
    }

    private void UpdateRecipeSegStyles()
    {
        foreach (var (m, b) in _posModeBtns) StyleSegBtn(b, m == _posMode);
        foreach (var (s, b) in _subdivBtns) StyleSegBtn(b, s == _subdivision);
        foreach (var (a, b) in _accentBtns) StyleSegBtn(b, a == _accentMode);
    }

    private void UpdateCofOptionStyles()
    {
        foreach (var (w, b) in _cofWinBtns) StyleSegBtn(b, w == _fixedWindowStartFret);
        foreach (var (k, b) in _cofKeyCountBtns) StyleSegBtn(b, k == _cofKeyCount);
        foreach (var (br, b) in _cofBarsBtns) StyleSegBtn(b, br == _cofBarsPerKey);
    }

    private static bool IsCircleOfFifthsPattern(DrillPatternKind kind) =>
        kind is DrillPatternKind.CircleOf4thsPentatonic or DrillPatternKind.CircleOf4thsMajor or DrillPatternKind.CircleOf5thsPentatonic;

    private static Border CreateSegTrack() => new()
    {
        Background = SegTrackBg,
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(2),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new StackPanel { Orientation = Orientation.Horizontal }
    };

    private static Button CreateSegBtn(string text, double minW = 0)
    {
        var btn = new Button
        {
            Content = text,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = AppDialogs.MutedBrush,
            Padding = new Thickness(7, 2.5, 7, 2.5),
            FontSize = 11,
            Cursor = Cursors.Hand
        };
        if (minW > 0) btn.MinWidth = minW;
        return btn;
    }

    private static void StyleSegBtn(Button btn, bool active)
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
            Padding = new Thickness(10, 3.5, 10, 3.5),
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

    private static TextBlock MakeMiniLabel(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = AppDialogs.MutedBrush,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 2, 4, 2)
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
        _tickTimer.Stop();
        _workoutSecondTimer.Stop();
    }

    /// <summary>
    /// Vector 12-Key Circle-of-Fifths live dial widget showing completed keys, active key + CAGED shape, and upcoming key.
    /// </summary>
    private sealed class CircleOfFifthsDial : FrameworkElement
    {
        private static readonly int[] ClockwiseFifthsPitches = [0, 7, 2, 9, 4, 11, 6, 1, 8, 3, 10, 5];
        private static readonly string[] ClockwiseLabels = ["C", "G", "D", "A", "E", "B", "Gb", "Db", "Ab", "Eb", "Bb", "F"];
        private static readonly Typeface DialFace = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        private readonly HashSet<int> _visited = [];
        private int _currentPitch;
        private int _nextPitch = 5;
        private string _cagedShape = "G";

        public CircleOfFifthsDial()
        {
            Width = 74;
            Height = 74;
            VerticalAlignment = VerticalAlignment.Center;
        }

        public void Reset(int startPitch)
        {
            _visited.Clear();
            _currentPitch = ((startPitch % 12) + 12) % 12;
            _nextPitch = (_currentPitch + 5) % 12;
            _cagedShape = "—";
            InvalidateVisual();
        }

        public void UpdateState(int currentPitch, int nextPitch, string cagedLetter)
        {
            _currentPitch = ((currentPitch % 12) + 12) % 12;
            _nextPitch = ((nextPitch % 12) + 12) % 12;
            _cagedShape = cagedLetter;
            _visited.Add(_currentPitch);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var center = new Point(Width / 2, Height / 2);
            const double ringR = 27.5;

            dc.DrawEllipse(Brushes.White, new Pen(CardBorder, 1.0), center, 35, 35);

            for (var i = 0; i < 12; i++)
            {
                var angle = (-90 + i * 30) * Math.PI / 180.0;
                var pt = new Point(center.X + Math.Cos(angle) * ringR, center.Y + Math.Sin(angle) * ringR);
                var pc = ClockwiseFifthsPitches[i];
                var isCur = pc == _currentPitch;
                var isNxt = pc == _nextPitch;
                var isDone = _visited.Contains(pc);

                Brush fill = isCur ? AppDialogs.AccentBrush
                    : isDone ? new SolidColorBrush(Color.FromRgb(187, 222, 201))
                    : Brushes.Transparent;
                Pen? pen = isNxt && !isCur ? new Pen(new SolidColorBrush(Color.FromRgb(217, 119, 6)), 1.5) : null;

                dc.DrawEllipse(fill, pen, pt, isCur ? 8.2 : 6.8, isCur ? 8.2 : 6.8);

                var ft = new FormattedText(
                    ClockwiseLabels[i],
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    DialFace,
                    isCur ? 7.8 : 7.0,
                    isCur ? Brushes.White : (isNxt ? new SolidColorBrush(Color.FromRgb(180, 83, 9)) : AppDialogs.BodyBrush),
                    dpi);
                dc.DrawText(ft, new Point(pt.X - ft.Width / 2, pt.Y - ft.Height / 2));
            }

            var centerFt = new FormattedText(
                $"{_cagedShape}形",
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                9.5,
                AppDialogs.AccentHoverBrush,
                dpi);
            dc.DrawText(centerFt, new Point(center.X - centerFt.Width / 2, center.Y - centerFt.Height / 2));
        }
    }

    private sealed class DrillTabSurface : FrameworkElement
    {
        private TabSection? _section;
        private TabPlayheadState _playhead = new();
        private readonly List<(int BarIndex, Rect Bounds)> _barBounds = [];

        public event Action<int, bool>? BarClicked;

        public DrillTabSurface()
        {
            Cursor = Cursors.Hand;
            SnapsToDevicePixels = true;
        }

        public void SetSection(TabSection section, TabPlayheadState playhead)
        {
            _section = section;
            _playhead = playhead;
            InvalidateMeasure();
            InvalidateVisual();
        }

        public void SetPlayhead(TabPlayheadState playhead)
        {
            _playhead = playhead;
            InvalidateVisual();
        }

        public double GetBarVerticalOffset(int barIndex)
        {
            foreach (var (idx, rect) in _barBounds)
            {
                if (idx == barIndex)
                    return rect.Top;
            }
            return -1;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsInfinity(availableSize.Width) ? 820 : Math.Max(340, availableSize.Width);
            if (_section is null)
                return new Size(width, 140);
            var h = TabPainter.MeasureSectionHeight(_section, width - 8) + 12;
            return new Size(width, Math.Max(140, h));
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            _barBounds.Clear();
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));
            if (_section is null || ActualWidth < 120) return;

            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            TabPainter.DrawSection(dc, _section, 4, 4, ActualWidth - 8, Brushes.White, dpi, _playhead, _barBounds);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            var pt = e.GetPosition(this);
            foreach (var (idx, rect) in _barBounds)
            {
                if (rect.Contains(pt))
                {
                    BarClicked?.Invoke(idx, (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift);
                    e.Handled = true;
                    break;
                }
            }
        }
    }
}
