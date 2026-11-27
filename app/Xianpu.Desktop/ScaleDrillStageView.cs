using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Xianpu.Desktop;

/// <summary>
/// Interactive Guitar Scale Drill &amp; Fretboard Mastery Stage ("⚡ 指板练功房"):
/// - 29 structured progressive drills (Spider, 3/4/5-note sequences, 3-against-4 &amp; offbeat accent displacement,
///   3rd–6th interval skips, diatonic triad/7th alternating arpeggios, single-position Circle of Fifths)
/// - Live algorithmic TAB sheet rendered via TabPainter with smooth playhead, > accent marks &amp; clickable A–B bar loop
/// - Synchronized FretboardCanvas lookahead arrow (Current note + Next note preview + Gold accent halo + live key morphing)
/// - 4-beat woodblock count-in, independent Guitar / Metronome (1234 or 2&amp;4 backbeat) layers, Speed Trainer (+BPM/loop),
///   mastered BPM check-in badges, high-DPI PNG export, and 1-click insert into active song
/// </summary>
public sealed class ScaleDrillStageView : UserControl, IDisposable
{
    private static readonly Brush SoftSurface = Freeze(new SolidColorBrush(Color.FromRgb(246, 248, 246)));
    private static readonly Brush CardBorder = Freeze(new SolidColorBrush(Color.FromRgb(220, 228, 223)));
    private static readonly Brush SegTrackBg = Freeze(new SolidColorBrush(Color.FromRgb(236, 240, 238)));
    private static readonly Brush WarmPaperBg = Freeze(new SolidColorBrush(Color.FromRgb(253, 251, 246)));
    private static readonly Brush WarmPaperBorder = Freeze(new SolidColorBrush(Color.FromRgb(232, 226, 211)));

    private readonly FretboardCanvas _fretboard;
    private readonly GuitarAudioService _audio;
    private readonly Action _syncHostTopBar;
    private readonly Func<SongModel?> _getCurrentSong;
    private readonly Func<LibraryData?> _getLibrary;
    private readonly Action _onLibraryMutated;
    private readonly Action _onSongMutated;
    private readonly Action<string> _setStatus;

    private readonly DispatcherTimer _tickTimer = new(DispatcherPriority.Render);
    private readonly DrillTabSurface _tabSurface = new();

    private StackPanel _presetListPanel = null!;
    private ScrollViewer _tabScroll = null!;
    private Border _bannerCard = null!;
    private TextBlock _drillTitleText = null!;
    private TextBlock _coachingTipText = null!;
    private TextBlock _liveKeyHudText = null!;
    private Button _playBtn = null!;
    private Button _countInBtn = null!;
    private Button _guitarSoundBtn = null!;
    private readonly Button[] _metroModeBtns = new Button[3]; // 0=Off, 1=AllBeats, 2=Backbeat24
    private TextBlock _bpmText = null!;
    private Button _speedTrainerBtn = null!;
    private Button _stampBpmBtn = null!;
    private Button _clearLoopBtn = null!;

    private readonly Dictionary<DrillPositionMode, Button> _posModeBtns = [];
    private readonly Dictionary<DrillSubdivision, Button> _subdivBtns = [];
    private readonly Dictionary<DrillAccentMode, Button> _accentBtns = [];

    private ScaleDrillPreset _activePreset = ScaleDrillCatalog.Presets[6]; // Default right to #28 3-note seq + 16th displacement!
    private DrillPositionMode _posMode;
    private DrillSubdivision _subdivision;
    private DrillAccentMode _accentMode;
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
        _tabSurface.BarClicked += OnTabBarClicked;

        Content = BuildLayout();
        ApplyPreset(_activePreset, applyRecommendedScale: false);
    }

    public void OnTabActivated()
    {
        RebuildPresetList();
        RebuildDrill();
    }

    public void OnFretboardParametersChanged()
    {
        if (_isPlaying && IsCircleOfFifthsPattern(_activePreset.Pattern))
            return;
        RebuildDrill();
    }

    public void StopPlayback()
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
        }

        _tabSurface.SetPlayhead(new TabPlayheadState(-1, -1, _loopStartBar, _loopEndBar));
    }

    private UIElement BuildLayout()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(272) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // ─── Left: 5-Unit Progressive Curriculum Rail ─────────────────────────
        _presetListPanel = new StackPanel();
        var presetScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _presetListPanel
        };
        var leftCard = new Border
        {
            Background = SoftSurface,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8),
            Child = presetScroll
        };
        Grid.SetColumn(leftCard, 0);
        grid.Children.Add(leftCard);

        // ─── Right: Control Cockpit + Live Interactive TAB Stage ──────────────
        var rightGrid = new Grid();
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Banner
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Transport & Recipe
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Live TAB

        // 1. Coaching Tip & Live Circle-of-Fifths HUD Banner
        _bannerCard = new Border
        {
            Background = WarmPaperBg,
            BorderBrush = WarmPaperBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 9, 14, 9),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var bannerDock = new DockPanel();
        var rightOutBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };

        var exportPngBtn = CreatePillButton("🖼 导出练习 PNG", isPrimary: false, toolTip: "将当前生成的六线谱练习导出为高清无水印 PNG 练习页");
        exportPngBtn.Margin = new Thickness(0, 0, 6, 0);
        exportPngBtn.Click += (_, _) => ExportDrillPng();

        var insertSongBtn = CreatePillButton("📥 插入当前曲谱", isPrimary: false, toolTip: "将本条练习作为六线谱段落插入当前正在编辑的曲谱末尾");
        insertSongBtn.Click += (_, _) => InsertDrillIntoCurrentSong();

        rightOutBtns.Children.Add(exportPngBtn);
        rightOutBtns.Children.Add(insertSongBtn);
        DockPanel.SetDock(rightOutBtns, Dock.Right);
        bannerDock.Children.Add(rightOutBtns);

        var bannerStack = new StackPanel();
        _drillTitleText = new TextBlock { FontSize = 14, FontWeight = FontWeights.Bold, Foreground = AppDialogs.TitleBrush };
        _coachingTipText = new TextBlock { FontSize = 11.5, Foreground = AppDialogs.BodyBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
        _liveKeyHudText = new TextBlock
        {
            Visibility = Visibility.Collapsed,
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(194, 65, 12)),
            Margin = new Thickness(0, 4, 0, 0)
        };
        bannerStack.Children.Add(_drillTitleText);
        bannerStack.Children.Add(_coachingTipText);
        bannerStack.Children.Add(_liveKeyHudText);
        bannerDock.Children.Add(bannerStack);
        _bannerCard.Child = bannerDock;
        Grid.SetRow(_bannerCard, 0);
        rightGrid.Children.Add(_bannerCard);

        // 2. Transport + Recipe Cockpit
        var ctrlCard = new Border
        {
            Background = SoftSurface,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var ctrlStack = new StackPanel();

        // Row A: Playback, Count-in, Sound layers, BPM, Speed Trainer, Stamp BPM
        var rowA = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 5) };

        _playBtn = CreatePillButton("▶ 开始跟练", isPrimary: true, toolTip: "启动指板预判箭头与下方六线谱动态游标同步跟练");
        _playBtn.Margin = new Thickness(0, 2, 8, 2);
        _playBtn.Click += (_, _) => TogglePlayback();
        rowA.Children.Add(_playBtn);

        _countInBtn = CreatePillButton("预备 4拍", isPrimary: false, toolTip: "开启后先打 4 拍木鱼倒数再进入第 1 音，从容落手");
        _countInBtn.Margin = new Thickness(0, 2, 6, 2);
        _countInBtn.Click += (_, _) => { _useCountIn = !_useCountIn; StyleTogglePill(_countInBtn, _useCountIn); };
        StyleTogglePill(_countInBtn, _useCountIn);
        rowA.Children.Add(_countInBtn);

        _guitarSoundBtn = CreatePillButton("🎸 示范音", isPrimary: false, toolTip: "可随时关闭吉他示范音，仅保留节拍器与指板光点自己跟弹");
        _guitarSoundBtn.Margin = new Thickness(0, 2, 8, 2);
        _guitarSoundBtn.Click += (_, _) => { _playGuitarDemo = !_playGuitarDemo; StyleTogglePill(_guitarSoundBtn, _playGuitarDemo); };
        StyleTogglePill(_guitarSoundBtn, _playGuitarDemo);
        rowA.Children.Add(_guitarSoundBtn);

        var metroSeg = CreateSegTrack();
        metroSeg.Margin = new Thickness(0, 2, 8, 2);
        var metroStack = (StackPanel)metroSeg.Child;
        string[] metroLabels = ["静音拍", "🪵 正拍", "🪵 2&4反拍"];
        for (var m = 0; m < 3; m++)
        {
            var mode = m;
            var mb = CreateSegBtn(metroLabels[m]);
            mb.ToolTip = mode == 2 ? "进阶律动训练：仅在第 2、4 反拍敲响木鱼" : "切换节拍器伴奏模式";
            mb.Click += (_, _) => { _metronomeMode = mode; UpdateMetroSegStyles(); };
            _metroModeBtns[m] = mb;
            metroStack.Children.Add(mb);
        }
        UpdateMetroSegStyles();
        rowA.Children.Add(metroSeg);

        var bpmSeg = CreateSegTrack();
        bpmSeg.Margin = new Thickness(0, 2, 8, 2);
        var bpmStack = (StackPanel)bpmSeg.Child;
        var minusBtn = CreateSegBtn("−", 22);
        minusBtn.Click += (_, _) => AdjustBpm(-4);
        _bpmText = new TextBlock { Text = "76 BPM", FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = AppDialogs.TitleBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
        var plusBtn = CreateSegBtn("+", 22);
        plusBtn.Click += (_, _) => AdjustBpm(4);
        bpmStack.Children.Add(minusBtn);
        bpmStack.Children.Add(_bpmText);
        bpmStack.Children.Add(plusBtn);
        rowA.Children.Add(bpmSeg);

        _speedTrainerBtn = CreatePillButton("🚀 阶梯提速", isPrimary: false, toolTip: "每完整弹完一轮自动 +4 BPM（上限 160 BPM），循序渐进突破手速");
        _speedTrainerBtn.Margin = new Thickness(0, 2, 6, 2);
        _speedTrainerBtn.Click += (_, _) => { _speedTrainer = !_speedTrainer; StyleTogglePill(_speedTrainerBtn, _speedTrainer, "#D97706"); };
        rowA.Children.Add(_speedTrainerBtn);

        _stampBpmBtn = CreatePillButton("★ 打卡当前速度", isPrimary: false, toolTip: "记录本条练习目前已稳定拿下的最高 BPM 到本地档案");
        _stampBpmBtn.Margin = new Thickness(0, 2, 0, 2);
        _stampBpmBtn.Click += (_, _) => StampMasteredBpm();
        rowA.Children.Add(_stampBpmBtn);

        ctrlStack.Children.Add(rowA);

        // Row B: Position Lock + Subdivision + Accent Displacement + Clear Loop
        var rowB = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };

        rowB.Children.Add(MakeMiniLabel("把位"));
        var posSeg = CreateSegTrack();
        posSeg.Margin = new Thickness(0, 2, 10, 2);
        var posStack = (StackPanel)posSeg.Child;
        (DrillPositionMode Mode, string Label)[] posItems =
        [
            (DrillPositionMode.FollowCaged, "跟随CAGED"),
            (DrillPositionMode.FixedWindow5To8, "锁5–8品"),
            (DrillPositionMode.ThreeNotesPerString, "每弦三音(3NPS)")
        ];
        foreach (var (pm, lbl) in posItems)
        {
            var m = pm;
            var b = CreateSegBtn(lbl);
            b.Click += (_, _) => { _posMode = m; UpdateRecipeSegStyles(); RebuildDrill(); };
            _posModeBtns[m] = b;
            posStack.Children.Add(b);
        }
        rowB.Children.Add(posSeg);

        rowB.Children.Add(MakeMiniLabel("细分"));
        var subSeg = CreateSegTrack();
        subSeg.Margin = new Thickness(0, 2, 10, 2);
        var subStack = (StackPanel)subSeg.Child;
        (DrillSubdivision Sub, string Label)[] subItems =
        [
            (DrillSubdivision.Eighth, "八分"),
            (DrillSubdivision.Triplet, "三连音"),
            (DrillSubdivision.Sixteenth, "十六分")
        ];
        foreach (var (sd, lbl) in subItems)
        {
            var s = sd;
            var b = CreateSegBtn(lbl);
            b.Click += (_, _) => { _subdivision = s; UpdateTimerIntervalIfPlaying(); UpdateRecipeSegStyles(); RebuildDrill(); };
            _subdivBtns[s] = b;
            subStack.Children.Add(b);
        }
        rowB.Children.Add(subSeg);

        rowB.Children.Add(MakeMiniLabel("重音移位"));
        var accSeg = CreateSegTrack();
        accSeg.Margin = new Thickness(0, 2, 8, 2);
        var accStack = (StackPanel)accSeg.Child;
        (DrillAccentMode Acc, string Label, string Tip)[] accItems =
        [
            (DrillAccentMode.None, "无", "均匀音量无额外重音"),
            (DrillAccentMode.BeatDownbeat, "拍头错位", "每拍第 1 音重弹（三音模进遇上十六分时自动产生组内重音轮转！）"),
            (DrillAccentMode.GroupOf3CrossBeat, "3对4跨拍", "每隔 3 个音重弹一次（>），打破小节线制造现代 Fusion 律动"),
            (DrillAccentMode.OffbeatSecond16th, "第2音后移", "每拍第 2 个十六分音符重弹 (1 >2 3 4)"),
            (DrillAccentMode.OffbeatUpbeat, "反拍(&)", "每拍正反拍重弹 (1 2 >3 4)"),
            (DrillAccentMode.Compound332, "3+3+2", "按 Tresillo 3+3+2 复合节奏重弹")
        ];
        foreach (var (am, lbl, tip) in accItems)
        {
            var a = am;
            var b = CreateSegBtn(lbl);
            b.ToolTip = tip;
            b.Click += (_, _) => { _accentMode = a; UpdateRecipeSegStyles(); RebuildDrill(); };
            _accentBtns[a] = b;
            accStack.Children.Add(b);
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
            _tabSurface.SetPlayhead(new TabPlayheadState(_isPlaying && _currentDrill is not null && _playEventIdx < _currentDrill.Events.Count ? _currentDrill.Events[_playEventIdx].BarIndex : -1, -1, -1, -1));
        };
        rowB.Children.Add(_clearLoopBtn);

        ctrlStack.Children.Add(rowB);
        ctrlCard.Child = ctrlStack;
        Grid.SetRow(ctrlCard, 1);
        rightGrid.Children.Add(ctrlCard);

        // 3. Interactive Live TAB Score Surface
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
        Grid.SetRow(tabCard, 2);
        rightGrid.Children.Add(tabCard);

        Grid.SetColumn(rightGrid, 2);
        grid.Children.Add(rightGrid);

        return grid;
    }

    // ─── Curriculum & Generation ──────────────────────────────────────────────────

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
            rowBorder.MouseLeftButtonDown += (_, _) => ApplyPreset(p, applyRecommendedScale: true);
            _presetListPanel.Children.Add(rowBorder);
        }
    }

    private void ApplyPreset(ScaleDrillPreset preset, bool applyRecommendedScale)
    {
        StopPlayback();
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

        if (applyRecommendedScale && preset.Pattern is not (DrillPatternKind.Spider1234 or DrillPatternKind.Spider1324))
        {
            _fretboard.Scale = MusicTheoryCatalog.GetScale(preset.RecommendedScaleId);
            _fretboard.HighlightedChord = null;
            _fretboard.JamTargetColorMode = false;
            _syncHostTopBar();
            _fretboard.Refresh();
        }

        UpdateRecipeSegStyles();
        RebuildPresetList();
        RebuildDrill();
    }

    private void RebuildDrill()
    {
        _currentDrill = ScaleDrillCatalog.Generate(
            _fretboard.TonicPitch,
            _fretboard.Scale,
            _fretboard.Tuning,
            _fretboard.Capo,
            _fretboard.ActiveCagedFilter,
            _posMode,
            _activePreset.Pattern,
            _subdivision,
            _accentMode);

        _drillTitleText.Text = $"[{_activePreset.CodeBadge}] {_activePreset.Title}  ·  {_currentDrill.Subtitle}（共 {_currentDrill.Section.Bars.Count} 小节 · 点击下方任意小节可单小节死磕循环）";
        _coachingTipText.Text = _activePreset.CoachingTip;
        _tabSurface.SetSection(_currentDrill.Section, new TabPlayheadState(-1, -1, _loopStartBar, _loopEndBar));
    }

    // ─── Real-Time Playback, Count-in, Lookahead & Speed Trainer ──────────────────

    private void TogglePlayback()
    {
        if (_isPlaying)
        {
            StopPlayback();
            return;
        }
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
        _liveKeyHudText.Text = $"⏳ 预备拍倒数：{beatNum} / 4  —  请将左手就位！（即将弹第 1 音）";

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
            StopPlayback();
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
            // Completed one full pass!
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

        // Live Circle-of-Fifths key & CAGED morphing on the panoramic fretboard!
        if (IsCircleOfFifthsPattern(_activePreset.Pattern))
        {
            if (_fretboard.TonicPitch != ev.EffectiveTonicPitch ||
                !string.Equals(_fretboard.ActiveCagedFilter, ev.EffectiveCagedLetter, StringComparison.OrdinalIgnoreCase))
            {
                _fretboard.TonicPitch = ev.EffectiveTonicPitch;
                _fretboard.ShowCagedBoxes = true;
                _fretboard.ActiveCagedFilter = ev.EffectiveCagedLetter;
                _syncHostTopBar();
            }
            _liveKeyHudText.Visibility = Visibility.Visible;
            _liveKeyHudText.Text = $"{ev.KeyBannerText}    ➔    {ev.NextKeyPreviewText}";
        }
        else
        {
            _liveKeyHudText.Visibility = Visibility.Collapsed;
        }

        // Update Fretboard Lookahead (Current + Next trajectory arrow + Gold accent halo)
        _fretboard.SetDrillLookahead((ev.StringIdx, ev.Fret, ev.IsAccent), (nxt.StringIdx, nxt.Fret));

        // Update Live TAB Playhead & Auto-Scroll
        _tabSurface.SetPlayhead(new TabPlayheadState(ev.BarIndex, ev.Column, _loopStartBar, _loopEndBar));
        var barY = _tabSurface.GetBarVerticalOffset(ev.BarIndex);
        if (barY >= 0 && (barY < _tabScroll.VerticalOffset || barY > _tabScroll.VerticalOffset + _tabScroll.ViewportHeight - 110))
            _tabScroll.ScrollToVerticalOffset(Math.Max(0, barY - 24));

        // Compute metronome click (Off / All Beats / 2 & 4 Backbeat)
        var beatInBar = ev.Column / Math.Max(1, _currentDrill.NotesPerBeat); // 0..3
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
                isAccentClick = true; // Crisp snare-like backbeat click on beats 2 & 4!
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
            var a = Math.Min(_loopStartBar, barIndex);
            var b = Math.Max(_loopStartBar, barIndex);
            _loopStartBar = a;
            _loopEndBar = b;
            _setStatus($"🔁 已设置 A–B 选段循环：第 {a + 1} ～ {b + 1} 小节。");
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

    // ─── Progress Check-in & PNG / Song Export ────────────────────────────────────

    private void StampMasteredBpm()
    {
        var lib = _getLibrary?.Invoke();
        if (lib is null) return;

        lib.DrillBestBpms[_activePreset.Id] = _bpm;
        _onLibraryMutated();
        RebuildPresetList();
        _setStatus($"★ 已打卡记录「[{_activePreset.CodeBadge}] {_activePreset.Title}」达标速度：{_bpm} BPM！");
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

    // ─── Visual Style Helpers ─────────────────────────────────────────────────────

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
    }

    /// <summary>
    /// Lightweight TabPainter-backed interactive surface supporting live playhead &amp; click-bar A–B loop.
    /// </summary>
    private sealed class DrillTabSurface : FrameworkElement
    {
        private TabSection? _section;
        private TabPlayheadState _playhead = new();
        private readonly List<(int BarIndex, Rect Bounds)> _barBounds = [];

        public event Action<int, bool>? BarClicked; // (barIndex, isShiftDown)

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
