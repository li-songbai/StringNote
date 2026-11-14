using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Xianpu.Desktop;

public sealed class ChordDiagramView : FrameworkElement
{
    public static readonly DependencyProperty ShapeProperty = DependencyProperty.Register(
        nameof(Shape), typeof(ChordShape), typeof(ChordDiagramView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public ChordShape? Shape
    {
        get => (ChordShape?)GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (Shape is null) return;
        var scale = Math.Min(1, Math.Min(ActualWidth / 96, ActualHeight / 120));
        ChordDiagramPainter.Draw(
            drawingContext,
            Shape,
            new Point((ActualWidth - 96 * scale) / 2, 0),
            scale,
            showCaption: true,
            pixelsPerDip: VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }
}

public static class ChordDiagramPainter
{
    private static readonly Brush Ink = Freeze(new SolidColorBrush(Color.FromRgb(52, 63, 53)));
    private static readonly Brush Muted = Freeze(new SolidColorBrush(Color.FromRgb(108, 117, 106)));
    private static readonly Brush Grid = Freeze(new SolidColorBrush(Color.FromRgb(179, 186, 176)));
    private static readonly Brush Green = Freeze(new SolidColorBrush(Color.FromRgb(63, 99, 68)));
    private static readonly Brush White = Brushes.White;
    private static readonly Typeface Sans = new(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface Bold = new(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    public static void Draw(DrawingContext dc, ChordShape shape, Point origin, double scale, bool showCaption, double pixelsPerDip)
    {
        var dx = 12.5 * scale;
        var dy = 13 * scale;
        var x0 = origin.X + 15 * scale;
        var y0 = origin.Y + 25 * scale;
        DrawText(dc, shape.Name, origin.X + 48 * scale, origin.Y + 1 * scale, 12 * scale, Bold, Ink, pixelsPerDip, TextAlignment.Center);

        for (var i = 0; i < 6; i++)
        {
            var mark = shape.Frets[i] switch { null => "×", 0 => "○", _ => "" };
            if (mark.Length > 0)
                DrawText(dc, mark, x0 + i * dx, y0 - 15 * scale, 9.5 * scale, Sans, Muted, pixelsPerDip, TextAlignment.Center);
        }

        var hairline = new Pen(Grid, 1 * scale);
        for (var i = 0; i < 6; i++) dc.DrawLine(hairline, new Point(x0 + i * dx, y0), new Point(x0 + i * dx, y0 + dy * 4));
        for (var i = 0; i <= 4; i++)
        {
            var pen = new Pen(i == 0 && shape.BaseFret == 1 ? Ink : Grid, i == 0 && shape.BaseFret == 1 ? 2.1 * scale : 1 * scale);
            dc.DrawLine(pen, new Point(x0, y0 + i * dy), new Point(x0 + 5 * dx, y0 + i * dy));
        }

        if (shape.Barre is { } barre)
        {
            var y = y0 + (barre.Fret - shape.BaseFret + .5) * dy;
            var rect = new Rect(x0 + barre.FromString * dx - 4 * scale, y - 4 * scale,
                (barre.EndString - barre.FromString) * dx + 8 * scale, 8 * scale);
            dc.DrawRoundedRectangle(Green, null, rect, 4 * scale, 4 * scale);
            DrawText(dc, "1", new Point(rect.Left + 4 * scale, y - 3.5 * scale), 6.6 * scale, Bold, White, pixelsPerDip);
        }

        for (var i = 0; i < 6; i++)
        {
            var fret = shape.Frets[i];
            if (fret is null or 0 || shape.Barre is { } b && shape.Fingers[i] == 1 && fret == b.Fret) continue;
            var center = new Point(x0 + i * dx, y0 + (fret.Value - shape.BaseFret + .5) * dy);
            dc.DrawEllipse(Green, null, center, 4.25 * scale, 4.25 * scale);
            if (shape.Fingers[i] > 0)
                DrawText(dc, shape.Fingers[i].ToString(CultureInfo.InvariantCulture), center, 6.3 * scale, Bold, White, pixelsPerDip);
        }

        if (shape.BaseFret > 1)
            DrawText(dc, $"{shape.BaseFret}品", new Point(origin.X + 1 * scale, y0 + 1 * scale), 7.5 * scale, Sans, Muted, pixelsPerDip);
        if (showCaption && !string.IsNullOrWhiteSpace(shape.Label))
            DrawText(dc, shape.Label, origin.X + 48 * scale, origin.Y + 101 * scale, 8.5 * scale, Sans, Muted, pixelsPerDip, TextAlignment.Center);
    }

    public static FormattedText CreateText(string text, double size, Typeface typeface, Brush brush, double pixelsPerDip) =>
        new(text ?? string.Empty, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, typeface, size, brush, pixelsPerDip);

    private static void DrawText(DrawingContext dc, string text, double x, double y, double size, Typeface typeface, Brush brush, double pixelsPerDip, TextAlignment alignment = TextAlignment.Left)
    {
        var formatted = CreateText(text, size, typeface, brush, pixelsPerDip);
        formatted.TextAlignment = alignment;
        if (alignment == TextAlignment.Center) x -= formatted.Width / 2;
        dc.DrawText(formatted, new Point(x, y));
    }

    private static void DrawText(DrawingContext dc, string text, Point center, double size, Typeface typeface, Brush brush, double pixelsPerDip)
    {
        var formatted = CreateText(text, size, typeface, brush, pixelsPerDip);
        dc.DrawText(formatted, new Point(center.X - formatted.Width / 2, center.Y - formatted.Height / 2));
    }
}
