using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Xianpu.Desktop;

public enum FretboardLabelMode
{
    ScaleDegree,
    NoteName,
    Interval
}

/// <summary>
/// Instrument-grade 0–17 fret interactive 2D vector guitar fretboard:
/// - Realistic maple / rosewood grain, neck binding, bone nut, metallic frets with specular highlight, pearl inlays, tapered strings
/// - Click or drag-sweep (glissando) to pluck notes with animated ripple rings
/// - Clickable CAGED 5-color badges right above the neck to toggle single-shape focus
/// - Animated note-by-note playback highlights synced with scale / CAGED / arpeggio audio
/// - Floating dark-glass hover tooltip pill showing note name, interval, and string/fret position
/// </summary>
public sealed class FretboardCanvas : FrameworkElement
{
    public const int TotalFrets = 17;

    private static readonly Typeface LabelTypeface = new(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Typeface DotTypeface = new(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private static readonly Typeface MonoTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    // Role colors (Refined warm editorial palette harmonizing with StringNote Pine Green)
    private static readonly Color RootColor = Color.FromRgb(226, 98, 36);       // Terracotta Orange
    private static readonly Color ThirdColor = Color.FromRgb(124, 77, 224);     // Royal Iris
    private static readonly Color FifthColor = Color.FromRgb(38, 126, 85);      // Pine Emerald
    private static readonly Brush RootBrush = Freeze(new SolidColorBrush(RootColor));
    private static readonly Brush ThirdBrush = Freeze(new SolidColorBrush(ThirdColor));
    private static readonly Brush FifthBrush = Freeze(new SolidColorBrush(FifthColor));
    private static readonly Brush ScaleNoteFillLight = Freeze(new SolidColorBrush(Color.FromRgb(255, 254, 250)));
    private static readonly Brush ScaleNoteFillDark = Freeze(new SolidColorBrush(Color.FromRgb(248, 245, 238)));
    private static readonly Brush ScaleNoteBorder = Freeze(new SolidColorBrush(Color.FromRgb(100, 116, 139)));
    private static readonly Brush ScaleNoteText = Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)));

    // 4-Color Jam Target Notes
    private static readonly Brush JamRootBrush = Freeze(new SolidColorBrush(Color.FromRgb(220, 38, 38)));    // Crimson
    private static readonly Brush JamChordBrush = Freeze(new SolidColorBrush(Color.FromRgb(217, 119, 6)));   // Amber Gold
    private static readonly Brush JamColorBrush = Freeze(new SolidColorBrush(Color.FromRgb(37, 99, 235)));   // Cobalt Blue
    private static readonly Brush JamPassBrush = Freeze(new SolidColorBrush(Color.FromRgb(148, 163, 184)));  // Slate Mist

    // Wood & hardware palettes
    private static readonly Color MapleTop = Color.FromRgb(246, 231, 198);
    private static readonly Color MapleMid = Color.FromRgb(239, 218, 176);
    private static readonly Color MapleBot = Color.FromRgb(228, 202, 154);
    private static readonly Brush MapleBinding = Freeze(new SolidColorBrush(Color.FromRgb(205, 176, 128)));
    private static readonly Brush MapleGrain = Freeze(new SolidColorBrush(Color.FromArgb(24, 158, 116, 62)));
    private static readonly Brush MapleInlay = Freeze(new SolidColorBrush(Color.FromArgb(145, 86, 66, 44)));

    private static readonly Color RoseTop = Color.FromRgb(79, 53, 41);
    private static readonly Color RoseMid = Color.FromRgb(64, 42, 32);
    private static readonly Color RoseBot = Color.FromRgb(50, 32, 24);
    private static readonly Brush RoseBinding = Freeze(new SolidColorBrush(Color.FromRgb(214, 198, 172)));
    private static readonly Brush RoseGrain = Freeze(new SolidColorBrush(Color.FromArgb(30, 26, 14, 10)));
    private static readonly Brush RoseInlay = Freeze(new SolidColorBrush(Color.FromArgb(205, 238, 232, 218)));

    private static readonly Brush ShadowBrush = Freeze(new SolidColorBrush(Color.FromArgb(46, 15, 23, 42)));
    private static readonly Brush SoftShadowBrush = Freeze(new SolidColorBrush(Color.FromArgb(26, 15, 23, 42)));
    private static readonly Brush CapoBodyBrush = Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)));
    private static readonly Brush CapoAccentBrush = Freeze(new SolidColorBrush(Color.FromRgb(226, 196, 142)));
    private static readonly Brush CapoVeilBrush = Freeze(new SolidColorBrush(Color.FromArgb(118, 15, 23, 42)));
    private static readonly Brush HoverPillBg = Freeze(new SolidColorBrush(Color.FromArgb(232, 22, 30, 42)));

    private sealed record BadgeHitRegion(string ShapeLetter, Rect Bounds);

    private double _pixelsPerDip = 1.0;
    private readonly double[] _fretX = new double[TotalFrets + 1];
    private readonly double[] _stringY = new double[6]; // index 0 = 6E (bottom), index 5 = 1e (top)
    private readonly List<BadgeHitRegion> _badgeRegions = [];
    private double _boardLeft;
    private double _boardTop;
    private double _boardWidth;
    private double _boardHeight;

    private (int StringIdx, int Fret)? _hover;
    private string? _hoverBadgeLetter;
    private bool _isDraggingPluck;
    private (int StringIdx, int Fret)? _lastDragSpot;

    // Animated ripples for clicks & sequence playback
    private readonly List<(int StringIdx, int Fret, DateTime ExpireAt)> _activeRipples = [];
    private readonly DispatcherTimer _rippleTimer;

    // Sequence playback animation queue
    private readonly DispatcherTimer _seqAnimTimer;
    private List<(int StringIdx, int Fret)> _seqAnimQueue = [];
    private int _seqAnimIndex;

    public int TonicPitch { get; set; } = 0;
    public ScaleDefinition Scale { get; set; } = MusicTheoryCatalog.Scales[0];
    public GuitarTuning Tuning { get; set; } = MusicTheoryCatalog.Tunings[0];
    public int Capo { get; set; } = 0;
    public FretboardLabelMode LabelMode { get; set; } = FretboardLabelMode.ScaleDegree;
    public bool DarkRosewood { get; set; } = false;
    public bool ShowCagedBoxes { get; set; } = true;
    public string ActiveCagedFilter { get; set; } = "ALL"; // ALL / C / A / E / G / D / ""
    public bool ShowThreeNpsBoxes { get; set; } = false;
    public int ActiveThreeNpsPosition { get; set; } = 0;   // 0 = ALL, 1..7 = Pos 1..7
    public bool ShowCapsules { get; set; } = false;
    public ChordShape? HighlightedChord { get; set; }
    public bool JamTargetColorMode { get; set; }
    public string? JamActiveChordName { get; set; }
    public (int StringIdx, int Fret, bool IsAccent)? DrillCurrentSpot { get; set; }
    public (int StringIdx, int Fret)? DrillNextSpot { get; set; }

    public event Action<int, int, int>? FretClicked;          // (stringIndex 0..5, fret 0..17, midiNote)
    public event Action<string>? CagedBadgeClicked;           // ("C" | "A" | "G" | "E" | "D")
    public event Action<int>? ThreeNpsBadgeClicked;           // (1..7)

    public FretboardCanvas()
    {
        MinHeight = 224;
        Height = 244;
        Cursor = Cursors.Hand;
        SnapsToDevicePixels = true;
        ClipToBounds = false;

        _rippleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _rippleTimer.Tick += (_, _) =>
        {
            var now = DateTime.UtcNow;
            _activeRipples.RemoveAll(r => r.ExpireAt <= now);
            if (_activeRipples.Count == 0)
                _rippleTimer.Stop();
            InvalidateVisual();
        };

        _seqAnimTimer = new DispatcherTimer();
        _seqAnimTimer.Tick += (_, _) =>
        {
            if (_seqAnimIndex >= _seqAnimQueue.Count)
            {
                _seqAnimTimer.Stop();
                return;
            }
            var (s, f) = _seqAnimQueue[_seqAnimIndex++];
            AddRipple(s, f, 420);
        };

        Loaded += (_, _) =>
        {
            _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            InvalidateVisual();
        };
    }

    public void Refresh() => InvalidateVisual();

    public void StopAnimations()
    {
        _seqAnimTimer.Stop();
        _seqAnimQueue.Clear();
        _seqAnimIndex = 0;
        DrillCurrentSpot = null;
        DrillNextSpot = null;
    }

    public void SetDrillLookahead((int StringIdx, int Fret, bool IsAccent)? current, (int StringIdx, int Fret)? next)
    {
        DrillCurrentSpot = current;
        DrillNextSpot = next;
        if (current is { } cur)
            AddRipple(cur.StringIdx, cur.Fret, cur.IsAccent ? 460 : 300);
        else
            InvalidateVisual();
    }

    public void FlashNote(int stringIndex, int fret, int durationMs = 360)
    {
        AddRipple(stringIndex, fret, durationMs);
    }

    public void FlashPitchClass(int pitchClass, int durationMs = 520)
    {
        var pc = ((pitchClass % 12) + 12) % 12;
        var expire = DateTime.UtcNow.AddMilliseconds(durationMs);
        var added = false;
        for (var s = 0; s < 6; s++)
        {
            for (var f = Capo; f <= TotalFrets; f++)
            {
                if (((Tuning.OpenMidi[s] + f) % 12 + 12) % 12 == pc)
                {
                    _activeRipples.RemoveAll(r => r.StringIdx == s && r.Fret == f);
                    _activeRipples.Add((s, f, expire));
                    added = true;
                }
            }
        }
        if (added)
        {
            if (!_rippleTimer.IsEnabled)
                _rippleTimer.Start();
            InvalidateVisual();
        }
    }

    public void FlashChordShape(ChordShape? shape, bool arpeggio = false)
    {
        if (shape is null) return;
        var spots = new List<(int Str, int Fret)>(6);
        for (var s = 0; s < 6; s++)
        {
            if (s < shape.Frets.Length && shape.Frets[s] is { } rel)
            {
                var abs = Capo + rel;
                if (abs >= Capo && abs <= TotalFrets)
                    spots.Add((s, abs));
            }
        }
        if (spots.Count == 0) return;
        AnimateSpotSequence(spots, arpeggio ? 155 : 32);
    }

    public void AnimateSpotSequence(IReadOnlyList<(int StringIdx, int Fret)> spots, int stepMs)
    {
        StopAnimations();
        if (spots.Count == 0) return;

        AddRipple(spots[0].StringIdx, spots[0].Fret, 420);
        if (spots.Count == 1) return;

        _seqAnimQueue = spots.ToList();
        _seqAnimIndex = 1;
        _seqAnimTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(24, stepMs));
        _seqAnimTimer.Start();
    }

    private void AddRipple(int stringIndex, int fret, int durationMs)
    {
        var expire = DateTime.UtcNow.AddMilliseconds(durationMs);
        _activeRipples.RemoveAll(r => r.StringIdx == stringIndex && r.Fret == fret);
        _activeRipples.Add((stringIndex, fret, expire));
        if (!_rippleTimer.IsEnabled)
            _rippleTimer.Start();
        InvalidateVisual();
    }

    public List<(int StringIdx, int Fret, int Midi)> GetActiveCagedOrScaleSpots()
    {
        if (ShowThreeNpsBoxes)
        {
            var npsBoxes = MusicTheoryCatalog.ComputeThreeNpsBoxes(TonicPitch, Scale, Tuning, TotalFrets);
            var targetNps = ActiveThreeNpsPosition > 0
                ? npsBoxes.FirstOrDefault(b => b.PositionNumber == ActiveThreeNpsPosition && b.MinFret >= Capo)
                : npsBoxes.FirstOrDefault(b => b.MinFret >= Capo);
            if (targetNps is not null)
            {
                var npsSpots = new List<(int Str, int Fret, int Midi)>(18);
                for (var s = 0; s < 6; s++)
                {
                    foreach (var f in targetNps.StringFrets[s])
                    {
                        if (f >= Capo && f <= TotalFrets)
                            npsSpots.Add((s, f, Tuning.OpenMidi[s] + f));
                    }
                }
                if (npsSpots.Count > 0) return npsSpots;
            }
        }

        var boxes = MusicTheoryCatalog.ComputeCagedBoxes(TonicPitch, Scale.IsMinorFamily, Tuning, TotalFrets);
        if (ShowCagedBoxes && !string.Equals(ActiveCagedFilter, "ALL", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(ActiveCagedFilter))
        {
            var matched = boxes.FirstOrDefault(b =>
                string.Equals(b.ShapeLetter, ActiveCagedFilter, StringComparison.OrdinalIgnoreCase) && b.MinFret >= Capo);
            if (matched is not null)
            {
                var spots = new List<(int Str, int Fret, int Midi)>(14);
                var scaleSet = new HashSet<int>(Scale.Intervals.Select(iv => (TonicPitch + iv) % 12));
                for (var s = 0; s < 6; s++)
                {
                    for (var f = matched.LeftFrets[s]; f <= matched.RightFrets[s]; f++)
                    {
                        if (f < Capo || f > TotalFrets) continue;
                        var midi = Tuning.OpenMidi[s] + f;
                        if (ShowCapsules)
                        {
                            if (f == matched.LeftFrets[s] || f == matched.RightFrets[s])
                                spots.Add((s, f, midi));
                        }
                        else if (scaleSet.Contains(midi % 12))
                        {
                            spots.Add((s, f, midi));
                        }
                    }
                }
                return spots.GroupBy(x => x.Midi).Select(g => g.First()).OrderBy(x => x.Midi).ToList();
            }
        }

        // Play one ascending octave of current scale on realistic lower/middle neck positions
        var startMidi = 40;
        for (var m = Tuning.OpenMidi[0] + Capo; m <= Tuning.OpenMidi[0] + Capo + 12; m++)
        {
            if (m % 12 == TonicPitch)
            {
                startMidi = m;
                break;
            }
        }
        var targets = Scale.Intervals.Select(iv => startMidi + iv).Append(startMidi + 12).ToList();
        var result = new List<(int Str, int Fret, int Midi)>(targets.Count);
        foreach (var midi in targets)
        {
            var found = FindBestSpotForMidi(midi, preferredMinFret: Math.Max(Capo, 0), preferredMaxFret: Math.Min(TotalFrets, Capo + 9));
            if (found is { } sp)
                result.Add((sp.Str, sp.Fret, midi));
        }
        return result;
    }

    public List<int> GetActiveCagedOrScaleMidiSequence() =>
        GetActiveCagedOrScaleSpots().Select(x => x.Midi).ToList();

    public List<(int StringIdx, int Fret, int Midi)> GetEmbeddedCagedTriadSpots()
    {
        if (HighlightedChord is not null)
        {
            var res = new List<(int, int, int)>(6);
            for (var s = 0; s < 6; s++)
            {
                if (s < HighlightedChord.Frets.Length && HighlightedChord.Frets[s] is { } rel)
                {
                    var abs = Capo + rel;
                    if (abs >= Capo && abs <= TotalFrets)
                        res.Add((s, abs, Tuning.OpenMidi[s] + abs));
                }
            }
            return res;
        }

        var boxes = MusicTheoryCatalog.ComputeCagedBoxes(TonicPitch, Scale.IsMinorFamily, Tuning, TotalFrets);
        CagedBoxInstance? target = null;
        if (!string.Equals(ActiveCagedFilter, "ALL", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(ActiveCagedFilter))
            target = boxes.FirstOrDefault(b => string.Equals(b.ShapeLetter, ActiveCagedFilter, StringComparison.OrdinalIgnoreCase) && b.MinFret >= Capo);
        target ??= boxes.FirstOrDefault(b => b.MinFret >= Capo);

        if (target is not null)
        {
            var spots = new List<(int, int, int)>(6);
            for (var s = 0; s < 6; s++)
            {
                if (target.TriadFrets[s] is { } f && f >= Capo && f <= TotalFrets)
                    spots.Add((s, f, Tuning.OpenMidi[s] + f));
            }
            if (spots.Count >= 3)
                return spots;
        }

        var rootName = MusicTheoryCatalog.NoteName(TonicPitch, MusicTheoryCatalog.PreferFlatsForTonic(TonicPitch));
        var chordName = Scale.IsMinorFamily ? rootName + "m" : rootName;
        var shape = ChordCatalog.ByName(chordName);
        if (shape is not null)
        {
            var spots = new List<(int, int, int)>(6);
            for (var s = 0; s < 6; s++)
            {
                if (s < shape.Frets.Length && shape.Frets[s] is { } rel && Capo + rel <= TotalFrets)
                    spots.Add((s, Capo + rel, Tuning.OpenMidi[s] + Capo + rel));
            }
            if (spots.Count >= 3) return spots;
        }
        return [];
    }

    public List<int> GetEmbeddedCagedTriadMidi()
    {
        var spots = GetEmbeddedCagedTriadSpots();
        if (spots.Count > 0) return spots.Select(x => x.Midi).ToList();
        return [48 + TonicPitch, 48 + TonicPitch + (Scale.IsMinorFamily ? 3 : 4), 48 + TonicPitch + 7];
    }

    private (int Str, int Fret)? FindBestSpotForMidi(int midi, int preferredMinFret, int preferredMaxFret)
    {
        (int Str, int Fret)? best = null;
        var bestScore = int.MaxValue;
        for (var s = 0; s < 6; s++)
        {
            var f = midi - Tuning.OpenMidi[s];
            if (f < Capo || f > TotalFrets) continue;
            var dist = f < preferredMinFret ? (preferredMinFret - f) * 2
                : f > preferredMaxFret ? (f - preferredMaxFret) * 2
                : 0;
            var score = dist * 10 + f;
            if (score < bestScore)
            {
                bestScore = score;
                best = (s, f);
            }
        }
        return best;
    }

    private void ComputeGeometry(double width, double height)
    {
        const double leftMargin = 50;
        const double rightMargin = 20;
        const double topMargin = 38;
        const double bottomMargin = 28;

        _boardLeft = leftMargin;
        _boardTop = topMargin;
        _boardWidth = Math.Max(320, width - leftMargin - rightMargin);
        _boardHeight = Math.Max(134, height - topMargin - bottomMargin);

        var weights = new double[TotalFrets + 1];
        weights[0] = 0.76; // open string nut column
        double sum = weights[0];
        for (var f = 1; f <= TotalFrets; f++)
        {
            weights[f] = Math.Pow(0.966, f - 1);
            sum += weights[f];
        }

        var acc = _boardLeft;
        _fretX[0] = acc + _boardWidth * (weights[0] / sum);
        acc = _fretX[0];
        for (var f = 1; f <= TotalFrets; f++)
        {
            acc += _boardWidth * (weights[f] / sum);
            _fretX[f] = acc;
        }

        var padY = 15.0;
        var usableH = Math.Max(68, _boardHeight - padY * 2);
        for (var s = 0; s < 6; s++)
        {
            var rowFromTop = 5 - s;
            _stringY[s] = _boardTop + padY + rowFromTop * (usableH / 5.0);
        }
    }

    private double GetFretCenterX(int fret)
    {
        if (fret <= 0)
            return (_boardLeft + _fretX[0]) / 2.0;
        return (_fretX[fret - 1] + _fretX[fret]) / 2.0;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (ActualWidth < 100 || ActualHeight < 90) return;

        ComputeGeometry(ActualWidth, ActualHeight);
        _badgeRegions.Clear();

        var preferFlats = MusicTheoryCatalog.PreferFlatsForTonic(TonicPitch);

        // ─── 1. Soft Ambient Drop Shadow under Neck ───────────────────────────────
        var woodRect = new Rect(_fretX[0], _boardTop, _fretX[TotalFrets] - _fretX[0], _boardHeight);
        dc.DrawRoundedRectangle(SoftShadowBrush, null, new Rect(woodRect.X + 2, woodRect.Y + 4, woodRect.Width, woodRect.Height), 7, 7);

        // ─── 2. Fretboard Wood Body + Subtle Grain + Edge Binding ─────────────────
        var woodBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = DarkRosewood
                ? [new GradientStop(RoseTop, 0), new GradientStop(RoseMid, 0.52), new GradientStop(RoseBot, 1)]
                : [new GradientStop(MapleTop, 0), new GradientStop(MapleMid, 0.52), new GradientStop(MapleBot, 1)]
        };
        var bindingBrush = DarkRosewood ? RoseBinding : MapleBinding;
        dc.DrawRoundedRectangle(woodBrush, new Pen(bindingBrush, 1.8), woodRect, 6, 6);

        // Subtle horizontal wood grain streaks
        var grainPen = new Pen(DarkRosewood ? RoseGrain : MapleGrain, 1.0);
        for (var i = 1; i <= 7; i++)
        {
            var gy = _boardTop + _boardHeight * (i / 8.0);
            dc.DrawLine(grainPen, new Point(_fretX[0] + 3, gy), new Point(_fretX[TotalFrets] - 3, gy));
        }

        // Open-string headstock/nut column subtle pill
        var openColRect = new Rect(_boardLeft, _boardTop + 1, _fretX[0] - _boardLeft - 2, _boardHeight - 2);
        dc.DrawRoundedRectangle(
            DarkRosewood ? new SolidColorBrush(Color.FromArgb(26, 30, 41, 59)) : new SolidColorBrush(Color.FromArgb(22, 100, 116, 139)),
            null,
            openColRect,
            5,
            5);

        // ─── 3. Pearl Fret Inlay Markers (3, 5, 7, 9, 12 double, 15, 17) ──────────
        var inlayBrush = DarkRosewood ? RoseInlay : MapleInlay;
        var inlayRim = new Pen(DarkRosewood ? new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 0.8);
        int[] singleDots = [3, 5, 7, 9, 15, 17];
        var midY = _boardTop + _boardHeight / 2.0;
        foreach (var f in singleDots)
        {
            if (f <= TotalFrets)
                dc.DrawEllipse(inlayBrush, inlayRim, new Point(GetFretCenterX(f), midY), 5.6, 5.6);
        }
        if (12 <= TotalFrets)
        {
            var x12 = GetFretCenterX(12);
            dc.DrawEllipse(inlayBrush, inlayRim, new Point(x12, (_stringY[4] + _stringY[3]) / 2.0), 5.4, 5.4);
            dc.DrawEllipse(inlayBrush, inlayRim, new Point(x12, (_stringY[2] + _stringY[1]) / 2.0), 5.4, 5.4);
        }

        // ─── 4. Metallic Fret Wires (with specular highlight) + Bone Nut ──────────
        var wireShadowPen = new Pen(new SolidColorBrush(Color.FromArgb(DarkRosewood ? (byte)95 : (byte)55, 15, 23, 42)), 2.2);
        var wireMetalPen = new Pen(DarkRosewood ? new SolidColorBrush(Color.FromRgb(204, 198, 190)) : new SolidColorBrush(Color.FromRgb(176, 166, 150)), 1.5);
        var wireShinePen = new Pen(new SolidColorBrush(Color.FromArgb(145, 255, 255, 255)), 0.7);
        for (var f = 1; f <= TotalFrets; f++)
        {
            var x = _fretX[f];
            dc.DrawLine(wireShadowPen, new Point(x + 1.1, _boardTop + 2), new Point(x + 1.1, _boardTop + _boardHeight - 2));
            dc.DrawLine(wireMetalPen, new Point(x, _boardTop + 2), new Point(x, _boardTop + _boardHeight - 2));
            dc.DrawLine(wireShinePen, new Point(x - 0.5, _boardTop + 2), new Point(x - 0.5, _boardTop + _boardHeight - 2));
        }

        // Bone Nut (ivory block with crisp border)
        var nutRect = new Rect(_fretX[0] - 3.5, _boardTop - 0.5, 7, _boardHeight + 1);
        var nutBrush = new LinearGradientBrush(
            DarkRosewood ? Color.FromRgb(248, 241, 224) : Color.FromRgb(74, 85, 104),
            DarkRosewood ? Color.FromRgb(220, 208, 184) : Color.FromRgb(45, 55, 72),
            0);
        dc.DrawRoundedRectangle(nutBrush, new Pen(new SolidColorBrush(Color.FromArgb(90, 15, 23, 42)), 0.9), nutRect, 2.2, 2.2);

        // ─── 5. Fret Numbers & Left Open-String Badges ────────────────────────────
        for (var f = 1; f <= TotalFrets; f++)
        {
            var isKeyFret = f is 3 or 5 or 7 or 9 or 12 or 15 or 17;
            var cx = GetFretCenterX(f);
            var numText = CreateText(
                f.ToString(CultureInfo.InvariantCulture),
                isKeyFret ? 11.2 : 10.2,
                isKeyFret ? DotTypeface : MonoTypeface,
                isKeyFret ? AppDialogs.TitleBrush : AppDialogs.MutedBrush);
            if (isKeyFret)
            {
                var pill = new Rect(cx - 11, _boardTop + _boardHeight + 5, 22, 16);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(234, 243, 237)), null, pill, 8, 8);
            }
            dc.DrawText(numText, new Point(cx - numText.Width / 2, _boardTop + _boardHeight + 5.5));
        }

        for (var s = 0; s < 6; s++)
        {
            var openName = MusicTheoryCatalog.NoteName(Tuning.OpenMidi[s], preferFlats);
            var badgeRect = new Rect(6, _stringY[s] - 10, 36, 20);
            dc.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                new Pen(new SolidColorBrush(Color.FromRgb(226, 232, 240)), 0.9),
                badgeRect,
                6,
                6);
            var numFt = CreateText($"{6 - s}", 9.5, MonoTypeface, AppDialogs.MutedBrush);
            var noteFt = CreateText(openName, 10.5, DotTypeface, AppDialogs.TitleBrush);
            dc.DrawText(numFt, new Point(badgeRect.Left + 5, _stringY[s] - numFt.Height / 2));
            dc.DrawText(noteFt, new Point(badgeRect.Right - noteFt.Width - 5, _stringY[s] - noteFt.Height / 2));
        }

        // ─── 6. Strings with Subtle Cast Shadow ───────────────────────────────────
        for (var s = 0; s < 6; s++)
        {
            var thickness = 1.0 + (5 - s) * 0.32;
            var y = _stringY[s];
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(45, 15, 23, 42)), thickness),
                new Point(_boardLeft + 2, y + 1.4), new Point(_fretX[TotalFrets] - 1, y + 1.4));

            var stringColor = DarkRosewood
                ? (s <= 2 ? Color.FromRgb(224, 192, 146) : Color.FromRgb(232, 236, 242))
                : (s <= 2 ? Color.FromRgb(148, 114, 72) : Color.FromRgb(112, 122, 134));
            dc.DrawLine(new Pen(new SolidColorBrush(stringColor), thickness),
                new Point(_boardLeft + 2, y), new Point(_fretX[TotalFrets] - 1, y));
        }

        // ─── 7. CAGED 5-Color Interlocking Outlines OR 3NPS 7-Position Runways ────
        var cagedBoxes = (ShowCagedBoxes && !ShowThreeNpsBoxes)
            ? MusicTheoryCatalog.ComputeCagedBoxes(TonicPitch, Scale.IsMinorFamily, Tuning, TotalFrets)
            : [];
        var visibleBoxes = cagedBoxes
            .Where(b => b.MaxFret >= Capo &&
                        (string.Equals(ActiveCagedFilter, "ALL", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(b.ShapeLetter, ActiveCagedFilter, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var threeNpsBoxes = ShowThreeNpsBoxes
            ? MusicTheoryCatalog.ComputeThreeNpsBoxes(TonicPitch, Scale, Tuning, TotalFrets)
            : [];
        var visibleThreeNps = threeNpsBoxes
            .Where(b => b.MaxFret >= Capo && (ActiveThreeNpsPosition == 0 || b.PositionNumber == ActiveThreeNpsPosition))
            .ToList();

        if (ShowThreeNpsBoxes)
        {
            foreach (var nps in visibleThreeNps)
                DrawThreeNpsBoxOutline(dc, nps, isSingleFocused: ActiveThreeNpsPosition > 0);
        }
        else if (ShowCagedBoxes)
        {
            foreach (var box in visibleBoxes)
                DrawCagedBoxOutline(dc, box, isSingleFocused: !string.Equals(ActiveCagedFilter, "ALL", StringComparison.OrdinalIgnoreCase));
        }

        if (ShowCapsules && !ShowThreeNpsBoxes)
        {
            foreach (var box in visibleBoxes)
                DrawCagedCapsules(dc, box);
        }

        // ─── 8. Sleek Capo Clamp Overlay ──────────────────────────────────────────
        if (Capo > 0 && Capo <= TotalFrets)
        {
            var capoX = GetFretCenterX(Capo);
            var veilRect = new Rect(_boardLeft, _boardTop, Math.Max(0, _fretX[Capo - 1] - _boardLeft), _boardHeight);
            dc.DrawRoundedRectangle(CapoVeilBrush, null, veilRect, 5, 5);

            var clampRect = new Rect(capoX - 6.5, _boardTop - 7, 13, _boardHeight + 14);
            dc.DrawRoundedRectangle(ShadowBrush, null, new Rect(clampRect.X + 2, clampRect.Y + 2, clampRect.Width, clampRect.Height), 6, 6);
            dc.DrawRoundedRectangle(CapoBodyBrush, new Pen(CapoAccentBrush, 1.5), clampRect, 6, 6);
            dc.DrawLine(new Pen(CapoAccentBrush, 2.0), new Point(capoX, _boardTop + 5), new Point(capoX, _boardTop + _boardHeight - 5));

            var capoTag = CreateText($"Capo {Capo}", 10, DotTypeface, Brushes.White);
            var capoPill = new Rect(capoX - capoTag.Width / 2 - 7, _boardTop - 27, capoTag.Width + 14, 18);
            dc.DrawRoundedRectangle(CapoBodyBrush, null, capoPill, 9, 9);
            dc.DrawText(capoTag, new Point(capoPill.Left + 7, capoPill.Top + 1.5));
        }

        // ─── 9. Scale / Chord / Jam Target Note Dots ──────────────────────────────
        var scaleSet = new HashSet<int>(Scale.Intervals.Select(iv => (TonicPitch + iv) % 12));
        var triadSet = new HashSet<int>(Scale.TriadIntervals.Select(iv => (TonicPitch + iv) % 12));

        var highlightedFretSet = new HashSet<(int Str, int Fret)>();
        if (HighlightedChord is not null)
        {
            for (var s = 0; s < 6; s++)
            {
                if (s < HighlightedChord.Frets.Length && HighlightedChord.Frets[s] is { } relFret)
                {
                    var absFret = Capo + relFret;
                    if (absFret >= Capo && absFret <= TotalFrets)
                        highlightedFretSet.Add((s, absFret));
                }
            }
        }

        ParsedChord? jamChord = null;
        if (JamTargetColorMode && !string.IsNullOrWhiteSpace(JamActiveChordName) &&
            ChordVoicingEngine.TryParseChord(JamActiveChordName, out var parsedJam))
        {
            jamChord = parsedJam;
        }

        HashSet<(int Str, int Fret)>? focusedBoxNotes = null;
        if (ShowThreeNpsBoxes && ActiveThreeNpsPosition > 0 && visibleThreeNps.Count > 0)
        {
            focusedBoxNotes = [];
            foreach (var nps in visibleThreeNps)
            {
                for (var s = 0; s < 6; s++)
                {
                    foreach (var f in nps.StringFrets[s])
                    {
                        if (f >= Capo && f <= TotalFrets)
                            focusedBoxNotes.Add((s, f));
                    }
                }
            }
        }
        else if (!ShowThreeNpsBoxes &&
                 ShowCagedBoxes &&
                 !string.IsNullOrEmpty(ActiveCagedFilter) &&
                 !string.Equals(ActiveCagedFilter, "ALL", StringComparison.OrdinalIgnoreCase) &&
                 visibleBoxes.Count > 0)
        {
            focusedBoxNotes = [];
            foreach (var box in visibleBoxes)
            {
                for (var s = 0; s < 6; s++)
                {
                    for (var f = box.LeftFrets[s]; f <= box.RightFrets[s]; f++)
                    {
                        if (f >= Capo && f <= TotalFrets)
                            focusedBoxNotes.Add((s, f));
                    }
                }
            }
        }

        var nowUtc = DateTime.UtcNow;

        for (var s = 0; s < 6; s++)
        {
            for (var f = Capo; f <= TotalFrets; f++)
            {
                var midi = Tuning.OpenMidi[s] + f;
                var pc = ((midi % 12) + 12) % 12;
                var isChordSpot = highlightedFretSet.Contains((s, f));
                var isDrillSpot = (DrillCurrentSpot is { } dcs && dcs.StringIdx == s && dcs.Fret == f) ||
                                  (DrillNextSpot is { } dns && dns.StringIdx == s && dns.Fret == f);
                var inScale = scaleSet.Contains(pc);
                var ripple = _activeRipples.FirstOrDefault(r => r.StringIdx == s && r.Fret == f && r.ExpireAt > nowUtc);
                var isRippling = ripple != default;

                if (!inScale && !isChordSpot && !isRippling && !isDrillSpot)
                    continue;

                var cx = GetFretCenterX(f);
                var cy = _stringY[s];
                var isHovered = _hover == (s, f);

                var dimmed = false;
                if (highlightedFretSet.Count > 0 && !isChordSpot)
                    dimmed = true;
                else if (focusedBoxNotes is not null && !focusedBoxNotes.Contains((s, f)))
                    dimmed = true;

                Brush fill;
                Brush textBrush = Brushes.White;
                Pen? borderPen = null;
                double radius;

                if (jamChord is not null)
                {
                    var ivFromChord = ((pc - jamChord.RootPitch) % 12 + 12) % 12;
                    if (pc == jamChord.RootPitch)
                    {
                        fill = JamRootBrush;
                        radius = 12.4;
                    }
                    else if (jamChord.AllPitches.Contains(pc))
                    {
                        fill = JamChordBrush;
                        radius = 11.4;
                    }
                    else if (ivFromChord is 2 or 9 or 10 or 11)
                    {
                        fill = JamColorBrush;
                        radius = 10.4;
                    }
                    else
                    {
                        fill = JamPassBrush;
                        radius = 9.0;
                    }
                }
                else
                {
                    var ivFromTonic = ((pc - TonicPitch) % 12 + 12) % 12;
                    if (ivFromTonic == 0)
                    {
                        fill = RootBrush;
                        radius = 12.6;
                    }
                    else if (triadSet.Contains(pc) && ivFromTonic is 3 or 4)
                    {
                        fill = ThirdBrush;
                        radius = 11.4;
                    }
                    else if (triadSet.Contains(pc) && ivFromTonic is 6 or 7 or 8)
                    {
                        fill = FifthBrush;
                        radius = 11.4;
                    }
                    else
                    {
                        fill = DarkRosewood ? ScaleNoteFillDark : ScaleNoteFillLight;
                        textBrush = ScaleNoteText;
                        borderPen = new Pen(ScaleNoteBorder, 1.3);
                        radius = 9.8;
                    }
                }

                if (isChordSpot)
                    radius = Math.Max(radius, 12.6);
                if (isHovered || isRippling)
                    radius += 1.6;

                var label = LabelMode switch
                {
                    FretboardLabelMode.NoteName => MusicTheoryCatalog.NoteName(pc, preferFlats),
                    FretboardLabelMode.Interval => jamChord is not null
                        ? MusicTheoryCatalog.GetIntervalLabel(pc, jamChord.RootPitch)
                        : MusicTheoryCatalog.GetIntervalLabel(pc, TonicPitch),
                    _ => MusicTheoryCatalog.GetDegreeLabel(pc, TonicPitch)
                };

                if (dimmed && !isRippling && !isHovered && !isDrillSpot)
                    dc.PushOpacity(0.24);

                // Animated expanding pluck ripple ring
                if (isRippling)
                {
                    var remRatio = Math.Clamp((ripple.ExpireAt - nowUtc).TotalMilliseconds / 420.0, 0, 1);
                    var expand = (1.0 - remRatio) * 10.5;
                    var alpha = (byte)(remRatio * 170);
                    dc.DrawEllipse(
                        new SolidColorBrush(Color.FromArgb((byte)(alpha / 2), RootColor.R, RootColor.G, RootColor.B)),
                        new Pen(new SolidColorBrush(Color.FromArgb(alpha, RootColor.R, RootColor.G, RootColor.B)), 2.0),
                        new Point(cx, cy),
                        radius + 2.5 + expand,
                        radius + 2.5 + expand);
                }
                else if (isChordSpot)
                {
                    dc.DrawEllipse(null, new Pen(Brushes.White, 2.6), new Point(cx, cy), radius + 2.4, radius + 2.4);
                }

                // Micro drop shadow for tactile 3D button feel
                dc.DrawEllipse(ShadowBrush, null, new Point(cx, cy + 1.5), radius, radius);
                dc.DrawEllipse(fill, borderPen, new Point(cx, cy), radius, radius);

                // Subtle top gloss highlight on colored badges
                if (borderPen is null)
                {
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)), null,
                        new Point(cx, cy - radius * 0.34), radius * 0.56, radius * 0.28);
                }

                var fontSize = label.Length >= 2 ? 9.5 : 11.2;
                var ft = CreateText(label, fontSize, DotTypeface, textBrush);
                dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));

                if (dimmed && !isRippling && !isHovered && !isDrillSpot)
                    dc.Pop();
            }
        }

        // ─── 9.5 Drill Lookahead Trajectory Arc + Next Ring + Current Accent Halo ─
        if (DrillCurrentSpot is { } curSpot && curSpot.Fret >= Capo && curSpot.Fret <= TotalFrets)
        {
            var cx1 = GetFretCenterX(curSpot.Fret);
            var cy1 = _stringY[curSpot.StringIdx];

            if (DrillNextSpot is { } nxtSpot && nxtSpot.Fret >= Capo && nxtSpot.Fret <= TotalFrets &&
                (nxtSpot.StringIdx != curSpot.StringIdx || nxtSpot.Fret != curSpot.Fret))
            {
                var cx2 = GetFretCenterX(nxtSpot.Fret);
                var cy2 = _stringY[nxtSpot.StringIdx];

                // Next-note ghost preview ring
                var nextPen = new Pen(new SolidColorBrush(Color.FromArgb(210, 37, 99, 235)), 2.0) { DashStyle = DashStyles.Dash };
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(30, 37, 99, 235)), nextPen, new Point(cx2, cy2), 15.5, 15.5);

                // Curved trajectory arrow from current note -> next note
                var dx = cx2 - cx1;
                var dy = cy2 - cy1;
                var len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 22)
                {
                    var ux = dx / len;
                    var uy = dy / len;
                    var pStart = new Point(cx1 + ux * 14.5, cy1 + uy * 14.5);
                    var pEnd = new Point(cx2 - ux * 16.5, cy2 - uy * 16.5);
                    var bend = Math.Clamp(len * 0.18, 10, 26);
                    var ctrl = new Point((pStart.X + pEnd.X) / 2 - uy * bend, (pStart.Y + pEnd.Y) / 2 - Math.Abs(ux) * bend);

                    var arcGeo = new StreamGeometry();
                    using (var ctx = arcGeo.Open())
                    {
                        ctx.BeginFigure(pStart, isFilled: false, isClosed: false);
                        ctx.QuadraticBezierTo(ctrl, pEnd, isStroked: true, isSmoothJoin: true);
                    }
                    arcGeo.Freeze();
                    var arcPen = new Pen(new SolidColorBrush(Color.FromArgb(195, 37, 99, 235)), 2.0) { DashStyle = DashStyles.Dash };
                    dc.DrawGeometry(null, arcPen, arcGeo);

                    // Arrowhead at pEnd
                    var tx = pEnd.X - ctrl.X;
                    var ty = pEnd.Y - ctrl.Y;
                    var tLen = Math.Max(0.001, Math.Sqrt(tx * tx + ty * ty));
                    tx /= tLen;
                    ty /= tLen;
                    var headPen = new Pen(new SolidColorBrush(Color.FromArgb(225, 37, 99, 235)), 2.1);
                    dc.DrawLine(headPen, pEnd, new Point(pEnd.X - tx * 7 - ty * 4.2, pEnd.Y - ty * 7 + tx * 4.2));
                    dc.DrawLine(headPen, pEnd, new Point(pEnd.X - tx * 7 + ty * 4.2, pEnd.Y - ty * 7 - tx * 4.2));
                }
            }

            // Current active note focus ring / blazing gold accent halo
            if (curSpot.IsAccent)
            {
                dc.DrawEllipse(
                    new SolidColorBrush(Color.FromArgb(55, 245, 158, 11)),
                    new Pen(new SolidColorBrush(Color.FromRgb(245, 158, 11)), 3.0),
                    new Point(cx1, cy1),
                    17.5,
                    17.5);
                var accBadge = CreateText(">", 12, DotTypeface, new SolidColorBrush(Color.FromRgb(217, 119, 6)));
                dc.DrawText(accBadge, new Point(cx1 - accBadge.Width / 2, cy1 - 28));
            }
            else
            {
                dc.DrawEllipse(
                    null,
                    new Pen(Brushes.White, 2.8),
                    new Point(cx1, cy1),
                    15.0,
                    15.0);
            }
        }

        // ─── 10. Floating Hover Glass Pill Tooltip ────────────────────────────────
        if (_hover is { } hv && hv.Fret >= Capo)
        {
            var hcx = GetFretCenterX(hv.Fret);
            var hcy = _stringY[hv.StringIdx];
            var hMidi = Tuning.OpenMidi[hv.StringIdx] + hv.Fret;
            var hPc = ((hMidi % 12) + 12) % 12;

            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(165, 47, 107, 72)), 1.8), new Point(hcx, hcy), 15.2, 15.2);

            var noteName = MusicTheoryCatalog.MidiToNoteWithOctave(hMidi, preferFlats);
            var deg = MusicTheoryCatalog.GetDegreeLabel(hPc, TonicPitch);
            var iv = MusicTheoryCatalog.GetIntervalLabel(hPc, jamChord?.RootPitch ?? TonicPitch);
            var tipStr = $"{noteName}  ·  {deg}级 ({iv})  ·  {6 - hv.StringIdx}弦{hv.Fret}品";
            var tipFt = CreateText(tipStr, 10.5, LabelTypeface, Brushes.White);

            var pillW = tipFt.Width + 16;
            var pillH = 21.0;
            var pillX = Math.Clamp(hcx - pillW / 2, _boardLeft, _fretX[TotalFrets] - pillW);
            var pillY = hcy - 36;
            if (pillY < 2) pillY = hcy + 18;

            var pillRect = new Rect(pillX, pillY, pillW, pillH);
            dc.DrawRoundedRectangle(SoftShadowBrush, null, new Rect(pillRect.X + 1, pillRect.Y + 2, pillRect.Width, pillRect.Height), 10, 10);
            dc.DrawRoundedRectangle(HoverPillBg, null, pillRect, 10, 10);
            dc.DrawText(tipFt, new Point(pillRect.Left + 8, pillRect.Top + 2.5));
        }
    }

    private void DrawCagedBoxOutline(DrawingContext dc, CagedBoxInstance box, bool isSingleFocused)
    {
        var color = (Color)ColorConverter.ConvertFromString(box.ColorHex);
        var isHoveredBadge = string.Equals(_hoverBadgeLetter, box.ShapeLetter, StringComparison.OrdinalIgnoreCase);

        var fillAlpha = isSingleFocused ? (byte)36 : (isHoveredBadge ? (byte)34 : (byte)16);
        var strokeAlpha = isSingleFocused ? (byte)220 : (isHoveredBadge ? (byte)215 : (byte)135);
        var strokeWidth = isSingleFocused || isHoveredBadge ? 2.4 : 1.7;

        var fillBrush = new SolidColorBrush(Color.FromArgb(fillAlpha, color.R, color.G, color.B));
        var strokePen = new Pen(new SolidColorBrush(Color.FromArgb(strokeAlpha, color.R, color.G, color.B)), strokeWidth)
        {
            LineJoin = PenLineJoin.Round
        };

        var halfBand = (_stringY[0] - _stringY[1]) * 0.45;
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            var first = true;
            for (var s = 5; s >= 0; s--)
            {
                var rx = GetFretCenterX(Math.Clamp(box.RightFrets[s], 0, TotalFrets)) + 14.0;
                var yTop = _stringY[s] - halfBand;
                var yBot = _stringY[s] + halfBand;
                if (first)
                {
                    ctx.BeginFigure(new Point(rx, yTop), isFilled: true, isClosed: true);
                    first = false;
                }
                else
                {
                    ctx.LineTo(new Point(rx, yTop), true, true);
                }
                ctx.LineTo(new Point(rx, yBot), true, true);
            }

            for (var s = 0; s <= 5; s++)
            {
                var lx = GetFretCenterX(Math.Clamp(box.LeftFrets[s], 0, TotalFrets)) - 14.0;
                var yBot = _stringY[s] + halfBand;
                var yTop = _stringY[s] - halfBand;
                ctx.LineTo(new Point(lx, yBot), true, true);
                ctx.LineTo(new Point(lx, yTop), true, true);
            }
        }
        geom.Freeze();
        dc.DrawGeometry(fillBrush, strokePen, geom);

        // Clickable CAGED Shape Badge Pill above the neck
        var centerX = (GetFretCenterX(Math.Clamp(box.MinFret, 0, TotalFrets)) + GetFretCenterX(Math.Clamp(box.MaxFret, 0, TotalFrets))) / 2.0;
        var badgeFt = CreateText($"{box.ShapeLetter} 形", 10.5, DotTypeface, Brushes.White);
        var pillW = badgeFt.Width + 16;
        var pillH = isHoveredBadge || isSingleFocused ? 21.0 : 19.0;
        var pillRect = new Rect(centerX - pillW / 2, _boardTop - 27 - (pillH - 19.0) / 2, pillW, pillH);

        _badgeRegions.Add(new BadgeHitRegion(box.ShapeLetter, pillRect));

        if (isHoveredBadge || isSingleFocused)
            dc.DrawRoundedRectangle(SoftShadowBrush, null, new Rect(pillRect.X, pillRect.Y + 2, pillRect.Width, pillRect.Height), 10, 10);

        dc.DrawRoundedRectangle(
            new SolidColorBrush(color),
            isSingleFocused || isHoveredBadge ? new Pen(Brushes.White, 1.4) : null,
            pillRect,
            10,
            10);
        dc.DrawText(badgeFt, new Point(pillRect.Left + 8, pillRect.Top + (pillH - badgeFt.Height) / 2));
    }

    private void DrawThreeNpsBoxOutline(DrawingContext dc, ThreeNpsBoxInstance box, bool isSingleFocused)
    {
        var color = (Color)ColorConverter.ConvertFromString(box.ColorHex);
        var badgeKey = $"3NPS:{box.PositionNumber}";
        var isHoveredBadge = string.Equals(_hoverBadgeLetter, badgeKey, StringComparison.OrdinalIgnoreCase);

        var fillAlpha = isSingleFocused ? (byte)36 : (isHoveredBadge ? (byte)34 : (byte)14);
        var strokeAlpha = isSingleFocused ? (byte)220 : (isHoveredBadge ? (byte)215 : (byte)125);
        var strokeWidth = isSingleFocused || isHoveredBadge ? 2.4 : 1.5;

        var fillBrush = new SolidColorBrush(Color.FromArgb(fillAlpha, color.R, color.G, color.B));
        var strokePen = new Pen(new SolidColorBrush(Color.FromArgb(strokeAlpha, color.R, color.G, color.B)), strokeWidth)
        {
            LineJoin = PenLineJoin.Round
        };

        var halfBand = (_stringY[0] - _stringY[1]) * 0.44;
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            var first = true;
            for (var s = 5; s >= 0; s--)
            {
                var rx = GetFretCenterX(Math.Clamp(box.RightFrets[s], 0, TotalFrets)) + 13.5;
                var yTop = _stringY[s] - halfBand;
                var yBot = _stringY[s] + halfBand;
                if (first)
                {
                    ctx.BeginFigure(new Point(rx, yTop), isFilled: true, isClosed: true);
                    first = false;
                }
                else
                {
                    ctx.LineTo(new Point(rx, yTop), true, true);
                }
                ctx.LineTo(new Point(rx, yBot), true, true);
            }

            for (var s = 0; s <= 5; s++)
            {
                var lx = GetFretCenterX(Math.Clamp(box.LeftFrets[s], 0, TotalFrets)) - 13.5;
                var yBot = _stringY[s] + halfBand;
                var yTop = _stringY[s] - halfBand;
                ctx.LineTo(new Point(lx, yBot), true, true);
                ctx.LineTo(new Point(lx, yTop), true, true);
            }
        }
        geom.Freeze();
        dc.DrawGeometry(fillBrush, strokePen, geom);

        var anchorX = (GetFretCenterX(Math.Clamp(box.LeftFrets[0], 0, TotalFrets)) + GetFretCenterX(Math.Clamp(box.RightFrets[5], 0, TotalFrets))) / 2.0;
        var label = isSingleFocused ? $"{box.PositionNumber}把 · {box.DegreeStartLabel}" : $"{box.PositionNumber}把";
        var badgeFt = CreateText(label, 10.2, DotTypeface, Brushes.White);
        var pillW = badgeFt.Width + 14;
        var pillH = isHoveredBadge || isSingleFocused ? 20.5 : 18.5;
        var pillRect = new Rect(anchorX - pillW / 2, _boardTop - 27 - (pillH - 18.5) / 2, pillW, pillH);

        _badgeRegions.Add(new BadgeHitRegion(badgeKey, pillRect));
        dc.DrawRoundedRectangle(
            new SolidColorBrush(color),
            isSingleFocused || isHoveredBadge ? new Pen(Brushes.White, 1.4) : null,
            pillRect,
            9.5,
            9.5);
        dc.DrawText(badgeFt, new Point(pillRect.Left + 7, pillRect.Top + (pillH - badgeFt.Height) / 2));
    }

    private void DrawCagedCapsules(DrawingContext dc, CagedBoxInstance box)
    {
        var color = (Color)ColorConverter.ConvertFromString(box.ColorHex);
        var capFill = new SolidColorBrush(Color.FromArgb(44, color.R, color.G, color.B));
        var capPen = new Pen(new SolidColorBrush(Color.FromArgb(165, color.R, color.G, color.B)), 1.5);

        for (var s = 0; s < 6; s++)
        {
            var lf = box.LeftFrets[s];
            var rf = box.RightFrets[s];
            if (lf < Capo || rf > TotalFrets || rf < lf) continue;
            var x1 = GetFretCenterX(lf) - 14.0;
            var x2 = GetFretCenterX(rf) + 14.0;
            var y = _stringY[s];
            var rect = new Rect(x1, y - 13.0, Math.Max(28, x2 - x1), 26);
            dc.DrawRoundedRectangle(capFill, capPen, rect, 13.0, 13.0);
        }
    }

    private string? HitTestBadge(Point pt)
    {
        foreach (var reg in _badgeRegions)
        {
            if (reg.Bounds.Contains(pt))
                return reg.ShapeLetter;
        }
        return null;
    }

    private (int StringIdx, int Fret)? HitTest(Point pt)
    {
        if (pt.Y < _boardTop - 8 || pt.Y > _boardTop + _boardHeight + 8 || pt.X < _boardLeft || pt.X > _fretX[TotalFrets])
            return null;

        var bestStr = -1;
        var bestDist = double.MaxValue;
        for (var s = 0; s < 6; s++)
        {
            var d = Math.Abs(pt.Y - _stringY[s]);
            if (d < bestDist)
            {
                bestDist = d;
                bestStr = s;
            }
        }
        if (bestStr < 0 || bestDist > 19) return null;

        var fret = 0;
        if (pt.X >= _fretX[0])
        {
            fret = TotalFrets;
            for (var f = 1; f <= TotalFrets; f++)
            {
                if (pt.X <= _fretX[f])
                {
                    fret = f;
                    break;
                }
            }
        }
        return (bestStr, fret);
    }

    private void TriggerSpotPluck((int StringIdx, int Fret) spot)
    {
        if (spot.Fret < Capo) return;
        AddRipple(spot.StringIdx, spot.Fret, 360);
        var midi = Tuning.OpenMidi[spot.StringIdx] + spot.Fret;
        FretClicked?.Invoke(spot.StringIdx, spot.Fret, midi);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos = e.GetPosition(this);

        if (_isDraggingPluck && e.LeftButton == MouseButtonState.Pressed)
        {
            var dragSpot = HitTest(pos);
            if (dragSpot is { } sp && sp != _lastDragSpot && sp.Fret >= Capo)
            {
                _lastDragSpot = sp;
                _hover = sp;
                TriggerSpotPluck(sp);
            }
            return;
        }

        var badge = HitTestBadge(pos);
        var next = badge is null ? HitTest(pos) : null;
        if (next != _hover || !string.Equals(badge, _hoverBadgeLetter, StringComparison.Ordinal))
        {
            _hover = next;
            _hoverBadgeLetter = badge;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _isDraggingPluck = false;
        _lastDragSpot = null;
        if (_hover is not null || _hoverBadgeLetter is not null)
        {
            _hover = null;
            _hoverBadgeLetter = null;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var pos = e.GetPosition(this);

        if (HitTestBadge(pos) is { } badgeLetter)
        {
            if (badgeLetter.StartsWith("3NPS:", StringComparison.Ordinal) &&
                int.TryParse(badgeLetter.AsSpan(5), out var npsPos))
            {
                ThreeNpsBadgeClicked?.Invoke(npsPos);
            }
            else
            {
                CagedBadgeClicked?.Invoke(badgeLetter);
            }
            e.Handled = true;
            return;
        }

        var hit = HitTest(pos);
        if (hit is { } spot && spot.Fret >= Capo)
        {
            _isDraggingPluck = true;
            _lastDragSpot = spot;
            CaptureMouse();
            TriggerSpotPluck(spot);
            e.Handled = true;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_isDraggingPluck)
        {
            _isDraggingPluck = false;
            _lastDragSpot = null;
            ReleaseMouseCapture();
        }
    }

    private FormattedText CreateText(string text, double size, Typeface typeface, Brush brush) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, brush, _pixelsPerDip);

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
