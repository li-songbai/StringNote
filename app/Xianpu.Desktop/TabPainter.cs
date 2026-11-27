using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Xianpu.Desktop;

public sealed record TabPlayheadState(
    int ActiveBarIndex = -1,
    int ActiveColumn = -1,
    int LoopStartBar = -1,
    int LoopEndBar = -1);

public static class TabPainter
{
    public const double ClefWidth = 32;
    public const double HeaderHeight = 26;
    public const double ChordRowHeight = 26;
    public const double StringSpacing = 9.5;
    public const double StaffHeight = StringSpacing * 5; // 47.5
    public const double RhythmRowHeight = 20;
    public const double RowBottomGap = 12;
    public const double SectionBottomGap = 10;

    private static readonly Typeface TabTitleTypeface = new(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private static readonly Typeface ClefTypeface = new(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private static readonly Typeface FretTypeface = new(new FontFamily("Consolas, Microsoft YaHei"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private static readonly Typeface MetaTypeface = new(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private static readonly Brush Ink = Freeze(new SolidColorBrush(Color.FromRgb(42, 47, 41)));
    private static readonly Brush Green = Freeze(new SolidColorBrush(Color.FromRgb(49, 92, 57)));
    private static readonly Brush PillBg = Freeze(new SolidColorBrush(Color.FromRgb(234, 243, 237)));
    private static readonly Brush PillBorder = Freeze(new SolidColorBrush(Color.FromRgb(148, 191, 164)));
    private static readonly Brush Quiet = Freeze(new SolidColorBrush(Color.FromRgb(115, 124, 114)));
    private static readonly Brush StringBrush = Freeze(new SolidColorBrush(Color.FromRgb(175, 184, 175)));
    private static readonly Brush BarlineBrush = Freeze(new SolidColorBrush(Color.FromRgb(108, 118, 108)));
    private static readonly Brush TickBrush = Freeze(new SolidColorBrush(Color.FromRgb(203, 213, 225)));

    private static readonly Pen StringPen = FreezePen(new Pen(StringBrush, 0.85));
    private static readonly Pen BarlinePen = FreezePen(new Pen(BarlineBrush, 1.1));
    private static readonly Pen HeavyBarlinePen = FreezePen(new Pen(Ink, 2.1));
    private static readonly Pen TickPen = FreezePen(new Pen(TickBrush, 0.85));
    private static readonly Pen PillPen = FreezePen(new Pen(PillBorder, 0.9));
    private static readonly Pen StemPen = FreezePen(new Pen(Ink, 1.15));
    private static readonly Pen BeamPen = FreezePen(new Pen(Ink, 2.2));
    private static readonly Pen SubBeamPen = FreezePen(new Pen(Ink, 1.8));
    private static readonly Pen LinkPen = FreezePen(new Pen(Green, 1.15));
    private static readonly Pen StrumPen = FreezePen(new Pen(Ink, 1.25));
    private static readonly Pen QuietPen = FreezePen(new Pen(Quiet, 0.95));
    private static readonly Pen DashedPmPen = CreateDashedPen(Quiet, 0.95);
    private static readonly Brush PlayheadColFill = Freeze(new SolidColorBrush(Color.FromArgb(42, 47, 107, 72)));
    private static readonly Brush PlayheadAccentFill = Freeze(new SolidColorBrush(Color.FromArgb(56, 226, 98, 36)));
    private static readonly Brush LoopBarFill = Freeze(new SolidColorBrush(Color.FromArgb(20, 37, 99, 235)));
    private static readonly Pen LoopBarPen = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(140, 37, 99, 235))), 1.2) { DashStyle = DashStyles.Dash });
    private static readonly Pen AccentMarkPen = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(217, 102, 31))), 1.55));

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

    private static Pen CreateDashedPen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness) { DashStyle = DashStyles.Dash };
        pen.Freeze();
        return pen;
    }

    public static int ComputeBarsPerRow(double availableWidth, int totalBars)
    {
        var maxFit = Math.Clamp((int)Math.Floor((availableWidth - ClefWidth) / 215.0), 1, 4);
        return totalBars > 0 ? Math.Min(maxFit, totalBars) : maxFit;
    }

    public static double MeasureSectionHeight(TabSection section, double availableWidth)
    {
        var rhythmHeight = section.ShowRhythmStems ? RhythmRowHeight : 6.0;
        var bars = section.Bars;
        if (bars.Count == 0)
            return HeaderHeight + ChordRowHeight + StaffHeight + rhythmHeight + RowBottomGap + SectionBottomGap;

        var barsPerRow = ComputeBarsPerRow(availableWidth, bars.Count);
        var rowCount = (int)Math.Ceiling(bars.Count / (double)barsPerRow);
        var rowHeight = ChordRowHeight + StaffHeight + rhythmHeight + RowBottomGap;
        return HeaderHeight + rowCount * rowHeight + SectionBottomGap;
    }

    public static void DrawSection(
        DrawingContext dc,
        TabSection section,
        double left,
        double top,
        double availableWidth,
        Brush paperBackground,
        double pixelsPerDip,
        TabPlayheadState? playhead = null,
        List<(int BarIndex, Rect Bounds)>? outBarBounds = null)
    {
        var bars = section.Bars;
        var rhythmHeight = section.ShowRhythmStems ? RhythmRowHeight : 6.0;
        var titleText = string.IsNullOrWhiteSpace(section.Title)
            ? section.Kind switch
            {
                TabSectionKind.Intro => "前奏",
                TabSectionKind.Outro => "尾奏",
                _ => "间奏"
            }
            : section.Title.Trim();

        var badgeText = CreateText(titleText, 11.5, TabTitleTypeface, Green, pixelsPerDip);
        var badgeRect = new Rect(left, top + 1, badgeText.Width + 16, 20);
        dc.DrawRoundedRectangle(PillBg, PillPen, badgeRect, 5, 5);
        dc.DrawText(badgeText, new Point(badgeRect.Left + 8, badgeRect.Top + (badgeRect.Height - badgeText.Height) / 2));

        var metaParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(section.TimeSignature))
            metaParts.Add(section.TimeSignature);
        if (bars.Count > 0)
            metaParts.Add($"{bars.Count} 小节");
        var metaText = CreateText(string.Join(" · ", metaParts), 10.5, MetaTypeface, Quiet, pixelsPerDip);
        dc.DrawText(metaText, new Point(badgeRect.Right + 8, badgeRect.Top + (badgeRect.Height - metaText.Height) / 2));

        var y = top + HeaderHeight;
        if (bars.Count == 0)
        {
            DrawEmptyRow(dc, left, y, availableWidth, pixelsPerDip);
            return;
        }

        var barsPerRow = ComputeBarsPerRow(availableWidth, bars.Count);
        var standardBarWidth = (availableWidth - ClefWidth) / barsPerRow;

        for (var rowStart = 0; rowStart < bars.Count; rowStart += barsPerRow)
        {
            var rowCount = Math.Min(barsPerRow, bars.Count - rowStart);
            var staffTop = y + ChordRowHeight;
            var staffBottom = staffTop + StaffHeight;
            var rowEndX = left + ClefWidth + rowCount * standardBarWidth;

            // 6 horizontal string lines (1弦 top .. 6弦 bottom)
            for (var s = 0; s < 6; s++)
            {
                var sy = staffTop + s * StringSpacing;
                dc.DrawLine(StringPen, new Point(left, sy), new Point(rowEndX, sy));
            }

            // TAB vertical clef letters
            DrawClef(dc, left, staffTop, pixelsPerDip);

            // Opening barline of row
            dc.DrawLine(BarlinePen, new Point(left + ClefWidth, staffTop), new Point(left + ClefWidth, staffBottom));

            for (var i = 0; i < rowCount; i++)
            {
                var barIndex = rowStart + i;
                var bar = bars[barIndex];
                var barLeft = left + ClefWidth + i * standardBarWidth;
                var barRight = barLeft + standardBarWidth;
                var colCount = Math.Max(1, bar.ColumnCount);
                var colWidth = standardBarWidth / colCount;

                outBarBounds?.Add((barIndex, new Rect(barLeft, y, standardBarWidth, ChordRowHeight + StaffHeight + rhythmHeight)));

                // Optional A-B loop bar highlight
                if (playhead is not null && playhead.LoopStartBar >= 0 &&
                    barIndex >= playhead.LoopStartBar && barIndex <= Math.Max(playhead.LoopStartBar, playhead.LoopEndBar))
                {
                    var loopRect = new Rect(barLeft + 1, staffTop - 5, standardBarWidth - 2, StaffHeight + 10);
                    dc.DrawRoundedRectangle(LoopBarFill, LoopBarPen, loopRect, 4, 4);
                }

                // Optional active playhead column highlight
                if (playhead is not null && playhead.ActiveBarIndex == barIndex &&
                    playhead.ActiveColumn >= 0 && playhead.ActiveColumn < colCount)
                {
                    var isAccentedCol = bar.ColumnMetas.Any(m => m.Column == playhead.ActiveColumn && m.Accent);
                    var phX = barLeft + playhead.ActiveColumn * colWidth;
                    var phRect = new Rect(phX + 1, staffTop - 6, Math.Max(6, colWidth - 2), StaffHeight + 12);
                    dc.DrawRoundedRectangle(isAccentedCol ? PlayheadAccentFill : PlayheadColFill, null, phRect, 4, 4);
                }

                // Accent marks (>) and triplet badges (3) above staff
                foreach (var meta in bar.ColumnMetas)
                {
                    if (meta.Column < 0 || meta.Column >= colCount) continue;
                    var mcx = barLeft + (meta.Column + 0.5) * colWidth;
                    if (meta.Accent)
                    {
                        var ay = staffTop - 9.5;
                        dc.DrawLine(AccentMarkPen, new Point(mcx - 4.2, ay - 2.6), new Point(mcx + 4.2, ay));
                        dc.DrawLine(AccentMarkPen, new Point(mcx - 4.2, ay + 2.6), new Point(mcx + 4.2, ay));
                    }
                    if (meta.TupletGroup == 3 && meta.Column % 3 == 1)
                    {
                        var tFt = CreateText("3", 8.8, TabTitleTypeface, Quiet, pixelsPerDip);
                        dc.DrawText(tFt, new Point(mcx - tFt.Width / 2, staffBottom + rhythmHeight - 11));
                    }
                }

                // Subtle beat ticks when rhythm stems are disabled
                if (!section.ShowRhythmStems)
                {
                    for (var c = 0; c < colCount; c++)
                    {
                        var cx = barLeft + (c + 0.5) * colWidth;
                        var isMajorBeat = colCount % 2 == 0 && c % 2 == 0;
                        var tickLen = isMajorBeat ? 5.0 : 3.0;
                        dc.DrawLine(TickPen, new Point(cx, staffBottom + 2), new Point(cx, staffBottom + 2 + tickLen));
                    }
                }

                // Literal chord annotations above staff (never transposed)
                foreach (var mark in bar.ChordMarks)
                {
                    if (mark.Column < 0 || mark.Column >= colCount || string.IsNullOrWhiteSpace(mark.Text))
                        continue;
                    var cx = barLeft + (mark.Column + 0.5) * colWidth;
                    var ft = CreateText(mark.Text, 11.5, TabTitleTypeface, Green, pixelsPerDip);
                    var markX = Math.Clamp(cx - ft.Width / 2, barLeft + 2, Math.Max(barLeft + 2, barRight - ft.Width - 2));
                    dc.DrawText(ft, new Point(markX, y + 1));
                }

                // Strum arrows and arpeggio waves behind fret masks
                DrawBarPreNotes(dc, bar, barLeft, barRight, staffTop, StringSpacing, scale: 1.0);

                // Fret numbers (and grace notes) on strings
                foreach (var note in bar.Notes)
                {
                    if (note.StringIndex is < 0 or > 5 || note.Column < 0 || note.Column >= colCount || string.IsNullOrWhiteSpace(note.Fret))
                        continue;

                    var cx = barLeft + (note.Column + 0.5) * colWidth;
                    var sy = staffTop + note.StringIndex * StringSpacing;
                    DrawNoteCell(dc, note, cx, sy, 10.5, FretTypeface, Ink, paperBackground, pixelsPerDip, scale: 1.0);
                }

                // Links, repeat barlines, and rhythm stems/beams/rests
                DrawBarPostNotes(
                    dc,
                    bar,
                    barLeft,
                    barRight,
                    staffTop,
                    staffBottom,
                    StringSpacing,
                    section.ShowRhythmStems,
                    paperBackground,
                    pixelsPerDip,
                    scale: 1.0);

                // Closing barline (skip if RepeatEnd already drew heavy double repeat)
                if (!bar.RepeatEnd)
                {
                    if (barIndex == bars.Count - 1)
                    {
                        dc.DrawLine(BarlinePen, new Point(barRight - 4, staffTop), new Point(barRight - 4, staffBottom));
                        dc.DrawLine(HeavyBarlinePen, new Point(barRight, staffTop), new Point(barRight, staffBottom));
                    }
                    else
                    {
                        dc.DrawLine(BarlinePen, new Point(barRight, staffTop), new Point(barRight, staffBottom));
                    }
                }
            }

            y += ChordRowHeight + StaffHeight + rhythmHeight + RowBottomGap;
        }
    }

    public static Dictionary<int, TabDuration> ResolveBarDurations(TabBar bar)
    {
        var result = new Dictionary<int, TabDuration>();
        var colCount = Math.Max(1, bar.ColumnCount);
        var metaByCol = bar.ColumnMetas.ToDictionary(m => m.Column);

        var activeCols = new SortedSet<int>();
        foreach (var note in bar.Notes)
        {
            if (note.Column >= 0 && note.Column < colCount && !string.IsNullOrWhiteSpace(note.Fret))
                activeCols.Add(note.Column);
        }
        foreach (var meta in bar.ColumnMetas)
        {
            if (meta.Column >= 0 && meta.Column < colCount &&
                (meta.Strum != TabStrumKind.None || meta.IsRest))
            {
                activeCols.Add(meta.Column);
            }
        }

        if (activeCols.Count == 0) return result;
        var ordered = activeCols.ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var col = ordered[i];
            if (metaByCol.TryGetValue(col, out var meta) && meta.Duration != TabDuration.Auto)
            {
                result[col] = meta.Duration;
                continue;
            }

            var nextCol = i + 1 < ordered.Count ? ordered[i + 1] : colCount;
            var span = Math.Max(1, nextCol - col);
            var ratio = span / (double)colCount;
            TabDuration inferred;
            if (ratio >= 0.875)
                inferred = TabDuration.Whole;
            else if (ratio >= 0.45)
                inferred = TabDuration.Half;
            else if (ratio >= 0.22)
                inferred = TabDuration.Quarter;
            else if (colCount >= 12 && ratio <= 0.085)
                inferred = TabDuration.Sixteenth;
            else
                inferred = TabDuration.Eighth;

            result[col] = inferred;
        }
        return result;
    }

    public static void DrawNoteCell(
        DrawingContext dc,
        TabNote note,
        double cx,
        double sy,
        double fretFontSize,
        Typeface fretTypeface,
        Brush inkBrush,
        Brush paperBackground,
        double pixelsPerDip,
        double scale = 1.0)
    {
        var displayFret = note.Technique switch
        {
            TabNoteTechnique.GhostNote => $"({note.Fret})",
            TabNoteTechnique.NaturalHarmonic or TabNoteTechnique.ArtificialHarmonic => $"<{note.Fret}>",
            _ => note.Fret
        };
        var ft = CreateText(displayFret, fretFontSize, fretTypeface, inkBrush, pixelsPerDip);
        var mainLeft = cx - ft.Width / 2;
        var mainRight = mainLeft + ft.Width;
        var mainMask = new Rect(mainLeft - 2.5 * scale, sy - ft.Height / 2 + 0.5 * scale, ft.Width + 5.0 * scale, ft.Height - 1.0 * scale);
        dc.DrawRectangle(paperBackground, null, mainMask);
        dc.DrawText(ft, new Point(mainLeft, sy - ft.Height / 2));

        var linkPen = Math.Abs(scale - 1.0) < 0.01 ? LinkPen : FreezePen(new Pen(Green, 1.15 * scale));
        if (note.Technique == TabNoteTechnique.SlideIn && string.IsNullOrWhiteSpace(note.GraceFret))
            dc.DrawLine(linkPen, new Point(mainLeft - 8.5 * scale, sy + 3.2 * scale), new Point(mainLeft - 1.5 * scale, sy - 2.2 * scale));

        if (note.Technique == TabNoteTechnique.SlideOut)
            dc.DrawLine(linkPen, new Point(mainRight + 1.5 * scale, sy - 2.2 * scale), new Point(mainRight + 8.5 * scale, sy + 3.2 * scale));

        if (string.IsNullOrWhiteSpace(note.GraceFret))
            return;

        var graceSize = Math.Max(6.5, fretFontSize * 0.72);
        var graceFt = CreateText(note.GraceFret.Trim(), graceSize, fretTypeface, inkBrush, pixelsPerDip);
        var linkGap = 7.0 * scale;
        var graceX = mainLeft - linkGap - graceFt.Width;
        var graceY = sy - graceFt.Height / 2 - 1.2 * scale;
        var graceMask = new Rect(graceX - 1.5 * scale, graceY + 0.5 * scale, graceFt.Width + 3.0 * scale, graceFt.Height - 1.0 * scale);
        dc.DrawRectangle(paperBackground, null, graceMask);
        dc.DrawText(graceFt, new Point(graceX, graceY));

        var xStart = graceX + graceFt.Width + 0.8 * scale;
        var xEnd = mainLeft - 1.0 * scale;
        if (xEnd <= xStart + 1.0) return;

        var effLink = note.GraceLink == TabLinkKind.None ? TabLinkKind.Slide : note.GraceLink;
        if (effLink == TabLinkKind.Slide)
        {
            var gNum = TryParseFretNumber(note.GraceFret);
            var tNum = TryParseFretNumber(note.Fret);
            var up = gNum is not null && tNum is not null ? tNum.Value >= gNum.Value : true;
            var dy = (up ? 2.0 : -2.0) * scale;
            dc.DrawLine(linkPen, new Point(xStart, sy + dy), new Point(xEnd, sy - dy));

            var sFt = CreateText("s", 7.2 * scale, TabTitleTypeface, Green, pixelsPerDip);
            var midX = (xStart + xEnd) / 2;
            var sY = sy - 9.2 * scale;
            dc.DrawRectangle(paperBackground, null, new Rect(midX - sFt.Width / 2 - 0.8 * scale, sY + 0.5 * scale, sFt.Width + 1.6 * scale, sFt.Height - 1.0 * scale));
            dc.DrawText(sFt, new Point(midX - sFt.Width / 2, sY));
        }
        else
        {
            var startPt = new Point(xStart, sy - 2.2 * scale);
            var endPt = new Point(xEnd, sy - 2.2 * scale);
            var ctrlPt = new Point((xStart + xEnd) / 2, sy - 7.0 * scale);
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(startPt, isFilled: false, isClosed: false);
                ctx.QuadraticBezierTo(ctrlPt, endPt, isStroked: true, isSmoothJoin: true);
            }
            geo.Freeze();
            dc.DrawGeometry(null, linkPen, geo);

            if (effLink is TabLinkKind.HammerOn or TabLinkKind.PullOff)
            {
                var tok = effLink == TabLinkKind.HammerOn ? "h" : "p";
                var lFt = CreateText(tok, 7.2 * scale, TabTitleTypeface, Green, pixelsPerDip);
                var midX = (xStart + xEnd) / 2;
                var lY = sy - 11.2 * scale;
                dc.DrawRectangle(paperBackground, null, new Rect(midX - lFt.Width / 2 - 0.8 * scale, lY + 0.5 * scale, lFt.Width + 1.6 * scale, lFt.Height - 1.0 * scale));
                dc.DrawText(lFt, new Point(midX - lFt.Width / 2, lY));
            }
        }
    }

    public static void DrawBarDecorations(
        DrawingContext dc,
        TabBar bar,
        double barLeft,
        double barRight,
        double staffTop,
        double staffBottom,
        double stringSpacing,
        bool showRhythmStems,
        Brush paperBackground,
        double pixelsPerDip,
        double scale = 1.0)
    {
        DrawBarPreNotes(dc, bar, barLeft, barRight, staffTop, stringSpacing, scale);
        DrawBarPostNotes(dc, bar, barLeft, barRight, staffTop, staffBottom, stringSpacing, showRhythmStems, paperBackground, pixelsPerDip, scale);
    }

    public static void DrawBarPreNotes(
        DrawingContext dc,
        TabBar bar,
        double barLeft,
        double barRight,
        double staffTop,
        double stringSpacing,
        double scale = 1.0)
    {
        var colCount = Math.Max(1, bar.ColumnCount);
        var colWidth = (barRight - barLeft) / colCount;
        var strumPen = Math.Abs(scale - 1.0) < 0.01 ? StrumPen : FreezePen(new Pen(Ink, 1.25 * scale));

        foreach (var meta in bar.ColumnMetas)
        {
            if (meta.Column < 0 || meta.Column >= colCount || meta.Strum == TabStrumKind.None)
                continue;

            var cx = barLeft + (meta.Column + 0.5) * colWidth;
            var colStrings = bar.Notes
                .Where(n => n.Column == meta.Column && !string.IsNullOrWhiteSpace(n.Fret) && n.StringIndex is >= 0 and <= 5)
                .Select(n => n.StringIndex)
                .ToList();

            var topStr = colStrings.Count > 0 ? colStrings.Min() : 0;
            var botStr = colStrings.Count > 0 ? colStrings.Max() : 5;
            if (botStr - topStr < 2)
            {
                topStr = 0;
                botStr = 5;
            }
            var yTop = staffTop + topStr * stringSpacing - 2.0 * scale;
            var yBot = staffTop + botStr * stringSpacing + 2.0 * scale;

            switch (meta.Strum)
            {
                case TabStrumKind.Down:
                {
                    dc.DrawLine(strumPen, new Point(cx, yBot), new Point(cx, yTop));
                    dc.DrawLine(strumPen, new Point(cx, yTop), new Point(cx - 3.2 * scale, yTop + 4.5 * scale));
                    dc.DrawLine(strumPen, new Point(cx, yTop), new Point(cx + 3.2 * scale, yTop + 4.5 * scale));
                    break;
                }
                case TabStrumKind.Up:
                {
                    dc.DrawLine(strumPen, new Point(cx, yTop), new Point(cx, yBot));
                    dc.DrawLine(strumPen, new Point(cx, yBot), new Point(cx - 3.2 * scale, yBot - 4.5 * scale));
                    dc.DrawLine(strumPen, new Point(cx, yBot), new Point(cx + 3.2 * scale, yBot - 4.5 * scale));
                    break;
                }
                case TabStrumKind.Mute:
                {
                    dc.DrawLine(strumPen, new Point(cx, yBot), new Point(cx, yTop));
                    var midY = (yTop + yBot) / 2;
                    var r = 3.8 * scale;
                    dc.DrawLine(strumPen, new Point(cx - r, midY - r), new Point(cx + r, midY + r));
                    dc.DrawLine(strumPen, new Point(cx + r, midY - r), new Point(cx - r, midY + r));
                    break;
                }
                case TabStrumKind.Arpeggio:
                {
                    var ax = cx - 9.0 * scale;
                    var waveStep = 3.4 * scale;
                    var waveAmp = 2.0 * scale;
                    var geo = new StreamGeometry();
                    using (var ctx = geo.Open())
                    {
                        var curY = yBot;
                        ctx.BeginFigure(new Point(ax, curY), isFilled: false, isClosed: false);
                        var dir = -1;
                        while (curY - waveStep > yTop + 3.0 * scale)
                        {
                            curY -= waveStep;
                            ctx.LineTo(new Point(ax + dir * waveAmp, curY), isStroked: true, isSmoothJoin: true);
                            dir = -dir;
                        }
                        ctx.LineTo(new Point(ax, yTop), isStroked: true, isSmoothJoin: true);
                    }
                    geo.Freeze();
                    dc.DrawGeometry(null, strumPen, geo);
                    dc.DrawLine(strumPen, new Point(ax, yTop), new Point(ax - 2.6 * scale, yTop + 3.6 * scale));
                    dc.DrawLine(strumPen, new Point(ax, yTop), new Point(ax + 2.6 * scale, yTop + 3.6 * scale));
                    break;
                }
            }
        }
    }

    public static void DrawBarPostNotes(
        DrawingContext dc,
        TabBar bar,
        double barLeft,
        double barRight,
        double staffTop,
        double staffBottom,
        double stringSpacing,
        bool showRhythmStems,
        Brush paperBackground,
        double pixelsPerDip,
        double scale = 1.0)
    {
        var colCount = Math.Max(1, bar.ColumnCount);
        var colWidth = (barRight - barLeft) / colCount;
        var barPen = Math.Abs(scale - 1.0) < 0.01 ? BarlinePen : FreezePen(new Pen(BarlineBrush, 1.1 * scale));
        var heavyPen = Math.Abs(scale - 1.0) < 0.01 ? HeavyBarlinePen : FreezePen(new Pen(Ink, 2.1 * scale));
        var linkPen = Math.Abs(scale - 1.0) < 0.01 ? LinkPen : FreezePen(new Pen(Green, 1.15 * scale));
        var stemPen = Math.Abs(scale - 1.0) < 0.01 ? StemPen : FreezePen(new Pen(Ink, 1.15 * scale));
        var beamPen = Math.Abs(scale - 1.0) < 0.01 ? BeamPen : FreezePen(new Pen(Ink, 2.2 * scale));
        var subBeamPen = Math.Abs(scale - 1.0) < 0.01 ? SubBeamPen : FreezePen(new Pen(Ink, 1.8 * scale));

        // 1. Repeat barlines
        if (bar.RepeatStart)
        {
            dc.DrawLine(heavyPen, new Point(barLeft, staffTop), new Point(barLeft, staffBottom));
            dc.DrawLine(barPen, new Point(barLeft + 3.5 * scale, staffTop), new Point(barLeft + 3.5 * scale, staffBottom));
            dc.DrawEllipse(Ink, null, new Point(barLeft + 7.2 * scale, staffTop + 1.5 * stringSpacing), 1.4 * scale, 1.4 * scale);
            dc.DrawEllipse(Ink, null, new Point(barLeft + 7.2 * scale, staffTop + 3.5 * stringSpacing), 1.4 * scale, 1.4 * scale);
        }

        if (bar.RepeatEnd)
        {
            dc.DrawEllipse(Ink, null, new Point(barRight - 7.2 * scale, staffTop + 1.5 * stringSpacing), 1.4 * scale, 1.4 * scale);
            dc.DrawEllipse(Ink, null, new Point(barRight - 7.2 * scale, staffTop + 3.5 * stringSpacing), 1.4 * scale, 1.4 * scale);
            dc.DrawLine(barPen, new Point(barRight - 3.5 * scale, staffTop), new Point(barRight - 3.5 * scale, staffBottom));
            dc.DrawLine(heavyPen, new Point(barRight, staffTop), new Point(barRight, staffBottom));
        }

        // 2. Note-to-note links (H / P / S / Tie)
        foreach (var note in bar.Notes)
        {
            if (note.LinkToNext == TabLinkKind.None || note.StringIndex is < 0 or > 5 || note.Column < 0 || note.Column >= colCount)
                continue;

            var nextNote = bar.Notes
                .Where(n => n.StringIndex == note.StringIndex && n.Column > note.Column && n.Column < colCount && !string.IsNullOrWhiteSpace(n.Fret))
                .OrderBy(n => n.Column)
                .FirstOrDefault();
            if (nextNote is null) continue;

            var x1 = barLeft + (note.Column + 0.5) * colWidth;
            var x2 = barLeft + (nextNote.Column + 0.5) * colWidth;
            var sy = staffTop + note.StringIndex * stringSpacing;

            if (note.LinkToNext == TabLinkKind.Slide)
            {
                var p1 = TryParseFretNumber(note.Fret);
                var p2 = TryParseFretNumber(nextNote.Fret);
                var up = p1 is not null && p2 is not null ? p2.Value >= p1.Value : true;
                var dy = (up ? 2.5 : -2.5) * scale;
                dc.DrawLine(linkPen, new Point(x1 + 5.5 * scale, sy + dy), new Point(x2 - 5.5 * scale, sy - dy));
                var sFt = CreateText("S", 7.8 * scale, TabTitleTypeface, Green, pixelsPerDip);
                var midX = (x1 + x2) / 2;
                dc.DrawRectangle(paperBackground, null, new Rect(midX - sFt.Width / 2 - 1, sy - 9.5 * scale, sFt.Width + 2, sFt.Height - 1));
                dc.DrawText(sFt, new Point(midX - sFt.Width / 2, sy - 10 * scale));
            }
            else
            {
                var span = Math.Max(10.0, x2 - x1 - 10 * scale);
                var arcLift = Math.Min(8.5 * scale, Math.Max(4.5 * scale, span * 0.18));
                var startPt = new Point(x1 + 5.0 * scale, sy - 3.2 * scale);
                var endPt = new Point(x2 - 5.0 * scale, sy - 3.2 * scale);
                var ctrlPt = new Point((startPt.X + endPt.X) / 2, startPt.Y - arcLift);

                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(startPt, isFilled: false, isClosed: false);
                    ctx.QuadraticBezierTo(ctrlPt, endPt, isStroked: true, isSmoothJoin: true);
                }
                geo.Freeze();
                dc.DrawGeometry(null, linkPen, geo);

                if (note.LinkToNext is TabLinkKind.HammerOn or TabLinkKind.PullOff)
                {
                    var label = note.LinkToNext == TabLinkKind.HammerOn ? "H" : "P";
                    var ft = CreateText(label, 7.8 * scale, TabTitleTypeface, Green, pixelsPerDip);
                    var labelX = (startPt.X + endPt.X) / 2 - ft.Width / 2;
                    var labelY = startPt.Y - arcLift * 0.6 - ft.Height + 1.5 * scale;
                    dc.DrawRectangle(paperBackground, null, new Rect(labelX - 1, labelY + 1, ft.Width + 2, ft.Height - 2));
                    dc.DrawText(ft, new Point(labelX, labelY));
                }
            }
        }

        // 2b. Note & column expression techniques (Bend, Harmonic labels, Vibrato, Palm Mute)
        DrawBarTechniques(dc, bar, barLeft, barRight, colCount, colWidth, staffTop, stringSpacing, linkPen, paperBackground, pixelsPerDip, scale);

        // 3. Rhythm stems, beams, dots & rests
        if (!showRhythmStems) return;

        var durations = ResolveBarDurations(bar);
        if (durations.Count == 0) return;

        var metaByCol = bar.ColumnMetas.ToDictionary(m => m.Column);
        var stemTop = staffBottom + 3.0 * scale;
        var stemBottom = staffBottom + 15.5 * scale;
        var halfStemTop = staffBottom + 8.0 * scale;
        var beatCols = colCount == 16 ? 4 : (colCount is 6 or 12 ? 3 : 2);

        foreach (var (col, dur) in durations.OrderBy(kv => kv.Key))
        {
            var cx = barLeft + (col + 0.5) * colWidth;
            var meta = metaByCol.GetValueOrDefault(col);
            if (meta?.IsRest == true)
            {
                DrawRestGlyph(dc, dur, cx, (stemTop + stemBottom) / 2, stemPen, scale);
                if (meta.Dotted)
                    dc.DrawEllipse(Ink, null, new Point(cx + 5.0 * scale, (stemTop + stemBottom) / 2), 1.35 * scale, 1.35 * scale);
                continue;
            }

            if (dur == TabDuration.Half)
                dc.DrawLine(stemPen, new Point(cx, halfStemTop), new Point(cx, stemBottom));
            else if (dur is TabDuration.Quarter or TabDuration.Eighth or TabDuration.Sixteenth)
                dc.DrawLine(stemPen, new Point(cx, stemTop), new Point(cx, stemBottom));

            if (meta?.Dotted == true)
                dc.DrawEllipse(Ink, null, new Point(cx + 4.2 * scale, stemBottom - 3.2 * scale), 1.35 * scale, 1.35 * scale);
        }

        // Beam consecutive Eighth / Sixteenth non-rest columns within each beat group
        var orderedNonRest = durations
            .Where(kv => metaByCol.GetValueOrDefault(kv.Key)?.IsRest != true && kv.Value is TabDuration.Eighth or TabDuration.Sixteenth)
            .OrderBy(kv => kv.Key)
            .ToList();

        var run = new List<(int Col, TabDuration Dur)>();
        void FlushRun()
        {
            if (run.Count == 0) return;
            if (run.Count == 1)
            {
                var cx = barLeft + (run[0].Col + 0.5) * colWidth;
                dc.DrawLine(subBeamPen, new Point(cx, stemBottom), new Point(cx + 5.2 * scale, stemBottom - 3.6 * scale));
                if (run[0].Dur == TabDuration.Sixteenth)
                    dc.DrawLine(subBeamPen, new Point(cx, stemBottom - 3.2 * scale), new Point(cx + 5.2 * scale, stemBottom - 6.8 * scale));
            }
            else
            {
                var xStart = barLeft + (run[0].Col + 0.5) * colWidth;
                var xEnd = barLeft + (run[^1].Col + 0.5) * colWidth;
                dc.DrawLine(beamPen, new Point(xStart, stemBottom), new Point(xEnd, stemBottom));

                var subY = stemBottom - 3.4 * scale;
                var idx = 0;
                while (idx < run.Count)
                {
                    if (run[idx].Dur != TabDuration.Sixteenth)
                    {
                        idx++;
                        continue;
                    }
                    var endIdx = idx;
                    while (endIdx + 1 < run.Count && run[endIdx + 1].Dur == TabDuration.Sixteenth)
                        endIdx++;

                    var sx1 = barLeft + (run[idx].Col + 0.5) * colWidth;
                    if (endIdx > idx)
                    {
                        var sx2 = barLeft + (run[endIdx].Col + 0.5) * colWidth;
                        dc.DrawLine(subBeamPen, new Point(sx1, subY), new Point(sx2, subY));
                    }
                    else
                    {
                        var hookDir = idx == 0 ? 1 : -1;
                        dc.DrawLine(subBeamPen, new Point(sx1, subY), new Point(sx1 + hookDir * Math.Min(6.5 * scale, colWidth * 0.45), subY));
                    }
                    idx = endIdx + 1;
                }
            }
            run.Clear();
        }

        var prevBeatGroup = -1;
        foreach (var item in orderedNonRest)
        {
            var beatGroup = item.Key / beatCols;
            if (run.Count > 0 && beatGroup != prevBeatGroup)
                FlushRun();
            run.Add((item.Key, item.Value));
            prevBeatGroup = beatGroup;
        }
        FlushRun();
    }

    private static void DrawBarTechniques(
        DrawingContext dc,
        TabBar bar,
        double barLeft,
        double barRight,
        int colCount,
        double colWidth,
        double staffTop,
        double stringSpacing,
        Pen linkPen,
        Brush paperBackground,
        double pixelsPerDip,
        double scale)
    {
        // 1. Bend arrows (Full / 1/2 / Release)
        foreach (var note in bar.Notes)
        {
            if (note.StringIndex is < 0 or > 5 || note.Column < 0 || note.Column >= colCount || string.IsNullOrWhiteSpace(note.Fret))
                continue;
            if (note.Technique is not (TabNoteTechnique.BendFull or TabNoteTechnique.BendHalf or TabNoteTechnique.BendRelease or TabNoteTechnique.BendHalfRelease))
                continue;

            var cx = barLeft + (note.Column + 0.5) * colWidth;
            var sy = staffTop + note.StringIndex * stringSpacing;
            var bendDx = Math.Min(9.5 * scale, Math.Max(5.5 * scale, colWidth * 0.30));
            var startPt = new Point(cx + 5.2 * scale, sy);
            var tipX = Math.Min(barRight - 3.0 * scale, startPt.X + bendDx);
            var tipY = staffTop - 3.2 * scale;
            var ctrlPt = new Point(tipX, sy);

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(startPt, isFilled: false, isClosed: false);
                ctx.QuadraticBezierTo(ctrlPt, new Point(tipX, tipY), isStroked: true, isSmoothJoin: true);
            }
            geo.Freeze();
            dc.DrawGeometry(null, linkPen, geo);

            dc.DrawLine(linkPen, new Point(tipX, tipY), new Point(tipX - 2.2 * scale, tipY + 3.2 * scale));
            dc.DrawLine(linkPen, new Point(tipX, tipY), new Point(tipX + 2.2 * scale, tipY + 3.2 * scale));

            var labelText = note.Technique is TabNoteTechnique.BendHalf or TabNoteTechnique.BendHalfRelease ? "½" : "Full";
            var labelFt = CreateText(labelText, 6.8 * scale, TabTitleTypeface, Green, pixelsPerDip);
            var labelX = Math.Clamp(tipX - labelFt.Width / 2, barLeft + 1, Math.Max(barLeft + 1, barRight - labelFt.Width - 1));
            var labelY = tipY - 8.5 * scale;
            dc.DrawRectangle(paperBackground, null, new Rect(labelX - 0.8 * scale, labelY + 0.5 * scale, labelFt.Width + 1.6 * scale, labelFt.Height - 1.0 * scale));
            dc.DrawText(labelFt, new Point(labelX, labelY));

            if (note.Technique is TabNoteTechnique.BendRelease or TabNoteTechnique.BendHalfRelease)
            {
                var relDx = Math.Min(6.0 * scale, Math.Max(4.0 * scale, bendDx * 0.65));
                var relEndX = Math.Min(barRight - 2.0 * scale, tipX + relDx);
                var relEndY = Math.Min(sy, staffTop + 4.5 * scale);
                var relGeo = new StreamGeometry();
                using (var ctx = relGeo.Open())
                {
                    ctx.BeginFigure(new Point(tipX, tipY), isFilled: false, isClosed: false);
                    ctx.QuadraticBezierTo(new Point(relEndX, tipY), new Point(relEndX, relEndY), isStroked: true, isSmoothJoin: true);
                }
                relGeo.Freeze();
                dc.DrawGeometry(null, linkPen, relGeo);
                dc.DrawLine(linkPen, new Point(relEndX, relEndY), new Point(relEndX - 2.0 * scale, relEndY - 3.0 * scale));
                dc.DrawLine(linkPen, new Point(relEndX, relEndY), new Point(relEndX + 2.0 * scale, relEndY - 3.0 * scale));
            }
        }

        // 2. Harmonics (N.H. / A.H.)
        var harmonicGroups = bar.Notes
            .Where(n => n.Column >= 0 && n.Column < colCount && !string.IsNullOrWhiteSpace(n.Fret) &&
                        n.Technique is TabNoteTechnique.NaturalHarmonic or TabNoteTechnique.ArtificialHarmonic)
            .GroupBy(n => n.Column);
        foreach (var group in harmonicGroups)
        {
            var label = group.Any(n => n.Technique == TabNoteTechnique.ArtificialHarmonic) ? "A.H." : "N.H.";
            var cx = barLeft + (group.Key + 0.5) * colWidth;
            var ft = CreateText(label, 7.2 * scale, TabTitleTypeface, Green, pixelsPerDip);
            var lx = Math.Clamp(cx - ft.Width / 2, barLeft + 1, Math.Max(barLeft + 1, barRight - ft.Width - 1));
            var ly = staffTop - 11.2 * scale;
            dc.DrawRectangle(paperBackground, null, new Rect(lx - 0.8 * scale, ly + 0.5 * scale, ft.Width + 1.6 * scale, ft.Height - 1.0 * scale));
            dc.DrawText(ft, new Point(lx, ly));
        }

        // 3. Vibrato waves across consecutive columns
        var vibCols = bar.Notes
            .Where(n => n.Column >= 0 && n.Column < colCount && !string.IsNullOrWhiteSpace(n.Fret) && n.Technique == TabNoteTechnique.Vibrato)
            .Select(n => n.Column);
        foreach (var (startCol, endCol) in GroupConsecutiveRuns(vibCols))
        {
            var x1 = barLeft + (startCol + 0.5) * colWidth - Math.Min(colWidth * 0.38, 6.5 * scale);
            var x2 = barLeft + (endCol + 0.5) * colWidth + Math.Min(colWidth * 0.38, 6.5 * scale);
            var vibY = staffTop - 6.0 * scale;
            var step = 2.6 * scale;
            var amp = 1.5 * scale;
            var vibGeo = new StreamGeometry();
            using (var ctx = vibGeo.Open())
            {
                var x = x1;
                var dir = -1;
                ctx.BeginFigure(new Point(x, vibY), isFilled: false, isClosed: false);
                while (x + step <= x2)
                {
                    x += step;
                    ctx.LineTo(new Point(x, vibY + dir * amp), isStroked: true, isSmoothJoin: true);
                    dir = -dir;
                }
                if (x < x2)
                    ctx.LineTo(new Point(x2, vibY), isStroked: true, isSmoothJoin: true);
            }
            vibGeo.Freeze();
            dc.DrawGeometry(null, linkPen, vibGeo);
        }

        // 4. Palm Mute (P.M. - - -)
        var pmCols = bar.ColumnMetas
            .Where(m => m.PalmMute && m.Column >= 0 && m.Column < colCount)
            .Select(m => m.Column);
        var pmRuns = GroupConsecutiveRuns(pmCols);
        if (pmRuns.Count > 0)
        {
            var pmPen = Math.Abs(scale - 1.0) < 0.01 ? DashedPmPen : CreateDashedPen(Quiet, 0.95 * scale);
            var pmEndPen = Math.Abs(scale - 1.0) < 0.01 ? QuietPen : FreezePen(new Pen(Quiet, 0.95 * scale));
            foreach (var (startCol, endCol) in pmRuns)
            {
                var xStart = barLeft + (startCol + 0.15) * colWidth;
                var pmFt = CreateText("P.M.", 7.4 * scale, TabTitleTypeface, Quiet, pixelsPerDip);
                var pmY = staffTop - 12.2 * scale;
                dc.DrawRectangle(paperBackground, null, new Rect(xStart - 0.8 * scale, pmY + 0.5 * scale, pmFt.Width + 1.6 * scale, pmFt.Height - 1.0 * scale));
                dc.DrawText(pmFt, new Point(xStart, pmY));

                if (endCol > startCol)
                {
                    var lineX1 = xStart + pmFt.Width + 2.5 * scale;
                    var lineX2 = barLeft + (endCol + 0.85) * colWidth;
                    var lineY = pmY + pmFt.Height * 0.55;
                    if (lineX2 > lineX1 + 2)
                    {
                        dc.DrawLine(pmPen, new Point(lineX1, lineY), new Point(lineX2, lineY));
                        dc.DrawLine(pmEndPen, new Point(lineX2, lineY), new Point(lineX2, lineY + 3.0 * scale));
                    }
                }
            }
        }
    }

    private static List<(int Start, int End)> GroupConsecutiveRuns(IEnumerable<int> columns)
    {
        var runs = new List<(int Start, int End)>();
        var sorted = columns.Distinct().OrderBy(c => c).ToList();
        if (sorted.Count == 0) return runs;

        var start = sorted[0];
        var prev = sorted[0];
        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i] == prev + 1)
            {
                prev = sorted[i];
            }
            else
            {
                runs.Add((start, prev));
                start = sorted[i];
                prev = sorted[i];
            }
        }
        runs.Add((start, prev));
        return runs;
    }

    private static void DrawRestGlyph(DrawingContext dc, TabDuration dur, double cx, double midY, Pen pen, double scale)
    {
        if (dur == TabDuration.Whole)
        {
            dc.DrawRectangle(Ink, null, new Rect(cx - 3.5 * scale, midY - 2.0 * scale, 7.0 * scale, 2.5 * scale));
            return;
        }
        if (dur == TabDuration.Half)
        {
            dc.DrawRectangle(Ink, null, new Rect(cx - 3.5 * scale, midY - 0.5 * scale, 7.0 * scale, 2.5 * scale));
            return;
        }
        if (dur is TabDuration.Eighth or TabDuration.Sixteenth)
        {
            var topY = midY - 4.0 * scale;
            var botY = midY + 4.5 * scale;
            dc.DrawEllipse(Ink, null, new Point(cx - 2.2 * scale, topY + 0.8 * scale), 1.3 * scale, 1.3 * scale);
            dc.DrawLine(pen, new Point(cx - 2.0 * scale, topY + 1.0 * scale), new Point(cx + 2.5 * scale, topY));
            dc.DrawLine(pen, new Point(cx + 2.5 * scale, topY), new Point(cx - 0.5 * scale, botY));
            if (dur == TabDuration.Sixteenth)
            {
                dc.DrawEllipse(Ink, null, new Point(cx - 2.6 * scale, topY + 4.0 * scale), 1.2 * scale, 1.2 * scale);
                dc.DrawLine(pen, new Point(cx - 2.4 * scale, topY + 4.2 * scale), new Point(cx + 1.4 * scale, topY + 3.2 * scale));
            }
            return;
        }

        // Quarter rest zigzag
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(cx - 1.5 * scale, midY - 4.5 * scale), isFilled: false, isClosed: false);
            ctx.LineTo(new Point(cx + 2.0 * scale, midY - 1.5 * scale), isStroked: true, isSmoothJoin: true);
            ctx.LineTo(new Point(cx - 2.0 * scale, midY + 1.5 * scale), isStroked: true, isSmoothJoin: true);
            ctx.LineTo(new Point(cx + 1.5 * scale, midY + 4.5 * scale), isStroked: true, isSmoothJoin: true);
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }

    private static int? TryParseFretNumber(string? fret)
    {
        if (string.IsNullOrWhiteSpace(fret)) return null;
        var i = 0;
        while (i < fret.Length && char.IsDigit(fret[i])) i++;
        return i > 0 && int.TryParse(fret[..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var val) ? val : null;
    }

    private static void DrawEmptyRow(DrawingContext dc, double left, double y, double availableWidth, double pixelsPerDip)
    {
        var staffTop = y + ChordRowHeight;
        var staffBottom = staffTop + StaffHeight;
        for (var s = 0; s < 6; s++)
        {
            var sy = staffTop + s * StringSpacing;
            dc.DrawLine(StringPen, new Point(left, sy), new Point(left + availableWidth, sy));
        }
        DrawClef(dc, left, staffTop, pixelsPerDip);
        dc.DrawLine(BarlinePen, new Point(left + ClefWidth, staffTop), new Point(left + ClefWidth, staffBottom));
        dc.DrawLine(BarlinePen, new Point(left + availableWidth, staffTop), new Point(left + availableWidth, staffBottom));
        var hint = CreateText("空白六线谱小节（点击可编辑）", 10.5, MetaTypeface, Quiet, pixelsPerDip);
        dc.DrawText(hint, new Point(left + ClefWidth + 14, staffTop + (StaffHeight - hint.Height) / 2));
    }

    private static void DrawClef(DrawingContext dc, double left, double staffTop, double pixelsPerDip)
    {
        var letters = new[] { "T", "A", "B" };
        for (var i = 0; i < letters.Length; i++)
        {
            var ft = CreateText(letters[i], 10.5, ClefTypeface, Quiet, pixelsPerDip);
            var cy = staffTop + (i + 0.5) * (StaffHeight / 3.0);
            dc.DrawText(ft, new Point(left + (ClefWidth - ft.Width) / 2 - 2, cy - ft.Height / 2));
        }
    }

    private static FormattedText CreateText(string text, double size, Typeface typeface, Brush brush, double pixelsPerDip) =>
        new(text ?? string.Empty, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, typeface, size, brush, pixelsPerDip);
}
