using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
        var winWidth = Math.Max(640, Math.Min(920, workArea.Width * 0.90));
        var winHeight = Math.Max(460, Math.Min(650, workArea.Height * 0.88));

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
        var pattern44Btn = MakeSmallBtn(owner, "模板 53231323");
        var pattern34Btn = MakeSmallBtn(owner, "模板 532123");
        var importAsciiBtn = MakeSmallBtn(owner, "导入文本谱...");

        pattern44Btn.ToolTip = "向当前小节填入常用 4/4 分解拨弦示意（可在各格修改品数）";
        pattern34Btn.ToolTip = "向当前小节填入常用 3/4 分解拨弦示意";
        importAsciiBtn.ToolTip = "粘贴标准 6 行 ASCII 文本六线谱（如 e|---0---|），自动解析为小节";

        barToolbar.Children.Add(addBarBtn);
        barToolbar.Children.Add(removeBarBtn);
        barToolbar.Children.Add(clearBarBtn);
        barToolbar.Children.Add(new Border { Width = 1, Height = 18, Background = AppDialogs.SubtleBorderBrush, Margin = new Thickness(4, 0, 8, 0) });
        barToolbar.Children.Add(pattern44Btn);
        barToolbar.Children.Add(pattern34Btn);
        barToolbar.Children.Add(importAsciiBtn);

        Grid.SetRow(barToolbar, 1);
        root.Children.Add(barToolbar);

        // ─── Row 2: Active Cell Info + Chord Mark + Quick Keypad ──────────────────────
        var cellBar = new Border
        {
            Background = AppDialogs.SubtleSurfaceBrush,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var cellWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };

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

        var keypadTokens = new[] { "0", "1", "2", "3", "4", "5", "7", "9", "12", "x", "h", "p", "/", "~" };
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

        cellBar.Child = cellWrap;
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
            Text = "操作提示：点击任意弦位，直接按键盘 0-9 / x 输入品数，方向键 ↑↓←→ 移动，Delete 擦除，Tab 下一格。",
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

        void SyncCellHeader()
        {
            syncing = true;
            try
            {
                var (bIdx, cIdx, sIdx) = canvas.ActiveCell;
                activeCellLabel.Text = $"第 {bIdx + 1} 小节 · 第 {cIdx + 1} 格 · {StringNames[sIdx]}";
                chordMarkBox.Text = canvas.GetChordMark(bIdx, cIdx) ?? "";
                removeBarBtn.IsEnabled = working.Bars.Count > 1;
            }
            finally
            {
                syncing = false;
            }
        }

        SyncInsertVisibility();
        SyncCellHeader();

        canvas.SelectionChanged += SyncCellHeader;

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
            if (colsBox.SelectedItem is not int count) return;
            working.ColumnsPerBar = count;
            foreach (var bar in working.Bars)
            {
                bar.ColumnCount = count;
                bar.Notes.RemoveAll(n => n.Column >= count);
                bar.ChordMarks.RemoveAll(m => m.Column >= count);
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
                working.Bars[bIdx].Notes.Clear();
                working.Bars[bIdx].ChordMarks.Clear();
                canvas.InvalidateVisual();
                SyncCellHeader();
            }
            canvas.Focus();
        };

        pattern44Btn.Click += (_, _) =>
        {
            var (bIdx, _, _) = canvas.ActiveCell;
            ApplyArpeggioPattern(working.Bars[bIdx], [5, 3, 2, 3, 1, 3, 2, 3]);
            canvas.InvalidateVisual();
            canvas.Focus();
        };

        pattern34Btn.Click += (_, _) =>
        {
            var (bIdx, _, _) = canvas.ActiveCell;
            ApplyArpeggioPattern(working.Bars[bIdx], [5, 3, 2, 1, 2, 3]);
            canvas.InvalidateVisual();
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
            working.Bars.Clear();
            working.Bars.AddRange(parsedBars);
            canvas.SelectCell(0, 0, 0);
            canvas.RebuildLayout();
            SyncCellHeader();
            canvas.Focus();
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
                foreach (Match match in Regex.Matches(segs[s], @"(\d{1,2}[hp/\\~b]?\d{0,2}|[xX])"))
                {
                    var ratio = (match.Index + match.Length * 0.5) / Math.Max(1, maxLen);
                    var col = Math.Clamp((int)Math.Floor(ratio * colCount), 0, colCount - 1);
                    bar.Notes.RemoveAll(n => n.Column == col && n.StringIndex == s);
                    bar.Notes.Add(new TabNote
                    {
                        Column = col,
                        StringIndex = s,
                        Fret = match.Value.ToLowerInvariant()
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

    private sealed record InsertOption(int Index, string Label);

    private sealed class TabEditorCanvas : FrameworkElement
    {
        private const double TuningWidth = 42;
        private const double ChordAreaHeight = 22;
        private const double StringSpacing = 20;
        private const double StaffHeight = StringSpacing * 5; // 100
        private const double RowGap = 28;
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
            Height = 16 + rows * (ChordAreaHeight + StaffHeight + RowGap);
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
            var bar = _section.Bars[barIndex];
            bar.ChordMarks.RemoveAll(m => m.Column == colIndex);
            if (!string.IsNullOrWhiteSpace(text))
                bar.ChordMarks.Add(new TabChordMark { Column = colIndex, Text = text.Trim() });
            InvalidateVisual();
        }

        public void ClearCurrentCell()
        {
            var (b, c, s) = _cursor;
            if (b < 0 || b >= _section.Bars.Count) return;
            _section.Bars[b].Notes.RemoveAll(n => n.Column == c && n.StringIndex == s);
            InvalidateVisual();
        }

        public void ApplyToken(string token)
        {
            var (b, c, s) = _cursor;
            if (b < 0 || b >= _section.Bars.Count) return;
            var bar = _section.Bars[b];
            var existing = bar.Notes.FirstOrDefault(n => n.Column == c && n.StringIndex == s);

            string next;
            if (token is "h" or "p" or "/" or "\\" or "~" or "b")
            {
                var baseVal = existing?.Fret ?? "0";
                next = baseVal.EndsWith(token, StringComparison.Ordinal) ? baseVal : baseVal + token;
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
                else if (existing.Fret.EndsWith('h') || existing.Fret.EndsWith('p') || existing.Fret.EndsWith('/') || existing.Fret.EndsWith('\\'))
                {
                    next = existing.Fret + token;
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
                var rowTop = 12 + r * (ChordAreaHeight + StaffHeight + RowGap);
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
                    dc.DrawText(barTag, new Point(barLeft + 2, rowTop - 10));

                    // Subtle vertical column guides
                    for (var c = 0; c < colCount; c++)
                    {
                        var cx = barLeft + (c + 0.5) * colWidth;
                        dc.DrawLine(LinePen, new Point(cx, staffBottom + 3), new Point(cx, staffBottom + 7));
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

                    // Fret notes
                    foreach (var note in bar.Notes)
                    {
                        if (note.StringIndex is < 0 or > 5 || note.Column < 0 || note.Column >= colCount || string.IsNullOrWhiteSpace(note.Fret))
                            continue;
                        var cx = barLeft + (note.Column + 0.5) * colWidth;
                        var sy = staffTop + note.StringIndex * StringSpacing;
                        var ft = CreateText(note.Fret, 12.5, FretFont, InkBrush, _pixelsPerDip);
                        dc.DrawRectangle(Brushes.White, null, new Rect(cx - ft.Width / 2 - 3, sy - ft.Height / 2 + 1, ft.Width + 6, ft.Height - 2));
                        dc.DrawText(ft, new Point(cx - ft.Width / 2, sy - ft.Height / 2));
                    }

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
                var rowTop = 12 + r * (ChordAreaHeight + StaffHeight + RowGap);
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
            if (e.Key is Key.Delete or Key.Back)
            {
                ClearCurrentCell();
                e.Handled = true;
                return;
            }

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
            if (e.Key == Key.H) { ApplyToken("h"); e.Handled = true; return; }
            if (e.Key == Key.P) { ApplyToken("p"); e.Handled = true; return; }
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
