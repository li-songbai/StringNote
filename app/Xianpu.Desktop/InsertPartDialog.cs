using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Xianpu.Desktop;

public enum InsertPartMode
{
    CloneExisting,
    ReferenceExisting,
    NewBlank
}

public sealed record InsertPartDialogResult(
    InsertPartMode Mode,
    int SourcePartIndex,
    bool IncludeChords,
    string PartTitle,
    List<string> NewLyrics);

internal static class InsertPartDialog
{
    private static readonly string[] PresetPartTitles =
    [
        "主歌", "副歌", "预副歌", "桥段", "尾声", "间奏", "过渡"
    ];

    public static InsertPartDialogResult? Show(Window? owner, SongModel song, int afterLineIndex)
    {
        var allParts = PartAnalyzer.BuildParts(song.Lines);
        var sourceParts = allParts.Where(p => !p.IsReference).ToList();

        string subtitle;
        if (afterLineIndex >= 0 && afterLineIndex < song.Lines.Count)
        {
            var raw = (song.Lines[afterLineIndex].Text ?? "").Trim();
            var snippet = raw.Length > 20 ? raw[..20] + "…" : raw;
            subtitle = $"将在第 {afterLineIndex + 1} 句「{snippet}」之后插入段落";
        }
        else
        {
            subtitle = "将在曲谱开头插入新段落";
        }

        var workArea = SystemParameters.WorkArea;
        var winWidth = Math.Min(600, Math.Max(460, workArea.Width * 0.85));

        var window = AppDialogs.CreateCardWindow(
            owner,
            "在句后插入段落",
            subtitle,
            winWidth,
            height: null,
            resizable: false,
            out var root,
            badgeText: "插");

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ─── Row 0: Mode Selector ───
        var hasSources = sourceParts.Count > 0;
        var modeStrip = new Border
        {
            Background = AppDialogs.SubtleSurfaceBrush,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var modePanel = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };

        var cloneRadio = new RadioButton
        {
            Content = "复用已有段落（含和弦副本）",
            GroupName = "InsertPartModeGroup",
            IsEnabled = hasSources,
            IsChecked = hasSources,
            FontSize = 12.5,
            Margin = new Thickness(4, 2, 16, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        var refRadio = new RadioButton
        {
            Content = "插入反复引用行",
            GroupName = "InsertPartModeGroup",
            IsEnabled = hasSources,
            FontSize = 12.5,
            Margin = new Thickness(0, 2, 16, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        var newRadio = new RadioButton
        {
            Content = "新建手填段落",
            GroupName = "InsertPartModeGroup",
            IsChecked = !hasSources,
            FontSize = 12.5,
            Margin = new Thickness(0, 2, 4, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        modePanel.Children.Add(cloneRadio);
        modePanel.Children.Add(refRadio);
        modePanel.Children.Add(newRadio);
        modeStrip.Child = modePanel;
        Grid.SetRow(modeStrip, 0);
        root.Children.Add(modeStrip);

        // ─── Row 1: Mode Content Panels ───
        var bodyHost = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        Grid.SetRow(bodyHost, 1);
        root.Children.Add(bodyHost);

        // Panel A: Clone existing part
        var clonePanel = new StackPanel();
        var cloneSourceBox = new ComboBox
        {
            FontSize = 12.5,
            Padding = new Thickness(8, 5, 8, 5),
            Margin = new Thickness(0, 0, 0, 10)
        };
        foreach (var p in sourceParts)
        {
            var itemText = $"{p.Title}（{p.LineCount} 行 · {p.ChordCount} 和弦）";
            cloneSourceBox.Items.Add(new ComboBoxItem { Content = itemText, Tag = p });
        }
        if (cloneSourceBox.Items.Count > 0)
            cloneSourceBox.SelectedIndex = 0;

        var cloneTitleBox = new TextBox
        {
            FontSize = 12.5,
            Padding = new Thickness(8, 5, 8, 5),
            Margin = new Thickness(0, 0, 0, 8),
            Text = sourceParts.Count > 0 ? sourceParts[0].Title : "副歌"
        };
        var includeChordsCheck = new CheckBox
        {
            Content = "同时复制该段落已填好的全部和弦（推荐）",
            IsChecked = true,
            FontSize = 12.5,
            Foreground = AppDialogs.BodyBrush,
            Margin = new Thickness(0, 2, 0, 10)
        };
        var clonePreviewText = new TextBlock
        {
            FontSize = 12,
            Foreground = AppDialogs.MutedBrush,
            TextWrapping = TextWrapping.Wrap
        };
        var clonePreviewBorder = new Border
        {
            Background = AppDialogs.SubtleSurfaceBrush,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 7, 10, 7),
            Child = clonePreviewText
        };

        void UpdateClonePreview()
        {
            if (cloneSourceBox.SelectedItem is ComboBoxItem { Tag: PartBlock part })
            {
                var linesPreview = new List<string>();
                for (var i = 0; i < Math.Min(3, part.LineCount); i++)
                    linesPreview.Add(song.Lines[part.StartLine + i].Text);
                if (part.LineCount > 3)
                    linesPreview.Add($"…（共 {part.LineCount} 行）");
                clonePreviewText.Text = string.Join(Environment.NewLine, linesPreview);
            }
        }

        cloneSourceBox.SelectionChanged += (_, _) =>
        {
            if (cloneSourceBox.SelectedItem is ComboBoxItem { Tag: PartBlock part })
            {
                cloneTitleBox.Text = part.Title == "全曲" ? "段落副本" : part.Title;
                UpdateClonePreview();
            }
        };
        UpdateClonePreview();

        clonePanel.Children.Add(MakeLabel("选择要复用的已有段落："));
        clonePanel.Children.Add(cloneSourceBox);
        clonePanel.Children.Add(MakeLabel("插入后的段落标题："));
        clonePanel.Children.Add(cloneTitleBox);
        clonePanel.Children.Add(includeChordsCheck);
        clonePanel.Children.Add(clonePreviewBorder);

        // Panel B: Reference existing part
        var refPanel = new StackPanel();
        var refSourceBox = new ComboBox
        {
            FontSize = 12.5,
            Padding = new Thickness(8, 5, 8, 5),
            Margin = new Thickness(0, 0, 0, 10)
        };
        foreach (var p in sourceParts)
        {
            var itemText = $"{p.Title}（{p.LineCount} 行）";
            refSourceBox.Items.Add(new ComboBoxItem { Content = itemText, Tag = p });
        }
        if (refSourceBox.Items.Count > 0)
            refSourceBox.SelectedIndex = 0;

        var refPreviewPillText = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.AccentHoverBrush
        };
        var refPreviewPill = new Border
        {
            Background = AppDialogs.AccentLightBrush,
            BorderBrush = AppDialogs.AccentBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 7, 12, 7),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0),
            Child = refPreviewPillText
        };
        void UpdateRefPreview()
        {
            if (refSourceBox.SelectedItem is ComboBoxItem { Tag: PartBlock part })
                refPreviewPillText.Text = $"→ 反复 [{part.Title}]";
        }
        refSourceBox.SelectionChanged += (_, _) => UpdateRefPreview();
        UpdateRefPreview();

        refPanel.Children.Add(MakeLabel("反复指向哪一个段落："));
        refPanel.Children.Add(refSourceBox);
        refPanel.Children.Add(MakeLabel("谱面显示预览："));
        refPanel.Children.Add(refPreviewPill);

        // Panel C: New blank part
        var newPanel = new StackPanel();
        var newTitleBox = new ComboBox
        {
            IsEditable = true,
            ItemsSource = PresetPartTitles,
            Text = "副歌",
            FontSize = 12.5,
            Padding = new Thickness(8, 5, 8, 5),
            Margin = new Thickness(0, 0, 0, 10),
            Width = 180,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var newLyricsBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 148,
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 13,
            VerticalContentAlignment = VerticalAlignment.Top
        };
        newPanel.Children.Add(MakeLabel("新段落标题（例如：副歌、桥段、尾声）："));
        newPanel.Children.Add(newTitleBox);
        newPanel.Children.Add(MakeLabel("输入该段落歌词（每行一句）："));
        newPanel.Children.Add(newLyricsBox);

        bodyHost.Children.Add(clonePanel);
        bodyHost.Children.Add(refPanel);
        bodyHost.Children.Add(newPanel);

        void SyncModePanels()
        {
            clonePanel.Visibility = cloneRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            refPanel.Visibility = refRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            newPanel.Visibility = newRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }
        cloneRadio.Checked += (_, _) => SyncModePanels();
        refRadio.Checked += (_, _) => SyncModePanels();
        newRadio.Checked += (_, _) =>
        {
            SyncModePanels();
            window.Dispatcher.BeginInvoke(() => newLyricsBox.Focus());
        };
        SyncModePanels();

        // ─── Row 2: Footer ───
        InsertPartDialogResult? result = null;
        var cancelBtn = AppDialogs.CreateSecondaryButton(owner, "取消", minWidth: 84);
        cancelBtn.IsCancel = true;
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);
        cancelBtn.Click += (_, _) => AppDialogs.CloseWithResult(window, false);

        var okBtn = AppDialogs.CreatePrimaryButton(owner, "插入到曲谱", minWidth: 110);
        okBtn.Click += (_, _) =>
        {
            if (cloneRadio.IsChecked == true)
            {
                if (cloneSourceBox.SelectedItem is not ComboBoxItem { Tag: PartBlock part })
                    return;
                result = new InsertPartDialogResult(
                    InsertPartMode.CloneExisting,
                    part.PartIndex,
                    includeChordsCheck.IsChecked == true,
                    (cloneTitleBox.Text ?? "").Trim(),
                    []);
                AppDialogs.CloseWithResult(window, true);
                return;
            }

            if (refRadio.IsChecked == true)
            {
                if (refSourceBox.SelectedItem is not ComboBoxItem { Tag: PartBlock part })
                    return;
                result = new InsertPartDialogResult(
                    InsertPartMode.ReferenceExisting,
                    part.PartIndex,
                    false,
                    part.Title,
                    []);
                AppDialogs.CloseWithResult(window, true);
                return;
            }

            var lines = (newLyricsBox.Text ?? "")
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
            if (lines.Count == 0)
            {
                AppDialogs.ShowInfo(window, "歌词提示", "请至少输入一行新段落的歌词。");
                newLyricsBox.Focus();
                return;
            }

            result = new InsertPartDialogResult(
                InsertPartMode.NewBlank,
                -1,
                false,
                (newTitleBox.Text ?? "").Trim(),
                lines);
            AppDialogs.CloseWithResult(window, true);
        };

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        footer.Children.Add(cancelBtn);
        footer.Children.Add(okBtn);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        return window.ShowDialog() == true ? result : null;
    }

    private static TextBlock MakeLabel(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        Foreground = AppDialogs.BodyBrush,
        Margin = new Thickness(0, 0, 0, 5)
    };
}
