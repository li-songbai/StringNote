using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Xianpu.Desktop;

public sealed record StackedLyricResult(string PrimaryText, List<string> AltLyrics);

internal static class StackedLyricDialog
{
    private static readonly FontFamily MonoFont = new("NSimSun");

    public static StackedLyricResult? Show(Window? owner, LyricLine line, bool addNewRow = false)
    {
        var workArea = SystemParameters.WorkArea;
        var winWidth = Math.Min(760, Math.Max(460, workArea.Width * 0.88));

        var window = AppDialogs.CreateCardWindow(
            owner,
            "多段叠行歌词与对齐",
            "第 1 遍主歌词上方显示已填和弦标尺；下方每遍可用空格微调列对齐",
            winWidth,
            height: null,
            resizable: true,
            out var root,
            badgeText: "叠");

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ─── Row 0: Horizontal scroll container holding chord ruler + verse rows ───
        var rulerBox = new TextBox
        {
            IsReadOnly = true,
            IsTabStop = false,
            FontFamily = MonoFont,
            FontSize = 15.5,
            FontWeight = FontWeights.Bold,
            Foreground = AppDialogs.AccentBrush,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(8, 2, 8, 2),
            MinWidth = 620
        };

        var rulerRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        rulerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        rulerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var rulerTag = new TextBlock
        {
            Text = "和弦",
            FontSize = 11,
            Foreground = AppDialogs.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetColumn(rulerTag, 0);
        Grid.SetColumn(rulerBox, 1);
        rulerRow.Children.Add(rulerTag);
        rulerRow.Children.Add(rulerBox);

        var rulerBorder = new Border
        {
            Background = AppDialogs.SubtleSurfaceBrush,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 5, 8, 3),
            Child = rulerRow
        };

        var versesPanel = new StackPanel { Margin = new Thickness(8, 8, 8, 6) };
        var verticalScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = Math.Min(360, Math.Max(200, workArea.Height * 0.45)),
            Content = versesPanel
        };

        var syncedGrid = new Grid();
        syncedGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        syncedGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(rulerBorder, 0);
        Grid.SetRow(verticalScroll, 1);
        syncedGrid.Children.Add(rulerBorder);
        syncedGrid.Children.Add(verticalScroll);

        var horizontalScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = syncedGrid
        };

        var containerBorder = new Border
        {
            Background = Brushes.White,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 10),
            ClipToBounds = true,
            Child = horizontalScroll
        };
        Grid.SetRow(containerBorder, 0);
        Grid.SetRowSpan(containerBorder, 2);
        root.Children.Add(containerBorder);

        var primaryBox = CreateVerseInput(line.Text);
        var altBoxes = new List<TextBox>();

        void RefreshRuler()
        {
            rulerBox.Text = BuildChordRuler(primaryBox.Text, line.Chords);
        }

        primaryBox.TextChanged += (_, _) => RefreshRuler();

        void RebuildVerseRows(TextBox? focusTarget = null)
        {
            versesPanel.Children.Clear();
            versesPanel.Children.Add(BuildVerseRow(
                badgeText: "1",
                isPrimary: true,
                input: primaryBox,
                onRemove: null));

            for (var i = 0; i < altBoxes.Count; i++)
            {
                var box = altBoxes[i];
                var badge = (i + 2).ToString();
                versesPanel.Children.Add(BuildVerseRow(
                    badgeText: badge,
                    isPrimary: false,
                    input: box,
                    onRemove: () =>
                    {
                        altBoxes.Remove(box);
                        RebuildVerseRows();
                    }));
            }

            if (focusTarget is not null)
            {
                window.Dispatcher.BeginInvoke(() =>
                {
                    focusTarget.Focus();
                    focusTarget.CaretIndex = focusTarget.Text.Length;
                });
            }
        }

        if (line.AltLyrics is { Count: > 0 })
        {
            foreach (var alt in line.AltLyrics)
                altBoxes.Add(CreateVerseInput(alt));
        }

        TextBox? initialFocus = null;
        if (addNewRow || altBoxes.Count == 0)
        {
            var newBox = CreateVerseInput("");
            altBoxes.Add(newBox);
            initialFocus = newBox;
        }

        RefreshRuler();
        RebuildVerseRows(initialFocus);

        // ─── Row 2: Action buttons + alignment tip ───
        var actionsBar = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };

        var addVerseBtn = AppDialogs.CreateSecondaryButton(owner, "+ 添加一遍歌词", minWidth: 112);
        addVerseBtn.Margin = new Thickness(0, 0, 8, 0);
        addVerseBtn.Click += (_, _) =>
        {
            var box = CreateVerseInput("");
            altBoxes.Add(box);
            RebuildVerseRows(box);
        };

        var smartAlignBtn = AppDialogs.CreateAccentOutlineButton(owner, "按空格智能对齐", minWidth: 118);
        smartAlignBtn.ToolTip = "根据第 1 遍词间空格，自动给下方各遍插入等宽对齐空格";
        smartAlignBtn.Click += (_, _) =>
        {
            foreach (var box in altBoxes)
                box.Text = PartAnalyzer.AlignAltLyricToPrimary(primaryBox.Text, box.Text);
        };

        var leftActions = new StackPanel { Orientation = Orientation.Horizontal };
        leftActions.Children.Add(addVerseBtn);
        leftActions.Children.Add(smartAlignBtn);
        DockPanel.SetDock(leftActions, Dock.Left);
        actionsBar.Children.Add(leftActions);

        var tipText = new TextBlock
        {
            Text = "提示：等宽字体下按 2 下空格 = 1 个汉字宽度，可对准上方和弦",
            FontSize = 11.5,
            Foreground = AppDialogs.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(12, 0, 0, 0)
        };
        actionsBar.Children.Add(tipText);

        Grid.SetRow(actionsBar, 2);
        root.Children.Add(actionsBar);

        // ─── Row 3: Footer ───
        StackedLyricResult? result = null;
        var cancelBtn = AppDialogs.CreateSecondaryButton(owner, "取消", minWidth: 82);
        cancelBtn.IsCancel = true;
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);
        cancelBtn.Click += (_, _) => AppDialogs.CloseWithResult(window, false);

        var okBtn = AppDialogs.CreatePrimaryButton(owner, "确定", minWidth: 92);
        okBtn.IsDefault = true;
        okBtn.Click += (_, _) =>
        {
            var primaryText = (primaryBox.Text ?? "").Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal).Trim();
            if (string.IsNullOrWhiteSpace(primaryText))
            {
                AppDialogs.ShowInfo(window, "歌词提示", "第 1 遍主歌词不能为空。");
                primaryBox.Focus();
                return;
            }

            var alts = altBoxes
                .Select(b => (b.Text ?? "").Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal))
                .ToList();
            while (alts.Count > 0 && string.IsNullOrWhiteSpace(alts[^1]))
                alts.RemoveAt(alts.Count - 1);
            if (alts.All(string.IsNullOrWhiteSpace))
                alts.Clear();

            result = new StackedLyricResult(primaryText, alts);
            AppDialogs.CloseWithResult(window, true);
        };

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        footer.Children.Add(cancelBtn);
        footer.Children.Add(okBtn);
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        return window.ShowDialog() == true ? result : null;
    }

    private static TextBox CreateVerseInput(string text) => new()
    {
        Text = text ?? "",
        FontFamily = MonoFont,
        FontSize = 15.5,
        Padding = new Thickness(8, 5, 8, 5),
        MinWidth = 560,
        BorderBrush = AppDialogs.InputBorderBrush,
        BorderThickness = new Thickness(1),
        Background = Brushes.White
    };

    private static Grid BuildVerseRow(string badgeText, bool isPrimary, TextBox input, Action? onRemove)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });

        var badge = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(12),
            Background = isPrimary ? AppDialogs.AccentLightBrush : AppDialogs.SubtleSurfaceBrush,
            BorderBrush = isPrimary ? AppDialogs.AccentBrush : AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = badgeText,
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = isPrimary ? AppDialogs.AccentHoverBrush : AppDialogs.MutedBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(badge, 0);
        row.Children.Add(badge);

        Grid.SetColumn(input, 1);
        row.Children.Add(input);

        if (onRemove is not null)
        {
            var removeBtn = new Button
            {
                Content = "×",
                Width = 26,
                Height = 26,
                Padding = new Thickness(0),
                Margin = new Thickness(6, 0, 0, 0),
                FontSize = 14,
                Foreground = AppDialogs.MutedBrush,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ToolTip = "删除此遍歌词"
            };
            removeBtn.Click += (_, _) => onRemove();
            Grid.SetColumn(removeBtn, 2);
            row.Children.Add(removeBtn);
        }

        return row;
    }

    private static string BuildChordRuler(string primaryText, IReadOnlyList<ChordPlacement> chords)
    {
        if (chords is null || chords.Count == 0)
            return "（该句尚未放置和弦）";

        var sb = new StringBuilder();
        var sorted = chords.OrderBy(c => c.Position).ThenBy(c => c.BeforeChar ? 0 : 1);
        foreach (var chord in sorted)
        {
            var pos = Math.Clamp(chord.Position, 0, primaryText.Length);
            var cellOffset = PartAnalyzer.MeasureHalfCells(primaryText[..pos]);
            if (chord.BeforeChar && cellOffset > 0)
                cellOffset--;

            if (sb.Length > cellOffset)
                cellOffset = sb.Length + 1;

            if (cellOffset > sb.Length)
                sb.Append(' ', cellOffset - sb.Length);

            sb.Append(chord.Name);
        }
        return sb.ToString();
    }
}
