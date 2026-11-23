using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Xianpu.Desktop;

public sealed record TabEditorResult(bool Saved, bool Deleted, TabSection? Section);

internal static class TabEditorWindow
{
    private static readonly string[] StringNames = ["1弦 (e)", "2弦 (B)", "3弦 (G)", "4弦 (D)", "5弦 (A)", "6弦 (E)"];
    private static readonly string[] TimeSignatures = ["4/4", "3/4", "6/8", "2/4"];
    private static readonly int[] ColumnOptions = [4, 6, 8, 12, 16];

    public static TabEditorResult Show(
        Window? owner,
        TabSection? existing,
        IReadOnlyList<LyricLine> lyricLines,
        TabSectionKind defaultKind = TabSectionKind.Intro,
        int defaultInsertAfterLine = -1)
    {
        var isNew = existing is null;
        var working = existing is not null
            ? existing.Clone()
            : CreateDefaultSection(defaultKind, defaultInsertAfterLine, lyricLines.Count);

        if (working.Bars.Count == 0)
            working.Bars.Add(new TabBar { ColumnCount = working.ColumnsPerBar });

        var workArea = SystemParameters.WorkArea;
        var winWidth = Math.Max(640, Math.Min(980, workArea.Width * 0.92));
        var winHeight = Math.Max(460, Math.Min(690, workArea.Height * 0.90));

        var window = AppDialogs.CreateCardWindow(
            owner,
            isNew ? "添加六线谱段落（前奏 / 间奏）" : $"编辑六线谱 · {working.Title}",
            "可选拓展组件 · 仅用于前奏、间奏等演奏片段记谱，全谱升降调与移调不会修改此处内容",
            winWidth,
            winHeight,
            resizable: true,
            out var root,
            badgeText: "六");

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Meta bar
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bar & preset toolbar
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Cell + keypad + chord mark
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Interactive canvas
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

        var syncing = false;
        var deleted = false;

        // ─── Row 0: Section Metadata ──────────────────────────────────────────────────
        var metaPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 8), VerticalAlignment = VerticalAlignment.Center };

        metaPanel.Children.Add(MakeCaption("类型"));
        var kindBox = new ComboBox { Width = 122, Margin = new Thickness(0, 2, 10, 2), FontSize = 12 };
        kindBox.Items.Add(new ComboBoxItem { Content = "前奏（开头）", Tag = TabSectionKind.Intro });
        kindBox.Items.Add(new ComboBoxItem { Content = "间奏（句间）", Tag = TabSectionKind.Interlude });
        kindBox.Items.Add(new ComboBoxItem { Content = "尾奏（结尾）", Tag = TabSectionKind.Outro });
        kindBox.SelectedIndex = working.Kind switch
        {
            TabSectionKind.Intro => 0,
            TabSectionKind.Outro => 2,
            _ => 1
        };
        metaPanel.Children.Add(kindBox);

        metaPanel.Children.Add(MakeCaption("标题"));
        var titleBox = new TextBox
        {
            Width = 110,
            Text = working.Title,
            FontSize = 12.5,
            Padding = new Thickness(7, 4, 7, 4),
            Margin = new Thickness(0, 2, 10, 2)
        };
        metaPanel.Children.Add(titleBox);

        var insertCaption = MakeCaption("插入位置");
        var insertAfterBox = new ComboBox
        {
            Width = 186,
            Margin = new Thickness(0, 2, 10, 2),
            FontSize = 12,
            ToolTip = "选择间奏插入在哪一句歌词之后"
        };
        PopulateInsertPositions(insertAfterBox, lyricLines, working.InsertAfterLineIndex);
        metaPanel.Children.Add(insertCaption);
        metaPanel.Children.Add(insertAfterBox);

        metaPanel.Children.Add(MakeCaption("拍号"));
        var timeSigBox = new ComboBox { Width = 68, IsEditable = true, Margin = new Thickness(0, 2, 10, 2), FontSize = 12 };
        foreach (var ts in TimeSignatures) timeSigBox.Items.Add(ts);
        timeSigBox.Text = working.TimeSignature;
        metaPanel.Children.Add(timeSigBox);

        metaPanel.Children.Add(MakeCaption("每小节格数"));
        var colsBox = new ComboBox { Width = 66, Margin = new Thickness(0, 2, 10, 2), FontSize = 12 };
        foreach (var c in ColumnOptions) colsBox.Items.Add(c);
        colsBox.SelectedItem = ColumnOptions.Contains(working.ColumnsPerBar) ? working.ColumnsPerBar : 8;
        metaPanel.Children.Add(colsBox);

        var immuneBadge = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = AppDialogs.AccentLightBrush,
            BorderBrush = new SolidColorBrush(Color.FromRgb(148, 191, 164)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 2, 0, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "不受变调影响",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = AppDialogs.AccentHoverBrush
            }
        };
        metaPanel.Children.Add(immuneBadge);

        Grid.SetRow(metaPanel, 0);
        root.Children.Add(metaPanel);

        // ─── Row 1: Bar & Preset Toolbar ──────────────────────────────────────────────
        var barToolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8), VerticalAlignment = VerticalAlignment.Center };

        var addBarBtn = MakeSmallBtn(owner, "+ 添加小节");
        var removeBarBtn = MakeSmallBtn(owner, "- 删除末小节");
        var clearBarBtn = MakeSmallBtn(owner, "清空当前小节");
        var insertSlotBtn = MakeSmallBtn(owner, "插拍 →");
        var deleteSlotBtn = MakeSmallBtn(owner, "删拍 ←");
        var undoBtn = MakeSmallBtn(owner, "撤销");
        undoBtn.IsEnabled = false;
        var copyBarBtn = MakeSmallBtn(owner, "复制小节");
        var pasteBarBtn = MakeSmallBtn(owner, "粘贴小节");
        pasteBarBtn.IsEnabled = false;
        var dupBarBtn = MakeSmallBtn(owner, "复制并追加");

        var repeatStartToggle = new ToggleButton
        {
            Content = "||: 起",
            Padding = new Thickness(7, 3, 7, 3),
            Margin = new Thickness(0, 2, 4, 2),
            FontSize = 11.5,
            Cursor = Cursors.Hand,
            ToolTip = "在当前小节左侧添加反复记号起点 ||:"
        };
        var repeatEndToggle = new ToggleButton
        {
            Content = ":|| 止",
            Padding = new Thickness(7, 3, 7, 3),
            Margin = new Thickness(0, 2, 6, 2),
            FontSize = 11.5,
            Cursor = Cursors.Hand,
            ToolTip = "在当前小节右侧添加反复记号终点 :||"
        };
        var showRhythmCheck = new CheckBox
        {
            Content = "时值符干",
            IsChecked = working.ShowRhythmStems,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 2, 8, 2),
            ToolTip = "在六线谱下方绘制节奏时值符干与连音横杠"
        };

        var pattern44Btn = MakeSmallBtn(owner, "模板 53231323");
        var pattern34Btn = MakeSmallBtn(owner, "模板 532123");
        var strumPatternBtn = MakeSmallBtn(owner, "模板 扫弦 ↓↑");
        var importAsciiBtn = MakeSmallBtn(owner, "导入文本谱...");
        var importTabBtn = MakeSmallBtn(owner, "导入片段...");
        var exportTabBtn = MakeSmallBtn(owner, "导出片段...");

        insertSlotBtn.ToolTip = "在当前拍位插入 1 个空拍，当前拍及后续所有音符跨小节自动后移 (Insert)";
        deleteSlotBtn.ToolTip = "删除当前拍位，后续所有音符跨小节自动前移 1 格 (Shift+Delete / Ctrl+Backspace)";
        undoBtn.ToolTip = "撤销上一步六线谱编辑 (Ctrl+Z)";
        copyBarBtn.ToolTip = "复制当前小节全部音符、时值、扫弦与和弦标注 (Ctrl+C)";
        pasteBarBtn.ToolTip = "将已复制的小节内容粘贴覆盖到当前小节 (Ctrl+V)";
        dupBarBtn.ToolTip = "复制当前小节并直接追加到下一小节 (Ctrl+D)";
        pattern44Btn.ToolTip = "向当前小节填入常用 4/4 分解拨弦示意（可在各格修改品数）";
        pattern34Btn.ToolTip = "向当前小节填入常用 3/4 分解拨弦示意";
        strumPatternBtn.ToolTip = "向当前小节填入常用 4/4 扫弦节奏型（↓ ↓↑ ↑↓↑）";
        importAsciiBtn.ToolTip = "粘贴标准 6 行 ASCII 文本六线谱（如 e|---0---|），自动解析为小节";
        importTabBtn.ToolTip = "导入好友分享的 .xianputab.json 六线谱片段";
        exportTabBtn.ToolTip = "将当前六线谱导出为 .xianputab.json 文件，发给好友可直接导入编辑";

        barToolbar.Children.Add(addBarBtn);
        barToolbar.Children.Add(removeBarBtn);
        barToolbar.Children.Add(clearBarBtn);
        barToolbar.Children.Add(new Border { Width = 1, Height = 18, Background = AppDialogs.SubtleBorderBrush, Margin = new Thickness(3, 2, 7, 2) });
        barToolbar.Children.Add(insertSlotBtn);
        barToolbar.Children.Add(deleteSlotBtn);
        barToolbar.Children.Add(undoBtn);
        barToolbar.Children.Add(new Border { Width = 1, Height = 18, Background = AppDialogs.SubtleBorderBrush, Margin = new Thickness(3, 2, 7, 2) });
        barToolbar.Children.Add(copyBarBtn);
        barToolbar.Children.Add(pasteBarBtn);
        barToolbar.Children.Add(dupBarBtn);
        barToolbar.Children.Add(new Border { Width = 1, Height = 18, Background = AppDialogs.SubtleBorderBrush, Margin = new Thickness(3, 2, 7, 2) });
        barToolbar.Children.Add(repeatStartToggle);
        barToolbar.Children.Add(repeatEndToggle);
        barToolbar.Children.Add(showRhythmCheck);
        barToolbar.Children.Add(new Border { Width = 1, Height = 18, Background = AppDialogs.SubtleBorderBrush, Margin = new Thickness(0, 2, 7, 2) });
        barToolbar.Children.Add(pattern44Btn);
        barToolbar.Children.Add(pattern34Btn);
        barToolbar.Children.Add(strumPatternBtn);
        barToolbar.Children.Add(importAsciiBtn);
        barToolbar.Children.Add(importTabBtn);
        barToolbar.Children.Add(exportTabBtn);

        Grid.SetRow(barToolbar, 1);
        root.Children.Add(barToolbar);

        // ─── Row 2: Active Cell Info + Chord Mark + Quick Keypad + Rhythm/Technique ──
        var cellBar = new Border
        {
            Background = AppDialogs.SubtleSurfaceBrush,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var cellStack = new StackPanel();
        var cellWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 5) };

        var activeCellLabel = new TextBlock
        {
            Text = "第 1 小节 · 第 1 格 · 1弦 (e)",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.AccentHoverBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 12, 2)
        };
        cellWrap.Children.Add(activeCellLabel);

        cellWrap.Children.Add(MakeCaption("当前拍位和弦标注"));
        var chordMarkBox = new TextBox
        {
            Width = 78,
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Padding = new Thickness(6, 3, 6, 3),
            Margin = new Thickness(0, 2, 12, 2),
            ToolTip = "在当前小节的当前拍位上方标注和弦（如 C、Am7、G/B，不受全谱变调影响）"
        };
        cellWrap.Children.Add(chordMarkBox);

        var keypadLabel = MakeCaption("快捷填入");
        cellWrap.Children.Add(keypadLabel);

        var keypadTokens = new[] { "0", "1", "2", "3", "4", "5", "7", "9", "12", "x", "s", "h", "p", "b", "~", "NH" };
        var keypadButtons = new List<Button>();
        foreach (var tok in keypadTokens)
        {
            var btn = new Button
            {
                Content = tok,
                Tag = tok,
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(0, 2, 3, 2),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                MinWidth = 26,
                Cursor = Cursors.Hand
            };
            if (owner?.TryFindResource(typeof(Button)) is Style baseStyle)
                btn.Style = baseStyle;
            cellWrap.Children.Add(btn);
            keypadButtons.Add(btn);
        }

        var clearCellBtn = MakeSmallBtn(owner, "擦除");
        clearCellBtn.Margin = new Thickness(3, 2, 0, 2);
        cellWrap.Children.Add(clearCellBtn);

        var exprWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        exprWrap.Children.Add(MakeCaption("时值"));
        var durationBox = new ComboBox { Width = 84, Margin = new Thickness(0, 2, 8, 2), FontSize = 11.5, ToolTip = "设置当前拍位时值（默认按前后空隙自动推导）" };
        durationBox.Items.Add(new ComboBoxItem { Content = "自动", Tag = TabDuration.Auto });
        durationBox.Items.Add(new ComboBoxItem { Content = "全音符", Tag = TabDuration.Whole });
        durationBox.Items.Add(new ComboBoxItem { Content = "二分", Tag = TabDuration.Half });
        durationBox.Items.Add(new ComboBoxItem { Content = "四分", Tag = TabDuration.Quarter });
        durationBox.Items.Add(new ComboBoxItem { Content = "八分", Tag = TabDuration.Eighth });
        durationBox.Items.Add(new ComboBoxItem { Content = "十六分", Tag = TabDuration.Sixteenth });
        exprWrap.Children.Add(durationBox);

        var dottedToggle = new ToggleButton
        {
            Content = "附点 .",
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(0, 2, 6, 2),
            FontSize = 11.5,
            Cursor = Cursors.Hand,
            ToolTip = "为当前拍位添加附点"
        };
        exprWrap.Children.Add(dottedToggle);

        var restToggle = new ToggleButton
        {
            Content = "休止",
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(0, 2, 12, 2),
            FontSize = 11.5,
            Cursor = Cursors.Hand,
            ToolTip = "将当前拍位标为休止符"
        };
        exprWrap.Children.Add(restToggle);

        exprWrap.Children.Add(MakeCaption("扫弦/琶音"));
        var strumBox = new ComboBox { Width = 104, Margin = new Thickness(0, 2, 12, 2), FontSize = 11.5, ToolTip = "设置当前拍位的整列扫弦箭头、切音或琶音符号" };
        strumBox.Items.Add(new ComboBoxItem { Content = "无", Tag = TabStrumKind.None });
        strumBox.Items.Add(new ComboBoxItem { Content = "下扫 (6→1)", Tag = TabStrumKind.Down });
        strumBox.Items.Add(new ComboBoxItem { Content = "上扫 (1→6)", Tag = TabStrumKind.Up });
        strumBox.Items.Add(new ComboBoxItem { Content = "切音 ×", Tag = TabStrumKind.Mute });
        strumBox.Items.Add(new ComboBoxItem { Content = "琶音 ⌇", Tag = TabStrumKind.Arpeggio });
        exprWrap.Children.Add(strumBox);

        var palmMuteToggle = new ToggleButton
        {
            Content = "P.M. 闷音",
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(0, 2, 12, 2),
            FontSize = 11.5,
            Cursor = Cursors.Hand,
            ToolTip = "标记当前拍位为右手闷音 (快捷键 m)，连续多拍自动画 P.M. 虚线"
        };
        exprWrap.Children.Add(palmMuteToggle);

        exprWrap.Children.Add(MakeCaption("连音(→下音)"));
        var linkBox = new ComboBox { Width = 92, Margin = new Thickness(0, 2, 10, 2), FontSize = 11.5, ToolTip = "连接当前格音符与同弦下一个音符（画连音弧线或滑音斜线）" };
        linkBox.Items.Add(new ComboBoxItem { Content = "无", Tag = TabLinkKind.None });
        linkBox.Items.Add(new ComboBoxItem { Content = "击弦 H", Tag = TabLinkKind.HammerOn });
        linkBox.Items.Add(new ComboBoxItem { Content = "勾弦 P", Tag = TabLinkKind.PullOff });
        linkBox.Items.Add(new ComboBoxItem { Content = "滑音 S", Tag = TabLinkKind.Slide });
        linkBox.Items.Add(new ComboBoxItem { Content = "延音线 ⌒", Tag = TabLinkKind.Tie });
        exprWrap.Children.Add(linkBox);

        exprWrap.Children.Add(MakeCaption("倚音(不占拍)"));
        var graceFretBox = new TextBox
        {
            Width = 38,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Padding = new Thickness(5, 2, 5, 2),
            Margin = new Thickness(0, 2, 4, 2),
            ToolTip = "不占拍子的前置装饰音品位（例：主音填 7、倚音填 5 即为 5s7 滑音，5 不占时值；也可在格内直接连按 5 s 7）"
        };
        exprWrap.Children.Add(graceFretBox);

        var graceLinkBox = new ComboBox
        {
            Width = 80,
            Margin = new Thickness(0, 2, 10, 2),
            FontSize = 11.5,
            ToolTip = "倚音连到主音的技巧符号"
        };
        graceLinkBox.Items.Add(new ComboBoxItem { Content = "滑入 S", Tag = TabLinkKind.Slide });
        graceLinkBox.Items.Add(new ComboBoxItem { Content = "击入 H", Tag = TabLinkKind.HammerOn });
        graceLinkBox.Items.Add(new ComboBoxItem { Content = "勾入 P", Tag = TabLinkKind.PullOff });
        graceLinkBox.Items.Add(new ComboBoxItem { Content = "连线 ⌒", Tag = TabLinkKind.Tie });
        exprWrap.Children.Add(graceLinkBox);

        exprWrap.Children.Add(MakeCaption("单音技巧"));
        var techniqueBox = new ComboBox
        {
            Width = 146,
            Margin = new Thickness(0, 2, 0, 2),
            FontSize = 11.5,
            ToolTip = "为当前选中音符添加推弦、泛音、揉弦、轻音或单向滑音技巧"
        };
        techniqueBox.Items.Add(new ComboBoxItem { Content = "无", Tag = TabNoteTechnique.None });
        techniqueBox.Items.Add(new ComboBoxItem { Content = "全音推弦 (Full)", Tag = TabNoteTechnique.BendFull });
        techniqueBox.Items.Add(new ComboBoxItem { Content = "半音推弦 (½)", Tag = TabNoteTechnique.BendHalf });
        techniqueBox.Items.Add(new ComboBoxItem { Content = "全音推弦回放 (Full ↑↓)", Tag = TabNoteTechnique.BendRelease });
        techniqueBox.Items.Add(new ComboBoxItem { Content = "半音推弦回放 (½ ↑↓)", Tag = TabNoteTechnique.BendHalfRelease });
        techniqueBox.Items.Add(new ComboBoxItem { Content = "揉弦 (~~~)", Tag = TabNoteTechnique.Vibrato });
        techniqueBox.Items.Add(new ComboBoxItem { Content = "自然泛音 (N.H.)", Tag = TabNoteTechnique.NaturalHarmonic });
        techniqueBox.Items.Add(new ComboBoxItem { Content = "人工泛音 (A.H.)", Tag = TabNoteTechnique.ArtificialHarmonic });
        techniqueBox.Items.Add(new ComboBoxItem { Content = "轻音括号 ((n))", Tag = TabNoteTechnique.GhostNote });
        techniqueBox.Items.Add(new ComboBoxItem { Content = "无起点滑入 (/n)", Tag = TabNoteTechnique.SlideIn });
        techniqueBox.Items.Add(new ComboBoxItem { Content = "无终点滑出 (n\\)", Tag = TabNoteTechnique.SlideOut });
        exprWrap.Children.Add(techniqueBox);

        cellStack.Children.Add(cellWrap);
        cellStack.Children.Add(exprWrap);
        cellBar.Child = cellStack;
        Grid.SetRow(cellBar, 2);
        root.Children.Add(cellBar);

        // ─── Row 3: Interactive Visual TAB Canvas ─────────────────────────────────────
        var canvas = new TabEditorCanvas(working);
        var canvasScroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.White,
            Padding = new Thickness(12),
            Content = canvas
        };
        var canvasBorder = new Border
        {
            BorderBrush = AppDialogs.InputBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Background = Brushes.White,
            ClipToBounds = true,
            Child = canvasScroller
        };
        Grid.SetRow(canvasBorder, 3);
        root.Children.Add(canvasBorder);

        // ─── Row 4: Footer ────────────────────────────────────────────────────────────
        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };
        var helpText = new TextBlock
        {
            Text = "提示：0-9/x 填谱，5 s 7 倚音滑音，b 推弦，n 泛音，~/v 揉弦，Insert 插拍，Shift+Del 删拍前移，Ctrl+Z 撤销，Ctrl+C/V/D 复制小节。",
            FontSize = 11.5,
            Foreground = AppDialogs.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        DockPanel.SetDock(helpText, Dock.Left);
        footer.Children.Add(helpText);

        var rightBtns = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(rightBtns, Dock.Right);

        if (!isNew)
        {
            var deleteBtn = AppDialogs.CreateDangerButton(owner, "删除此六线谱", minWidth: 102);
            deleteBtn.Margin = new Thickness(0, 0, 8, 0);
            deleteBtn.Click += (_, _) =>
            {
                if (AppDialogs.Confirm(window, "删除六线谱", $"确定删除六线谱段落「{working.Title}」吗？", "确定删除", isDanger: true))
                {
                    deleted = true;
                    AppDialogs.CloseWithResult(window, false);
                }
            };
            rightBtns.Children.Add(deleteBtn);
        }

        var cancelBtn = AppDialogs.CreateSecondaryButton(owner, "取消", minWidth: 74);
        cancelBtn.IsCancel = true;
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);
        cancelBtn.Click += (_, _) => AppDialogs.CloseWithResult(window, false);

        var saveBtn = AppDialogs.CreatePrimaryButton(owner, "保存并应用", minWidth: 102);
        saveBtn.Click += (_, _) =>
        {
            working.Title = string.IsNullOrWhiteSpace(titleBox.Text)
                ? working.Kind switch
                {
                    TabSectionKind.Intro => "前奏",
                    TabSectionKind.Outro => "尾奏",
                    _ => "间奏"
                }
                : titleBox.Text.Trim();
            working.TimeSignature = string.IsNullOrWhiteSpace(timeSigBox.Text) ? "4/4" : timeSigBox.Text.Trim();
            AppDialogs.CloseWithResult(window, true);
        };

        rightBtns.Children.Add(cancelBtn);
        rightBtns.Children.Add(saveBtn);
        footer.Children.Add(rightBtns);
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        // ─── Wiring ───────────────────────────────────────────────────────────────────
        void SyncInsertVisibility()
        {
            var isInterlude = working.Kind == TabSectionKind.Interlude && lyricLines.Count > 0;
            insertCaption.Visibility = isInterlude ? Visibility.Visible : Visibility.Collapsed;
            insertAfterBox.Visibility = isInterlude ? Visibility.Visible : Visibility.Collapsed;
        }

        TabBar? copiedBar = null;
        var undoHistory = new List<List<TabBar>>();

        void PushUndo()
        {
            if (undoHistory.Count >= 50)
                undoHistory.RemoveAt(0);
            undoHistory.Add(working.Bars.Select(b => b.Clone()).ToList());
            undoBtn.IsEnabled = true;
        }

        void PerformUndo()
        {
            if (undoHistory.Count == 0) return;
            var snapshot = undoHistory[^1];
            undoHistory.RemoveAt(undoHistory.Count - 1);
            working.Bars.Clear();
            working.Bars.AddRange(snapshot);
            if (working.Bars.Count == 0)
                working.Bars.Add(new TabBar { ColumnCount = working.ColumnsPerBar });
            undoBtn.IsEnabled = undoHistory.Count > 0;
            canvas.ClampCursor();
            canvas.RebuildLayout();
            SyncCellHeader();
        }

        void InsertSlotRipple()
        {
            var (bIdx, cIdx, _) = canvas.ActiveCell;
            if (bIdx < 0 || bIdx >= working.Bars.Count) return;
            PushUndo();
            RippleInsertSlot(working, bIdx, cIdx);
            canvas.ClampCursor();
            canvas.RebuildLayout();
            SyncCellHeader();
        }

        void DeleteSlotRipple()
        {
            var (bIdx, cIdx, _) = canvas.ActiveCell;
            if (bIdx < 0 || bIdx >= working.Bars.Count) return;
            PushUndo();
            RippleDeleteSlot(working, bIdx, cIdx);
            canvas.InvalidateVisual();
            SyncCellHeader();
        }

        void SyncCellHeader()
        {
            syncing = true;
            try
            {
                var (bIdx, cIdx, sIdx) = canvas.ActiveCell;
                activeCellLabel.Text = $"第 {bIdx + 1} 小节 · 第 {cIdx + 1} 格 · {StringNames[sIdx]}";
                chordMarkBox.Text = canvas.GetChordMark(bIdx, cIdx) ?? "";
                removeBarBtn.IsEnabled = working.Bars.Count > 1;

                if (bIdx >= 0 && bIdx < working.Bars.Count)
                {
                    var curBar = working.Bars[bIdx];
                    repeatStartToggle.IsChecked = curBar.RepeatStart;
                    repeatEndToggle.IsChecked = curBar.RepeatEnd;
                }

                var colMeta = canvas.GetColumnMeta(bIdx, cIdx);
                SelectComboByTag(durationBox, colMeta?.Duration ?? TabDuration.Auto);
                dottedToggle.IsChecked = colMeta?.Dotted == true;
                restToggle.IsChecked = colMeta?.IsRest == true;
                SelectComboByTag(strumBox, colMeta?.Strum ?? TabStrumKind.None);
                palmMuteToggle.IsChecked = colMeta?.PalmMute == true;

                var activeNote = canvas.GetActiveNote();
                linkBox.IsEnabled = activeNote is not null;
                SelectComboByTag(linkBox, activeNote?.LinkToNext ?? TabLinkKind.None);

                graceFretBox.IsEnabled = activeNote is not null;
                graceFretBox.Text = activeNote?.GraceFret ?? "";
                graceLinkBox.IsEnabled = activeNote is not null && !string.IsNullOrWhiteSpace(activeNote.GraceFret);
                SelectComboByTag(
                    graceLinkBox,
                    activeNote is { GraceLink: not TabLinkKind.None } ? activeNote.GraceLink : TabLinkKind.Slide);

                techniqueBox.IsEnabled = activeNote is not null;
                SelectComboByTag(techniqueBox, activeNote?.Technique ?? TabNoteTechnique.None);
            }
            finally
            {
                syncing = false;
            }
        }

        void CopyCurrentBar()
        {
            var (bIdx, _, _) = canvas.ActiveCell;
            if (bIdx < 0 || bIdx >= working.Bars.Count) return;
            copiedBar = working.Bars[bIdx].Clone();
            pasteBarBtn.IsEnabled = true;
            pasteBarBtn.ToolTip = $"将已复制的第 {bIdx + 1} 小节内容粘贴覆盖到当前小节 (Ctrl+V)";
        }

        void PasteToCurrentBar()
        {
            if (copiedBar is null) return;
            var (bIdx, _, sIdx) = canvas.ActiveCell;
            if (bIdx < 0 || bIdx >= working.Bars.Count) return;
            PushUndo();
            var target = working.Bars[bIdx];
            ApplyCopiedBarToTarget(copiedBar, target);
            canvas.SelectCell(bIdx, 0, sIdx);
            canvas.InvalidateVisual();
            SyncCellHeader();
        }

        void DuplicateCurrentBar()
        {
            var (bIdx, _, sIdx) = canvas.ActiveCell;
            if (bIdx < 0 || bIdx >= working.Bars.Count) return;
            PushUndo();
            var clone = working.Bars[bIdx].Clone();
            working.Bars.Insert(bIdx + 1, clone);
            canvas.SelectCell(bIdx + 1, 0, sIdx);
            canvas.RebuildLayout();
            SyncCellHeader();
        }

        SyncInsertVisibility();
        SyncCellHeader();

        canvas.SelectionChanged += SyncCellHeader;
        canvas.BeforeEdit += PushUndo;
        canvas.CopyBarRequested += CopyCurrentBar;
        canvas.PasteBarRequested += PasteToCurrentBar;
        canvas.DuplicateBarRequested += DuplicateCurrentBar;
        canvas.InsertSlotRequested += InsertSlotRipple;
        canvas.DeleteSlotRequested += DeleteSlotRipple;
        canvas.UndoRequested += PerformUndo;

        kindBox.SelectionChanged += (_, _) =>
        {
            if (kindBox.SelectedItem is not ComboBoxItem { Tag: TabSectionKind kind }) return;
            var prevDefault = working.Kind switch
            {
                TabSectionKind.Intro => "前奏",
                TabSectionKind.Outro => "尾奏",
                _ => "间奏"
            };
            working.Kind = kind;
            if (string.IsNullOrWhiteSpace(titleBox.Text) || titleBox.Text.Trim() == prevDefault)
            {
                titleBox.Text = kind switch
                {
                    TabSectionKind.Intro => "前奏",
                    TabSectionKind.Outro => "尾奏",
                    _ => "间奏"
                };
            }
            working.InsertAfterLineIndex = kind switch
            {
                TabSectionKind.Intro => -1,
                TabSectionKind.Outro => lyricLines.Count > 0 ? lyricLines.Count - 1 : -1,
                _ => lyricLines.Count > 0
                    ? (insertAfterBox.SelectedValue is int idx ? idx : 0)
                    : -1
            };
            SyncInsertVisibility();
        };

        insertAfterBox.SelectionChanged += (_, _) =>
        {
            if (working.Kind == TabSectionKind.Interlude && insertAfterBox.SelectedValue is int idx)
                working.InsertAfterLineIndex = idx;
        };

        colsBox.SelectionChanged += (_, _) =>
        {
            if (syncing || colsBox.SelectedItem is not int count) return;
            PushUndo();
            working.ColumnsPerBar = count;
            foreach (var bar in working.Bars)
            {
                bar.ColumnCount = count;
                bar.Notes.RemoveAll(n => n.Column >= count);
                bar.ChordMarks.RemoveAll(m => m.Column >= count);
                bar.ColumnMetas.RemoveAll(m => m.Column >= count);
            }
            canvas.ClampCursor();
            canvas.RebuildLayout();
            SyncCellHeader();
        };

        chordMarkBox.TextChanged += (_, _) =>
        {
            if (syncing) return;
            var (bIdx, cIdx, _) = canvas.ActiveCell;
            canvas.SetChordMark(bIdx, cIdx, chordMarkBox.Text.Trim());
        };

        chordMarkBox.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Escape)
            {
                canvas.Focus();
                e.Handled = true;
            }
        };

        foreach (var btn in keypadButtons)
        {
            btn.Click += (_, _) =>
            {
                if (btn.Tag is string tok)
                {
                    canvas.ApplyToken(tok);
                    canvas.Focus();
                }
            };
        }

        clearCellBtn.Click += (_, _) =>
        {
            canvas.ClearCurrentCell();
            canvas.Focus();
        };

        addBarBtn.Click += (_, _) =>
        {
            working.Bars.Add(new TabBar { ColumnCount = working.ColumnsPerBar });
            canvas.SelectCell(working.Bars.Count - 1, 0, canvas.ActiveCell.StringIndex);
            canvas.RebuildLayout();
            SyncCellHeader();
            canvas.Focus();
        };

        removeBarBtn.Click += (_, _) =>
        {
            if (working.Bars.Count <= 1) return;
            PushUndo();
            working.Bars.RemoveAt(working.Bars.Count - 1);
            canvas.ClampCursor();
            canvas.RebuildLayout();
            SyncCellHeader();
            canvas.Focus();
        };

        clearBarBtn.Click += (_, _) =>
        {
            var (bIdx, _, _) = canvas.ActiveCell;
            if (bIdx >= 0 && bIdx < working.Bars.Count)
            {
                PushUndo();
                working.Bars[bIdx].Notes.Clear();
                working.Bars[bIdx].ChordMarks.Clear();
                working.Bars[bIdx].ColumnMetas.Clear();
                working.Bars[bIdx].RepeatStart = false;
                working.Bars[bIdx].RepeatEnd = false;
                canvas.InvalidateVisual();
                SyncCellHeader();
            }
            canvas.Focus();
        };

        insertSlotBtn.Click += (_, _) => { InsertSlotRipple(); canvas.Focus(); };
        deleteSlotBtn.Click += (_, _) => { DeleteSlotRipple(); canvas.Focus(); };
        undoBtn.Click += (_, _) => { PerformUndo(); canvas.Focus(); };
        copyBarBtn.Click += (_, _) => { CopyCurrentBar(); canvas.Focus(); };
        pasteBarBtn.Click += (_, _) => { PasteToCurrentBar(); canvas.Focus(); };
        dupBarBtn.Click += (_, _) => { DuplicateCurrentBar(); canvas.Focus(); };

        void UpdateCurrentBarRepeat()
        {
            if (syncing) return;
            var (bIdx, _, _) = canvas.ActiveCell;
            if (bIdx < 0 || bIdx >= working.Bars.Count) return;
            PushUndo();
            working.Bars[bIdx].RepeatStart = repeatStartToggle.IsChecked == true;
            working.Bars[bIdx].RepeatEnd = repeatEndToggle.IsChecked == true;
            canvas.InvalidateVisual();
            canvas.Focus();
        }
        repeatStartToggle.Checked += (_, _) => UpdateCurrentBarRepeat();
        repeatStartToggle.Unchecked += (_, _) => UpdateCurrentBarRepeat();
        repeatEndToggle.Checked += (_, _) => UpdateCurrentBarRepeat();
        repeatEndToggle.Unchecked += (_, _) => UpdateCurrentBarRepeat();

        showRhythmCheck.Checked += (_, _) => { if (!syncing) { working.ShowRhythmStems = true; canvas.InvalidateVisual(); canvas.Focus(); } };
        showRhythmCheck.Unchecked += (_, _) => { if (!syncing) { working.ShowRhythmStems = false; canvas.InvalidateVisual(); canvas.Focus(); } };

        durationBox.SelectionChanged += (_, _) =>
        {
            if (syncing || durationBox.SelectedItem is not ComboBoxItem { Tag: TabDuration dur }) return;
            var (bIdx, cIdx, _) = canvas.ActiveCell;
            canvas.UpdateColumnMeta(bIdx, cIdx, m => m.Duration = dur);
            canvas.Focus();
        };
        dottedToggle.Checked += (_, _) => { if (!syncing) { var (b, c, _) = canvas.ActiveCell; canvas.UpdateColumnMeta(b, c, m => m.Dotted = true); canvas.Focus(); } };
        dottedToggle.Unchecked += (_, _) => { if (!syncing) { var (b, c, _) = canvas.ActiveCell; canvas.UpdateColumnMeta(b, c, m => m.Dotted = false); canvas.Focus(); } };
        restToggle.Checked += (_, _) => { if (!syncing) { var (b, c, _) = canvas.ActiveCell; canvas.UpdateColumnMeta(b, c, m => m.IsRest = true); canvas.Focus(); } };
        restToggle.Unchecked += (_, _) => { if (!syncing) { var (b, c, _) = canvas.ActiveCell; canvas.UpdateColumnMeta(b, c, m => m.IsRest = false); canvas.Focus(); } };
        strumBox.SelectionChanged += (_, _) =>
        {
            if (syncing || strumBox.SelectedItem is not ComboBoxItem { Tag: TabStrumKind strum }) return;
            var (bIdx, cIdx, _) = canvas.ActiveCell;
            canvas.UpdateColumnMeta(bIdx, cIdx, m => m.Strum = strum);
            canvas.Focus();
        };
        palmMuteToggle.Checked += (_, _) => { if (!syncing) { var (b, c, _) = canvas.ActiveCell; canvas.UpdateColumnMeta(b, c, m => m.PalmMute = true); canvas.Focus(); } };
        palmMuteToggle.Unchecked += (_, _) => { if (!syncing) { var (b, c, _) = canvas.ActiveCell; canvas.UpdateColumnMeta(b, c, m => m.PalmMute = false); canvas.Focus(); } };
        linkBox.SelectionChanged += (_, _) =>
        {
            if (syncing || linkBox.SelectedItem is not ComboBoxItem { Tag: TabLinkKind link }) return;
            canvas.SetActiveNoteLink(link);
            canvas.Focus();
        };

        graceFretBox.TextChanged += (_, _) =>
        {
            if (syncing) return;
            var text = graceFretBox.Text.Trim();
            TabLinkKind? desiredLink = !string.IsNullOrEmpty(text) && graceLinkBox.SelectedItem is ComboBoxItem { Tag: TabLinkKind l }
                ? l
                : null;
            canvas.SetActiveNoteGrace(text, desiredLink);
            graceLinkBox.IsEnabled = canvas.GetActiveNote() is not null && !string.IsNullOrEmpty(text);
        };
        graceFretBox.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Escape)
            {
                canvas.Focus();
                e.Handled = true;
            }
        };
        graceLinkBox.SelectionChanged += (_, _) =>
        {
            if (syncing || graceLinkBox.SelectedItem is not ComboBoxItem { Tag: TabLinkKind link }) return;
            var activeNote = canvas.GetActiveNote();
            if (activeNote is null || string.IsNullOrWhiteSpace(activeNote.GraceFret)) return;
            canvas.SetActiveNoteGrace(activeNote.GraceFret, link);
            canvas.Focus();
        };
        techniqueBox.SelectionChanged += (_, _) =>
        {
            if (syncing || techniqueBox.SelectedItem is not ComboBoxItem { Tag: TabNoteTechnique tech }) return;
            canvas.SetActiveNoteTechnique(tech);
            canvas.Focus();
        };

        pattern44Btn.Click += (_, _) =>
        {
            var (bIdx, _, _) = canvas.ActiveCell;
            PushUndo();
            ApplyArpeggioPattern(working.Bars[bIdx], [5, 3, 2, 3, 1, 3, 2, 3]);
            canvas.InvalidateVisual();
            SyncCellHeader();
            canvas.Focus();
        };

        pattern34Btn.Click += (_, _) =>
        {
            var (bIdx, _, _) = canvas.ActiveCell;
            PushUndo();
            ApplyArpeggioPattern(working.Bars[bIdx], [5, 3, 2, 1, 2, 3]);
            canvas.InvalidateVisual();
            SyncCellHeader();
            canvas.Focus();
        };

        strumPatternBtn.Click += (_, _) =>
        {
            var (bIdx, _, _) = canvas.ActiveCell;
            PushUndo();
            ApplyStrumPattern(working.Bars[bIdx]);
            canvas.InvalidateVisual();
            SyncCellHeader();
            canvas.Focus();
        };

        importAsciiBtn.Click += (_, _) =>
        {
            var sample = "e|---0-------0-----|\nB|-----1-------1---|\nG|-------0-------0-|\nD|-----------------|\nA|-3---------------|\nE|-----------------|";
            var text = AppDialogs.PromptLyrics(
                window,
                "从文本六线谱导入",
                "粘贴 6 行吉他六线谱（从上到下依次为 1弦 e 到 6弦 E，用 | 分隔小节）：",
                sample,
                height: 300);
            if (string.IsNullOrWhiteSpace(text)) return;

            var parsedBars = ParseAsciiTab(text, working.ColumnsPerBar);
            if (parsedBars.Count == 0)
            {
                AppDialogs.ShowInfo(window, "未识别到文本六线谱", "请确认输入包含 6 行以 | 分隔的弦谱内容。");
                return;
            }
            PushUndo();
            working.Bars.Clear();
            working.Bars.AddRange(parsedBars);
            canvas.SelectCell(0, 0, 0);
            canvas.RebuildLayout();
            SyncCellHeader();
            canvas.Focus();
        };

        importTabBtn.Click += (_, _) =>
        {
            var dialog = new OpenFileDialog
            {
                Filter = "弦谱六线谱片段 (*.xianputab.json;*.json)|*.xianputab.json;*.json|所有文件 (*.*)|*.*",
                Title = "导入六线谱片段"
            };
            if (dialog.ShowDialog(window) != true) return;
            try
            {
                var imported = LibraryStore.LoadTabSection(dialog.FileName);
                PushUndo();
                working.TimeSignature = imported.TimeSignature;
                working.ColumnsPerBar = imported.ColumnsPerBar;
                working.ShowRhythmStems = imported.ShowRhythmStems;
                working.Bars.Clear();
                working.Bars.AddRange(imported.Bars);
                if (working.Bars.Count == 0)
                    working.Bars.Add(new TabBar { ColumnCount = working.ColumnsPerBar });

                syncing = true;
                try
                {
                    timeSigBox.Text = working.TimeSignature;
                    if (ColumnOptions.Contains(working.ColumnsPerBar))
                        colsBox.SelectedItem = working.ColumnsPerBar;
                    showRhythmCheck.IsChecked = working.ShowRhythmStems;
                }
                finally
                {
                    syncing = false;
                }

                canvas.SelectCell(0, 0, 0);
                canvas.RebuildLayout();
                SyncCellHeader();
                canvas.Focus();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
            {
                AppDialogs.ShowError(window, "导入片段失败", ex.Message);
            }
        };

        exportTabBtn.Click += (_, _) =>
        {
            var baseName = string.IsNullOrWhiteSpace(titleBox.Text) ? working.Title : titleBox.Text.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
                baseName = baseName.Replace(invalid, '_');
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "六线谱片段";

            var dialog = new SaveFileDialog
            {
                Filter = "弦谱六线谱片段 (*.xianputab.json)|*.xianputab.json|JSON 文件 (*.json)|*.json",
                FileName = baseName + ".xianputab.json",
                DefaultExt = ".json",
                AddExtension = false,
                Title = "导出六线谱片段"
            };
            if (dialog.ShowDialog(window) != true) return;
            var targetPath = dialog.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? dialog.FileName
                : dialog.FileName + ".xianputab.json";

            try
            {
                var snapshot = working.Clone();
                snapshot.Title = string.IsNullOrWhiteSpace(titleBox.Text) ? working.Title : titleBox.Text.Trim();
                snapshot.TimeSignature = string.IsNullOrWhiteSpace(timeSigBox.Text) ? "4/4" : timeSigBox.Text.Trim();
                LibraryStore.SaveTabSection(snapshot, targetPath);
                AppDialogs.ShowInfo(
                    window,
                    "导出完成",
                    $"六线谱片段已导出：\n{targetPath}\n\n发送给使用同款软件的好友，对方在六线谱编辑器中点「导入片段...」即可载入编辑。",
                    "打开所在文件夹",
                    () => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{targetPath}\"") { UseShellExecute = true }));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AppDialogs.ShowError(window, "导出失败", ex.Message);
            }
        };

        window.Loaded += (_, _) => canvas.Focus();

        var ok = window.ShowDialog() == true;
        if (deleted)
            return new TabEditorResult(Saved: false, Deleted: true, Section: null);
        if (!ok)
            return new TabEditorResult(Saved: false, Deleted: false, Section: null);

        LibraryStore.NormalizeSong(new SongModel { TabSections = [working] });
        return new TabEditorResult(Saved: true, Deleted: false, Section: working);
    }

    private static TabSection CreateDefaultSection(TabSectionKind kind, int insertAfterLine, int lineCount) => new()
    {
        Kind = kind,
        Title = kind switch
        {
            TabSectionKind.Intro => "前奏",
            TabSectionKind.Outro => "尾奏",
            _ => "间奏"
        },
        InsertAfterLineIndex = kind switch
        {
            TabSectionKind.Intro => -1,
            TabSectionKind.Outro => lineCount > 0 ? lineCount - 1 : -1,
            _ => lineCount > 0 ? Math.Clamp(insertAfterLine < 0 ? 0 : insertAfterLine, 0, lineCount - 1) : -1
        },
        TimeSignature = "4/4",
        ColumnsPerBar = 8,
        Bars =
        [
            new TabBar { ColumnCount = 8 },
            new TabBar { ColumnCount = 8 }
        ]
    };

    private static void PopulateInsertPositions(ComboBox box, IReadOnlyList<LyricLine> lines, int currentInsertAfter)
    {
        box.Items.Clear();
        box.DisplayMemberPath = "Label";
        box.SelectedValuePath = "Index";

        if (lines.Count == 0)
        {
            box.Items.Add(new InsertOption(-1, "暂无歌词（将置于谱面顶部）"));
            box.SelectedIndex = 0;
            return;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var text = lines[i].Text.Trim();
            var preview = text.Length > 14 ? text[..14] + "…" : text;
            box.Items.Add(new InsertOption(i, $"第 {i + 1} 句后 · {preview}"));
        }

        var clamped = Math.Clamp(currentInsertAfter < 0 ? 0 : currentInsertAfter, 0, lines.Count - 1);
        box.SelectedValue = clamped;
    }

    private static TextBlock MakeCaption(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = AppDialogs.MutedBrush,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 2, 6, 2)
    };

    private static Button MakeSmallBtn(Window? owner, string content)
    {
        var btn = new Button
        {
            Content = content,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 2, 5, 2),
            FontSize = 11.5,
            Cursor = Cursors.Hand
        };
        if (owner?.TryFindResource(typeof(Button)) is Style baseStyle)
            btn.Style = baseStyle;
        return btn;
    }

    private static void SelectComboByTag<T>(ComboBox box, T value)
    {
        for (var i = 0; i < box.Items.Count; i++)
        {
            if (box.Items[i] is ComboBoxItem { Tag: T tag } && EqualityComparer<T>.Default.Equals(tag, value))
            {
                box.SelectedIndex = i;
                return;
            }
        }
        if (box.Items.Count > 0)
            box.SelectedIndex = 0;
    }

    private static void ApplyCopiedBarToTarget(TabBar source, TabBar target)
    {
        var srcCols = Math.Max(1, source.ColumnCount);
        var tgtCols = Math.Max(1, target.ColumnCount);
        int MapCol(int c) => srcCols == tgtCols
            ? c
            : Math.Clamp((int)Math.Round(c * (double)tgtCols / srcCols), 0, tgtCols - 1);

        target.RepeatStart = source.RepeatStart;
        target.RepeatEnd = source.RepeatEnd;

        target.Notes = source.Notes
            .Select(n => new TabNote
            {
                Column = MapCol(n.Column),
                StringIndex = n.StringIndex,
                Fret = n.Fret,
                LinkToNext = n.LinkToNext,
                GraceFret = n.GraceFret,
                GraceLink = n.GraceLink,
                Technique = n.Technique
            })
            .GroupBy(n => (n.Column, n.StringIndex))
            .Select(g => g.Last())
            .ToList();

        target.ChordMarks = source.ChordMarks
            .Select(m => new TabChordMark { Column = MapCol(m.Column), Text = m.Text })
            .GroupBy(m => m.Column)
            .Select(g => g.Last())
            .ToList();

        target.ColumnMetas = source.ColumnMetas
            .Select(m => new TabColumnMeta
            {
                Column = MapCol(m.Column),
                Duration = m.Duration,
                Dotted = m.Dotted,
                IsRest = m.IsRest,
                Strum = m.Strum,
                PalmMute = m.PalmMute
            })
            .GroupBy(m => m.Column)
            .Select(g => g.Last())
            .ToList();
    }

    private static void ApplyStrumPattern(TabBar bar)
    {
        bar.Notes.Clear();
        bar.ColumnMetas.Clear();
        var colCount = Math.Max(4, bar.ColumnCount);
        (int Step8, TabStrumKind Strum)[] template =
        [
            (0, TabStrumKind.Down),
            (2, TabStrumKind.Down),
            (3, TabStrumKind.Up),
            (5, TabStrumKind.Up),
            (6, TabStrumKind.Down),
            (7, TabStrumKind.Up)
        ];
        foreach (var (step8, strum) in template)
        {
            var col = Math.Clamp((int)Math.Floor(step8 * (colCount / 8.0)), 0, colCount - 1);
            bar.ColumnMetas.RemoveAll(m => m.Column == col);
            bar.ColumnMetas.Add(new TabColumnMeta { Column = col, Strum = strum });
        }
    }

    private static void ApplyArpeggioPattern(TabBar bar, int[] stringOrder1To6)
    {
        bar.Notes.Clear();
        var count = Math.Min(bar.ColumnCount, stringOrder1To6.Length);
        for (var c = 0; c < count; c++)
        {
            var stringIndex = Math.Clamp(stringOrder1To6[c] - 1, 0, 5);
            bar.Notes.Add(new TabNote
            {
                Column = c,
                StringIndex = stringIndex,
                Fret = "0"
            });
        }
    }

    private static List<TabBar> ParseAsciiTab(string raw, int defaultColumnsPerBar)
    {
        var lines = raw.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Contains('|') || l.Contains('-'))
            .ToList();

        if (lines.Count < 6) return [];

        // Take the first 6 staff lines
        var staffLines = lines.Take(6).Select(StripPrefix).ToArray();
        var barSegmentsPerString = staffLines
            .Select(l => l.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(s => s.TrimEnd()).ToArray())
            .ToArray();

        var barCount = barSegmentsPerString.Min(s => s.Length);
        if (barCount <= 0) return [];

        var bars = new List<TabBar>(barCount);
        for (var b = 0; b < barCount; b++)
        {
            var segs = new string[6];
            var maxLen = 0;
            for (var s = 0; s < 6; s++)
            {
                segs[s] = barSegmentsPerString[s][b];
                maxLen = Math.Max(maxLen, segs[s].Length);
            }
            if (maxLen == 0) continue;

            var colCount = Math.Clamp(defaultColumnsPerBar, 4, 16);
            var bar = new TabBar { ColumnCount = colCount };

            for (var s = 0; s < 6; s++)
            {
                foreach (Match match in Regex.Matches(segs[s], @"(\(\d{1,2}\)|<\d{1,2}>|/\d{1,2}|\d{1,2}[hp/\\~b]?\d{0,2}|[xX])"))
                {
                    var ratio = (match.Index + match.Length * 0.5) / Math.Max(1, maxLen);
                    var col = Math.Clamp((int)Math.Floor(ratio * colCount), 0, colCount - 1);
                    bar.Notes.RemoveAll(n => n.Column == col && n.StringIndex == s);

                    var rawTok = match.Value;
                    var tech = TabNoteTechnique.None;
                    var fret = rawTok.ToLowerInvariant();
                    if (rawTok.StartsWith('(') && rawTok.EndsWith(')') && rawTok.Length >= 3)
                    {
                        fret = rawTok[1..^1];
                        tech = TabNoteTechnique.GhostNote;
                    }
                    else if (rawTok.StartsWith('<') && rawTok.EndsWith('>') && rawTok.Length >= 3)
                    {
                        fret = rawTok[1..^1];
                        tech = TabNoteTechnique.NaturalHarmonic;
                    }
                    else if (rawTok.StartsWith('/') && rawTok.Length >= 2)
                    {
                        fret = rawTok[1..];
                        tech = TabNoteTechnique.SlideIn;
                    }
                    else if (rawTok.EndsWith('\\') && rawTok.Length >= 2 && char.IsDigit(rawTok[0]))
                    {
                        fret = rawTok[..^1];
                        tech = TabNoteTechnique.SlideOut;
                    }
                    else if ((rawTok.EndsWith('b') || rawTok.EndsWith('B')) && rawTok.Length >= 2 && char.IsDigit(rawTok[0]))
                    {
                        fret = rawTok[..^1];
                        tech = TabNoteTechnique.BendFull;
                    }
                    else if (rawTok.EndsWith('~') && rawTok.Length >= 2 && char.IsDigit(rawTok[0]))
                    {
                        fret = rawTok[..^1];
                        tech = TabNoteTechnique.Vibrato;
                    }

                    bar.Notes.Add(new TabNote
                    {
                        Column = col,
                        StringIndex = s,
                        Fret = fret,
                        Technique = tech
                    });
                }
            }
            bars.Add(bar);
        }
        return bars;
    }

    private static string StripPrefix(string line)
    {
        var pipeIdx = line.IndexOf('|');
        return pipeIdx >= 0 && pipeIdx <= 4 ? line[(pipeIdx + 1)..] : line;
    }

    private sealed record BeatSlot(List<TabNote> Notes, List<TabChordMark> Chords, TabColumnMeta? Meta);

    private static List<BeatSlot> FlattenSlots(TabSection working, int barIdx, int colIdx, out int flatIndex)
    {
        var slots = new List<BeatSlot>();
        flatIndex = 0;
        for (var b = 0; b < working.Bars.Count; b++)
        {
            var bar = working.Bars[b];
            var colCount = Math.Max(1, bar.ColumnCount);
            if (b < barIdx)
                flatIndex += colCount;
            else if (b == barIdx)
                flatIndex += Math.Clamp(colIdx, 0, colCount - 1);

            for (var c = 0; c < colCount; c++)
            {
                var notes = bar.Notes.Where(n => n.Column == c).ToList();
                var chords = bar.ChordMarks.Where(m => m.Column == c).ToList();
                var meta = bar.ColumnMetas.FirstOrDefault(m => m.Column == c);
                slots.Add(new BeatSlot(notes, chords, meta));
            }
        }
        return slots;
    }

    private static void WriteSlotsToBars(TabSection working, List<BeatSlot> slots)
    {
        var cursor = 0;
        foreach (var bar in working.Bars)
        {
            var colCount = Math.Max(1, bar.ColumnCount);
            bar.Notes.Clear();
            bar.ChordMarks.Clear();
            bar.ColumnMetas.Clear();
            for (var c = 0; c < colCount && cursor < slots.Count; c++)
            {
                var slot = slots[cursor++];
                foreach (var n in slot.Notes)
                {
                    n.Column = c;
                    bar.Notes.Add(n);
                }
                foreach (var cm in slot.Chords)
                {
                    cm.Column = c;
                    bar.ChordMarks.Add(cm);
                }
                if (slot.Meta is { } meta)
                {
                    meta.Column = c;
                    bar.ColumnMetas.Add(meta);
                }
            }
        }
    }

    private static void RippleInsertSlot(TabSection working, int barIdx, int colIdx)
    {
        var slots = FlattenSlots(working, barIdx, colIdx, out var flatIdx);
        if (slots.Count == 0) return;

        var last = slots[^1];
        if (last.Notes.Count > 0 || last.Chords.Count > 0 || last.Meta is not null)
        {
            var newBar = new TabBar { ColumnCount = Math.Max(1, working.ColumnsPerBar) };
            working.Bars.Add(newBar);
            for (var k = 0; k < newBar.ColumnCount; k++)
                slots.Add(new BeatSlot([], [], null));
        }

        slots.Insert(flatIdx, new BeatSlot([], [], null));
        slots.RemoveAt(slots.Count - 1);
        WriteSlotsToBars(working, slots);
    }

    private static void RippleDeleteSlot(TabSection working, int barIdx, int colIdx)
    {
        var slots = FlattenSlots(working, barIdx, colIdx, out var flatIdx);
        if (slots.Count == 0) return;

        slots.RemoveAt(flatIdx);
        slots.Add(new BeatSlot([], [], null));
        WriteSlotsToBars(working, slots);
    }

    private sealed record InsertOption(int Index, string Label);

    private sealed class TabEditorCanvas : FrameworkElement
    {
        private const double TuningWidth = 42;
        private const double ChordAreaHeight = 38;
        private const double StringSpacing = 20;
        private const double StaffHeight = StringSpacing * 5; // 100
        private const double RowGap = 44;
        private const double BarWidth = 368;
        private const int BarsPerRow = 2;

        private static readonly string[] ShortTunings = ["e", "B", "G", "D", "A", "E"];
        private static readonly Typeface FretFont = new(new FontFamily("Consolas, Microsoft YaHei"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface LabelFont = new(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        private static readonly Typeface SmallFont = new(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Brush InkBrush = Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)));
        private static readonly Brush GreenBrush = Freeze(new SolidColorBrush(Color.FromRgb(47, 107, 72)));
        private static readonly Brush MutedBrush = Freeze(new SolidColorBrush(Color.FromRgb(100, 116, 139)));
        private static readonly Brush ActiveCellFill = Freeze(new SolidColorBrush(Color.FromArgb(46, 47, 107, 72)));
        private static readonly Brush ActiveCellBorder = Freeze(new SolidColorBrush(Color.FromRgb(47, 107, 72)));
        private static readonly Brush HoverCellFill = Freeze(new SolidColorBrush(Color.FromArgb(24, 100, 116, 139)));
        private static readonly Pen LinePen = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(203, 213, 225))), 1.1));
        private static readonly Pen BarPen = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(100, 116, 139))), 1.4));
        private static readonly Pen CursorPen = FreezePen(new Pen(ActiveCellBorder, 1.6));

        private readonly TabSection _section;
        private (int Bar, int Column, int StringIndex) _cursor = (0, 0, 0);
        private (int Bar, int Column, int StringIndex)? _hover;
        private DateTime _lastDigitTime = DateTime.MinValue;
        private double _pixelsPerDip = 1;

        public (int Bar, int Column, int StringIndex) ActiveCell => _cursor;
        public event Action? SelectionChanged;
        public event Action? BeforeEdit;
        public event Action? CopyBarRequested;
        public event Action? PasteBarRequested;
        public event Action? DuplicateBarRequested;
        public event Action? InsertSlotRequested;
        public event Action? DeleteSlotRequested;
        public event Action? UndoRequested;

        public TabEditorCanvas(TabSection section)
        {
            _section = section;
            Focusable = true;
            SnapsToDevicePixels = true;
            Loaded += (_, _) =>
            {
                _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                RebuildLayout();
            };
        }

        private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
        private static Pen FreezePen(Pen p) { p.Freeze(); return p; }

        public void RebuildLayout()
        {
            var barCount = Math.Max(1, _section.Bars.Count);
            var rows = (int)Math.Ceiling(barCount / (double)BarsPerRow);
            var rowCols = Math.Min(BarsPerRow, barCount);
            Width = TuningWidth + rowCols * BarWidth + 24;
            Height = 20 + rows * (ChordAreaHeight + StaffHeight + RowGap);
            InvalidateVisual();
        }

        public void ClampCursor()
        {
            var barIdx = Math.Clamp(_cursor.Bar, 0, Math.Max(0, _section.Bars.Count - 1));
            var colCount = _section.Bars.Count > 0 ? Math.Max(1, _section.Bars[barIdx].ColumnCount) : 8;
            var colIdx = Math.Clamp(_cursor.Column, 0, colCount - 1);
            var strIdx = Math.Clamp(_cursor.StringIndex, 0, 5);
            _cursor = (barIdx, colIdx, strIdx);
        }

        public void SelectCell(int bar, int col, int str)
        {
            _cursor = (bar, col, str);
            ClampCursor();
            InvalidateVisual();
            SelectionChanged?.Invoke();
        }

        public string? GetChordMark(int barIndex, int colIndex)
        {
            if (barIndex < 0 || barIndex >= _section.Bars.Count) return null;
            return _section.Bars[barIndex].ChordMarks.FirstOrDefault(m => m.Column == colIndex)?.Text;
        }

        public void SetChordMark(int barIndex, int colIndex, string text)
        {
            if (barIndex < 0 || barIndex >= _section.Bars.Count) return;
            BeforeEdit?.Invoke();
            var bar = _section.Bars[barIndex];
            bar.ChordMarks.RemoveAll(m => m.Column == colIndex);
            if (!string.IsNullOrWhiteSpace(text))
                bar.ChordMarks.Add(new TabChordMark { Column = colIndex, Text = text.Trim() });
            InvalidateVisual();
        }

        public TabColumnMeta? GetColumnMeta(int barIndex, int colIndex)
        {
            if (barIndex < 0 || barIndex >= _section.Bars.Count) return null;
            return _section.Bars[barIndex].ColumnMetas.FirstOrDefault(m => m.Column == colIndex);
        }

        public void UpdateColumnMeta(int barIndex, int colIndex, Action<TabColumnMeta> mutate)
        {
            if (barIndex < 0 || barIndex >= _section.Bars.Count) return;
            var bar = _section.Bars[barIndex];
            if (colIndex < 0 || colIndex >= bar.ColumnCount) return;
            BeforeEdit?.Invoke();
            var meta = bar.ColumnMetas.FirstOrDefault(m => m.Column == colIndex);
            if (meta is null)
            {
                meta = new TabColumnMeta { Column = colIndex };
                bar.ColumnMetas.Add(meta);
            }
            mutate(meta);
            if (meta.Duration == TabDuration.Auto && !meta.Dotted && !meta.IsRest && meta.Strum == TabStrumKind.None && !meta.PalmMute)
                bar.ColumnMetas.Remove(meta);
            InvalidateVisual();
        }

        public TabNote? GetActiveNote()
        {
            var (b, c, s) = _cursor;
            if (b < 0 || b >= _section.Bars.Count) return null;
            return _section.Bars[b].Notes.FirstOrDefault(n => n.Column == c && n.StringIndex == s && !string.IsNullOrWhiteSpace(n.Fret));
        }

        public void SetActiveNoteLink(TabLinkKind link)
        {
            var note = GetActiveNote();
            if (note is null || note.LinkToNext == link) return;
            BeforeEdit?.Invoke();
            note.LinkToNext = link;
            InvalidateVisual();
        }

        public void SetActiveNoteGrace(string graceFret, TabLinkKind? graceLink = null)
        {
            var note = GetActiveNote();
            if (note is null) return;
            BeforeEdit?.Invoke();
            var cleaned = (graceFret ?? "").Trim();
            note.GraceFret = cleaned;
            if (string.IsNullOrEmpty(cleaned))
                note.GraceLink = TabLinkKind.None;
            else if (graceLink is { } link)
                note.GraceLink = link;
            else if (note.GraceLink == TabLinkKind.None)
                note.GraceLink = TabLinkKind.Slide;
            InvalidateVisual();
        }

        public void SetActiveNoteTechnique(TabNoteTechnique technique)
        {
            var note = GetActiveNote();
            if (note is null || note.Technique == technique) return;
            BeforeEdit?.Invoke();
            note.Technique = technique;
            InvalidateVisual();
        }

        public void ToggleColumnPalmMute()
        {
            var (b, c, _) = _cursor;
            var current = GetColumnMeta(b, c)?.PalmMute == true;
            UpdateColumnMeta(b, c, m => m.PalmMute = !current);
        }

        public void ClearCurrentCell()
        {
            var (b, c, s) = _cursor;
            if (b < 0 || b >= _section.Bars.Count) return;
            if (!_section.Bars[b].Notes.Any(n => n.Column == c && n.StringIndex == s)) return;
            BeforeEdit?.Invoke();
            _section.Bars[b].Notes.RemoveAll(n => n.Column == c && n.StringIndex == s);
            InvalidateVisual();
            SelectionChanged?.Invoke();
        }

        public void ApplyToken(string token)
        {
            var (b, c, s) = _cursor;
            if (b < 0 || b >= _section.Bars.Count) return;
            var bar = _section.Bars[b];
            var existing = bar.Notes.FirstOrDefault(n => n.Column == c && n.StringIndex == s);
            var hasExistingFret = existing is not null && !string.IsNullOrWhiteSpace(existing.Fret) && existing.Fret != "/";

            if (token is "b" or "~" or "v" or "NH" or "n" or "g" or "\\")
            {
                if (!hasExistingFret || existing is null) return;
                BeforeEdit?.Invoke();
                existing.Technique = token switch
                {
                    "b" => existing.Technique switch
                    {
                        TabNoteTechnique.None => TabNoteTechnique.BendFull,
                        TabNoteTechnique.BendFull => TabNoteTechnique.BendHalf,
                        TabNoteTechnique.BendHalf => TabNoteTechnique.BendRelease,
                        TabNoteTechnique.BendRelease => TabNoteTechnique.BendHalfRelease,
                        _ => TabNoteTechnique.None
                    },
                    "~" or "v" => existing.Technique == TabNoteTechnique.Vibrato ? TabNoteTechnique.None : TabNoteTechnique.Vibrato,
                    "NH" or "n" => existing.Technique switch
                    {
                        TabNoteTechnique.NaturalHarmonic => TabNoteTechnique.ArtificialHarmonic,
                        TabNoteTechnique.ArtificialHarmonic => TabNoteTechnique.None,
                        _ => TabNoteTechnique.NaturalHarmonic
                    },
                    "g" => existing.Technique == TabNoteTechnique.GhostNote ? TabNoteTechnique.None : TabNoteTechnique.GhostNote,
                    _ => existing.Technique == TabNoteTechnique.SlideOut ? TabNoteTechnique.None : TabNoteTechnique.SlideOut
                };
                InvalidateVisual();
                SelectionChanged?.Invoke();
                return;
            }

            if (token == "/")
            {
                if (!hasExistingFret)
                {
                    if (existing is null)
                    {
                        BeforeEdit?.Invoke();
                        bar.Notes.Add(new TabNote { Column = c, StringIndex = s, Fret = "/" });
                        InvalidateVisual();
                        SelectionChanged?.Invoke();
                    }
                    return;
                }
                if (!existing!.Fret.EndsWith("/", StringComparison.Ordinal))
                {
                    BeforeEdit?.Invoke();
                    existing.Fret += "/";
                    InvalidateVisual();
                    SelectionChanged?.Invoke();
                }
                return;
            }

            if (token is "s" or "h" or "p" && existing is { Fret: "/" })
                return;

            BeforeEdit?.Invoke();
            string next;
            if (token is "s" or "h" or "p")
            {
                var baseVal = existing?.Fret ?? "0";
                next = baseVal.EndsWith(token, StringComparison.Ordinal) ? baseVal : baseVal + token;
            }
            else if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
                     existing is { Fret: "/" })
            {
                existing.Technique = TabNoteTechnique.SlideIn;
                next = token;
            }
            else if (token.Length == 1 && char.IsDigit(token[0]) &&
                     existing is not null &&
                     existing.Fret.Length >= 2 &&
                     existing.Fret[^1] is 's' or '/' or '\\' or 'h' or 'p')
            {
                var op = existing.Fret[^1];
                var prefix = existing.Fret[..^1].Trim();
                if (!string.IsNullOrEmpty(prefix))
                {
                    existing.GraceFret = prefix;
                    existing.GraceLink = op switch
                    {
                        'h' => TabLinkKind.HammerOn,
                        'p' => TabLinkKind.PullOff,
                        _ => TabLinkKind.Slide
                    };
                    next = token;
                }
                else
                {
                    next = token;
                }
            }
            else if (token.Length == 1 && char.IsDigit(token[0]) &&
                     existing is not null &&
                     existing.Fret.Length > 0 &&
                     (DateTime.UtcNow - _lastDigitTime).TotalMilliseconds < 900)
            {
                if (int.TryParse(existing.Fret, out var prevNum) && prevNum is >= 1 and <= 2)
                {
                    var combined = prevNum * 10 + (token[0] - '0');
                    next = combined <= 24 ? combined.ToString(CultureInfo.InvariantCulture) : token;
                }
                else
                {
                    next = token;
                }
            }
            else
            {
                next = token;
            }

            _lastDigitTime = token.Length == 1 && char.IsDigit(token[0]) ? DateTime.UtcNow : DateTime.MinValue;

            if (existing is not null)
                existing.Fret = next;
            else
                bar.Notes.Add(new TabNote { Column = c, StringIndex = s, Fret = next });

            InvalidateVisual();
            SelectionChanged?.Invoke();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, Math.Max(ActualWidth, Width), Math.Max(ActualHeight, Height)));

            var bars = _section.Bars;
            var totalRows = (int)Math.Ceiling(Math.Max(1, bars.Count) / (double)BarsPerRow);

            for (var r = 0; r < totalRows; r++)
            {
                var rowStart = r * BarsPerRow;
                var countInRow = Math.Min(BarsPerRow, bars.Count - rowStart);
                var rowTop = 16 + r * (ChordAreaHeight + StaffHeight + RowGap);
                var staffTop = rowTop + ChordAreaHeight;
                var staffBottom = staffTop + StaffHeight;
                var rowEndX = TuningWidth + countInRow * BarWidth;

                // String tuning labels + horizontal lines
                for (var s = 0; s < 6; s++)
                {
                    var sy = staffTop + s * StringSpacing;
                    var label = CreateText(ShortTunings[s], 11.5, LabelFont, MutedBrush, _pixelsPerDip);
                    dc.DrawText(label, new Point(14, sy - label.Height / 2));
                    dc.DrawLine(LinePen, new Point(TuningWidth - 6, sy), new Point(rowEndX, sy));
                }

                dc.DrawLine(BarPen, new Point(TuningWidth, staffTop), new Point(TuningWidth, staffBottom));

                for (var i = 0; i < countInRow; i++)
                {
                    var bIdx = rowStart + i;
                    var bar = bars[bIdx];
                    var barLeft = TuningWidth + i * BarWidth;
                    var barRight = barLeft + BarWidth;
                    var colCount = Math.Max(1, bar.ColumnCount);
                    var colWidth = BarWidth / colCount;

                    // Bar number
                    var barTag = CreateText($"小节 {bIdx + 1}", 10, SmallFont, MutedBrush, _pixelsPerDip);
                    dc.DrawText(barTag, new Point(barLeft + 2, rowTop - 12));

                    // Subtle vertical column guides when rhythm stems are hidden
                    if (!_section.ShowRhythmStems)
                    {
                        for (var c = 0; c < colCount; c++)
                        {
                            var cx = barLeft + (c + 0.5) * colWidth;
                            dc.DrawLine(LinePen, new Point(cx, staffBottom + 3), new Point(cx, staffBottom + 7));
                        }
                    }

                    // Hover cell highlight
                    if (_hover is { } hv && hv.Bar == bIdx && hv.Column < colCount)
                    {
                        var rect = GetCellRect(barLeft, staffTop, colWidth, hv.Column, hv.StringIndex);
                        dc.DrawRoundedRectangle(HoverCellFill, null, rect, 4, 4);
                    }

                    // Active cursor highlight
                    if (_cursor.Bar == bIdx && _cursor.Column < colCount)
                    {
                        var rect = GetCellRect(barLeft, staffTop, colWidth, _cursor.Column, _cursor.StringIndex);
                        dc.DrawRoundedRectangle(ActiveCellFill, CursorPen, rect, 4, 4);
                    }

                    // Chord marks above bar
                    foreach (var mark in bar.ChordMarks)
                    {
                        if (mark.Column < 0 || mark.Column >= colCount || string.IsNullOrWhiteSpace(mark.Text))
                            continue;
                        var cx = barLeft + (mark.Column + 0.5) * colWidth;
                        var ft = CreateText(mark.Text, 12, LabelFont, GreenBrush, _pixelsPerDip);
                        dc.DrawText(ft, new Point(cx - ft.Width / 2, rowTop + 2));
                    }

                    // Strum arrows and arpeggio waves behind fret masks
                    TabPainter.DrawBarPreNotes(dc, bar, barLeft, barRight, staffTop, StringSpacing, scale: 1.45);

                    // Fret notes (and grace notes)
                    foreach (var note in bar.Notes)
                    {
                        if (note.StringIndex is < 0 or > 5 || note.Column < 0 || note.Column >= colCount || string.IsNullOrWhiteSpace(note.Fret))
                            continue;
                        var cx = barLeft + (note.Column + 0.5) * colWidth;
                        var sy = staffTop + note.StringIndex * StringSpacing;
                        TabPainter.DrawNoteCell(dc, note, cx, sy, 12.5, FretFont, InkBrush, Brushes.White, _pixelsPerDip, scale: 1.35);
                    }

                    // Repeat barlines, links (H/P/S/Tie), and rhythm stems/beams/rests
                    TabPainter.DrawBarPostNotes(
                        dc,
                        bar,
                        barLeft,
                        barRight,
                        staffTop,
                        staffBottom,
                        StringSpacing,
                        _section.ShowRhythmStems,
                        Brushes.White,
                        _pixelsPerDip,
                        scale: 1.45);

                    if (!bar.RepeatEnd)
                        dc.DrawLine(BarPen, new Point(barRight, staffTop), new Point(barRight, staffBottom));
                }
            }
        }

        private static Rect GetCellRect(double barLeft, double staffTop, double colWidth, int col, int str)
        {
            var cx = barLeft + (col + 0.5) * colWidth;
            var sy = staffTop + str * StringSpacing;
            var w = Math.Min(32, colWidth - 4);
            return new Rect(cx - w / 2, sy - 9, w, 18);
        }

        private (int Bar, int Column, int StringIndex)? HitTestCell(Point pt)
        {
            var bars = _section.Bars;
            var totalRows = (int)Math.Ceiling(Math.Max(1, bars.Count) / (double)BarsPerRow);
            for (var r = 0; r < totalRows; r++)
            {
                var rowStart = r * BarsPerRow;
                var countInRow = Math.Min(BarsPerRow, bars.Count - rowStart);
                var rowTop = 16 + r * (ChordAreaHeight + StaffHeight + RowGap);
                var staffTop = rowTop + ChordAreaHeight;
                var staffBottom = staffTop + StaffHeight;

                if (pt.Y < rowTop || pt.Y > staffBottom + 12)
                    continue;

                for (var i = 0; i < countInRow; i++)
                {
                    var bIdx = rowStart + i;
                    var barLeft = TuningWidth + i * BarWidth;
                    var barRight = barLeft + BarWidth;
                    if (pt.X < barLeft || pt.X > barRight)
                        continue;

                    var colCount = Math.Max(1, bars[bIdx].ColumnCount);
                    var colWidth = BarWidth / colCount;
                    var col = Math.Clamp((int)Math.Floor((pt.X - barLeft) / colWidth), 0, colCount - 1);
                    var str = Math.Clamp((int)Math.Round((pt.Y - staffTop) / StringSpacing), 0, 5);
                    return (bIdx, col, str);
                }
            }
            return null;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            if (HitTestCell(e.GetPosition(this)) is { } hit)
            {
                _lastDigitTime = DateTime.MinValue;
                SelectCell(hit.Bar, hit.Column, hit.StringIndex);
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var hit = HitTestCell(e.GetPosition(this));
            Cursor = hit is not null ? Cursors.Hand : Cursors.Arrow;
            if (hit != _hover)
            {
                _hover = hit;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            Cursor = Cursors.Arrow;
            if (_hover is not null)
            {
                _hover = null;
                InvalidateVisual();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_section.Bars.Count == 0) return;

            if (e.Key == Key.Up)
            {
                SelectCell(_cursor.Bar, _cursor.Column, Math.Max(0, _cursor.StringIndex - 1));
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Down)
            {
                SelectCell(_cursor.Bar, _cursor.Column, Math.Min(5, _cursor.StringIndex + 1));
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Left)
            {
                MoveHorizontal(-1);
                e.Handled = true;
                return;
            }
            if (e.Key is Key.Right or Key.Tab or Key.Space)
            {
                var step = (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) ? -1 : 1;
                MoveHorizontal(step);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Insert && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == ModifierKeys.None)
            {
                InsertSlotRequested?.Invoke();
                e.Handled = true;
                return;
            }
            if ((e.Key == Key.Delete && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) ||
                (e.Key == Key.Back && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control))
            {
                DeleteSlotRequested?.Invoke();
                e.Handled = true;
                return;
            }
            if (e.Key is Key.Delete or Key.Back)
            {
                ClearCurrentCell();
                e.Handled = true;
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.None)
            {
                if (e.Key == Key.Z) { UndoRequested?.Invoke(); e.Handled = true; return; }
                if (e.Key == Key.C) { CopyBarRequested?.Invoke(); e.Handled = true; return; }
                if (e.Key == Key.V) { PasteBarRequested?.Invoke(); e.Handled = true; return; }
                if (e.Key == Key.D) { DuplicateBarRequested?.Invoke(); e.Handled = true; return; }
            }

            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != ModifierKeys.None)
                return;

            if (e.Key is >= Key.D0 and <= Key.D9)
            {
                ApplyToken(((char)('0' + (e.Key - Key.D0))).ToString());
                e.Handled = true;
                return;
            }
            if (e.Key is >= Key.NumPad0 and <= Key.NumPad9)
            {
                ApplyToken(((char)('0' + (e.Key - Key.NumPad0))).ToString());
                e.Handled = true;
                return;
            }
            if (e.Key == Key.X) { ApplyToken("x"); e.Handled = true; return; }
            if (e.Key == Key.S) { ApplyToken("s"); e.Handled = true; return; }
            if (e.Key == Key.H) { ApplyToken("h"); e.Handled = true; return; }
            if (e.Key == Key.P) { ApplyToken("p"); e.Handled = true; return; }
            if (e.Key == Key.B) { ApplyToken("b"); e.Handled = true; return; }
            if (e.Key == Key.N) { ApplyToken("n"); e.Handled = true; return; }
            if (e.Key == Key.V) { ApplyToken("~"); e.Handled = true; return; }
            if (e.Key == Key.G) { ApplyToken("g"); e.Handled = true; return; }
            if (e.Key == Key.M) { ToggleColumnPalmMute(); SelectionChanged?.Invoke(); e.Handled = true; return; }
            if (e.Key == Key.OemQuestion) { ApplyToken("/"); e.Handled = true; return; }
            if (e.Key == Key.OemPipe) { ApplyToken("\\"); e.Handled = true; return; }
            if (e.Key == Key.Oem3) { ApplyToken("~"); e.Handled = true; return; }
        }

        private void MoveHorizontal(int delta)
        {
            _lastDigitTime = DateTime.MinValue;
            var b = _cursor.Bar;
            var c = _cursor.Column + delta;
            if (c < 0)
            {
                if (b > 0)
                {
                    b--;
                    c = Math.Max(0, _section.Bars[b].ColumnCount - 1);
                }
                else
                {
                    c = 0;
                }
            }
            else if (c >= _section.Bars[b].ColumnCount)
            {
                if (b < _section.Bars.Count - 1)
                {
                    b++;
                    c = 0;
                }
                else
                {
                    c = _section.Bars[b].ColumnCount - 1;
                }
            }
            SelectCell(b, c, _cursor.StringIndex);
        }

        private static FormattedText CreateText(string text, double size, Typeface typeface, Brush brush, double pixelsPerDip) =>
            new(text ?? string.Empty, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, typeface, size, brush, pixelsPerDip);
    }
}
