using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Xianpu.Desktop;

public sealed record LyricInlineTarget(
    int LineIndex,
    int VerseIndex,
    int CharIndex,
    Rect Bounds,
    double MaxWidth,
    bool IsStacked);

public sealed partial class ScoreSurface : FrameworkElement
{
    private const double FlowPaperWidth = 1120;
    private const double FlowMargin = 56;
    private const double PhraseGap = 48;
    private const double StackedColumnGap = 52;
    private const int MaxPhrasesPerRow = 3;
    private const double SingleColumnPaperWidth = 920;
    private const double TwoColumnPaperWidth = 1120;
    private const double SingleColumnMargin = 60;
    private const double TwoColumnMargin = 52;
    private const double TwoColumnGap = 48;
    private const double HeaderHeight = 96;
    private const double LyricFontSize = 19;
    private const double ChordFontSize = 16.5;
    private const double LyricLineHeight = 34;
    private const double AltLyricLineHeight = 29;
    private const double RowBottomGap = 14;
    private const double StackedRowBottomGap = 24;
    private const double PartHeaderHeight = 34;
    private const double PartHeaderTopGap = 18;
    private const double VerseBadgeWidth = 28;
    private const double StackedHalfCellWidth = 12.5;
    private const double StackedFullCellWidth = 25.0;
    private const double NameMarkerHeight = 28;
    private const double DiagramMarkerHeight = 74;
    private const double DiagramMarkerScale = 0.62;
    private const double DiagramWidth = 96 * DiagramMarkerScale;
    private const double DiagramHeight = 88 * DiagramMarkerScale;
    private const double DiagramMinGap = 14.0;
    private const double DiagramCharTracking = 2.0;

    private static readonly Typeface LyricTypeface = new(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface ChordTypeface = new(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private static readonly Brush Ink = Freeze(new SolidColorBrush(Color.FromRgb(42, 47, 41)));
    private static readonly Brush AltLyricInk = Freeze(new SolidColorBrush(Color.FromRgb(63, 71, 64)));
    private static readonly Brush Green = Freeze(new SolidColorBrush(Color.FromRgb(49, 92, 57)));
    private static readonly Brush SelectedChordInk = Freeze(new SolidColorBrush(Color.FromRgb(30, 68, 39)));
    private static readonly Brush Quiet = Freeze(new SolidColorBrush(Color.FromRgb(126, 134, 122)));
    private static readonly Brush Rule = Freeze(new SolidColorBrush(Color.FromRgb(228, 232, 224)));
    private static readonly Brush ColumnRule = Freeze(new SolidColorBrush(Color.FromRgb(236, 239, 233)));
    private static readonly Brush PartBadgeFill = Freeze(new SolidColorBrush(Color.FromRgb(234, 243, 237)));
    private static readonly Brush PartBadgeBorder = Freeze(new SolidColorBrush(Color.FromRgb(199, 223, 206)));
    private static readonly Brush RepeatBadgeFill = Freeze(new SolidColorBrush(Color.FromRgb(47, 107, 72)));
    private static readonly Brush RefLineFill = Freeze(new SolidColorBrush(Color.FromRgb(244, 247, 244)));
    private static readonly Brush RefLineBorder = Freeze(new SolidColorBrush(Color.FromRgb(213, 226, 215)));
    private static readonly Brush HoverCharFill = Freeze(new SolidColorBrush(Color.FromArgb(46, 108, 158, 114)));
    private static readonly Brush HoverSlotFill = Freeze(new SolidColorBrush(Color.FromArgb(32, 108, 158, 114)));
    private static readonly Brush HoverSlotBorder = Freeze(new SolidColorBrush(Color.FromArgb(120, 96, 142, 102)));
    private static readonly Brush CaretCharFill = Freeze(new SolidColorBrush(Color.FromArgb(65, 79, 134, 88)));
    private static readonly Brush CaretBorder = Freeze(new SolidColorBrush(Color.FromRgb(74, 124, 82)));
    private static readonly Brush SelectedChordFill = Freeze(new SolidColorBrush(Color.FromRgb(233, 243, 234)));
    private static readonly Brush SelectedChordBorder = Freeze(new SolidColorBrush(Color.FromRgb(94, 140, 100)));
    private static readonly Pen CharSlotTickPen = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(75, 110, 140, 115))), 0.8));
    private static readonly Pen PartRulePen = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(229, 235, 227))), 1));
    private static readonly Pen VerseBracketPen = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(184, 206, 188))), 1.5));
    private static readonly Pen ChordAnchorUnderlinePen = FreezeRoundedPen(Freeze(new SolidColorBrush(Color.FromRgb(47, 107, 72))), 1.8);
    private static readonly Pen SelectedAnchorUnderlinePen = FreezeRoundedPen(SelectedChordInk, 2.4);
    private static readonly Pen DiagramConnectorPen = FreezeRoundedPen(Freeze(new SolidColorBrush(Color.FromRgb(184, 206, 188))), 1.0);
    private static readonly Pen ColumnRulePen = FreezePen(new Pen(ColumnRule, 1));

    private readonly List<RowLayout> _rows = [];
    private readonly List<TabSectionLayout> _tabLayouts = [];
    private double _pixelsPerDip = 1;
    private int _columnCount = 1;
    private double _pageMarginCurrent = FlowMargin;
    private double _twoColDividerTop = HeaderHeight + 4;
    private double _twoColDividerBottom = HeaderHeight + 4;
    private Point? _pressPoint;
    private Hit? _pressedChord;
    private bool _draggingChord;
    private ChordAnchor? _dragTarget;
    private ChordAnchor? _caret;
    private ChordAnchor? _hoverAnchor;
    private (int LineIndex, int VerseIndex)? _hoverLyricVerse;
    private string? _hoverTabSectionId;
    private Hit? _selectedPlaced;
    private ChordShape? _externalDragShape;
    private ChordAnchor? _externalDragTarget;
    private LyricLine? _copiedLineSnapshot;

    public SongModel? Song { get; private set; }
    public string? SelectedShapeId { get; set; }
    public string? ActiveChordBrush { get; set; }
    public bool ContinuousPaintMode { get; set; }
    public bool LyricEditMode { get; set; }
    public (int LineIndex, int VerseIndex)? ActiveInlineLyricEdit { get; set; }
    public int? SelectedLineIndex { get; private set; }
    public bool IsExporting { get; set; }
    public bool HideHeaderOnScreen { get; set; } = true;
    private double EffectiveHeaderHeight => (HideHeaderOnScreen && !IsExporting) ? 14.0 : HeaderHeight;
    public bool HasSelectedChord => _selectedPlaced is not null;
    public ChordAnchor? Caret => _caret;
    public double PaperWidthCurrent { get; private set; } = FlowPaperWidth;
    public int? ActiveLineIndex => _caret?.LineIndex ?? SelectedLineIndex;
    public bool HasCopiedLineChords => _copiedLineSnapshot is { Chords.Count: > 0 };
    public bool SelectedLineHasChords =>
        Song is not null &&
        ActiveLineIndex is { } idx &&
        idx >= 0 &&
        idx < Song.Lines.Count &&
        !Song.Lines[idx].IsReferenceLine &&
        Song.Lines[idx].Chords.Count > 0;
    public string CopiedLineChordsSummary =>
        _copiedLineSnapshot is null ? "" : FormatChordSummary(_copiedLineSnapshot);

    // FrameworkElement has no Background property; this property makes the full paper a hit-test target.
    public Brush Background { get; set; } = Brushes.White;

    public event Action? Changed;
    public event Action? SelectionChanged;
    public event Action<int>? LineEditRequested;
    public event Action<LyricInlineTarget>? InlineLyricEditRequested;
    public event Action<int>? LineSelected;
    public event Action<ChordAnchor, Rect, string?>? CharacterSlotClicked;
    public event Action<ChordAnchor, Rect, string>? QuickTypeStarted;
    public event Action<TabSection>? TabSectionEditRequested;
    public event Action<TabSection>? TabSectionDuplicateRequested;
    public event Action<TabSection>? TabSectionDeleteRequested;
    public event Action<TabSectionKind, int>? TabSectionInsertRequested;
    public event Action<int>? AddAltLyricRequested;
    public event Action? PartArrangeRequested;
    public event Action<int>? InsertPartAfterRequested;
    public event Action<string>? StatusMessageRequested;
    public event Action? EscapePressed;

    public bool IsPointInteractive(Point point) =>
        FindChord(point) is not null ||
        GetAnchorAt(point) is not null ||
        GetLyricTargetAt(point) is not null ||
        FindTabSection(point) is not null ||
        FindReferenceRow(point) is not null ||
        (LyricEditMode && Song is { Lines.Count: 0 });

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    private static Pen FreezePen(Pen pen)
    {
        pen.Freeze();
        return pen;
    }

    private static Pen FreezeRoundedPen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }

    public ScoreSurface()
    {
        Width = PaperWidthCurrent;
        Height = 460;
        Focusable = true;
        AllowDrop = true;
        SnapsToDevicePixels = true;
        InputMethod.SetIsInputMethodEnabled(this, false);
        Loaded += (_, _) =>
        {
            _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            Rebuild();
        };
    }

    public void SetSong(SongModel? song)
    {
        Song = song;
        SelectedShapeId = null;
        ActiveChordBrush = null;
        SelectedLineIndex = null;
        _selectedPlaced = null;
        _caret = null;
        _hoverAnchor = null;
        _hoverLyricVerse = null;
        _hoverTabSectionId = null;
        ActiveInlineLyricEdit = null;
        SelectionChanged?.Invoke();
        Rebuild();
    }

    public void Rebuild()
    {
        _rows.Clear();
        _tabLayouts.Clear();
        if (Song is null)
        {
            _columnCount = 1;
            PaperWidthCurrent = FlowPaperWidth;
            _pageMarginCurrent = FlowMargin;
            Width = PaperWidthCurrent;
            Height = 420;
            InvalidateVisual();
            return;
        }

        if (Song.ColumnMode == 2)
        {
            _columnCount = 2;
            PaperWidthCurrent = TwoColumnPaperWidth;
            _pageMarginCurrent = TwoColumnMargin;
        }
        else if (Song.ColumnMode == 1)
        {
            _columnCount = 1;
            PaperWidthCurrent = SingleColumnPaperWidth;
            _pageMarginCurrent = SingleColumnMargin;
        }
        else
        {
            _columnCount = 1;
            PaperWidthCurrent = FlowPaperWidth;
            _pageMarginCurrent = FlowMargin;
        }
        Width = PaperWidthCurrent;

        var showTabs = Song.ShowTabSections && Song.TabSections.Count > 0;
        if (Song.Lines.Count == 0 && !showTabs)
        {
            Height = 320;
            InvalidateVisual();
            return;
        }

        var markerHeight = Song.ChordDisplayMode == "diagram" ? DiagramMarkerHeight : NameMarkerHeight;
        var usableWidth = PaperWidthCurrent - _pageMarginCurrent * 2;
        var columnWidth = _columnCount == 2
            ? (usableWidth - TwoColumnGap) / 2
            : usableWidth;

        var introTabs = showTabs
            ? Song.TabSections.Where(t => t.Kind == TabSectionKind.Intro || t.InsertAfterLineIndex < 0).ToList()
            : [];
        var interludesByLine = showTabs
            ? Song.TabSections
                .Where(t => t.Kind == TabSectionKind.Interlude && t.InsertAfterLineIndex >= 0 && t.InsertAfterLineIndex < Song.Lines.Count)
                .GroupBy(t => t.InsertAfterLineIndex)
                .ToDictionary(g => g.Key, g => g.ToList())
            : new Dictionary<int, List<TabSection>>();
        var outroTabs = showTabs
            ? Song.TabSections.Where(t => t.Kind == TabSectionKind.Outro || (t.Kind == TabSectionKind.Interlude && t.InsertAfterLineIndex >= Song.Lines.Count)).ToList()
            : [];

        var topY = EffectiveHeaderHeight;
        foreach (var tab in introTabs)
        {
            var tabHeight = TabPainter.MeasureSectionHeight(tab, usableWidth);
            _tabLayouts.Add(new TabSectionLayout(tab, new Rect(_pageMarginCurrent, topY, usableWidth, tabHeight)));
            topY += tabHeight;
        }

        _twoColDividerTop = topY + 4;

        var isHorizontalFlow = Song.ColumnMode != 1 && Song.ColumnMode != 2;
        int leftCount;
        if (_columnCount != 2 || Song.Lines.Count <= 1)
        {
            leftCount = Song.Lines.Count;
        }
        else
        {
            var estDiagramMode = Song.ChordDisplayMode == "diagram";
            var estHeights = new double[Song.Lines.Count];
            var totalEstHeight = 0d;
            for (var i = 0; i < Song.Lines.Count; i++)
            {
                var l = Song.Lines[i];
                var hasHdr = !l.IsReferenceLine && (!string.IsNullOrWhiteSpace(l.PartTitle) || l.RepeatCount > 1);
                var h = hasHdr ? PartHeaderHeight + (i > 0 ? PartHeaderTopGap : 0d) : 0d;
                if (l.IsReferenceLine)
                {
                    h += 34 + RowBottomGap;
                }
                else if (l.HasStackedLyrics)
                {
                    var wrapped = WrapStacked(l, Math.Max(120, columnWidth - VerseBadgeWidth), estDiagramMode);
                    h += wrapped.Count * (markerHeight + LyricLineHeight + l.AltLyrics.Count * AltLyricLineHeight) + StackedRowBottomGap;
                }
                else
                {
                    var wrapped = Wrap(l, columnWidth, estDiagramMode);
                    h += wrapped.Count * (markerHeight + LyricLineHeight) + RowBottomGap;
                }
                if (interludesByLine.TryGetValue(i, out var lineInterludes))
                {
                    h += 6;
                    foreach (var tab in lineInterludes)
                        h += TabPainter.MeasureSectionHeight(tab, columnWidth);
                }
                estHeights[i] = h;
                totalEstHeight += h;
            }

            leftCount = 1;
            var bestScore = double.MaxValue;
            var leftAcc = 0d;
            for (var split = 1; split < Song.Lines.Count; split++)
            {
                leftAcc += estHeights[split - 1];
                var rightAcc = totalEstHeight - leftAcc;
                var nextLine = Song.Lines[split];
                var startsPart = !nextLine.IsReferenceLine && (!string.IsNullOrWhiteSpace(nextLine.PartTitle) || nextLine.RepeatCount > 1);
                var score = Math.Abs(leftAcc - rightAcc) - (startsPart ? 60.0 : 0.0);
                if (score < bestScore)
                {
                    bestScore = score;
                    leftCount = split;
                }
            }
        }

        var yLeft = topY;
        var yRight = topY;

        var stackedHalfWidth = (usableWidth - StackedColumnGap) / 2;
        var curX = _pageMarginCurrent;
        var curY = topY;
        var phrasesInRow = 0;
        var rowMaxHeight = 0d;
        var rowIsStacked = false;
        var rowHeaderReserve = 0d;

        void FlushFlowRow()
        {
            if (phrasesInRow <= 0) return;
            curY += rowMaxHeight;
            curX = _pageMarginCurrent;
            phrasesInRow = 0;
            rowMaxHeight = 0;
            rowIsStacked = false;
            rowHeaderReserve = 0;
        }

        for (var index = 0; index < Song.Lines.Count; index++)
        {
            var line = Song.Lines[index];
            var col = _columnCount == 2 && index >= leftCount ? 1 : 0;
            var hasPartHeader = !line.IsReferenceLine && (!string.IsNullOrWhiteSpace(line.PartTitle) || line.RepeatCount > 1);
            var isStacked = !line.IsReferenceLine && line.HasStackedLyrics;

            var currentColTop = isHorizontalFlow
                ? curY
                : (col == 0 ? yLeft : yRight);
            var headerTopGap = hasPartHeader && currentColTop > topY + 0.5 ? PartHeaderTopGap : 0d;
            var headerReserve = hasPartHeader ? headerTopGap + PartHeaderHeight : 0d;

            List<TextChunk> chunks;
            var height = headerReserve;

            var isDiagramMode = Song.ChordDisplayMode == "diagram";

            if (line.IsReferenceLine)
            {
                chunks = [new TextChunk(0, 0, line.Text.Length, line.Text, [], [], 0, null, headerReserve, headerReserve)];
                height += 34 + RowBottomGap;
            }
            else if (isStacked)
            {
                var availableWidth = Math.Max(120, columnWidth - VerseBadgeWidth);
                chunks = WrapStacked(line, availableWidth, isDiagramMode);
                for (var i = 0; i < chunks.Count; i++)
                {
                    var chunkTop = height;
                    var lyricOffset = chunkTop + markerHeight;
                    chunks[i] = chunks[i] with { Top = chunkTop, LyricTop = lyricOffset };
                    height = lyricOffset + LyricLineHeight + line.AltLyrics.Count * AltLyricLineHeight;
                }
                height += StackedRowBottomGap;
            }
            else
            {
                chunks = Wrap(line, columnWidth, isDiagramMode);
                for (var i = 0; i < chunks.Count; i++)
                {
                    var chunkTop = height;
                    var lyricOffset = chunkTop + markerHeight;
                    chunks[i] = chunks[i] with { Top = chunkTop, LyricTop = lyricOffset };
                    height = lyricOffset + LyricLineHeight;
                }
                height += RowBottomGap;
            }

            double rowLeft;
            double rowTop;
            double rowWidth;

            if (isHorizontalFlow)
            {
                var canFlow = !line.IsReferenceLine && chunks.Count == 1 && !string.IsNullOrWhiteSpace(line.Text);

                if (hasPartHeader)
                {
                    FlushFlowRow();
                    if (curY > topY + 0.5 && headerTopGap == 0)
                    {
                        headerTopGap = PartHeaderTopGap;
                        headerReserve = headerTopGap + PartHeaderHeight;
                        for (var i = 0; i < chunks.Count; i++)
                        {
                            chunks[i] = chunks[i] with
                            {
                                Top = chunks[i].Top + headerTopGap,
                                LyricTop = chunks[i].LyricTop + headerTopGap
                            };
                        }
                        height += headerTopGap;
                    }

                    rowLeft = _pageMarginCurrent;
                    rowTop = curY;
                    if (canFlow)
                    {
                        var footprint = MeasureSingleChunkFootprint(line, chunks[0], isStacked, isDiagramMode);
                        if (isStacked && footprint <= stackedHalfWidth)
                        {
                            rowWidth = footprint;
                            phrasesInRow = 1;
                            rowIsStacked = true;
                            rowHeaderReserve = headerReserve;
                            rowMaxHeight = height;
                        }
                        else if (!isStacked && footprint < usableWidth)
                        {
                            rowWidth = footprint;
                            curX = rowLeft + footprint;
                            phrasesInRow = 1;
                            rowIsStacked = false;
                            rowHeaderReserve = headerReserve;
                            rowMaxHeight = height;
                        }
                        else
                        {
                            rowWidth = usableWidth;
                            curY += height;
                        }
                    }
                    else
                    {
                        rowWidth = usableWidth;
                        curY += height;
                    }
                }
                else if (canFlow && isStacked)
                {
                    var footprint = MeasureSingleChunkFootprint(line, chunks[0], isStacked: true, isDiagramMode);
                    if (phrasesInRow == 1 && rowIsStacked && footprint <= stackedHalfWidth)
                    {
                        rowLeft = _pageMarginCurrent + stackedHalfWidth + StackedColumnGap;
                        rowTop = curY + rowHeaderReserve;
                        rowWidth = footprint;
                        phrasesInRow = 2;
                        rowMaxHeight = Math.Max(rowMaxHeight, rowHeaderReserve + height);
                    }
                    else
                    {
                        FlushFlowRow();
                        rowLeft = _pageMarginCurrent;
                        rowTop = curY;
                        if (footprint <= stackedHalfWidth)
                        {
                            rowWidth = footprint;
                            phrasesInRow = 1;
                            rowIsStacked = true;
                            rowHeaderReserve = 0;
                            rowMaxHeight = height;
                        }
                        else
                        {
                            rowWidth = usableWidth;
                            curY += height;
                        }
                    }
                }
                else if (canFlow && !isStacked)
                {
                    var footprint = MeasureSingleChunkFootprint(line, chunks[0], isStacked: false, isDiagramMode);
                    if (phrasesInRow > 0 && !rowIsStacked && phrasesInRow < MaxPhrasesPerRow &&
                        curX + PhraseGap + footprint <= _pageMarginCurrent + usableWidth)
                    {
                        rowLeft = curX + PhraseGap;
                        rowTop = curY + rowHeaderReserve;
                        rowWidth = footprint;
                        curX = rowLeft + footprint;
                        phrasesInRow++;
                        rowMaxHeight = Math.Max(rowMaxHeight, rowHeaderReserve + height);
                    }
                    else
                    {
                        FlushFlowRow();
                        rowLeft = _pageMarginCurrent;
                        rowTop = curY;
                        if (footprint < usableWidth)
                        {
                            rowWidth = footprint;
                            curX = rowLeft + footprint;
                            phrasesInRow = 1;
                            rowIsStacked = false;
                            rowHeaderReserve = 0;
                            rowMaxHeight = height;
                        }
                        else
                        {
                            rowWidth = usableWidth;
                            curY += height;
                        }
                    }
                }
                else
                {
                    FlushFlowRow();
                    rowLeft = _pageMarginCurrent;
                    rowTop = curY;
                    rowWidth = usableWidth;
                    curY += height;
                }
            }
            else
            {
                rowLeft = col == 0 ? _pageMarginCurrent : _pageMarginCurrent + columnWidth + TwoColumnGap;
                rowTop = col == 0 ? yLeft : yRight;
                rowWidth = columnWidth;
                if (col == 0) yLeft += height;
                else yRight += height;
            }

            var contentLeft = isStacked ? rowLeft + VerseBadgeWidth : rowLeft;
            var partHeaderTop = rowTop + headerTopGap;
            var placements = line.IsReferenceLine
                ? []
                : BuildPlacements(index, line, chunks, rowLeft, contentLeft, rowTop);
            _rows.Add(new RowLayout(
                index, col, line, rowLeft, contentLeft, rowWidth, rowTop, height,
                hasPartHeader, partHeaderTop, isStacked, chunks, placements,
                isHorizontalFlow ? usableWidth : columnWidth));

            if (interludesByLine.TryGetValue(index, out var lineTabs))
            {
                if (isHorizontalFlow)
                {
                    FlushFlowRow();
                    curY += 6;
                    foreach (var tab in lineTabs)
                    {
                        var tabHeight = TabPainter.MeasureSectionHeight(tab, usableWidth);
                        _tabLayouts.Add(new TabSectionLayout(tab, new Rect(_pageMarginCurrent, curY, usableWidth, tabHeight)));
                        curY += tabHeight;
                    }
                }
                else
                {
                    var tabLeft = col == 0 ? _pageMarginCurrent : _pageMarginCurrent + columnWidth + TwoColumnGap;
                    if (col == 0) yLeft += 6; else yRight += 6;
                    foreach (var tab in lineTabs)
                    {
                        var tabTop = col == 0 ? yLeft : yRight;
                        var tabHeight = TabPainter.MeasureSectionHeight(tab, columnWidth);
                        _tabLayouts.Add(new TabSectionLayout(tab, new Rect(tabLeft, tabTop, columnWidth, tabHeight)));
                        if (col == 0) yLeft += tabHeight; else yRight += tabHeight;
                    }
                }
            }
        }

        var yBottom = isHorizontalFlow
            ? curY + (phrasesInRow > 0 ? rowMaxHeight : 0)
            : Math.Max(yLeft, yRight);

        _twoColDividerBottom = Math.Max(_twoColDividerTop, yBottom);

        if (outroTabs.Count > 0)
        {
            yBottom += 8;
            foreach (var tab in outroTabs)
            {
                var tabHeight = TabPainter.MeasureSectionHeight(tab, usableWidth);
                _tabLayouts.Add(new TabSectionLayout(tab, new Rect(_pageMarginCurrent, yBottom, usableWidth, tabHeight)));
                yBottom += tabHeight;
            }
        }

        Height = Math.Max(yBottom, 260) + 36;
        InvalidateVisual();
    }

    private string GetDisplayChordText(ChordPlacement chord)
    {
        if (Song?.ChordDisplayMode == "degree")
        {
            var effectiveKey = string.IsNullOrWhiteSpace(Song.SelectedKey) ? Song.OriginalKey : Song.SelectedKey;
            return ChordTranspose.ToDegree(chord.Name, effectiveKey);
        }
        return chord.Name;
    }

    private double MeasureSingleChunkFootprint(LyricLine line, TextChunk chunk, bool isStacked, bool isDiagramMode)
    {
        var contentOffset = isStacked ? VerseBadgeWidth : 0d;
        var rightEdge = contentOffset + Math.Max(chunk.TotalWidth, chunk.VisualWidth);
        var prevRight = double.NegativeInfinity;

        foreach (var chord in line.Chords)
        {
            var position = Math.Clamp(chord.Position, 0, line.Text.Length);
            var (slotX, rawCharWidth) = GetSlotBounds(chunk, line, position);
            var anchorX = contentOffset + slotX;
            var charWidth = Math.Max(16, rawCharWidth);
            var charCenterX = anchorX + charWidth / 2;
            var shape = ChordCatalog.ByShapeId(chord.ShapeId) ?? ChordCatalog.ByName(chord.Name);

            if (isDiagramMode && shape is not null)
            {
                var idealOriginX = chord.BeforeChar
                    ? anchorX - DiagramWidth / 2 - 4
                    : charCenterX - DiagramWidth / 2;
                var originX = Math.Max(0d, idealOriginX);
                if (!double.IsNegativeInfinity(prevRight) && originX < prevRight + DiagramMinGap)
                    originX = prevRight + DiagramMinGap;
                var chordRight = originX + DiagramWidth;
                prevRight = chordRight;
                rightEdge = Math.Max(rightEdge, chordRight);
            }
            else
            {
                var displayText = GetDisplayChordText(chord);
                var textWidth = Measure(displayText, ChordFontSize, ChordTypeface).Width;
                double idealX;
                if (chord.BeforeChar)
                {
                    idealX = position == 0
                        ? Math.Max(0d, anchorX - Math.Min(textWidth, 26) - 3)
                        : anchorX - textWidth / 2 - 2;
                }
                else
                {
                    idealX = textWidth <= charWidth + 4 ? charCenterX - textWidth / 2 : anchorX;
                }
                var minX = chord.BeforeChar ? Math.Max(0d, contentOffset - 24.0) : contentOffset;
                var drawX = Math.Max(minX, idealX);
                if (!double.IsNegativeInfinity(prevRight) && drawX < prevRight + 8)
                    drawX = prevRight + 8;
                var chordRight = drawX + textWidth;
                prevRight = chordRight;
                rightEdge = Math.Max(rightEdge, chordRight);
            }
        }

        return rightEdge;
    }

    private List<Placement> BuildPlacements(int lineIndex, LyricLine line, List<TextChunk> chunks, double rowLeft, double contentLeft, double rowTop)
    {
        var placements = new List<Placement>();
        var prevRightByChunk = new Dictionary<int, double>();
        for (var chordIndex = 0; chordIndex < line.Chords.Count; chordIndex++)
        {
            var chord = line.Chords[chordIndex];
            var position = Math.Clamp(chord.Position, 0, line.Text.Length);
            var chunkIndex = FindChunk(chunks, position);
            var chunk = chunks[chunkIndex];
            var (slotX, rawCharWidth) = GetSlotBounds(chunk, line, position);
            var anchorX = contentLeft + slotX;
            var charWidth = Math.Max(16, rawCharWidth);
            var targetChar = position < line.Text.Length ? StringInfo.GetNextTextElement(line.Text, position) : "";
            var charCenterX = anchorX + charWidth / 2;
            var shape = ChordCatalog.ByShapeId(chord.ShapeId) ?? ChordCatalog.ByName(chord.Name);
            double drawX;
            if (Song?.ChordDisplayMode == "diagram" && shape is not null)
            {
                var idealOriginX = chord.BeforeChar
                    ? anchorX - DiagramWidth / 2 - 4
                    : charCenterX - DiagramWidth / 2;
                var minDiagramX = rowLeft > _pageMarginCurrent + 1 ? Math.Max(4.0, rowLeft - 8.0) : 4.0;
                var originX = Math.Max(minDiagramX, idealOriginX);
                if (prevRightByChunk.TryGetValue(chunkIndex, out var prevRight) && originX < prevRight + DiagramMinGap)
                    originX = prevRight + DiagramMinGap;
                prevRightByChunk[chunkIndex] = originX + DiagramWidth;
                drawX = originX;
            }
            else
            {
                var displayText = GetDisplayChordText(chord);
                var textWidth = Measure(displayText, ChordFontSize, ChordTypeface).Width;
                double idealX;
                if (chord.BeforeChar)
                {
                    idealX = position == 0
                        ? Math.Max(8, anchorX - Math.Min(textWidth, 26) - 3)
                        : anchorX - textWidth / 2 - 2;
                }
                else
                {
                    idealX = textWidth <= charWidth + 4 ? charCenterX - textWidth / 2 : anchorX;
                }
                var minX = chord.BeforeChar
                    ? (rowLeft > _pageMarginCurrent + 1 ? Math.Max(rowLeft - 12.0, contentLeft - 24.0) : Math.Max(8.0, contentLeft - 30.0))
                    : contentLeft;
                drawX = Math.Max(minX, idealX);
                if (prevRightByChunk.TryGetValue(chunkIndex, out var prevRight) && drawX < prevRight + 8)
                    drawX = prevRight + 8;
                prevRightByChunk[chunkIndex] = drawX + textWidth;
            }
            placements.Add(new Placement(lineIndex, chordIndex, chord, drawX, anchorX, charWidth, targetChar, rowTop + chunk.Top, chunkIndex));
        }
        return placements;
    }

    public void SetChordAt(ChordAnchor anchor, string? chordName)
    {
        if (Song is null || anchor.LineIndex < 0 || anchor.LineIndex >= Song.Lines.Count) return;
        var line = Song.Lines[anchor.LineIndex];
        var pos = Math.Clamp(anchor.Position, 0, line.Text.Length);
        var normalizedAnchor = new ChordAnchor(anchor.LineIndex, pos, anchor.BeforeChar);

        if (string.IsNullOrWhiteSpace(chordName))
        {
            var removed = line.Chords.RemoveAll(c => c.Position == pos && c.BeforeChar == anchor.BeforeChar);
            _selectedPlaced = null;
            _caret = normalizedAnchor;
            SelectedLineIndex = anchor.LineIndex;
            SelectionChanged?.Invoke();
            if (removed > 0) Changed?.Invoke();
            Rebuild();
            return;
        }

        var normalizedName = ChordCatalog.NormalizeChordName(chordName);
        if (string.IsNullOrWhiteSpace(normalizedName)) return;
        var shape = ChordCatalog.ByName(normalizedName);
        var existing = line.Chords.FirstOrDefault(c => c.Position == pos && c.BeforeChar == anchor.BeforeChar);
        if (existing is not null)
        {
            existing.Name = normalizedName;
            existing.ShapeId = shape?.Id ?? "";
            existing.BeforeChar = anchor.BeforeChar;
        }
        else
        {
            existing = new ChordPlacement
            {
                Position = pos,
                BeforeChar = anchor.BeforeChar,
                Name = normalizedName,
                ShapeId = shape?.Id ?? ""
            };
            line.Chords.Add(existing);
            SortChords(line);
        }

        var insertedIndex = line.Chords.FindIndex(x => ReferenceEquals(x, existing));
        _selectedPlaced = insertedIndex >= 0 ? new Hit(anchor.LineIndex, insertedIndex) : null;
        if (!ContinuousPaintMode) SelectedShapeId = null;
        SelectedLineIndex = anchor.LineIndex;
        _caret = normalizedAnchor;
        SelectionChanged?.Invoke();
        Changed?.Invoke();
        Rebuild();
    }

    public void SetChordPlacement(ChordAnchor oldAnchor, ChordAnchor newAnchor, string? chordName)
    {
        if (Song is null) return;
        if (oldAnchor == newAnchor)
        {
            SetChordAt(newAnchor, chordName);
            return;
        }

        var removed = 0;
        if (oldAnchor.LineIndex >= 0 && oldAnchor.LineIndex < Song.Lines.Count)
        {
            var oldLine = Song.Lines[oldAnchor.LineIndex];
            var oldPos = Math.Clamp(oldAnchor.Position, 0, oldLine.Text.Length);
            removed = oldLine.Chords.RemoveAll(c => c.Position == oldPos && c.BeforeChar == oldAnchor.BeforeChar);
        }

        if (!string.IsNullOrWhiteSpace(chordName))
        {
            SetChordAt(newAnchor, chordName);
        }
        else
        {
            SelectAnchor(newAnchor);
            if (removed > 0) Changed?.Invoke();
            Rebuild();
        }
    }

    public void SetChordAtCaret(string? chordName)
    {
        if (_caret is { } caret)
        {
            SetChordAt(caret, chordName);
            return;
        }
        if (Song is not null && _selectedPlaced is { } hit &&
            hit.LineIndex >= 0 && hit.LineIndex < Song.Lines.Count &&
            hit.ChordIndex >= 0 && hit.ChordIndex < Song.Lines[hit.LineIndex].Chords.Count)
        {
            var ch = Song.Lines[hit.LineIndex].Chords[hit.ChordIndex];
            SetChordAt(new ChordAnchor(hit.LineIndex, ch.Position, ch.BeforeChar), chordName);
        }
    }

    public string? GetChordAt(ChordAnchor anchor)
    {
        if (Song is null || anchor.LineIndex < 0 || anchor.LineIndex >= Song.Lines.Count) return null;
        var line = Song.Lines[anchor.LineIndex];
        var pos = Math.Clamp(anchor.Position, 0, line.Text.Length);
        return line.Chords.FirstOrDefault(c => c.Position == pos && c.BeforeChar == anchor.BeforeChar)?.Name;
    }

    public void SelectAnchor(ChordAnchor anchor)
    {
        if (Song is null || anchor.LineIndex < 0 || anchor.LineIndex >= Song.Lines.Count) return;
        var line = Song.Lines[anchor.LineIndex];
        var pos = Math.Clamp(anchor.Position, 0, line.Text.Length);
        _caret = new ChordAnchor(anchor.LineIndex, pos, anchor.BeforeChar);
        SelectedLineIndex = anchor.LineIndex;
        var chordIndex = line.Chords.FindIndex(c => c.Position == pos && c.BeforeChar == anchor.BeforeChar);
        _selectedPlaced = chordIndex >= 0 ? new Hit(anchor.LineIndex, chordIndex) : null;
        SelectionChanged?.Invoke();
        InvalidateVisual();
    }

    public Rect GetAnchorRect(ChordAnchor anchor)
    {
        var row = _rows.FirstOrDefault(r => r.Index == anchor.LineIndex);
        if (row is null)
            return new Rect(_pageMarginCurrent, EffectiveHeaderHeight, 22, 52);

        var pos = Math.Clamp(anchor.Position, 0, row.Line.Text.Length);
        var chunkIndex = FindChunk(row.Chunks, pos);
        var chunk = row.Chunks[chunkIndex];
        var clampedPos = Math.Clamp(pos, chunk.Start, chunk.End);
        var (slotX, charWidth) = GetSlotBounds(chunk, row.Line, clampedPos);
        var x = row.ContentLeft + slotX;
        var y = row.Top + chunk.Top;
        var height = (chunk.LyricTop - chunk.Top) + LyricLineHeight;
        return new Rect(x, y, Math.Max(18, charWidth), height);
    }

    public bool MoveCaret(int deltaChars, int deltaLines = 0)
    {
        if (Song is null || Song.Lines.Count == 0) return false;
        var editableIndices = new List<int>();
        for (var i = 0; i < Song.Lines.Count; i++)
        {
            if (!Song.Lines[i].IsReferenceLine)
                editableIndices.Add(i);
        }
        if (editableIndices.Count == 0) return false;

        var current = _caret ?? new ChordAnchor(Math.Clamp(SelectedLineIndex ?? editableIndices[0], 0, Song.Lines.Count - 1), 0);
        var lineIndex = Math.Clamp(current.LineIndex, 0, Song.Lines.Count - 1);
        if (Song.Lines[lineIndex].IsReferenceLine)
            lineIndex = editableIndices[0];

        var positions = GetElementPositions(Song.Lines[lineIndex].Text);
        var elemIdx = FindClosestElementIndex(positions, current.Position);

        if (deltaLines != 0)
        {
            var curEditablePos = editableIndices.IndexOf(lineIndex);
            var targetEditablePos = Math.Clamp((curEditablePos >= 0 ? curEditablePos : 0) + deltaLines, 0, editableIndices.Count - 1);
            var targetLine = editableIndices[targetEditablePos];
            var targetPositions = GetElementPositions(Song.Lines[targetLine].Text);
            var targetElemIdx = Math.Clamp(elemIdx, 0, targetPositions.Count - 1);
            lineIndex = targetLine;
            elemIdx = targetElemIdx;
            positions = targetPositions;
        }

        if (deltaChars != 0)
        {
            var curEditablePos = editableIndices.IndexOf(lineIndex);
            if (curEditablePos < 0) curEditablePos = 0;
            var nextElemIdx = elemIdx + deltaChars;
            while (nextElemIdx < 0 && curEditablePos > 0)
            {
                curEditablePos--;
                lineIndex = editableIndices[curEditablePos];
                positions = GetElementPositions(Song.Lines[lineIndex].Text);
                nextElemIdx += positions.Count;
            }
            while (nextElemIdx >= positions.Count && curEditablePos < editableIndices.Count - 1)
            {
                nextElemIdx -= positions.Count;
                curEditablePos++;
                lineIndex = editableIndices[curEditablePos];
                positions = GetElementPositions(Song.Lines[lineIndex].Text);
            }
            elemIdx = Math.Clamp(nextElemIdx, 0, positions.Count - 1);
        }

        var finalPos = positions[elemIdx];
        _caret = new ChordAnchor(lineIndex, finalPos);
        SelectedLineIndex = lineIndex;
        var existingChordIdx = Song.Lines[lineIndex].Chords.FindIndex(c => c.Position == finalPos && !c.BeforeChar);
        _selectedPlaced = existingChordIdx >= 0 ? new Hit(lineIndex, existingChordIdx) : null;
        SelectionChanged?.Invoke();
        LineSelected?.Invoke(lineIndex);
        InvalidateVisual();
        return true;
    }

    public bool PlaceChordAt(Point point, ChordShape shape)
    {
        var anchor = GetAnchorAt(point);
        if (anchor is null) return false;
        SetChordAt(anchor, shape.Name);
        return true;
    }

    public void RemoveSelectedChord()
    {
        if (Song is null) return;
        if (_selectedPlaced is { } hit &&
            hit.LineIndex >= 0 && hit.LineIndex < Song.Lines.Count &&
            hit.ChordIndex >= 0 && hit.ChordIndex < Song.Lines[hit.LineIndex].Chords.Count)
        {
            var removedChord = Song.Lines[hit.LineIndex].Chords[hit.ChordIndex];
            Song.Lines[hit.LineIndex].Chords.RemoveAt(hit.ChordIndex);
            _caret = new ChordAnchor(hit.LineIndex, removedChord.Position, removedChord.BeforeChar);
            _selectedPlaced = null;
            SelectionChanged?.Invoke();
            Changed?.Invoke();
            Rebuild();
            return;
        }

        if (_caret is { } caret && GetChordAt(caret) is not null)
            SetChordAt(caret, null);
    }

    public bool CopyLineChords(int lineIndex)
    {
        if (Song is null || lineIndex < 0 || lineIndex >= Song.Lines.Count) return false;
        var line = Song.Lines[lineIndex];
        if (line.IsReferenceLine || line.Chords.Count == 0) return false;

        _copiedLineSnapshot = line.Clone();
        SelectionChanged?.Invoke();
        var summary = FormatChordSummary(line);
        StatusMessageRequested?.Invoke($"已复制本句和弦（{summary}），可在目标句右键粘贴或按 Ctrl+V；按 Ctrl+D 可直接复制到下一句。");
        return true;
    }

    public bool PasteLineChords(int targetLineIndex)
    {
        if (Song is null || !HasCopiedLineChords || targetLineIndex < 0 || targetLineIndex >= Song.Lines.Count)
            return false;
        var targetLine = Song.Lines[targetLineIndex];
        if (targetLine.IsReferenceLine) return false;

        PartAnalyzer.CopyLineChordsProportionally(_copiedLineSnapshot!, targetLine);
        var firstChord = targetLine.Chords.FirstOrDefault();
        _caret = new ChordAnchor(targetLineIndex, firstChord?.Position ?? 0, firstChord?.BeforeChar ?? false);
        SelectedLineIndex = targetLineIndex;
        _selectedPlaced = null;
        Rebuild();
        SelectionChanged?.Invoke();
        Changed?.Invoke();
        StatusMessageRequested?.Invoke($"已将和弦（{CopiedLineChordsSummary}）粘贴到第 {targetLineIndex + 1} 句。");
        return true;
    }

    public bool DuplicateLineChordsToNext(int sourceLineIndex)
    {
        if (Song is null || sourceLineIndex < 0 || sourceLineIndex >= Song.Lines.Count)
            return false;
        var sourceLine = Song.Lines[sourceLineIndex];
        if (sourceLine.IsReferenceLine || sourceLine.Chords.Count == 0)
            return false;

        _copiedLineSnapshot = sourceLine.Clone();
        var nextIdx = FindNextEditableLineIndex(sourceLineIndex);
        if (nextIdx is null)
        {
            SelectionChanged?.Invoke();
            StatusMessageRequested?.Invoke("已经是最后一句歌词，无法复制到下一句。");
            return false;
        }

        var targetLine = Song.Lines[nextIdx.Value];
        PartAnalyzer.CopyLineChordsProportionally(_copiedLineSnapshot, targetLine);
        var firstChord = targetLine.Chords.FirstOrDefault();
        _caret = new ChordAnchor(nextIdx.Value, firstChord?.Position ?? 0, firstChord?.BeforeChar ?? false);
        SelectedLineIndex = nextIdx.Value;
        _selectedPlaced = null;
        Rebuild();
        SelectionChanged?.Invoke();
        Changed?.Invoke();
        StatusMessageRequested?.Invoke($"已将本句和弦（{CopiedLineChordsSummary}）复制到下一句。");
        return true;
    }

    public bool PasteLineChordsToPart(int targetLineIndex)
    {
        if (Song is null || !HasCopiedLineChords || targetLineIndex < 0 || targetLineIndex >= Song.Lines.Count)
            return false;

        var parts = PartAnalyzer.BuildParts(Song.Lines);
        var part = parts.FirstOrDefault(p => targetLineIndex >= p.StartLine && targetLineIndex < p.StartLine + p.LineCount);
        if (part is null || part.IsReference)
            return false;

        var appliedCount = 0;
        for (var i = part.StartLine; i < part.StartLine + part.LineCount; i++)
        {
            if (Song.Lines[i].IsReferenceLine) continue;
            PartAnalyzer.CopyLineChordsProportionally(_copiedLineSnapshot!, Song.Lines[i]);
            appliedCount++;
        }
        if (appliedCount == 0)
            return false;

        var currentLine = Song.Lines[targetLineIndex];
        var firstChord = currentLine.Chords.FirstOrDefault();
        _caret = new ChordAnchor(targetLineIndex, firstChord?.Position ?? 0, firstChord?.BeforeChar ?? false);
        SelectedLineIndex = targetLineIndex;
        _selectedPlaced = null;
        Rebuild();
        SelectionChanged?.Invoke();
        Changed?.Invoke();
        StatusMessageRequested?.Invoke($"已将和弦（{CopiedLineChordsSummary}）粘贴到「{part.Title}」全部 {appliedCount} 句。");
        return true;
    }

    private int? FindNextEditableLineIndex(int fromIndex)
    {
        if (Song is null) return null;
        for (var i = fromIndex + 1; i < Song.Lines.Count; i++)
        {
            if (!Song.Lines[i].IsReferenceLine)
                return i;
        }
        return null;
    }

    private string FormatChordSummary(LyricLine line)
    {
        if (line.Chords is null || line.Chords.Count == 0) return "";
        return string.Join(" - ", line.Chords.Select(GetDisplayChordText));
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        DrawScore(dc);
    }

    public System.Windows.Media.Imaging.BitmapSource RenderPng()
    {
        var visual = new DrawingVisual();
        IsExporting = true;
        try
        {
            Rebuild();
            using (var drawing = visual.RenderOpen())
                DrawScore(drawing);
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)Math.Ceiling(PaperWidthCurrent * 2),
                (int)Math.Ceiling(Height * 2),
                192,
                192,
                PixelFormats.Pbgra32);
            bitmap.Render(visual);
            return bitmap;
        }
        finally
        {
            IsExporting = false;
            Rebuild();
        }
    }

    private void DrawScore(DrawingContext dc)
    {
        dc.DrawRectangle(Background, null, new Rect(0, 0, PaperWidthCurrent, Height));
        if (Song is null)
        {
            DrawCentered(dc, "导入 LRC 歌词，开始排你的吉他弹唱谱", 21, 160, ChordTypeface, Ink);
            DrawCentered(dc, "单击任意进唱字位即可填入或点选左手和弦 · 支持合行紧凑排版与高清图片导出", 13, 200, LyricTypeface, Quiet);
            return;
        }

        if (!HideHeaderOnScreen || IsExporting)
        {
            var title = string.IsNullOrWhiteSpace(Song.Title) ? "未命名歌曲" : Song.Title;
            DrawCentered(dc, title, 28, 18, ChordTypeface, Ink);
            var metadata = string.Join("      ", new[]
            {
                string.IsNullOrWhiteSpace(Song.Artist) ? null : $"原唱 {Song.Artist}",
                string.IsNullOrWhiteSpace(Song.OriginalKey) ? null : $"原调 {Song.OriginalKey}",
                string.IsNullOrWhiteSpace(Song.SelectedKey) ? null : $"选调 {Song.SelectedKey}",
                string.IsNullOrWhiteSpace(Song.Capo) ? null : $"变调夹 {Song.Capo}",
                Song.Bpm > 0 ? $"BPM {Song.Bpm}" : null
            }.Where(x => x is not null));
            if (!string.IsNullOrWhiteSpace(metadata))
                DrawCentered(dc, metadata, 13.5, 57, LyricTypeface, Quiet);
            dc.DrawLine(new Pen(Rule, 1), new Point(_pageMarginCurrent, 82), new Point(PaperWidthCurrent - _pageMarginCurrent, 82));
        }

        if (_rows.Count == 0 && _tabLayouts.Count == 0)
        {
            DrawCentered(dc, "这首曲谱还没有歌词", 16, EffectiveHeaderHeight + 54, ChordTypeface, Quiet);
            DrawCentered(dc, "双击纸面直接打字输入歌词，或点击左上角「+ 导入」铺开歌词；也可点「+ 六线谱」添加前奏/间奏", 12.5, EffectiveHeaderHeight + 84, LyricTypeface, Quiet);
            return;
        }

        if (_columnCount == 2 && _rows.Count > 0)
        {
            var dividerX = PaperWidthCurrent / 2;
            dc.DrawLine(new Pen(ColumnRule, 1), new Point(dividerX, _twoColDividerTop), new Point(dividerX, _twoColDividerBottom));
        }

        foreach (var tabLayout in _tabLayouts)
        {
            if (!IsExporting && _hoverTabSectionId == tabLayout.Section.Id)
            {
                var highlightRect = new Rect(
                    tabLayout.Bounds.Left - 8,
                    tabLayout.Bounds.Top - 4,
                    tabLayout.Bounds.Width + 16,
                    tabLayout.Bounds.Height + 2);
                dc.DrawRoundedRectangle(HoverSlotFill, new Pen(HoverSlotBorder, 1), highlightRect, 6, 6);
                var editHint = Measure("点击编辑六线谱", 10.5, LyricTypeface);
                DrawText(dc, "点击编辑六线谱", tabLayout.Bounds.Right - editHint.Width - 2, tabLayout.Bounds.Top + 3, 10.5, LyricTypeface, Green);
            }

            TabPainter.DrawSection(
                dc,
                tabLayout.Section,
                tabLayout.Bounds.Left,
                tabLayout.Bounds.Top,
                tabLayout.Bounds.Width,
                Background,
                _pixelsPerDip);
        }

        if (!IsExporting && LyricEditMode && _hoverLyricVerse is { } hoverVerse && ActiveInlineLyricEdit != hoverVerse)
        {
            DrawLyricVerseHover(dc, hoverVerse.LineIndex, hoverVerse.VerseIndex);
        }
        else if (!IsExporting && !LyricEditMode)
        {
            if (_hoverAnchor is { } hover && (_caret is null || hover != _caret) && ActiveInlineLyricEdit?.LineIndex != hover.LineIndex)
                DrawAnchorHighlight(dc, hover, isCaret: false);

            if (_caret is { } caret && ActiveInlineLyricEdit?.LineIndex != caret.LineIndex)
                DrawAnchorHighlight(dc, caret, isCaret: true);
        }

        foreach (var row in _rows)
        {
            if (row.HasPartHeader)
                DrawPartHeader(dc, row);

            if (row.Line.IsReferenceLine)
            {
                DrawReferenceRow(dc, row);
                continue;
            }

            if (Song.ColumnMode == 0 && row.Left > _pageMarginCurrent + 1 && row.Chunks.Count > 0)
            {
                if (row.IsStacked)
                {
                    var divX = row.Left - StackedColumnGap / 2;
                    var divTop = row.Top + 6;
                    var divBottom = row.Top + Math.Max(20, row.Height - StackedRowBottomGap + 2);
                    dc.DrawLine(ColumnRulePen, new Point(divX, divTop), new Point(divX, divBottom));
                }
                else
                {
                    var sepText = "·";
                    var sepWidth = Measure(sepText, LyricFontSize, LyricTypeface).Width;
                    DrawText(dc, sepText, row.Left - PhraseGap / 2 - sepWidth / 2, row.Top + row.Chunks[0].LyricTop, LyricFontSize, LyricTypeface, Quiet);
                }
            }

            var hideMainText = !IsExporting && ActiveInlineLyricEdit == (row.Index, 0);

            if (row.IsStacked)
            {
                foreach (var chunk in row.Chunks)
                {
                    var baseTop = row.Top + chunk.LyricTop;
                    var bracketTop = baseTop + 3;
                    var bracketBottom = baseTop + LyricLineHeight + row.Line.AltLyrics.Count * AltLyricLineHeight - 6;
                    dc.DrawLine(VerseBracketPen, new Point(row.Left + 2, bracketTop), new Point(row.Left + 2, bracketBottom));

                    DrawText(dc, "1", row.Left + 7, baseTop + 5, 10.5, ChordTypeface, Green);
                    if (!hideMainText)
                        DrawStackedTextElements(dc, chunk.Text, row.ContentLeft, baseTop, Ink, chunk.CellXMap);

                    for (var altIdx = 0; altIdx < row.Line.AltLyrics.Count; altIdx++)
                    {
                        var altTop = baseTop + LyricLineHeight + altIdx * AltLyricLineHeight;
                        DrawText(dc, (altIdx + 2).ToString(CultureInfo.InvariantCulture), row.Left + 7, altTop + 5, 10.5, LyricTypeface, Quiet);
                        if (!IsExporting && ActiveInlineLyricEdit == (row.Index, altIdx + 1))
                            continue;
                        var altSlice = altIdx < chunk.AltSlices.Count ? chunk.AltSlices[altIdx] : "";
                        if (!string.IsNullOrEmpty(altSlice))
                            DrawStackedTextElements(dc, altSlice, row.ContentLeft, altTop, AltLyricInk, chunk.CellXMap);
                    }
                }
            }
            else if (!hideMainText)
            {
                var drawSlotsIndividually = Song.ChordDisplayMode == "diagram" && row.Line.Chords.Count > 0 && row.Chunks.Any(c => c.Slots.Count > 0);
                foreach (var chunk in row.Chunks)
                {
                    var baseline = row.Top + chunk.LyricTop;
                    if (drawSlotsIndividually)
                    {
                        foreach (var slot in chunk.Slots)
                        {
                            if (!string.IsNullOrWhiteSpace(slot.Text))
                                DrawText(dc, slot.Text, row.ContentLeft + slot.X, baseline, LyricFontSize, LyricTypeface, Ink);
                        }
                    }
                    else
                    {
                        DrawText(dc, chunk.Text, row.ContentLeft, baseline, LyricFontSize, LyricTypeface, Ink);
                    }
                }
            }

            foreach (var placement in row.Placements)
            {
                var isSelected = !IsExporting && _selectedPlaced is { } hit &&
                                 hit.LineIndex == placement.LineIndex && hit.ChordIndex == placement.ChordIndex;
                var lyricBaseline = row.Top + row.Chunks[placement.ChunkIndex].LyricTop;
                DrawPlacement(dc, placement, isSelected, lyricBaseline);
            }
        }

        if (!IsExporting && _draggingChord && _dragTarget is { } target && _pressedChord is { } pressed &&
            pressed.LineIndex < Song.Lines.Count && pressed.ChordIndex < Song.Lines[pressed.LineIndex].Chords.Count)
        {
            DrawGhost(dc, Song.Lines[pressed.LineIndex].Chords[pressed.ChordIndex], target);
        }
        else if (!IsExporting && _externalDragShape is { } shape && _externalDragTarget is { } externalTarget)
        {
            DrawGhost(dc, new ChordPlacement { Name = shape.Name, ShapeId = shape.Id }, externalTarget);
        }
        else if (!IsExporting && !_draggingChord && _hoverAnchor is { } hoverTarget && !string.IsNullOrWhiteSpace(ActiveChordBrush))
        {
            DrawGhost(dc, new ChordPlacement { Name = ActiveChordBrush, ShapeId = ChordCatalog.ByName(ActiveChordBrush)?.Id ?? "" }, hoverTarget);
        }
    }

    private void DrawPartHeader(DrawingContext dc, RowLayout row)
    {
        var y = row.PartHeaderTop;
        var x = row.Left;

        if (!string.IsNullOrWhiteSpace(row.Line.PartTitle))
        {
            var titleFmt = ChordDiagramPainter.CreateText(row.Line.PartTitle, 11.5, ChordTypeface, Green, _pixelsPerDip);
            var pillRect = new Rect(x, y + 2, titleFmt.Width + 16, 20);
            dc.DrawRoundedRectangle(PartBadgeFill, new Pen(PartBadgeBorder, 1), pillRect, 5, 5);
            dc.DrawText(titleFmt, new Point(x + 8, y + 4));
            x = pillRect.Right + 6;
        }

        if (row.Line.RepeatCount > 1)
        {
            var repeatText = $"×{row.Line.RepeatCount}";
            var repeatFmt = ChordDiagramPainter.CreateText(repeatText, 11, ChordTypeface, Brushes.White, _pixelsPerDip);
            var repeatRect = new Rect(x, y + 3, repeatFmt.Width + 12, 18);
            dc.DrawRoundedRectangle(RepeatBadgeFill, null, repeatRect, 9, 9);
            dc.DrawText(repeatFmt, new Point(x + 6, y + 4));
            x = repeatRect.Right + 8;
        }

        var ruleStart = x > row.Left ? x + 4 : row.Left;
        var ruleEnd = row.Left + row.PartHeaderWidth;
        if (ruleEnd - ruleStart > 16)
            dc.DrawLine(PartRulePen, new Point(ruleStart, y + 12), new Point(ruleEnd, y + 12));
    }

    private void DrawReferenceRow(DrawingContext dc, RowLayout row)
    {
        var text = string.IsNullOrWhiteSpace(row.Line.Text) ? "→ 反复段落" : row.Line.Text;
        var fmt = ChordDiagramPainter.CreateText(text, 13.5, ChordTypeface, Green, _pixelsPerDip);
        var pillWidth = Math.Min(row.Width, Math.Max(140, fmt.Width + 28));
        var rect = new Rect(row.Left, row.Top + (row.HasPartHeader ? PartHeaderHeight : 0) + 4, pillWidth, 26);
        dc.DrawRoundedRectangle(RefLineFill, new Pen(RefLineBorder, 1), rect, 6, 6);
        dc.DrawText(fmt, new Point(rect.Left + 14, rect.Top + 4));
    }

    private void DrawStackedTextElements(DrawingContext dc, string text, double startX, double y, Brush brush, double[]? cellXMap = null)
    {
        if (string.IsNullOrEmpty(text)) return;
        var cellOffset = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            var units = PartAnalyzer.GetHalfCellUnits(element);
            double xOffset;
            double slotWidth;
            if (cellXMap is not null && cellOffset + units < cellXMap.Length)
            {
                xOffset = cellXMap[cellOffset];
                slotWidth = cellXMap[cellOffset + units] - xOffset;
            }
            else
            {
                xOffset = cellOffset * StackedHalfCellWidth;
                slotWidth = units * StackedHalfCellWidth;
            }
            if (!string.IsNullOrWhiteSpace(element))
            {
                var fmt = ChordDiagramPainter.CreateText(element, LyricFontSize, LyricTypeface, brush, _pixelsPerDip);
                var drawX = startX + xOffset + (slotWidth - fmt.Width) / 2;
                dc.DrawText(fmt, new Point(drawX, y));
            }
            cellOffset += units;
        }
    }

    private void DrawAnchorHighlight(DrawingContext dc, ChordAnchor anchor, bool isCaret)
    {
        var row = _rows.FirstOrDefault(r => r.Index == anchor.LineIndex);
        if (row is null || row.Line.IsReferenceLine) return;
        var pos = Math.Clamp(anchor.Position, 0, row.Line.Text.Length);
        var chunkIndex = FindChunk(row.Chunks, pos);
        var chunk = row.Chunks[chunkIndex];
        var clampedPos = Math.Clamp(pos, chunk.Start, chunk.End);
        var (slotX, slotW) = GetSlotBounds(chunk, row.Line, clampedPos);
        var x = row.ContentLeft + slotX;
        var charW = Math.Max(18, slotW);
        var lyricY = row.Top + chunk.LyricTop;
        var chordTopY = row.Top + chunk.Top;
        var markerHeight = chunk.LyricTop - chunk.Top;

        if (anchor.BeforeChar)
        {
            var barBrush = isCaret ? CaretBorder : HoverSlotBorder;
            dc.DrawRoundedRectangle(barBrush, null, new Rect(x - 3.5, lyricY + 3, 2.5, 20), 1, 1);
        }
        else if (isCaret)
        {
            dc.DrawRoundedRectangle(CaretCharFill, new Pen(CaretBorder, 1), new Rect(x - 1, lyricY + 1, charW + 2, 24), 4, 4);
            dc.DrawLine(new Pen(CaretBorder, 1.5), new Point(x, chordTopY + markerHeight - 2), new Point(x + charW, chordTopY + markerHeight - 2));
        }
        else
        {
            dc.DrawRoundedRectangle(HoverCharFill, null, new Rect(x - 1, lyricY + 1, charW + 2, 24), 4, 4);
            dc.DrawRoundedRectangle(HoverSlotFill, new Pen(HoverSlotBorder, 0.8), new Rect(x - 1, chordTopY + 2, charW + 2, Math.Max(16, markerHeight - 4)), 3, 3);
        }

        // Subtle character slot tick marks along the active chunk so character boundaries are clear during editing.
        dc.DrawLine(CharSlotTickPen, new Point(row.ContentLeft, lyricY + 22), new Point(row.ContentLeft, lyricY + 26));
        foreach (var slot in chunk.Slots)
        {
            var tickX = row.ContentLeft + slot.X + slot.Width;
            dc.DrawLine(CharSlotTickPen, new Point(tickX, lyricY + 22), new Point(tickX, lyricY + 26));
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        var point = e.GetPosition(this);
        if (e.ClickCount >= 2 && FindChord(point) is null)
        {
            if (GetLyricTargetAt(point) is { } lyricTarget)
            {
                InlineLyricEditRequested?.Invoke(lyricTarget);
                e.Handled = true;
                return;
            }
            if (FindReferenceRow(point) is not null)
            {
                PartArrangeRequested?.Invoke();
                e.Handled = true;
                return;
            }
            if (Song is { Lines.Count: 0 } && FindTabSection(point) is null)
            {
                StartFirstBlankLyricLine();
                e.Handled = true;
                return;
            }
        }

        var hitChord = FindChord(point);
        var hitLyric = hitChord is null && LyricEditMode ? GetLyricTargetAt(point) : null;
        var hitAnchor = hitChord is null && hitLyric is null ? GetAnchorAt(point) : null;
        var hitTab = hitChord is null && hitLyric is null && hitAnchor is null ? FindTabSection(point) : null;
        var hitRef = hitChord is null && hitLyric is null && hitAnchor is null && hitTab is null ? FindReferenceRow(point) : null;
        var hitEmptyPaperInLyricMode = LyricEditMode && hitChord is null && hitLyric is null && hitTab is null && hitRef is null && Song is { Lines.Count: 0 };

        if (hitChord is null && hitLyric is null && hitAnchor is null && hitTab is null && hitRef is null && !hitEmptyPaperInLyricMode)
        {
            if (_selectedPlaced is not null)
            {
                _selectedPlaced = null;
                SelectionChanged?.Invoke();
                InvalidateVisual();
            }
            return;
        }

        _pressedChord = hitChord;
        _pressPoint = point;
        _draggingChord = false;
        _dragTarget = null;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = e.GetPosition(this);
        if (_pressPoint is not { } origin || e.LeftButton != MouseButtonState.Pressed)
        {
            var overChord = FindChord(point) is not null;
            if (LyricEditMode)
            {
                var lyricHover = !overChord ? GetLyricTargetAt(point) : null;
                var overTabInLyric = lyricHover is null && !overChord ? FindTabSection(point) : null;
                var overRefInLyric = lyricHover is null && !overChord && overTabInLyric is null && FindReferenceRow(point) is not null;
                var nextVerse = lyricHover is not null ? (lyricHover.LineIndex, lyricHover.VerseIndex) : ((int, int)?)null;
                var nextTabInLyric = overTabInLyric?.Section.Id;

                Cursor = lyricHover is not null || (Song is { Lines.Count: 0 } && !overChord && overTabInLyric is null)
                    ? Cursors.IBeam
                    : (overChord || overTabInLyric is not null || overRefInLyric ? Cursors.Hand : Cursors.Arrow);

                if (nextVerse != _hoverLyricVerse || nextTabInLyric != _hoverTabSectionId || _hoverAnchor is not null)
                {
                    _hoverLyricVerse = nextVerse;
                    _hoverAnchor = null;
                    _hoverTabSectionId = nextTabInLyric;
                    InvalidateVisual();
                }
                return;
            }

            var hover = GetAnchorAt(point);
            var overTab = hover is null && !overChord ? FindTabSection(point) : null;
            var overRef = hover is null && !overChord && overTab is null && FindReferenceRow(point) is not null;
            var nextTabId = overTab?.Section.Id;

            Cursor = hover is not null || overChord || overTab is not null || overRef ? Cursors.Hand : Cursors.Arrow;
            if (hover != _hoverAnchor || nextTabId != _hoverTabSectionId || _hoverLyricVerse is not null)
            {
                _hoverAnchor = hover;
                _hoverLyricVerse = null;
                _hoverTabSectionId = nextTabId;
                InvalidateVisual();
            }
            return;
        }

        if (!_draggingChord && _pressedChord is not null &&
            (Math.Abs(point.X - origin.X) > SystemParameters.MinimumHorizontalDragDistance ||
             Math.Abs(point.Y - origin.Y) > SystemParameters.MinimumVerticalDragDistance))
        {
            _draggingChord = true;
        }

        if (_draggingChord)
        {
            _dragTarget = GetAnchorAt(point);
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverAnchor is not null || _hoverLyricVerse is not null || _hoverTabSectionId is not null)
        {
            _hoverAnchor = null;
            _hoverLyricVerse = null;
            _hoverTabSectionId = null;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_pressPoint is null) return;
        var point = e.GetPosition(this);
        ReleaseMouseCapture();

        if (_pressedChord is { } pressed && Song is not null)
        {
            if (_draggingChord && GetAnchorAt(point) is { } target)
            {
                var chord = Song.Lines[pressed.LineIndex].Chords[pressed.ChordIndex];
                if (pressed.LineIndex != target.LineIndex || chord.Position != target.Position || chord.BeforeChar != target.BeforeChar)
                {
                    Song.Lines[pressed.LineIndex].Chords.RemoveAt(pressed.ChordIndex);
                    Song.Lines[target.LineIndex].Chords.RemoveAll(c => c.Position == target.Position && c.BeforeChar == target.BeforeChar);
                    chord.Position = target.Position;
                    chord.BeforeChar = target.BeforeChar;
                    Song.Lines[target.LineIndex].Chords.Add(chord);
                    SortChords(Song.Lines[target.LineIndex]);
                }
                var movedIndex = Song.Lines[target.LineIndex].Chords.FindIndex(x => ReferenceEquals(x, chord));
                _selectedPlaced = movedIndex >= 0 ? new Hit(target.LineIndex, movedIndex) : null;
                _caret = target;
                SelectedShapeId = null;
                SelectedLineIndex = target.LineIndex;
                SelectionChanged?.Invoke();
                Changed?.Invoke();
                Rebuild();
            }
            else if (!_draggingChord)
            {
                var chord = Song.Lines[pressed.LineIndex].Chords[pressed.ChordIndex];
                var anchor = new ChordAnchor(pressed.LineIndex, chord.Position, chord.BeforeChar);
                if (ContinuousPaintMode && !string.IsNullOrWhiteSpace(ActiveChordBrush))
                {
                    SetChordAt(anchor, ActiveChordBrush);
                    LineSelected?.Invoke(anchor.LineIndex);
                }
                else
                {
                    _selectedPlaced = pressed;
                    _caret = anchor;
                    SelectedShapeId = null;
                    SelectedLineIndex = pressed.LineIndex;
                    SelectionChanged?.Invoke();
                    LineSelected?.Invoke(pressed.LineIndex);
                    InvalidateVisual();
                    CharacterSlotClicked?.Invoke(anchor, GetAnchorRect(anchor), chord.Name);
                }
            }
        }
        else if (LyricEditMode && Song is not null && GetLyricTargetAt(point) is { } lyricClickTarget)
        {
            _selectedPlaced = null;
            _caret = new ChordAnchor(lyricClickTarget.LineIndex, Math.Clamp(lyricClickTarget.CharIndex, 0, Song.Lines[lyricClickTarget.LineIndex].Text.Length));
            SelectedLineIndex = lyricClickTarget.LineIndex;
            SelectionChanged?.Invoke();
            LineSelected?.Invoke(lyricClickTarget.LineIndex);
            InvalidateVisual();
            InlineLyricEditRequested?.Invoke(lyricClickTarget);
        }
        else if (LyricEditMode && Song is { Lines.Count: 0 } && FindTabSection(point) is null)
        {
            StartFirstBlankLyricLine();
        }
        else if (GetAnchorAt(point) is { } anchor)
        {
            _caret = anchor;
            SelectedLineIndex = anchor.LineIndex;
            if (!string.IsNullOrWhiteSpace(ActiveChordBrush))
            {
                var brushChord = ActiveChordBrush;
                SetChordAt(anchor, brushChord);
                if (!ContinuousPaintMode)
                {
                    ActiveChordBrush = null;
                    SelectedShapeId = null;
                }
                LineSelected?.Invoke(anchor.LineIndex);
            }
            else if (ChordCatalog.ByShapeId(SelectedShapeId) is { } shape)
            {
                SetChordAt(anchor, shape.Name);
                if (!ContinuousPaintMode) SelectedShapeId = null;
                LineSelected?.Invoke(anchor.LineIndex);
            }
            else
            {
                var existingIdx = Song?.Lines[anchor.LineIndex].Chords.FindIndex(c => c.Position == anchor.Position && c.BeforeChar == anchor.BeforeChar) ?? -1;
                _selectedPlaced = existingIdx >= 0 ? new Hit(anchor.LineIndex, existingIdx) : null;
                SelectionChanged?.Invoke();
                LineSelected?.Invoke(anchor.LineIndex);
                InvalidateVisual();
                CharacterSlotClicked?.Invoke(anchor, GetAnchorRect(anchor), GetChordAt(anchor));
            }
        }
        else if (FindTabSection(point) is { } tabHit)
        {
            _selectedPlaced = null;
            SelectionChanged?.Invoke();
            InvalidateVisual();
            TabSectionEditRequested?.Invoke(tabHit.Section);
        }
        else if (FindReferenceRow(point) is not null)
        {
            _selectedPlaced = null;
            SelectionChanged?.Invoke();
            InvalidateVisual();
            PartArrangeRequested?.Invoke();
        }
        else
        {
            _selectedPlaced = null;
            SelectionChanged?.Invoke();
            InvalidateVisual();
        }

        _pressPoint = null;
        _pressedChord = null;
        _draggingChord = false;
        _dragTarget = null;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        var point = e.GetPosition(this);
        var hit = FindChord(point);
        if (hit is not null && Song is not null)
        {
            Focus();
            var chord = Song.Lines[hit.LineIndex].Chords[hit.ChordIndex];
            _selectedPlaced = hit;
            _caret = new ChordAnchor(hit.LineIndex, chord.Position, chord.BeforeChar);
            SelectedLineIndex = hit.LineIndex;
            SelectedShapeId = null;
            SelectionChanged?.Invoke();
            InvalidateVisual();
            var menu = new System.Windows.Controls.ContextMenu { PlacementTarget = this };
            var editItem = new System.Windows.Controls.MenuItem { Header = $"修改和弦 ({chord.Name})..." };
            editItem.Click += (_, _) =>
            {
                var anchor = new ChordAnchor(hit.LineIndex, chord.Position, chord.BeforeChar);
                CharacterSlotClicked?.Invoke(anchor, GetAnchorRect(anchor), chord.Name);
            };
            var removeItem = new System.Windows.Controls.MenuItem { Header = "删除此和弦" };
            removeItem.Click += (_, _) => RemoveSelectedChord();
            menu.Items.Add(editItem);

            var voicingCandidates = ChordCatalog.GetCandidates(chord.Name);
            if (voicingCandidates.Count >= 2)
            {
                var currentShape = ChordCatalog.ByShapeId(chord.ShapeId) ?? ChordCatalog.ByName(chord.Name);
                var currentIdx = 0;
                if (currentShape is not null)
                {
                    var found = voicingCandidates
                        .Select((s, i) => (s, i))
                        .FirstOrDefault(x => string.Equals(x.s.Id, currentShape.Id, StringComparison.OrdinalIgnoreCase));
                    if (found.s is not null)
                        currentIdx = found.i;
                }
                var nextIdx = (currentIdx + 1) % voicingCandidates.Count;
                var nextShape = voicingCandidates[nextIdx];

                var cycleItem = new System.Windows.Controls.MenuItem
                {
                    Header = $"↻ 切换指法把位（{currentIdx + 1}/{voicingCandidates.Count} → {nextShape.Label}）"
                };
                cycleItem.Click += (_, _) =>
                {
                    chord.ShapeId = nextShape.Id;
                    Changed?.Invoke();
                    Rebuild();
                    StatusMessageRequested?.Invoke($"已将「{chord.Name}」切换至第 {nextIdx + 1}/{voicingCandidates.Count} 个把位（{nextShape.Label}）。");
                };
                menu.Items.Add(cycleItem);
            }

            menu.Items.Add(removeItem);

            var hitLine = Song.Lines[hit.LineIndex];
            if (hitLine.Chords.Count > 0)
            {
                var summary = FormatChordSummary(hitLine);
                menu.Items.Add(new System.Windows.Controls.Separator());
                var copyLineItem = new System.Windows.Controls.MenuItem { Header = $"复制本句和弦 ({summary})" };
                copyLineItem.Click += (_, _) => CopyLineChords(hit.LineIndex);
                menu.Items.Add(copyLineItem);

                if (FindNextEditableLineIndex(hit.LineIndex) is not null)
                {
                    var dupNextItem = new System.Windows.Controls.MenuItem { Header = "复制本句和弦到下一句 (Ctrl+D)" };
                    dupNextItem.Click += (_, _) => DuplicateLineChordsToNext(hit.LineIndex);
                    menu.Items.Add(dupNextItem);
                }
            }

            menu.IsOpen = true;
            e.Handled = true;
            return;
        }

        var tabHit = FindTabSection(point);
        if (tabHit is not null && Song is not null)
        {
            Focus();
            var menu = new System.Windows.Controls.ContextMenu { PlacementTarget = this };
            var editItem = new System.Windows.Controls.MenuItem { Header = $"编辑六线谱「{tabHit.Section.Title}」..." };
            editItem.Click += (_, _) => TabSectionEditRequested?.Invoke(tabHit.Section);
            var dupItem = new System.Windows.Controls.MenuItem { Header = "复制此六线谱段落" };
            dupItem.Click += (_, _) => TabSectionDuplicateRequested?.Invoke(tabHit.Section);
            var delItem = new System.Windows.Controls.MenuItem { Header = "删除此六线谱段落" };
            delItem.Click += (_, _) => TabSectionDeleteRequested?.Invoke(tabHit.Section);
            menu.Items.Add(editItem);
            menu.Items.Add(dupItem);
            menu.Items.Add(new System.Windows.Controls.Separator());
            menu.Items.Add(delItem);
            menu.IsOpen = true;
            e.Handled = true;
            return;
        }

        var refRow = FindReferenceRow(point);
        if (refRow is not null && Song is not null)
        {
            Focus();
            var menu = new System.Windows.Controls.ContextMenu { PlacementTarget = this };
            var arrangeItem = new System.Windows.Controls.MenuItem { Header = "整理段落与重复段..." };
            arrangeItem.Click += (_, _) => PartArrangeRequested?.Invoke();
            var insertAfterRefItem = new System.Windows.Controls.MenuItem { Header = "在此行后插入段落..." };
            insertAfterRefItem.Click += (_, _) => InsertPartAfterRequested?.Invoke(refRow.Index);
            menu.Items.Add(arrangeItem);
            menu.Items.Add(insertAfterRefItem);
            menu.IsOpen = true;
            e.Handled = true;
            return;
        }

        var lineAnchor = GetAnchorAt(point);
        if (lineAnchor is not null && Song is not null)
        {
            Focus();
            _caret = lineAnchor;
            SelectedLineIndex = lineAnchor.LineIndex;
            SelectionChanged?.Invoke();
            InvalidateVisual();

            var targetLine = Song.Lines[lineAnchor.LineIndex];
            var nextVerse = targetLine.AltLyrics.Count + 2;
            var menu = new System.Windows.Controls.ContextMenu { PlacementTarget = this };
            var hasClipboardSection = false;

            if (targetLine.Chords.Count > 0)
            {
                var summary = FormatChordSummary(targetLine);
                var copyLineItem = new System.Windows.Controls.MenuItem { Header = $"复制本句和弦 ({summary})" };
                copyLineItem.Click += (_, _) => CopyLineChords(lineAnchor.LineIndex);
                menu.Items.Add(copyLineItem);

                if (FindNextEditableLineIndex(lineAnchor.LineIndex) is not null)
                {
                    var dupNextItem = new System.Windows.Controls.MenuItem { Header = "复制本句和弦到下一句 (Ctrl+D)" };
                    dupNextItem.Click += (_, _) => DuplicateLineChordsToNext(lineAnchor.LineIndex);
                    menu.Items.Add(dupNextItem);
                }
                hasClipboardSection = true;
            }

            if (HasCopiedLineChords)
            {
                var pasteLineItem = new System.Windows.Controls.MenuItem { Header = $"粘贴和弦到此句 ({CopiedLineChordsSummary})" };
                pasteLineItem.Click += (_, _) => PasteLineChords(lineAnchor.LineIndex);
                menu.Items.Add(pasteLineItem);

                var parts = PartAnalyzer.BuildParts(Song.Lines);
                var enclosingPart = parts.FirstOrDefault(p =>
                    lineAnchor.LineIndex >= p.StartLine && lineAnchor.LineIndex < p.StartLine + p.LineCount);
                if (enclosingPart is { IsReference: false, LineCount: >= 2 })
                {
                    var pastePartItem = new System.Windows.Controls.MenuItem
                    {
                        Header = $"粘贴和弦到本段全部句（{enclosingPart.Title}）"
                    };
                    pastePartItem.Click += (_, _) => PasteLineChordsToPart(lineAnchor.LineIndex);
                    menu.Items.Add(pastePartItem);
                }
                hasClipboardSection = true;
            }

            if (hasClipboardSection)
                menu.Items.Add(new System.Windows.Controls.Separator());

            var rightClickedLyric = GetLyricTargetAt(point);
            var inlineEditHeader = rightClickedLyric is { VerseIndex: > 0 } vt
                ? $"原位编辑第 {vt.VerseIndex + 1} 遍歌词 (双击)"
                : "原位编辑此句歌词 (双击 / F2)";
            var inlineEditItem = new System.Windows.Controls.MenuItem { Header = inlineEditHeader };
            inlineEditItem.Click += (_, _) =>
            {
                var target = rightClickedLyric ?? GetInlineLyricTarget(lineAnchor.LineIndex, 0, lineAnchor.Position);
                if (target is not null)
                    InlineLyricEditRequested?.Invoke(target);
            };
            menu.Items.Add(inlineEditItem);

            var addAltItem = new System.Windows.Controls.MenuItem
            {
                Header = $"添加叠行歌词（第 {nextVerse} 遍）"
            };
            addAltItem.Click += (_, _) => AddAltLyricRequested?.Invoke(lineAnchor.LineIndex);
            menu.Items.Add(addAltItem);

            if (targetLine.HasStackedLyrics)
            {
                var alignDialogItem = new System.Windows.Controls.MenuItem
                {
                    Header = "叠行字位对齐微调弹窗..."
                };
                alignDialogItem.Click += (_, _) => LineEditRequested?.Invoke(lineAnchor.LineIndex);
                menu.Items.Add(alignDialogItem);
            }

            var arrangePartsItem = new System.Windows.Controls.MenuItem { Header = "整理段落与重复段..." };
            arrangePartsItem.Click += (_, _) => PartArrangeRequested?.Invoke();
            var insertPartAfterItem = new System.Windows.Controls.MenuItem { Header = "在此句后插入段落..." };
            insertPartAfterItem.Click += (_, _) => InsertPartAfterRequested?.Invoke(lineAnchor.LineIndex);
            var insertInterludeItem = new System.Windows.Controls.MenuItem { Header = "在此句后插入间奏六线谱..." };
            insertInterludeItem.Click += (_, _) => TabSectionInsertRequested?.Invoke(TabSectionKind.Interlude, lineAnchor.LineIndex);
            var insertIntroItem = new System.Windows.Controls.MenuItem { Header = "在开头添加前奏六线谱..." };
            insertIntroItem.Click += (_, _) => TabSectionInsertRequested?.Invoke(TabSectionKind.Intro, -1);
            menu.Items.Add(arrangePartsItem);
            menu.Items.Add(insertPartAfterItem);
            menu.Items.Add(new System.Windows.Controls.Separator());
            menu.Items.Add(insertInterludeItem);
            menu.Items.Add(insertIntroItem);
            menu.IsOpen = true;
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var key = e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;

        if (key == Key.F2 && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (ActiveLineIndex is { } f2LineIdx &&
                GetInlineLyricTarget(f2LineIdx, 0, _caret?.LineIndex == f2LineIdx ? _caret.Position : null) is { } f2Target)
            {
                InlineLyricEditRequested?.Invoke(f2Target);
                e.Handled = true;
            }
            else if (Song is { Lines.Count: 0 })
            {
                StartFirstBlankLyricLine();
                e.Handled = true;
            }
            return;
        }

        if (key is Key.Delete or Key.Back)
        {
            if (HasSelectedChord || (_caret is { } c && GetChordAt(c) is not null))
            {
                RemoveSelectedChord();
                e.Handled = true;
            }
            return;
        }

        if (key == Key.Escape)
        {
            _selectedPlaced = null;
            SelectedShapeId = null;
            ActiveChordBrush = null;
            SelectionChanged?.Invoke();
            EscapePressed?.Invoke();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (key == Key.Left && MoveCaret(-1, 0)) { e.Handled = true; return; }
        if (key == Key.Right && MoveCaret(1, 0)) { e.Handled = true; return; }
        if (key == Key.Up && MoveCaret(0, -1)) { e.Handled = true; return; }
        if (key == Key.Down && MoveCaret(0, 1)) { e.Handled = true; return; }

        if (key is Key.Enter or Key.Space && _caret is { } caretAnchor)
        {
            CharacterSlotClicked?.Invoke(caretAnchor, GetAnchorRect(caretAnchor), GetChordAt(caretAnchor));
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
            (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.None)
        {
            if (key == Key.C && ActiveLineIndex is { } copyIdx && CopyLineChords(copyIdx))
            {
                e.Handled = true;
                return;
            }
            if (key == Key.V && ActiveLineIndex is { } pasteIdx && PasteLineChords(pasteIdx))
            {
                e.Handled = true;
                return;
            }
            if (key == Key.D && ActiveLineIndex is { } dupIdx && DuplicateLineChordsToNext(dupIdx))
            {
                e.Handled = true;
                return;
            }
        }

        if (key is >= Key.A and <= Key.G &&
            (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == ModifierKeys.None)
        {
            if (_caret is null && Song is not null && Song.Lines.Count > 0)
                _caret = new ChordAnchor(Math.Clamp(SelectedLineIndex ?? 0, 0, Song.Lines.Count - 1), 0);
            if (_caret is { } activeCaret)
            {
                var letter = ((char)('A' + (key - Key.A))).ToString();
                QuickTypeStarted?.Invoke(activeCaret, GetAnchorRect(activeCaret), letter);
                e.Handled = true;
            }
        }
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        _externalDragShape = e.Data.GetData(typeof(ChordShape)) as ChordShape;
        _externalDragTarget = _externalDragShape is not null ? GetAnchorAt(e.GetPosition(this)) : null;
        e.Effects = _externalDragTarget is not null ? DragDropEffects.Copy : DragDropEffects.None;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnDragLeave(DragEventArgs e)
    {
        base.OnDragLeave(e);
        _externalDragShape = null;
        _externalDragTarget = null;
        InvalidateVisual();
    }

    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        if (e.Data.GetData(typeof(ChordShape)) is ChordShape shape && PlaceChordAt(e.GetPosition(this), shape))
        {
            Focus();
            e.Effects = DragDropEffects.Copy;
            _externalDragShape = null;
            _externalDragTarget = null;
            e.Handled = true;
            return;
        }
        e.Effects = DragDropEffects.None;
        _externalDragShape = null;
        _externalDragTarget = null;
        InvalidateVisual();
        e.Handled = true;
    }

    private void DrawPlacement(DrawingContext dc, Placement placement, bool selected, double lyricBaseline)
    {
        DrawChordAnchorMark(dc, placement, selected, lyricBaseline);

        var shape = ChordCatalog.ByShapeId(placement.Chord.ShapeId) ?? ChordCatalog.ByName(placement.Chord.Name);
        if (Song?.ChordDisplayMode == "diagram" && shape is not null)
        {
            var origin = new Point(placement.X, placement.Y + 2);
            var diagramCenterX = origin.X + DiagramWidth / 2;
            var anchorCenterX = placement.Chord.BeforeChar
                ? Math.Max(6.0, placement.AnchorX - 7.5)
                : placement.AnchorX + placement.CharWidth / 2;
            if (Math.Abs(diagramCenterX - anchorCenterX) > 14.0)
                dc.DrawLine(DiagramConnectorPen, new Point(diagramCenterX, origin.Y + DiagramHeight + 1), new Point(anchorCenterX, lyricBaseline - 1));

            if (selected)
            {
                dc.DrawRoundedRectangle(
                    SelectedChordFill,
                    new Pen(SelectedChordBorder, 1.25),
                    new Rect(origin.X - 3, origin.Y - 2, DiagramWidth + 6, DiagramHeight + 4),
                    5,
                    5);
            }
            ChordDiagramPainter.Draw(dc, shape, origin, DiagramMarkerScale, showCaption: false, _pixelsPerDip);
            return;
        }

        var displayText = GetDisplayChordText(placement.Chord);
        var text = Measure(displayText, ChordFontSize, ChordTypeface);
        var drawY = placement.Y + 2;
        if (selected)
        {
            dc.DrawRoundedRectangle(
                SelectedChordFill,
                new Pen(SelectedChordBorder, 1),
                new Rect(placement.X - 4, drawY - 2, text.Width + 8, text.Height + 4),
                4,
                4);
        }
        DrawText(dc, displayText, placement.X, drawY, ChordFontSize, ChordTypeface, selected ? SelectedChordInk : Green);
    }

    private void DrawChordAnchorMark(DrawingContext dc, Placement placement, bool selected, double lyricBaseline)
    {
        var pen = selected ? SelectedAnchorUnderlinePen : ChordAnchorUnderlinePen;
        var underlineY = lyricBaseline + 24.5;

        if (placement.Chord.BeforeChar)
        {
            var stopX = Math.Max(10.0, placement.AnchorX - 2.5);
            var startX = Math.Max(4.0, stopX - 10.5);
            dc.DrawLine(pen, new Point(startX, underlineY), new Point(stopX, underlineY));
            dc.DrawLine(pen, new Point(stopX, lyricBaseline + 16.0), new Point(stopX, underlineY));
        }
        else
        {
            var startX = placement.AnchorX + 2.0;
            var endX = placement.AnchorX + Math.Max(14.0, placement.CharWidth) - 2.0;
            if (endX <= startX + 2.0)
                endX = startX + 10.0;
            dc.DrawLine(pen, new Point(startX, underlineY), new Point(endX, underlineY));
        }
    }

    private void DrawGhost(DrawingContext dc, ChordPlacement chord, ChordAnchor target)
    {
        var row = _rows.FirstOrDefault(x => x.Index == target.LineIndex);
        if (row is null || row.Line.IsReferenceLine) return;
        var chunkIndex = FindChunk(row.Chunks, target.Position);
        var chunk = row.Chunks[chunkIndex];
        var end = Math.Clamp(target.Position, chunk.Start, chunk.End);
        var (slotX, slotW) = GetSlotBounds(chunk, row.Line, end);
        var x = row.ContentLeft + slotX;
        var y = row.Top + chunk.Top;
        var shape = ChordCatalog.ByShapeId(chord.ShapeId) ?? ChordCatalog.ByName(chord.Name);
        dc.PushOpacity(0.65);
        if (Song?.ChordDisplayMode == "diagram" && shape is not null)
        {
            var centerX = target.BeforeChar ? x - 4.0 : x + Math.Max(16.0, slotW) / 2;
            var originX = Math.Max(4.0, centerX - DiagramWidth / 2);
            ChordDiagramPainter.Draw(dc, shape, new Point(originX, y + 2), DiagramMarkerScale, showCaption: false, _pixelsPerDip);
        }
        else
        {
            var drawX = target.BeforeChar
                ? (target.Position == 0 ? Math.Max(8, x - 20) : Math.Max(8, x - 10))
                : x;
            DrawText(dc, GetDisplayChordText(chord), drawX, y + 2, ChordFontSize, ChordTypeface, Green);
        }
        dc.Pop();
    }

    private void DrawLyricVerseHover(DrawingContext dc, int lineIndex, int verseIndex)
    {
        if (GetInlineLyricTarget(lineIndex, verseIndex) is not { } target)
            return;

        var rect = new Rect(
            target.Bounds.X - 4,
            target.Bounds.Y - 1,
            Math.Max(64, target.Bounds.Width + 8),
            target.Bounds.Height + 2);
        dc.DrawRoundedRectangle(HoverSlotFill, new Pen(HoverSlotBorder, 0.9), rect, 5, 5);
    }

    private void StartFirstBlankLyricLine()
    {
        if (Song is null) return;
        if (Song.Lines.Count == 0)
        {
            Song.Lines.Add(new LyricLine { Text = "" });
            Rebuild();
        }
        if (GetInlineLyricTarget(0, 0, 0) is { } firstTarget)
            InlineLyricEditRequested?.Invoke(firstTarget);
    }

    public LyricInlineTarget? GetInlineLyricTarget(int lineIndex, int verseIndex = 0, int? preferredCharIndex = null)
    {
        if (Song is null || lineIndex < 0 || lineIndex >= Song.Lines.Count)
            return null;

        var row = _rows.FirstOrDefault(r => r.Index == lineIndex);
        if (row is null || row.Line.IsReferenceLine || row.Chunks.Count == 0)
            return null;

        var clampedVerse = Math.Clamp(verseIndex, 0, row.Line.AltLyrics.Count);
        var verseText = clampedVerse == 0 ? row.Line.Text : row.Line.AltLyrics[clampedVerse - 1];
        var charIdx = Math.Clamp(preferredCharIndex ?? verseText.Length, 0, verseText.Length);

        var firstChunk = row.Chunks[0];
        var baseTop = row.Top + firstChunk.LyricTop;
        var y = clampedVerse == 0
            ? baseTop
            : baseTop + LyricLineHeight + (clampedVerse - 1) * AltLyricLineHeight;
        var h = clampedVerse == 0 ? 28.0 : 26.0;

        var colRightEdge = _columnCount == 2 && row.Column == 0
            ? PaperWidthCurrent / 2 - TwoColumnGap / 2
            : PaperWidthCurrent - _pageMarginCurrent;
        var maxWidth = Math.Max(180.0, colRightEdge - row.ContentLeft);

        var naturalWidth = string.IsNullOrEmpty(verseText)
            ? 120.0
            : Measure(verseText, LyricFontSize, LyricTypeface).Width + 16.0;
        var chunkSpanWidth = row.Chunks.Max(c => Math.Max(c.TotalWidth, c.VisualWidth));
        var boxWidth = Math.Min(maxWidth, Math.Max(140.0, Math.Max(naturalWidth, chunkSpanWidth + 8.0)));

        return new LyricInlineTarget(
            lineIndex,
            clampedVerse,
            charIdx,
            new Rect(row.ContentLeft, y, boxWidth, h),
            maxWidth,
            row.IsStacked);
    }

    private LyricInlineTarget? GetLyricTargetAt(Point point)
    {
        if (Song is null || _rows.Count == 0 || !new Rect(0, 0, PaperWidthCurrent, Height).Contains(point))
            return null;

        var targetCol = _columnCount == 2 && point.X >= PaperWidthCurrent / 2 ? 1 : 0;
        var candidates = _rows.Where(r => r.Column == targetCol).ToList();
        if (candidates.Count == 0) candidates = _rows;

        var minYDist = candidates.Min(r => DistanceToRange(point.Y, r.Top, r.Top + r.Height));
        if (minYDist > 24)
            return null;

        var row = candidates
            .Where(r => DistanceToRange(point.Y, r.Top, r.Top + r.Height) <= minYDist + 2)
            .OrderBy(r => DistanceToRange(point.X, r.Left, r.Left + r.Width))
            .ThenBy(r => DistanceToRange(point.Y, r.Top, r.Top + r.Height))
            .First();

        if (row.Line.IsReferenceLine || row.Chunks.Count == 0)
            return null;

        var chunk = row.Chunks.LastOrDefault(c => row.Top + c.Top <= point.Y) ?? row.Chunks[0];
        var targetX = point.X - row.ContentLeft;
        var colRightEdge = _columnCount == 2 && row.Column == 0
            ? PaperWidthCurrent / 2 - TwoColumnGap / 2
            : PaperWidthCurrent - _pageMarginCurrent;

        if (point.X < row.Left - 16 || point.X > colRightEdge + 24)
            return null;
        if (row.Chunks.Count == 1 && targetX > Math.Max(120, Math.Max(chunk.TotalWidth, chunk.VisualWidth)) + 72 && !LyricEditMode)
            return null;

        var baseTop = row.Top + chunk.LyricTop;
        var verseIndex = 0;
        if (row.IsStacked && row.Line.AltLyrics.Count > 0 && point.Y >= baseTop + LyricLineHeight - 3)
        {
            var altIdx = (int)((point.Y - (baseTop + LyricLineHeight - 3)) / AltLyricLineHeight);
            verseIndex = Math.Clamp(altIdx + 1, 1, row.Line.AltLyrics.Count);
        }

        int charIndex;
        if (verseIndex == 0)
        {
            charIndex = ResolveMainCharIndexAtX(chunk, targetX);
        }
        else
        {
            var altText = row.Line.AltLyrics[verseIndex - 1];
            charIndex = ResolveAltCharIndexAtX(row, chunk, altText, targetX);
        }

        return GetInlineLyricTarget(row.Index, verseIndex, charIndex);
    }

    private static int ResolveMainCharIndexAtX(TextChunk chunk, double targetX)
    {
        if (chunk.Slots.Count == 0 || targetX <= 0)
            return chunk.Start;

        foreach (var slot in chunk.Slots)
        {
            if (targetX < slot.X + slot.Width / 2)
                return slot.Start;
            if (targetX < slot.X + slot.Width)
                return slot.End;
        }

        return chunk.End;
    }

    private static int ResolveAltCharIndexAtX(RowLayout row, TextChunk chunk, string altText, double targetX)
    {
        if (string.IsNullOrEmpty(altText))
            return 0;

        var chunkStartCells = 0;
        for (var i = 0; i < chunk.Index && i < row.Chunks.Count; i++)
        {
            var prevMap = row.Chunks[i].CellXMap;
            chunkStartCells += prevMap is { Length: > 1 } ? prevMap.Length - 1 : PartAnalyzer.MeasureHalfCells(row.Chunks[i].Text);
        }

        var localCells = 0;
        if (targetX > 0)
        {
            if (chunk.CellXMap is { Length: > 1 } map)
            {
                localCells = map.Length - 1;
                for (var c = 0; c < map.Length - 1; c++)
                {
                    var mid = (map[c] + map[c + 1]) / 2;
                    if (targetX < mid)
                    {
                        localCells = c;
                        break;
                    }
                }
            }
            else
            {
                localCells = (int)Math.Round(targetX / StackedHalfCellWidth, MidpointRounding.AwayFromZero);
            }
        }

        var targetCells = Math.Max(0, chunkStartCells + localCells);
        var acc = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(altText);
        while (enumerator.MoveNext())
        {
            var elem = enumerator.GetTextElement();
            var units = PartAnalyzer.GetHalfCellUnits(elem);
            if (targetCells < acc + (units + 1) / 2)
                return enumerator.ElementIndex;
            acc += units;
        }

        return altText.Length;
    }

    private ChordAnchor? GetAnchorAt(Point point)
    {
        if (Song is null || _rows.Count == 0 || !new Rect(0, 0, PaperWidthCurrent, Height).Contains(point))
            return null;

        var targetCol = _columnCount == 2 && point.X >= PaperWidthCurrent / 2 ? 1 : 0;
        var candidates = _rows.Where(r => r.Column == targetCol).ToList();
        if (candidates.Count == 0) candidates = _rows;

        var minYDist = candidates.Min(r => DistanceToRange(point.Y, r.Top, r.Top + r.Height));
        if (minYDist > 28)
            return null;

        var row = candidates
            .Where(r => DistanceToRange(point.Y, r.Top, r.Top + r.Height) <= minYDist + 2)
            .OrderBy(r => DistanceToRange(point.X, r.Left, r.Left + r.Width))
            .ThenBy(r => DistanceToRange(point.Y, r.Top, r.Top + r.Height))
            .First();

        if (row.Line.IsReferenceLine)
            return null;

        var chunk = row.Chunks.LastOrDefault(candidate => row.Top + candidate.Top <= point.Y) ?? row.Chunks[0];
        var targetX = point.X - row.ContentLeft;
        if (targetX < -24) return null;
        if (targetX < 0)
            return new ChordAnchor(row.Index, chunk.Start, BeforeChar: true);

        if (chunk.Slots.Count > 0 && targetX > Math.Max(chunk.TotalWidth, chunk.VisualWidth) + 36)
            return null;

        var position = chunk.Start;
        foreach (var slot in chunk.Slots)
        {
            position = slot.Start;
            if (targetX < slot.X + slot.Width) break;
        }
        return new ChordAnchor(row.Index, position, BeforeChar: false);
    }

    private RowLayout? FindReferenceRow(Point point)
    {
        foreach (var row in _rows)
        {
            if (!row.Line.IsReferenceLine) continue;
            var rect = new Rect(row.Left - 4, row.Top, row.Width + 8, row.Height);
            if (rect.Contains(point))
                return row;
        }
        return null;
    }

    private Hit? FindChord(Point point)
    {
        foreach (var row in _rows)
        {
            foreach (var placement in row.Placements)
            {
                var shape = ChordCatalog.ByShapeId(placement.Chord.ShapeId) ?? ChordCatalog.ByName(placement.Chord.Name);
                Rect rect;
                if (Song?.ChordDisplayMode == "diagram" && shape is not null)
                {
                    rect = new Rect(placement.X - 3, placement.Y, DiagramWidth + 6, DiagramHeight + 6);
                }
                else
                {
                    var text = Measure(GetDisplayChordText(placement.Chord), ChordFontSize, ChordTypeface);
                    rect = new Rect(placement.X - 4, placement.Y, Math.Max(18, text.Width + 8), text.Height + 6);
                }
                if (rect.Contains(point))
                    return new Hit(placement.LineIndex, placement.ChordIndex);
            }
        }
        return null;
    }

    private static (double X, double Width) GetSlotBounds(TextChunk chunk, LyricLine line, int position)
    {
        foreach (var slot in chunk.Slots)
        {
            if (position >= slot.Start && position < slot.End)
                return (slot.X, slot.Width);
        }
        var fallbackWidth = line.HasStackedLyrics ? StackedFullCellWidth : LyricFontSize;
        if (chunk.Slots.Count > 0 && position >= chunk.End)
            return (chunk.TotalWidth, fallbackWidth);
        return (0.0, fallbackWidth);
    }

    private List<TextChunk> Wrap(LyricLine line, double maxWidth, bool isDiagramMode)
    {
        var rawSlots = new List<CharSlot>();
        var x = 0d;
        var charTracking = isDiagramMode && line.Chords.Count > 0 ? DiagramCharTracking : 0d;
        foreach (var element in TextElements(line.Text, 0, line.Text.Length))
        {
            var elementWidth = Measure(element.Text, LyricFontSize, LyricTypeface).Width;
            rawSlots.Add(new CharSlot(element.Start, element.Start + element.Text.Length, element.Text, x, elementWidth));
            x += elementWidth + charTracking;
        }

        if (isDiagramMode && line.Chords.Count > 0 && rawSlots.Count > 1)
            ApplyDiagramSpacing(rawSlots, line.Chords);

        var chunks = new List<TextChunk>();
        if (rawSlots.Count == 0)
        {
            chunks.Add(new TextChunk(0, 0, 0, "", [], [], 0));
            return chunks;
        }

        var chunkStartSlot = 0;
        var chunkOriginX = 0d;
        for (var i = 0; i < rawSlots.Count; i++)
        {
            var slot = rawSlots[i];
            if (i > chunkStartSlot && (slot.X - chunkOriginX + slot.Width) > maxWidth)
            {
                chunks.Add(BuildTextChunk(chunks.Count, rawSlots, chunkStartSlot, i, chunkOriginX));
                chunkStartSlot = i;
                chunkOriginX = slot.X;
            }
        }
        chunks.Add(BuildTextChunk(chunks.Count, rawSlots, chunkStartSlot, rawSlots.Count, chunkOriginX));
        return chunks;
    }

    private static TextChunk BuildTextChunk(int chunkIndex, List<CharSlot> rawSlots, int startSlot, int endSlot, double originX)
    {
        var rebased = new List<CharSlot>(endSlot - startSlot);
        var textBuilder = new System.Text.StringBuilder();
        for (var i = startSlot; i < endSlot; i++)
        {
            var s = rawSlots[i];
            rebased.Add(s with { X = s.X - originX });
            textBuilder.Append(s.Text);
        }
        var totalWidth = rebased.Count > 0 ? rebased[^1].X + rebased[^1].Width : 0d;
        return new TextChunk(
            chunkIndex,
            rebased[0].Start,
            rebased[^1].End,
            textBuilder.ToString(),
            [],
            rebased,
            totalWidth,
            VisualWidth: totalWidth);
    }

    private static void ApplyDiagramSpacing(List<CharSlot> slots, List<ChordPlacement> chords)
    {
        var minStep = DiagramWidth + DiagramMinGap;
        var anchorEntries = new List<(int SlotIdx, bool BeforeChar)>();
        foreach (var chord in chords)
        {
            var slotIdx = FindSlotIndex(slots, chord.Position);
            if (slotIdx >= 0)
                anchorEntries.Add((slotIdx, chord.BeforeChar));
        }

        var distinctAnchors = anchorEntries
            .GroupBy(a => a.SlotIdx)
            .Select(g => (SlotIdx: g.Key, BeforeChar: g.All(x => x.BeforeChar)))
            .OrderBy(a => a.SlotIdx)
            .ToList();

        if (distinctAnchors.Count < 2) return;

        for (var i = 1; i < distinctAnchors.Count; i++)
        {
            var prev = distinctAnchors[i - 1];
            var curr = distinctAnchors[i];
            var gapCount = curr.SlotIdx - prev.SlotIdx;
            if (gapCount <= 0) continue;

            var centerA = prev.BeforeChar ? slots[prev.SlotIdx].X - 4.0 : slots[prev.SlotIdx].X + slots[prev.SlotIdx].Width / 2;
            var centerB = curr.BeforeChar ? slots[curr.SlotIdx].X - 4.0 : slots[curr.SlotIdx].X + slots[curr.SlotIdx].Width / 2;
            var deficit = minStep - (centerB - centerA);
            if (deficit <= 0) continue;

            var extraPerGap = Math.Min(64.0, deficit / gapCount);
            for (var k = prev.SlotIdx + 1; k < slots.Count; k++)
            {
                var steps = Math.Min(k - prev.SlotIdx, gapCount);
                slots[k] = slots[k] with { X = slots[k].X + steps * extraPerGap };
            }
        }
    }

    private static int FindSlotIndex(List<CharSlot> slots, int charPos)
    {
        if (slots.Count == 0) return -1;
        for (var i = 0; i < slots.Count; i++)
        {
            if (charPos >= slots[i].Start && charPos < slots[i].End)
                return i;
        }
        return slots.Count - 1;
    }

    private static List<TextChunk> WrapStacked(LyricLine line, double maxWidth, bool isDiagramMode)
    {
        var primaryElems = new List<(int Start, int End, string Text, int StartCell, int Units)>();
        var totalPrimaryCells = 0;
        foreach (var element in TextElements(line.Text, 0, line.Text.Length))
        {
            var units = PartAnalyzer.GetHalfCellUnits(element.Text);
            primaryElems.Add((element.Start, element.Start + element.Text.Length, element.Text, totalPrimaryCells, units));
            totalPrimaryCells += units;
        }

        var maxAltCells = line.AltLyrics.Count > 0 ? line.AltLyrics.Max(PartAnalyzer.MeasureHalfCells) : 0;
        var totalCells = Math.Max(1, Math.Max(totalPrimaryCells, maxAltCells));
        var cellX = new double[totalCells + 1];
        for (var c = 0; c <= totalCells; c++)
            cellX[c] = c * StackedHalfCellWidth;

        if (isDiagramMode && line.Chords.Count > 0 && primaryElems.Count > 1)
        {
            var minStep = DiagramWidth + DiagramMinGap;
            var anchors = line.Chords
                .Select(c => (ElemIdx: FindPrimaryElementIndex(primaryElems, c.Position), c.BeforeChar))
                .Where(x => x.ElemIdx >= 0)
                .GroupBy(x => x.ElemIdx)
                .Select(g => (ElemIdx: g.Key, BeforeChar: g.All(x => x.BeforeChar)))
                .OrderBy(x => x.ElemIdx)
                .ToList();

            for (var i = 1; i < anchors.Count; i++)
            {
                var prev = anchors[i - 1];
                var curr = anchors[i];
                var elemA = primaryElems[prev.ElemIdx];
                var elemB = primaryElems[curr.ElemIdx];
                var cellGap = elemB.StartCell - elemA.StartCell;
                if (cellGap <= 0) continue;

                var centerA = prev.BeforeChar
                    ? cellX[elemA.StartCell] - 4.0
                    : (cellX[elemA.StartCell] + cellX[elemA.StartCell + elemA.Units]) / 2;
                var centerB = curr.BeforeChar
                    ? cellX[elemB.StartCell] - 4.0
                    : (cellX[elemB.StartCell] + cellX[elemB.StartCell + elemB.Units]) / 2;
                var deficit = minStep - (centerB - centerA);
                if (deficit <= 0) continue;

                var extraPerHalfCell = Math.Min(32.0, deficit / cellGap);
                for (var c = elemA.StartCell + 1; c < cellX.Length; c++)
                {
                    var steps = Math.Min(c - elemA.StartCell, cellGap);
                    cellX[c] += steps * extraPerHalfCell;
                }
            }
        }

        var rawPrimary = new List<(int Start, int End, string Text, int StartCell, int EndCell, List<CharSlot> Slots, double Width, double[] CellXMap, double VisualWidth)>();
        var chunkStartElem = 0;
        var chunkStartCell = 0;
        var chunkOriginX = 0d;

        void EmitStackedChunk(int endElem, int endCell)
        {
            var slots = new List<CharSlot>(endElem - chunkStartElem);
            var textBuilder = new System.Text.StringBuilder();
            for (var e = chunkStartElem; e < endElem; e++)
            {
                var el = primaryElems[e];
                var sx = cellX[el.StartCell] - chunkOriginX;
                var sw = cellX[el.StartCell + el.Units] - cellX[el.StartCell];
                slots.Add(new CharSlot(el.Start, el.End, el.Text, sx, sw));
                textBuilder.Append(el.Text);
            }
            var mapLen = Math.Max(1, endCell - chunkStartCell + 1);
            var localMap = new double[mapLen];
            for (var k = 0; k < mapLen && chunkStartCell + k < cellX.Length; k++)
                localMap[k] = cellX[chunkStartCell + k] - chunkOriginX;
            var startChar = slots.Count > 0 ? slots[0].Start : 0;
            var endChar = slots.Count > 0 ? slots[^1].End : 0;
            var chunkW = slots.Count > 0 ? slots[^1].X + slots[^1].Width : 0d;
            var visualWidth = Math.Max(chunkW, localMap.Length > 0 ? localMap[^1] : 0d);
            rawPrimary.Add((startChar, endChar, textBuilder.ToString(), chunkStartCell, endCell, slots, chunkW, localMap, visualWidth));
        }

        for (var i = 0; i < primaryElems.Count; i++)
        {
            var el = primaryElems[i];
            var endX = cellX[el.StartCell + el.Units] - chunkOriginX;
            if (i > chunkStartElem && endX > maxWidth)
            {
                EmitStackedChunk(i, el.StartCell);
                chunkStartElem = i;
                chunkStartCell = el.StartCell;
                chunkOriginX = cellX[el.StartCell];
            }
        }

        if (primaryElems.Count > 0)
            EmitStackedChunk(primaryElems.Count, totalCells);
        else
            rawPrimary.Add((0, 0, "", 0, 0, [], 0, [0], 0));

        var altRanges = rawPrimary
            .Select(p => (p.Start, p.End, p.Text, p.StartCell, EndCell: p.StartCell + p.CellXMap.Length - 1))
            .ToList();
        var altSliced = new List<List<string>>(rawPrimary.Count);
        for (var i = 0; i < rawPrimary.Count; i++)
            altSliced.Add([]);

        foreach (var alt in line.AltLyrics)
        {
            var slices = SliceByCellRanges(alt, altRanges);
            for (var i = 0; i < rawPrimary.Count; i++)
                altSliced[i].Add(slices[i]);
        }

        var chunks = new List<TextChunk>(rawPrimary.Count);
        for (var i = 0; i < rawPrimary.Count; i++)
        {
            var p = rawPrimary[i];
            chunks.Add(new TextChunk(i, p.Start, p.End, p.Text, altSliced[i], p.Slots, p.Width, p.CellXMap, VisualWidth: p.VisualWidth));
        }
        return chunks;
    }

    private static int FindPrimaryElementIndex(List<(int Start, int End, string Text, int StartCell, int Units)> elems, int charPos)
    {
        if (elems.Count == 0) return -1;
        for (var i = 0; i < elems.Count; i++)
        {
            if (charPos >= elems[i].Start && charPos < elems[i].End)
                return i;
        }
        return elems.Count - 1;
    }

    private static List<string> SliceByCellRanges(string text, List<(int Start, int End, string Text, int StartCell, int EndCell)> ranges)
    {
        var results = Enumerable.Repeat("", ranges.Count).ToList();
        if (string.IsNullOrEmpty(text) || ranges.Count == 0) return results;

        var builders = new System.Text.StringBuilder[ranges.Count];
        for (var i = 0; i < ranges.Count; i++)
            builders[i] = new System.Text.StringBuilder();

        var cellPos = 0;
        var chunkIdx = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            var units = PartAnalyzer.GetHalfCellUnits(element);
            while (chunkIdx < ranges.Count - 1 && cellPos >= ranges[chunkIdx].EndCell)
            {
                chunkIdx++;
                if (cellPos > ranges[chunkIdx].StartCell && builders[chunkIdx].Length == 0)
                    builders[chunkIdx].Append(' ', cellPos - ranges[chunkIdx].StartCell);
            }
            builders[chunkIdx].Append(element);
            cellPos += units;
        }

        for (var i = 0; i < ranges.Count; i++)
            results[i] = builders[i].ToString();
        return results;
    }

    private double MeasureElementAt(string text, int index)
    {
        if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length)
            return LyricFontSize;
        var element = StringInfo.GetNextTextElement(text, index);
        return Measure(element, LyricFontSize, LyricTypeface).Width;
    }

    private static List<int> GetElementPositions(string text)
    {
        var result = new List<int>();
        if (string.IsNullOrEmpty(text))
        {
            result.Add(0);
            return result;
        }
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
            result.Add(enumerator.ElementIndex);
        if (result.Count == 0) result.Add(0);
        return result;
    }

    private static int FindClosestElementIndex(List<int> positions, int charPos)
    {
        var idx = 0;
        for (var i = 0; i < positions.Count; i++)
        {
            if (positions[i] <= charPos) idx = i;
            else break;
        }
        return idx;
    }

    private static IEnumerable<(int Start, string Text)> TextElements(string text, int start, int end)
    {
        if (text.Length == 0) yield break;
        var enumerator = StringInfo.GetTextElementEnumerator(text, start);
        while (enumerator.MoveNext())
        {
            var index = enumerator.ElementIndex;
            if (index >= end) yield break;
            var element = enumerator.GetTextElement();
            if (index + element.Length <= end) yield return (index, element);
        }
    }

    private static int FindChunk(IReadOnlyList<TextChunk> chunks, int position)
    {
        for (var i = 0; i < chunks.Count; i++)
            if (position >= chunks[i].Start && (position < chunks[i].End || i == chunks.Count - 1)) return i;
        return chunks.Count - 1;
    }

    private FormattedText Measure(string text, double fontSize, Typeface typeface) =>
        ChordDiagramPainter.CreateText(text, fontSize, typeface, Ink, _pixelsPerDip);

    private void DrawText(DrawingContext dc, string text, double x, double y, double fontSize, Typeface typeface, Brush brush)
    {
        dc.DrawText(ChordDiagramPainter.CreateText(text, fontSize, typeface, brush, _pixelsPerDip), new Point(x, y));
    }

    private void DrawCentered(DrawingContext dc, string text, double fontSize, double y, Typeface typeface, Brush brush)
    {
        var formatted = ChordDiagramPainter.CreateText(text, fontSize, typeface, brush, _pixelsPerDip);
        dc.DrawText(formatted, new Point((PaperWidthCurrent - formatted.Width) / 2, y));
    }

    private static double DistanceToRange(double value, double min, double max) =>
        value < min ? min - value : value > max ? value - max : 0;

    private static void SortChords(LyricLine line) =>
        line.Chords = line.Chords.OrderBy(x => x.Position).ThenBy(x => x.BeforeChar ? 0 : 1).ToList();

    private TabSectionLayout? FindTabSection(Point point)
    {
        foreach (var layout in _tabLayouts)
        {
            var hitRect = new Rect(
                layout.Bounds.Left - 8,
                layout.Bounds.Top - 4,
                layout.Bounds.Width + 16,
                layout.Bounds.Height + 2);
            if (hitRect.Contains(point))
                return layout;
        }
        return null;
    }

    private sealed record CharSlot(int Start, int End, string Text, double X, double Width);
    private sealed record TextChunk(int Index, int Start, int End, string Text, List<string> AltSlices, List<CharSlot> Slots, double TotalWidth, double[]? CellXMap = null, double Top = 0, double LyricTop = 0, double VisualWidth = 0);
    private sealed record Placement(int LineIndex, int ChordIndex, ChordPlacement Chord, double X, double AnchorX, double CharWidth, string TargetChar, double Y, int ChunkIndex);
    private sealed record RowLayout(int Index, int Column, LyricLine Line, double Left, double ContentLeft, double Width, double Top, double Height, bool HasPartHeader, double PartHeaderTop, bool IsStacked, List<TextChunk> Chunks, List<Placement> Placements, double PartHeaderWidth);
    private sealed record TabSectionLayout(TabSection Section, Rect Bounds);
    private sealed record Hit(int LineIndex, int ChordIndex);
}
