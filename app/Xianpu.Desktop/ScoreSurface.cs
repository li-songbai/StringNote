using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Xianpu.Desktop;

public sealed class ScoreSurface : FrameworkElement
{
    private const double FlowPaperWidth = 1120;
    private const double FlowMargin = 56;
    private const double PhraseGap = 40;
    private const int MaxPhrasesPerRow = 3;
    private const double SingleColumnPaperWidth = 920;
    private const double TwoColumnPaperWidth = 1120;
    private const double SingleColumnMargin = 60;
    private const double TwoColumnMargin = 52;
    private const double TwoColumnGap = 48;
    private const double HeaderHeight = 96;
    private const double LyricFontSize = 19;
    private const double ChordFontSize = 16.5;
    private const double LyricLineHeight = 28;
    private const double RowBottomGap = 6;
    private const double NameMarkerHeight = 24;
    private const double DiagramMarkerHeight = 68;
    private const double DiagramMarkerScale = 0.62;

    private static readonly Typeface LyricTypeface = new(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface ChordTypeface = new(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private static readonly Brush Ink = Freeze(new SolidColorBrush(Color.FromRgb(42, 47, 41)));
    private static readonly Brush Green = Freeze(new SolidColorBrush(Color.FromRgb(49, 92, 57)));
    private static readonly Brush SelectedChordInk = Freeze(new SolidColorBrush(Color.FromRgb(30, 68, 39)));
    private static readonly Brush Quiet = Freeze(new SolidColorBrush(Color.FromRgb(126, 134, 122)));
    private static readonly Brush Rule = Freeze(new SolidColorBrush(Color.FromRgb(228, 232, 224)));
    private static readonly Brush ColumnRule = Freeze(new SolidColorBrush(Color.FromRgb(236, 239, 233)));
    private static readonly Brush HoverCharFill = Freeze(new SolidColorBrush(Color.FromArgb(46, 108, 158, 114)));
    private static readonly Brush HoverSlotFill = Freeze(new SolidColorBrush(Color.FromArgb(32, 108, 158, 114)));
    private static readonly Brush HoverSlotBorder = Freeze(new SolidColorBrush(Color.FromArgb(120, 96, 142, 102)));
    private static readonly Brush CaretCharFill = Freeze(new SolidColorBrush(Color.FromArgb(65, 79, 134, 88)));
    private static readonly Brush CaretBorder = Freeze(new SolidColorBrush(Color.FromRgb(74, 124, 82)));
    private static readonly Brush SelectedChordFill = Freeze(new SolidColorBrush(Color.FromRgb(233, 243, 234)));
    private static readonly Brush SelectedChordBorder = Freeze(new SolidColorBrush(Color.FromRgb(94, 140, 100)));
    private static readonly Pen CharSlotTickPen = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(75, 110, 140, 115))), 0.8));

    private readonly List<RowLayout> _rows = [];
    private double _pixelsPerDip = 1;
    private int _columnCount = 1;
    private double _pageMarginCurrent = FlowMargin;
    private Point? _pressPoint;
    private Hit? _pressedChord;
    private bool _draggingChord;
    private ChordAnchor? _dragTarget;
    private ChordAnchor? _caret;
    private ChordAnchor? _hoverAnchor;
    private Hit? _selectedPlaced;
    private ChordShape? _externalDragShape;
    private ChordAnchor? _externalDragTarget;

    public SongModel? Song { get; private set; }
    public string? SelectedShapeId { get; set; }
    public string? ActiveChordBrush { get; set; }
    public bool ContinuousPaintMode { get; set; }
    public int? SelectedLineIndex { get; private set; }
    public bool IsExporting { get; set; }
    public bool HasSelectedChord => _selectedPlaced is not null;
    public ChordAnchor? Caret => _caret;
    public double PaperWidthCurrent { get; private set; } = FlowPaperWidth;

    // FrameworkElement has no Background property; this property makes the full paper a hit-test target.
    public Brush Background { get; set; } = Brushes.White;

    public event Action? Changed;
    public event Action? SelectionChanged;
    public event Action<int>? LineEditRequested;
    public event Action<int>? LineSelected;
    public event Action<ChordAnchor, Rect, string?>? CharacterSlotClicked;
    public event Action<ChordAnchor, Rect, string>? QuickTypeStarted;

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

    public ScoreSurface()
    {
        Width = PaperWidthCurrent;
        Height = 460;
        Focusable = true;
        AllowDrop = true;
        SnapsToDevicePixels = true;
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
        SelectionChanged?.Invoke();
        Rebuild();
    }

    public void Rebuild()
    {
        _rows.Clear();
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

        if (Song.Lines.Count == 0)
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

        var isHorizontalFlow = Song.ColumnMode != 1 && Song.ColumnMode != 2;
        var leftCount = _columnCount == 2 ? (Song.Lines.Count + 1) / 2 : Song.Lines.Count;
        var yLeft = HeaderHeight;
        var yRight = HeaderHeight;

        var curX = _pageMarginCurrent;
        var curY = HeaderHeight;
        var phrasesInRow = 0;
        var rowMaxHeight = 0d;

        for (var index = 0; index < Song.Lines.Count; index++)
        {
            var line = Song.Lines[index];
            var col = _columnCount == 2 && index >= leftCount ? 1 : 0;

            var chunks = Wrap(line.Text, columnWidth);
            var height = 0d;
            for (var i = 0; i < chunks.Count; i++)
            {
                var chunkTop = height;
                var lyricOffset = chunkTop + markerHeight;
                chunks[i] = chunks[i] with { Top = chunkTop, LyricTop = lyricOffset };
                height = lyricOffset + LyricLineHeight;
            }
            height += RowBottomGap;

            double rowLeft;
            double rowTop;
            double rowWidth;

            if (isHorizontalFlow)
            {
                var isSingleShortLine = chunks.Count == 1 && !string.IsNullOrWhiteSpace(line.Text);
                var singleWidth = isSingleShortLine ? Measure(line.Text, LyricFontSize, LyricTypeface).Width : usableWidth;

                if (phrasesInRow > 0 && isSingleShortLine && phrasesInRow < MaxPhrasesPerRow &&
                    curX + PhraseGap + singleWidth <= _pageMarginCurrent + usableWidth)
                {
                    rowLeft = curX + PhraseGap;
                    rowTop = curY;
                    rowWidth = singleWidth;
                    curX = rowLeft + singleWidth;
                    phrasesInRow++;
                    rowMaxHeight = Math.Max(rowMaxHeight, height);
                }
                else
                {
                    if (phrasesInRow > 0)
                    {
                        curY += rowMaxHeight;
                        curX = _pageMarginCurrent;
                        phrasesInRow = 0;
                        rowMaxHeight = 0;
                    }

                    rowLeft = _pageMarginCurrent;
                    rowTop = curY;
                    if (isSingleShortLine)
                    {
                        rowWidth = singleWidth;
                        curX = rowLeft + singleWidth;
                        phrasesInRow = 1;
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
                rowLeft = col == 0 ? _pageMarginCurrent : _pageMarginCurrent + columnWidth + TwoColumnGap;
                rowTop = col == 0 ? yLeft : yRight;
                rowWidth = columnWidth;
                if (col == 0) yLeft += height;
                else yRight += height;
            }

            var placements = BuildPlacements(index, line, chunks, rowLeft, rowTop);
            _rows.Add(new RowLayout(index, col, line, rowLeft, rowWidth, rowTop, height, chunks, placements));
        }

        var yBottom = isHorizontalFlow
            ? curY + (phrasesInRow > 0 ? rowMaxHeight : 0)
            : Math.Max(yLeft, yRight);
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

    private List<Placement> BuildPlacements(int lineIndex, LyricLine line, List<TextChunk> chunks, double rowLeft, double rowTop)
    {
        var placements = new List<Placement>();
        var prevRightByChunk = new Dictionary<int, double>();
        for (var chordIndex = 0; chordIndex < line.Chords.Count; chordIndex++)
        {
            var chord = line.Chords[chordIndex];
            var position = Math.Clamp(chord.Position, 0, line.Text.Length);
            var chunkIndex = FindChunk(chunks, position);
            var chunk = chunks[chunkIndex];
            var anchorX = rowLeft + Measure(line.Text[chunk.Start..position], LyricFontSize, LyricTypeface).Width;
            var charWidth = Math.Max(16, MeasureElementAt(line.Text, position));
            var targetChar = position < line.Text.Length ? StringInfo.GetNextTextElement(line.Text, position) : "";
            var charCenterX = anchorX + charWidth / 2;
            var drawX = chord.BeforeChar ? Math.Max(8, anchorX - 18) : anchorX;
            if (Song?.ChordDisplayMode != "diagram")
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
                var minX = chord.BeforeChar ? Math.Max(8, rowLeft - 30) : rowLeft;
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
            return new Rect(_pageMarginCurrent, HeaderHeight, 22, 52);

        var pos = Math.Clamp(anchor.Position, 0, row.Line.Text.Length);
        var chunkIndex = FindChunk(row.Chunks, pos);
        var chunk = row.Chunks[chunkIndex];
        var clampedPos = Math.Clamp(pos, chunk.Start, chunk.End);
        var x = row.Left + Measure(row.Line.Text[chunk.Start..clampedPos], LyricFontSize, LyricTypeface).Width;
        var charWidth = MeasureElementAt(row.Line.Text, clampedPos);
        var y = row.Top + chunk.Top;
        var height = (chunk.LyricTop - chunk.Top) + LyricLineHeight;
        return new Rect(x, y, Math.Max(18, charWidth), height);
    }

    public bool MoveCaret(int deltaChars, int deltaLines = 0)
    {
        if (Song is null || Song.Lines.Count == 0) return false;
        var current = _caret ?? new ChordAnchor(Math.Clamp(SelectedLineIndex ?? 0, 0, Song.Lines.Count - 1), 0);
        var lineIndex = Math.Clamp(current.LineIndex, 0, Song.Lines.Count - 1);
        var positions = GetElementPositions(Song.Lines[lineIndex].Text);
        var elemIdx = FindClosestElementIndex(positions, current.Position);

        if (deltaLines != 0)
        {
            var targetLine = Math.Clamp(lineIndex + deltaLines, 0, Song.Lines.Count - 1);
            var targetPositions = GetElementPositions(Song.Lines[targetLine].Text);
            var targetElemIdx = Math.Clamp(elemIdx, 0, targetPositions.Count - 1);
            lineIndex = targetLine;
            elemIdx = targetElemIdx;
            positions = targetPositions;
        }

        if (deltaChars != 0)
        {
            var nextElemIdx = elemIdx + deltaChars;
            while (nextElemIdx < 0 && lineIndex > 0)
            {
                lineIndex--;
                positions = GetElementPositions(Song.Lines[lineIndex].Text);
                nextElemIdx += positions.Count;
            }
            while (nextElemIdx >= positions.Count && lineIndex < Song.Lines.Count - 1)
            {
                nextElemIdx -= positions.Count;
                lineIndex++;
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

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        DrawScore(dc);
    }

    public System.Windows.Media.Imaging.BitmapSource RenderPng()
    {
        Rebuild();
        var visual = new DrawingVisual();
        IsExporting = true;
        try
        {
            using (var drawing = visual.RenderOpen())
                DrawScore(drawing);
        }
        finally
        {
            IsExporting = false;
        }
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)Math.Ceiling(PaperWidthCurrent * 2),
            (int)Math.Ceiling(Height * 2),
            192,
            192,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
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

        var title = string.IsNullOrWhiteSpace(Song.Title) ? "未命名歌曲" : Song.Title;
        DrawCentered(dc, title, 28, 18, ChordTypeface, Ink);
        var metadata = string.Join("      ", new[]
        {
            string.IsNullOrWhiteSpace(Song.OriginalKey) ? null : $"原调 {Song.OriginalKey}",
            string.IsNullOrWhiteSpace(Song.SelectedKey) ? null : $"选调 {Song.SelectedKey}",
            string.IsNullOrWhiteSpace(Song.Capo) ? null : $"变调夹 {Song.Capo}"
        }.Where(x => x is not null));
        if (!string.IsNullOrWhiteSpace(metadata))
            DrawCentered(dc, metadata, 13.5, 57, LyricTypeface, Quiet);
        dc.DrawLine(new Pen(Rule, 1), new Point(_pageMarginCurrent, 82), new Point(PaperWidthCurrent - _pageMarginCurrent, 82));

        if (_rows.Count == 0)
        {
            DrawCentered(dc, "这首曲谱还没有歌词", 16, HeaderHeight + 54, ChordTypeface, Quiet);
            DrawCentered(dc, "点击上方「导入 LRC」或「编辑全文歌词」铺开歌词，再点击字位填入左手和弦", 12.5, HeaderHeight + 84, LyricTypeface, Quiet);
            return;
        }

        if (_columnCount == 2)
        {
            var dividerX = PaperWidthCurrent / 2;
            dc.DrawLine(new Pen(ColumnRule, 1), new Point(dividerX, HeaderHeight + 4), new Point(dividerX, Height - 24));
        }

        if (!IsExporting && _hoverAnchor is { } hover && (_caret is null || hover != _caret))
            DrawAnchorHighlight(dc, hover, isCaret: false);

        if (!IsExporting && _caret is { } caret)
            DrawAnchorHighlight(dc, caret, isCaret: true);

        foreach (var row in _rows)
        {
            if (Song.ColumnMode == 0 && row.Left > _pageMarginCurrent + 1 && row.Chunks.Count > 0)
            {
                var sepText = "·";
                var sepWidth = Measure(sepText, LyricFontSize, LyricTypeface).Width;
                DrawText(dc, sepText, row.Left - PhraseGap / 2 - sepWidth / 2, row.Top + row.Chunks[0].LyricTop, LyricFontSize, LyricTypeface, Quiet);
            }
            foreach (var chunk in row.Chunks)
            {
                var baseline = row.Top + chunk.LyricTop;
                DrawText(dc, chunk.Text, row.Left, baseline, LyricFontSize, LyricTypeface, Ink);
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

    private void DrawAnchorHighlight(DrawingContext dc, ChordAnchor anchor, bool isCaret)
    {
        var row = _rows.FirstOrDefault(r => r.Index == anchor.LineIndex);
        if (row is null) return;
        var pos = Math.Clamp(anchor.Position, 0, row.Line.Text.Length);
        var chunkIndex = FindChunk(row.Chunks, pos);
        var chunk = row.Chunks[chunkIndex];
        var clampedPos = Math.Clamp(pos, chunk.Start, chunk.End);
        var x = row.Left + Measure(row.Line.Text[chunk.Start..clampedPos], LyricFontSize, LyricTypeface).Width;
        var charW = Math.Max(18, MeasureElementAt(row.Line.Text, clampedPos));
        var lyricY = row.Top + chunk.LyricTop;
        var chordTopY = row.Top + chunk.Top;
        var markerHeight = chunk.LyricTop - chunk.Top;

        if (anchor.BeforeChar)
        {
            var barBrush = isCaret ? CaretBorder : HoverSlotBorder;
            dc.DrawRectangle(barBrush, null, new Rect(x - 2, chordTopY + 2, 2, lyricY + 22 - (chordTopY + 2)));
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
        var tickX = row.Left;
        dc.DrawLine(CharSlotTickPen, new Point(tickX, lyricY + 22), new Point(tickX, lyricY + 26));
        foreach (var element in TextElements(row.Line.Text, chunk.Start, chunk.End))
        {
            tickX += Measure(element.Text, LyricFontSize, LyricTypeface).Width;
            dc.DrawLine(CharSlotTickPen, new Point(tickX, lyricY + 22), new Point(tickX, lyricY + 26));
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        var point = e.GetPosition(this);
        if (e.ClickCount >= 2 && FindChord(point) is null && GetAnchorAt(point) is { } editAnchor)
        {
            LineEditRequested?.Invoke(editAnchor.LineIndex);
            e.Handled = true;
            return;
        }
        _pressedChord = FindChord(point);
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
            var hover = GetAnchorAt(point);
            var overChord = FindChord(point) is not null;
            Cursor = hover is not null || overChord ? Cursors.Hand : Cursors.Arrow;
            if (hover != _hoverAnchor)
            {
                _hoverAnchor = hover;
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
        if (_hoverAnchor is not null)
        {
            _hoverAnchor = null;
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
            menu.Items.Add(removeItem);
            menu.IsOpen = true;
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Delete or Key.Back)
        {
            if (HasSelectedChord || (_caret is { } c && GetChordAt(c) is not null))
            {
                RemoveSelectedChord();
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Escape)
        {
            _selectedPlaced = null;
            SelectedShapeId = null;
            ActiveChordBrush = null;
            SelectionChanged?.Invoke();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left && MoveCaret(-1, 0)) { e.Handled = true; return; }
        if (e.Key == Key.Right && MoveCaret(1, 0)) { e.Handled = true; return; }
        if (e.Key == Key.Up && MoveCaret(0, -1)) { e.Handled = true; return; }
        if (e.Key == Key.Down && MoveCaret(0, 1)) { e.Handled = true; return; }

        if (e.Key is Key.Enter or Key.Space && _caret is { } caretAnchor)
        {
            CharacterSlotClicked?.Invoke(caretAnchor, GetAnchorRect(caretAnchor), GetChordAt(caretAnchor));
            e.Handled = true;
            return;
        }

        if (e.Key is >= Key.A and <= Key.G &&
            (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == ModifierKeys.None)
        {
            if (_caret is null && Song is not null && Song.Lines.Count > 0)
                _caret = new ChordAnchor(Math.Clamp(SelectedLineIndex ?? 0, 0, Song.Lines.Count - 1), 0);
            if (_caret is { } activeCaret)
            {
                var letter = ((char)('A' + (e.Key - Key.A))).ToString();
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
        var shape = ChordCatalog.ByShapeId(placement.Chord.ShapeId) ?? ChordCatalog.ByName(placement.Chord.Name);
        if (Song?.ChordDisplayMode == "diagram" && shape is not null)
        {
            var width = 96 * DiagramMarkerScale;
            var height = 88 * DiagramMarkerScale;
            var origin = new Point(placement.X - width / 2 + 9, placement.Y + 2);
            if (selected)
            {
                dc.DrawRoundedRectangle(
                    SelectedChordFill,
                    new Pen(SelectedChordBorder, 1.25),
                    new Rect(origin.X - 3, origin.Y - 2, width + 6, height + 4),
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

    private void DrawGhost(DrawingContext dc, ChordPlacement chord, ChordAnchor target)
    {
        var row = _rows.FirstOrDefault(x => x.Index == target.LineIndex);
        if (row is null) return;
        var chunkIndex = FindChunk(row.Chunks, target.Position);
        var chunk = row.Chunks[chunkIndex];
        var end = Math.Clamp(target.Position, chunk.Start, chunk.End);
        var x = row.Left + Measure(row.Line.Text[chunk.Start..end], LyricFontSize, LyricTypeface).Width;
        if (target.BeforeChar)
            x = target.Position == 0 ? Math.Max(8, x - 20) : Math.Max(8, x - 10);
        var y = row.Top + chunk.Top;
        var shape = ChordCatalog.ByShapeId(chord.ShapeId) ?? ChordCatalog.ByName(chord.Name);
        dc.PushOpacity(0.65);
        if (Song?.ChordDisplayMode == "diagram" && shape is not null)
        {
            ChordDiagramPainter.Draw(dc, shape, new Point(x - 48 * DiagramMarkerScale + 9, y + 2), DiagramMarkerScale, showCaption: false, _pixelsPerDip);
        }
        else
        {
            DrawText(dc, GetDisplayChordText(chord), x, y + 2, ChordFontSize, ChordTypeface, Green);
        }
        dc.Pop();
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

        var chunk = row.Chunks.LastOrDefault(candidate => row.Top + candidate.Top <= point.Y) ?? row.Chunks[0];
        var targetX = point.X - row.Left;
        if (targetX < -24) return null;
        if (targetX < 0)
            return new ChordAnchor(row.Index, chunk.Start, BeforeChar: true);

        var totalChunkWidth = Measure(chunk.Text, LyricFontSize, LyricTypeface).Width;
        if (chunk.Text.Length > 0 && targetX > totalChunkWidth + 36)
            return null;

        var width = 0d;
        var position = chunk.Start;
        foreach (var element in TextElements(row.Line.Text, chunk.Start, chunk.End))
        {
            var nextWidth = Measure(element.Text, LyricFontSize, LyricTypeface).Width;
            position = element.Start;
            if (targetX < width + nextWidth) break;
            width += nextWidth;
        }
        return new ChordAnchor(row.Index, position, BeforeChar: false);
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
                    var width = 96 * DiagramMarkerScale;
                    var height = 88 * DiagramMarkerScale;
                    rect = new Rect(placement.X - width / 2 + 6, placement.Y, width + 6, height + 4);
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

    private List<TextChunk> Wrap(string text, double maxWidth)
    {
        var chunks = new List<TextChunk>();
        var current = "";
        var start = 0;
        var width = 0d;
        foreach (var element in TextElements(text, 0, text.Length))
        {
            var elementWidth = Measure(element.Text, LyricFontSize, LyricTypeface).Width;
            if (current.Length > 0 && width + elementWidth > maxWidth)
            {
                chunks.Add(new TextChunk(chunks.Count, start, element.Start, current));
                current = "";
                start = element.Start;
                width = 0;
            }
            current += element.Text;
            width += elementWidth;
        }
        if (current.Length > 0) chunks.Add(new TextChunk(chunks.Count, start, text.Length, current));
        if (chunks.Count == 0) chunks.Add(new TextChunk(0, 0, 0, ""));
        return chunks;
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

    private sealed record TextChunk(int Index, int Start, int End, string Text, double Top = 0, double LyricTop = 0);
    private sealed record Placement(int LineIndex, int ChordIndex, ChordPlacement Chord, double X, double AnchorX, double CharWidth, string TargetChar, double Y, int ChunkIndex);
    private sealed record RowLayout(int Index, int Column, LyricLine Line, double Left, double Width, double Top, double Height, List<TextChunk> Chunks, List<Placement> Placements);
    private sealed record Hit(int LineIndex, int ChordIndex);
}
