using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Xianpu.Desktop;

public sealed partial class ScoreSurface
{
    public DocumentPaginator CreatePrintPaginator(Size printablePageSize)
    {
        Rebuild();
        return new ScorePrintPaginator(this, printablePageSize);
    }

    private List<(double StartY, double EndY)> ComputePrintPageSlices(double maxSliceHeight)
    {
        var totalHeight = Math.Max(120.0, Height);
        if (maxSliceHeight <= 80.0 || totalHeight <= maxSliceHeight)
            return [(0.0, totalHeight)];

        // Gather indivisible vertical bands (lyric rows + TAB sections) so we avoid slicing text/tabs in half
        var bands = new List<(double Top, double Bottom)>();
        foreach (var row in _rows)
        {
            var top = row.HasPartHeader ? Math.Min(row.PartHeaderTop, row.Top) : row.Top;
            var bottom = row.Top + row.Height;
            if (bottom > top)
                bands.Add((top, bottom));
        }
        foreach (var tab in _tabLayouts)
        {
            if (tab.Bounds.Height > 0)
                bands.Add((tab.Bounds.Top, tab.Bounds.Bottom));
        }

        var slices = new List<(double StartY, double EndY)>();
        var currentStart = 0.0;

        while (currentStart + 0.5 < totalHeight)
        {
            var idealEnd = Math.Min(totalHeight, currentStart + maxSliceHeight);
            if (idealEnd >= totalHeight - 0.5)
            {
                slices.Add((currentStart, totalHeight));
                break;
            }

            // Find any band straddling idealEnd; if moving break before its Top still makes meaningful progress, break cleanly before it
            var cutY = idealEnd;
            foreach (var (top, bottom) in bands)
            {
                if (top < cutY && bottom > cutY)
                {
                    var candidate = Math.Max(currentStart, top - 6.0);
                    if (candidate - currentStart >= maxSliceHeight * 0.28)
                        cutY = Math.Min(cutY, candidate);
                }
            }

            if (cutY <= currentStart + 20.0)
                cutY = idealEnd;

            slices.Add((currentStart, cutY));
            currentStart = cutY;
        }

        return slices.Count > 0 ? slices : [(0.0, totalHeight)];
    }

    private sealed class ScorePrintPaginator : DocumentPaginator
    {
        private const double MarginX = 34.0;
        private const double MarginTop = 32.0;
        private const double MarginBottom = 38.0;

        private readonly ScoreSurface _surface;
        private Size _pageSize;
        private double _scale;
        private List<(double StartY, double EndY)> _slices = [];

        public ScorePrintPaginator(ScoreSurface surface, Size pageSize)
        {
            _surface = surface;
            _pageSize = new Size(
                pageSize.Width > 100 ? pageSize.Width : 793.7,   // A4 width at 96 DPI
                pageSize.Height > 100 ? pageSize.Height : 1122.5 // A4 height at 96 DPI
            );
            Recalculate();
        }

        private void Recalculate()
        {
            var usableWidth = Math.Max(120.0, _pageSize.Width - MarginX * 2);
            var usableHeight = Math.Max(160.0, _pageSize.Height - MarginTop - MarginBottom);
            _scale = Math.Min(1.0, usableWidth / Math.Max(1.0, _surface.PaperWidthCurrent));
            var maxSliceHeight = usableHeight / Math.Max(0.1, _scale);
            _slices = _surface.ComputePrintPageSlices(maxSliceHeight);
        }

        public override bool IsPageCountValid => true;

        public override int PageCount => _slices.Count;

        public override Size PageSize
        {
            get => _pageSize;
            set
            {
                if (value.Width > 100 && value.Height > 100 && value != _pageSize)
                {
                    _pageSize = value;
                    Recalculate();
                }
            }
        }

        public override IDocumentPaginatorSource? Source => null;

        public override DocumentPage GetPage(int pageNumber)
        {
            if (pageNumber < 0 || pageNumber >= _slices.Count)
                return DocumentPage.Missing;

            var (startY, endY) = _slices[pageNumber];
            var sliceHeight = Math.Max(1.0, endY - startY);
            var scaledWidth = _surface.PaperWidthCurrent * _scale;
            var offsetX = Math.Max(MarginX, (_pageSize.Width - scaledWidth) / 2.0);

            var visual = new DrawingVisual();
            _surface.IsExporting = true;
            try
            {
                using var dc = visual.RenderOpen();
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, _pageSize.Width, _pageSize.Height));

                dc.PushTransform(new TranslateTransform(offsetX, MarginTop));
                dc.PushTransform(new ScaleTransform(_scale, _scale));
                dc.PushClip(new RectangleGeometry(new Rect(0, 0, _surface.PaperWidthCurrent, sliceHeight)));
                dc.PushTransform(new TranslateTransform(0, -startY));
                _surface.DrawScore(dc);
                dc.Pop();
                dc.Pop();
                dc.Pop();
                dc.Pop();

                if (_slices.Count > 1)
                {
                    var footerText = new FormattedText(
                        $"第 {pageNumber + 1} / {_slices.Count} 页",
                        CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight,
                        LyricTypeface,
                        10.5,
                        Quiet,
                        1.0);
                    dc.DrawText(footerText, new Point((_pageSize.Width - footerText.Width) / 2.0, _pageSize.Height - 24.0));
                }
            }
            finally
            {
                _surface.IsExporting = false;
            }

            var pageRect = new Rect(0, 0, _pageSize.Width, _pageSize.Height);
            return new DocumentPage(visual, _pageSize, pageRect, pageRect);
        }
    }
}
