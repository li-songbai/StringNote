using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Xianpu.Desktop;

public static class TabPainter
{
    public const double ClefWidth = 32;
    public const double HeaderHeight = 26;
    public const double ChordRowHeight = 18;
    public const double StringSpacing = 9.5;
    public const double StaffHeight = StringSpacing * 5; // 47.5
    public const double RowBottomGap = 14;
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

    public static int ComputeBarsPerRow(double availableWidth, int totalBars)
    {
        var maxFit = Math.Clamp((int)Math.Floor((availableWidth - ClefWidth) / 215.0), 1, 4);
        return totalBars > 0 ? Math.Min(maxFit, totalBars) : maxFit;
    }

    public static double MeasureSectionHeight(TabSection section, double availableWidth)
    {
        var bars = section.Bars;
        if (bars.Count == 0)
            return HeaderHeight + ChordRowHeight + StaffHeight + RowBottomGap + SectionBottomGap;

        var barsPerRow = ComputeBarsPerRow(availableWidth, bars.Count);
        var rowCount = (int)Math.Ceiling(bars.Count / (double)barsPerRow);
        var rowHeight = ChordRowHeight + StaffHeight + RowBottomGap;
        return HeaderHeight + rowCount * rowHeight + SectionBottomGap;
    }

    public static void DrawSection(
        DrawingContext dc,
        TabSection section,
        double left,
        double top,
        double availableWidth,
        Brush paperBackground,
        double pixelsPerDip)
    {
        var bars = section.Bars;
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

                // Subtle beat ticks below 6th string
                for (var c = 0; c < colCount; c++)
                {
                    var cx = barLeft + (c + 0.5) * colWidth;
                    var isMajorBeat = colCount % 2 == 0 && c % 2 == 0;
                    var tickLen = isMajorBeat ? 5.0 : 3.0;
                    dc.DrawLine(TickPen, new Point(cx, staffBottom + 2), new Point(cx, staffBottom + 2 + tickLen));
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

                // Fret numbers on strings
                foreach (var note in bar.Notes)
                {
                    if (note.StringIndex is < 0 or > 5 || note.Column < 0 || note.Column >= colCount || string.IsNullOrWhiteSpace(note.Fret))
                        continue;

                    var cx = barLeft + (note.Column + 0.5) * colWidth;
                    var sy = staffTop + note.StringIndex * StringSpacing;
                    var ft = CreateText(note.Fret, 10.5, FretTypeface, Ink, pixelsPerDip);
                    var maskRect = new Rect(cx - ft.Width / 2 - 2.5, sy - ft.Height / 2 + 0.5, ft.Width + 5, ft.Height - 1);
                    dc.DrawRectangle(paperBackground, null, maskRect);
                    dc.DrawText(ft, new Point(cx - ft.Width / 2, sy - ft.Height / 2));
                }

                // Closing barline (double bar at very end of section)
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

            y += ChordRowHeight + StaffHeight + RowBottomGap;
        }
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
