using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Xianpu.Desktop;

/// <summary>
/// Card-styled dialog &amp; A4 vector DocumentPaginator for printing or exporting a multi-drill
/// guitar fretboard workbook (PDF via Microsoft Print to PDF, physical A4 print, or stitched high-DPI PNG).
/// </summary>
public static class DrillWorkbookDialog
{
    public static void Show(
        Window? owner,
        int tonicPitch,
        ScaleDefinition scale,
        GuitarTuning tuning,
        int capo,
        string activeCagedFilter,
        DrillPositionMode posMode,
        int fixedWindowStartFret,
        int threeNpsPos,
        IDictionary<string, int>? bestBpms,
        Action<string>? setStatus)
    {
        var host = owner ?? Application.Current?.MainWindow;
        var pf = MusicTheoryCatalog.PreferFlatsForTonic(tonicPitch);
        var keyLabel = $"{MusicTheoryCatalog.NoteName(tonicPitch, pf)} · {scale.Name}";

        var dialog = AppDialogs.CreateCardWindow(
            host,
            "打印 / 导出指板练习册（A4 合集）",
            $"当前基准：{keyLabel}  ·  勾选多条练习一键生成防断行 A4 练习册或高清长图",
            580,
            520,
            false,
            out var layout);

        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var checks = new List<(ScaleDrillPreset Preset, CheckBox Box)>(ScaleDrillCatalog.Presets.Count);
        string[] defaultCoreIds = ["u1_spider_1234", "u2_28_disp_beat", "u3_45_skip_4th_2", "u4_56_7th_alt_up", "u5_58_cof_pent"];

        // Top quick-select row
        var quickRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var selectWorkoutBtn = AppDialogs.CreateSecondaryButton(dialog, "推荐核心 5 项", minWidth: 96);
        selectWorkoutBtn.Margin = new Thickness(0, 0, 8, 0);
        selectWorkoutBtn.Click += (_, _) =>
        {
            foreach (var (p, cb) in checks)
                cb.IsChecked = defaultCoreIds.Contains(p.Id);
        };

        var selectAllBtn = AppDialogs.CreateSecondaryButton(dialog, "全选", minWidth: 68);
        selectAllBtn.Margin = new Thickness(0, 0, 8, 0);
        selectAllBtn.Click += (_, _) =>
        {
            foreach (var (_, cb) in checks)
                cb.IsChecked = true;
        };

        var clearBtn = AppDialogs.CreateSecondaryButton(dialog, "清空", minWidth: 68);
        clearBtn.Click += (_, _) =>
        {
            foreach (var (_, cb) in checks)
                cb.IsChecked = false;
        };

        quickRow.Children.Add(selectWorkoutBtn);
        quickRow.Children.Add(selectAllBtn);
        quickRow.Children.Add(clearBtn);
        Grid.SetRow(quickRow, 0);
        layout.Children.Add(quickRow);

        // Checklist container
        var listStack = new StackPanel { Margin = new Thickness(2, 0, 6, 0) };
        string? lastUnit = null;
        foreach (var preset in ScaleDrillCatalog.Presets)
        {
            if (preset.UnitTitle != lastUnit)
            {
                lastUnit = preset.UnitTitle;
                listStack.Children.Add(new TextBlock
                {
                    Text = preset.UnitTitle,
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = AppDialogs.AccentHoverBrush,
                    Margin = new Thickness(2, listStack.Children.Count == 0 ? 2 : 10, 0, 4)
                });
            }

            var bestHint = bestBpms is not null && bestBpms.TryGetValue(preset.Id, out var best) && best > 0
                ? $"  (已打卡 ★{best} BPM)"
                : $"  (建议 {preset.SuggestedBpm} BPM)";

            var cb = new CheckBox
            {
                Content = $"[{preset.CodeBadge}] {preset.Title}{bestHint}",
                IsChecked = defaultCoreIds.Contains(preset.Id),
                FontSize = 12,
                Foreground = AppDialogs.BodyBrush,
                Margin = new Thickness(4, 3, 4, 3)
            };
            checks.Add((preset, cb));
            listStack.Children.Add(cb);
        }

        var listBorder = new Border
        {
            Background = AppDialogs.SubtleSurfaceBrush,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 14),
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = listStack
            }
        };
        Grid.SetRow(listBorder, 1);
        layout.Children.Add(listBorder);

        List<(ScaleDrillPreset Preset, GeneratedScaleDrill Drill)> BuildSelectedDrills()
        {
            var selected = checks.Where(x => x.Box.IsChecked == true).Select(x => x.Preset).ToList();
            var list = new List<(ScaleDrillPreset, GeneratedScaleDrill)>(selected.Count);
            foreach (var p in selected)
            {
                var effScale = p.Pattern is DrillPatternKind.Spider1234 or DrillPatternKind.Spider1324
                    ? scale
                    : MusicTheoryCatalog.GetScale(p.RecommendedScaleId);
                var gen = ScaleDrillCatalog.Generate(
                    tonicPitch,
                    effScale,
                    tuning,
                    capo,
                    activeCagedFilter,
                    posMode,
                    p.Pattern,
                    p.Subdivision,
                    p.Accent,
                    fixedWindowStartFret: fixedWindowStartFret,
                    cofKeyCount: 5,
                    cofBarsPerKey: 2,
                    threeNpsPosition: threeNpsPos);
                list.Add((p, gen));
            }
            return list;
        }

        // Footer actions
        var footer = new DockPanel { LastChildFill = false };
        var cancelBtn = AppDialogs.CreateSecondaryButton(dialog, "取消", minWidth: 76);
        cancelBtn.IsCancel = true;
        cancelBtn.Click += (_, _) => AppDialogs.CloseWithResult(dialog, false);
        DockPanel.SetDock(cancelBtn, Dock.Right);

        var printBtn = AppDialogs.CreatePrimaryButton(dialog, "🖨 打印 / 导出 A4 PDF...", minWidth: 156);
        printBtn.Margin = new Thickness(0, 0, 8, 0);
        printBtn.Click += (_, _) =>
        {
            var items = BuildSelectedDrills();
            if (items.Count == 0)
            {
                AppDialogs.ShowInfo(dialog, "未勾选练习", "请至少勾选 1 条指板练习后再打印或导出。");
                return;
            }
            var pd = new PrintDialog();
            if (pd.ShowDialog() != true) return;

            var paginator = new WorkbookPaginator(
                $"弦笺 · 吉他指板内化训练册（{MusicTheoryCatalog.NoteName(tonicPitch, pf)} 调基准）",
                items,
                new Size(pd.PrintableAreaWidth, pd.PrintableAreaHeight));
            pd.PrintDocument(paginator, "弦笺指板练习册");
            setStatus?.Invoke($"已发送指板练习册打印 / PDF 任务（共 {items.Count} 课 · {paginator.PageCount} 页 A4）。");
            AppDialogs.CloseWithResult(dialog, true);
        };
        DockPanel.SetDock(printBtn, Dock.Right);

        var pngBtn = AppDialogs.CreateSecondaryButton(dialog, "🖼 导出合集长图 (PNG)...", minWidth: 148);
        pngBtn.Margin = new Thickness(0, 0, 8, 0);
        pngBtn.Click += (_, _) =>
        {
            var items = BuildSelectedDrills();
            if (items.Count == 0)
            {
                AppDialogs.ShowInfo(dialog, "未勾选练习", "请至少勾选 1 条指板练习后再导出。");
                return;
            }
            var sfd = new SaveFileDialog
            {
                Title = "导出指板练习册高清长图 PNG",
                Filter = "PNG 图片 (*.png)|*.png",
                FileName = $"弦笺指板练习册_{MusicTheoryCatalog.NoteName(tonicPitch, pf)}调_{items.Count}课合集.png"
            };
            if (sfd.ShowDialog() != true) return;

            ExportWorkbookPng(sfd.FileName, $"弦笺 · 吉他指板内化训练册（{MusicTheoryCatalog.NoteName(tonicPitch, pf)} 调基准）", items);
            setStatus?.Invoke($"已导出 {items.Count} 课合集高清长图：{sfd.FileName}");
            AppDialogs.CloseWithResult(dialog, true);
        };
        DockPanel.SetDock(pngBtn, Dock.Right);

        footer.Children.Add(cancelBtn);
        footer.Children.Add(printBtn);
        footer.Children.Add(pngBtn);
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);

        dialog.ShowDialog();
    }

    private static void ExportWorkbookPng(
        string filePath,
        string workbookTitle,
        List<(ScaleDrillPreset Preset, GeneratedScaleDrill Drill)> items)
    {
        const double width = 980;
        const double pad = 34;
        const double dpiScale = 2.0;
        var contentW = width - pad * 2;

        double totalH = pad + 56;
        foreach (var (_, drill) in items)
            totalH += 34 + TabPainter.MeasureSectionHeight(drill.Section, contentW) + 18;
        totalH += pad;

        var titleFace = new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var bodyFace = new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, totalH));
            var hFt = new FormattedText(workbookTitle, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, titleFace, 20, AppDialogs.TitleBrush, dpiScale);
            dc.DrawText(hFt, new Point(pad, pad));
            var subFt = new FormattedText($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}  ·  共收录 {items.Count} 套渐进练习", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, bodyFace, 11.5, AppDialogs.MutedBrush, dpiScale);
            dc.DrawText(subFt, new Point(pad, pad + 28));

            var y = pad + 56.0;
            foreach (var (preset, drill) in items)
            {
                var itemTitle = new FormattedText($"[{preset.CodeBadge}] {preset.Title}  （建议 {preset.SuggestedBpm} BPM）— {preset.CoachingTip}",
                    CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, bodyFace, 11.5, AppDialogs.BodyBrush, dpiScale)
                {
                    MaxTextWidth = contentW
                };
                dc.DrawText(itemTitle, new Point(pad, y));
                y += itemTitle.Height + 6;

                TabPainter.DrawSection(dc, drill.Section, pad, y, contentW, Brushes.White, dpiScale);
                y += TabPainter.MeasureSectionHeight(drill.Section, contentW) + 18;
            }
        }

        var rtb = new RenderTargetBitmap((int)(width * dpiScale), (int)(totalH * dpiScale), 96 * dpiScale, 96 * dpiScale, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(filePath);
        enc.Save(fs);
    }

    private sealed class WorkbookPaginator : DocumentPaginator
    {
        private const double MarginX = 36;
        private const double MarginTop = 34;
        private const double MarginBottom = 38;

        private static readonly Typeface TitleFace = new(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface BodyFace = new(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private readonly string _title;
        private readonly List<(ScaleDrillPreset Preset, GeneratedScaleDrill Drill)> _items;
        private Size _pageSize;
        private readonly List<List<(ScaleDrillPreset Preset, GeneratedScaleDrill Drill)>> _pages = [];

        public WorkbookPaginator(
            string title,
            List<(ScaleDrillPreset Preset, GeneratedScaleDrill Drill)> items,
            Size pageSize)
        {
            _title = title;
            _items = items;
            _pageSize = new Size(
                pageSize.Width > 120 ? pageSize.Width : 793.7,
                pageSize.Height > 120 ? pageSize.Height : 1122.5);
            Paginate();
        }

        private void Paginate()
        {
            _pages.Clear();
            var contentW = Math.Max(320, _pageSize.Width - MarginX * 2);
            var firstPageBudget = Math.Max(220, _pageSize.Height - MarginTop - MarginBottom - 54);
            var normalPageBudget = Math.Max(220, _pageSize.Height - MarginTop - MarginBottom - 20);

            var current = new List<(ScaleDrillPreset, GeneratedScaleDrill)>();
            var used = 0.0;
            var budget = firstPageBudget;

            foreach (var entry in _items)
            {
                var h = 36 + TabPainter.MeasureSectionHeight(entry.Drill.Section, contentW) + 14;
                if (current.Count > 0 && used + h > budget)
                {
                    _pages.Add(current);
                    current = [];
                    used = 0;
                    budget = normalPageBudget;
                }
                current.Add(entry);
                used += h;
            }

            if (current.Count > 0 || _pages.Count == 0)
                _pages.Add(current);
        }

        public override bool IsPageCountValid => true;
        public override int PageCount => _pages.Count;
        public override Size PageSize
        {
            get => _pageSize;
            set { _pageSize = value; Paginate(); }
        }
        public override IDocumentPaginatorSource? Source => null;

        public override DocumentPage GetPage(int pageNumber)
        {
            var dv = new DrawingVisual();
            var contentW = Math.Max(320, _pageSize.Width - MarginX * 2);
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, _pageSize.Width, _pageSize.Height));
                var y = MarginTop;

                if (pageNumber == 0)
                {
                    var hFt = new FormattedText(_title, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, TitleFace, 17, AppDialogs.TitleBrush, 1.0);
                    dc.DrawText(hFt, new Point(MarginX, y));
                    var subFt = new FormattedText($"打印日期：{DateTime.Now:yyyy-MM-dd}  ·  共 {_items.Count} 套练习", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, BodyFace, 10.5, AppDialogs.MutedBrush, 1.0);
                    dc.DrawText(subFt, new Point(MarginX, y + 24));
                    y += 50;
                }

                foreach (var (preset, drill) in _pages[pageNumber])
                {
                    var tipFt = new FormattedText(
                        $"[{preset.CodeBadge}] {preset.Title}（建议 {preset.SuggestedBpm} BPM）— {preset.CoachingTip}",
                        CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, BodyFace, 10.5, AppDialogs.BodyBrush, 1.0)
                    {
                        MaxTextWidth = contentW
                    };
                    dc.DrawText(tipFt, new Point(MarginX, y));
                    y += tipFt.Height + 5;

                    TabPainter.DrawSection(dc, drill.Section, MarginX, y, contentW, Brushes.White, 1.0);
                    y += TabPainter.MeasureSectionHeight(drill.Section, contentW) + 14;
                }

                var pageFt = new FormattedText(
                    $"弦笺 StringNote 指板练功册  ·  第 {pageNumber + 1} / {_pages.Count} 页",
                    CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, BodyFace, 9.5, AppDialogs.MutedBrush, 1.0);
                dc.DrawText(pageFt, new Point((_pageSize.Width - pageFt.Width) / 2, _pageSize.Height - MarginBottom + 10));
            }

            return new DocumentPage(dv, _pageSize, new Rect(_pageSize), new Rect(_pageSize));
        }
    }
}
