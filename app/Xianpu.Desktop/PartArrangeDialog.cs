using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Xianpu.Desktop;

internal static class PartArrangeDialog
{
    private static readonly string[] PresetPartTitles =
    [
        "主歌", "主歌 1", "主歌 2", "预副歌", "副歌", "桥段", "尾声", "过渡"
    ];

    public static List<LyricLine>? Show(Window? owner, SongModel song)
    {
        var workArea = SystemParameters.WorkArea;
        var winWidth = Math.Min(920, Math.Max(660, workArea.Width * 0.88));
        var winHeight = Math.Min(640, Math.Max(460, workArea.Height * 0.88));

        var window = AppDialogs.CreateCardWindow(
            owner,
            "段落编排与重复段整理",
            "切分或合并段落，将同结构主歌折叠为「多段叠行」，或把完全重复副歌折为反复引用",
            winWidth,
            winHeight,
            resizable: true,
            out var root,
            badgeText: "段");

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var cloneToOriginal = new Dictionary<LyricLine, LyricLine>(ReferenceEqualityComparer.Instance);
        var workingLines = new List<LyricLine>(song.Lines.Count);
        foreach (var orig in song.Lines)
        {
            var copy = orig.Clone();
            cloneToOriginal[copy] = orig;
            workingLines.Add(copy);
        }

        var checkedParts = new HashSet<int>();

        // ─── Row 0: Toolbar ───
        var toolbarBorder = new Border
        {
            Background = AppDialogs.SubtleSurfaceBrush,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var toolbarDock = new DockPanel();

        var summaryBadge = new TextBlock
        {
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.AccentHoverBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        var summaryBorder = new Border
        {
            Background = AppDialogs.AccentLightBrush,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 3, 10, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Child = summaryBadge
        };
        DockPanel.SetDock(summaryBorder, Dock.Right);
        toolbarDock.Children.Add(summaryBorder);

        var actionsWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        var autoSegBtn = AppDialogs.CreateAccentOutlineButton(owner, "自动识别分段", minWidth: 106);
        autoSegBtn.Margin = new Thickness(0, 2, 6, 2);
        autoSegBtn.ToolTip = "根据重复片段和句数节律自动切分段落";

        var foldStackBtn = AppDialogs.CreatePrimaryButton(owner, "合并勾选段为叠行", minWidth: 126);
        foldStackBtn.Margin = new Thickness(0, 2, 6, 2);
        foldStackBtn.ToolTip = "勾选 2~4 个结构相近的段落，合并成一套和弦下的多遍叠行歌词";

        var copyChordsBtn = AppDialogs.CreateSecondaryButton(owner, "复制和弦到勾选段", minWidth: 126);
        copyChordsBtn.Margin = new Thickness(0, 2, 6, 2);
        copyChordsBtn.ToolTip = "把已填和弦的勾选段落按比例复制到其余勾选段落";

        var expandAllBtn = AppDialogs.CreateSecondaryButton(owner, "全部展开叠行", minWidth: 104);
        expandAllBtn.Margin = new Thickness(0, 2, 0, 2);
        expandAllBtn.ToolTip = "将所有多段叠行拆回按遍顺序展开，并同步保留和弦";

        actionsWrap.Children.Add(autoSegBtn);
        actionsWrap.Children.Add(foldStackBtn);
        actionsWrap.Children.Add(copyChordsBtn);
        actionsWrap.Children.Add(expandAllBtn);
        toolbarDock.Children.Add(actionsWrap);
        toolbarBorder.Child = toolbarDock;
        Grid.SetRow(toolbarBorder, 0);
        root.Children.Add(toolbarBorder);

        // ─── Row 1: Scrollable Cards ───
        var cardsPanel = new StackPanel();
        var cardsScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 4, 0),
            Content = cardsPanel
        };
        Grid.SetRow(cardsScroll, 1);
        root.Children.Add(cardsScroll);

        List<PartBlock> currentParts = [];

        void UpdateToolbarState()
        {
            var validChecked = checkedParts
                .Where(idx => idx >= 0 && idx < currentParts.Count && !currentParts[idx].IsReference)
                .OrderBy(idx => idx)
                .ToList();

            foldStackBtn.IsEnabled = validChecked.Count >= 2;
            copyChordsBtn.IsEnabled = validChecked.Count >= 2 && validChecked.Any(idx => currentParts[idx].ChordCount > 0);
            expandAllBtn.IsEnabled = currentParts.Any(p => p.HasStacked);

            var dupCount = currentParts.Count(p => p.DuplicateOfPartIndex is not null);
            var simCount = currentParts.Count(p => p.SimilarToPartIndex is not null);
            summaryBadge.Text = $"共 {currentParts.Count} 段 · {dupCount} 组同词 · {simCount} 组可叠行";
        }

        void RebuildCards()
        {
            cardsPanel.Children.Clear();
            currentParts = PartAnalyzer.BuildParts(workingLines);
            checkedParts.RemoveWhere(idx => idx < 0 || idx >= currentParts.Count || currentParts[idx].IsReference);

            if (currentParts.Count == 0)
            {
                cardsPanel.Children.Add(new TextBlock
                {
                    Text = "当前曲谱暂无歌词行，请先导入或编辑歌词后再进行段落编排。",
                    FontSize = 13,
                    Foreground = AppDialogs.MutedBrush,
                    Margin = new Thickness(8, 24, 8, 24),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                UpdateToolbarState();
                return;
            }

            foreach (var part in currentParts)
            {
                var card = BuildPartCard(
                    owner,
                    part,
                    currentParts,
                    workingLines,
                    checkedParts,
                    onSelectionChanged: UpdateToolbarState,
                    onMutated: RebuildCards);
                cardsPanel.Children.Add(card);
            }

            UpdateToolbarState();
        }

        autoSegBtn.Click += (_, _) =>
        {
            checkedParts.Clear();
            PartAnalyzer.AutoSegment(workingLines);
            RebuildCards();
        };

        foldStackBtn.Click += (_, _) =>
        {
            var indices = checkedParts
                .Where(idx => idx >= 0 && idx < currentParts.Count && !currentParts[idx].IsReference)
                .OrderBy(idx => idx)
                .ToList();
            if (indices.Count < 2) return;
            PartAnalyzer.FoldPartsAsStacked(workingLines, indices);
            checkedParts.Clear();
            RebuildCards();
        };

        copyChordsBtn.Click += (_, _) =>
        {
            var indices = checkedParts
                .Where(idx => idx >= 0 && idx < currentParts.Count && !currentParts[idx].IsReference)
                .OrderBy(idx => idx)
                .ToList();
            if (indices.Count < 2) return;
            var sourceIdx = indices.FirstOrDefault(idx => currentParts[idx].ChordCount > 0, -1);
            if (sourceIdx < 0) return;
            var targets = indices.Where(idx => idx != sourceIdx).ToList();
            PartAnalyzer.CopyPartChords(workingLines, sourceIdx, targets);
            RebuildCards();
        };

        expandAllBtn.Click += (_, _) =>
        {
            for (var i = currentParts.Count - 1; i >= 0; i--)
            {
                if (currentParts[i].HasStacked)
                    PartAnalyzer.ExpandStackedPart(workingLines, i, copyChords: true);
            }
            checkedParts.Clear();
            RebuildCards();
        };

        RebuildCards();

        // ─── Row 2: Footer ───
        List<LyricLine>? result = null;
        var cancelBtn = AppDialogs.CreateSecondaryButton(owner, "取消", minWidth: 86);
        cancelBtn.IsCancel = true;
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);
        cancelBtn.Click += (_, _) => AppDialogs.CloseWithResult(window, false);

        var applyBtn = AppDialogs.CreatePrimaryButton(owner, "应用到曲谱", minWidth: 112);
        applyBtn.Click += (_, _) =>
        {
            var applied = new List<LyricLine>(workingLines.Count);
            foreach (var copy in workingLines)
            {
                if (cloneToOriginal.TryGetValue(copy, out var orig))
                {
                    orig.Text = copy.Text;
                    orig.AltLyrics = [.. copy.AltLyrics];
                    orig.PartTitle = copy.PartTitle;
                    orig.RepeatCount = copy.RepeatCount;
                    orig.IsReferenceLine = copy.IsReferenceLine;
                    orig.Chords = copy.Chords;
                    applied.Add(orig);
                }
                else
                {
                    applied.Add(copy);
                }
            }
            result = applied;
            AppDialogs.CloseWithResult(window, true);
        };

        var footerDock = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var footerTip = new TextBlock
        {
            Text = "提示：点击段落内任意两句之间的「切分」条，可将该处拆为新段落",
            FontSize = 11.5,
            Foreground = AppDialogs.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        footerDock.Children.Add(footerTip);

        var footerButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        footerButtons.Children.Add(cancelBtn);
        footerButtons.Children.Add(applyBtn);
        DockPanel.SetDock(footerButtons, Dock.Right);
        footerDock.Children.Add(footerButtons);

        Grid.SetRow(footerDock, 2);
        root.Children.Add(footerDock);

        return window.ShowDialog() == true ? result : null;
    }

    private static Border BuildPartCard(
        Window? owner,
        PartBlock part,
        IReadOnlyList<PartBlock> allParts,
        IList<LyricLine> workingLines,
        HashSet<int> checkedParts,
        Action onSelectionChanged,
        Action onMutated)
    {
        var firstLine = workingLines[part.StartLine];

        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = part.IsReference ? AppDialogs.AccentBrush : AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var cardStack = new StackPanel();
        card.Child = cardStack;

        // ─── Card Header ───
        var headerBorder = new Border
        {
            Background = part.IsReference ? AppDialogs.AccentLightBrush : AppDialogs.SubtleSurfaceBrush,
            BorderBrush = AppDialogs.DividerBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            Padding = new Thickness(10, 7, 10, 7)
        };
        var headerDock = new DockPanel();

        var rightActions = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };

        if (!part.IsReference && part.DuplicateOfPartIndex is { } dupIdx)
        {
            var foldRefBtn = AppDialogs.CreateAccentOutlineButton(owner, "折为反复引用", minWidth: 94);
            foldRefBtn.Padding = new Thickness(8, 3, 8, 3);
            foldRefBtn.FontSize = 11.5;
            foldRefBtn.Margin = new Thickness(4, 1, 0, 1);
            foldRefBtn.ToolTip = $"与「{allParts[dupIdx].Title}」歌词完全一致，折叠为单行反复提示";
            foldRefBtn.Click += (_, _) =>
            {
                PartAnalyzer.FoldDuplicateAsReference(workingLines, part.PartIndex, dupIdx);
                onMutated();
            };
            rightActions.Children.Add(foldRefBtn);
        }
        else if (!part.IsReference && part.SimilarToPartIndex is { } simIdx)
        {
            var quickStackBtn = AppDialogs.CreateAccentOutlineButton(owner, $"与「{allParts[simIdx].Title}」叠行", minWidth: 100);
            quickStackBtn.Padding = new Thickness(8, 3, 8, 3);
            quickStackBtn.FontSize = 11.5;
            quickStackBtn.Margin = new Thickness(4, 1, 0, 1);
            quickStackBtn.ToolTip = "句数与长度相近，一键合并成多段叠行歌词";
            quickStackBtn.Click += (_, _) =>
            {
                PartAnalyzer.FoldPartsAsStacked(workingLines, [simIdx, part.PartIndex]);
                onMutated();
            };
            rightActions.Children.Add(quickStackBtn);
        }

        if (part.HasStacked)
        {
            var expandBtn = AppDialogs.CreateSecondaryButton(owner, "展开叠行", minWidth: 74);
            expandBtn.Padding = new Thickness(8, 3, 8, 3);
            expandBtn.FontSize = 11.5;
            expandBtn.Margin = new Thickness(4, 1, 0, 1);
            expandBtn.Click += (_, _) =>
            {
                PartAnalyzer.ExpandStackedPart(workingLines, part.PartIndex, copyChords: true);
                onMutated();
            };
            rightActions.Children.Add(expandBtn);
        }

        if (part.IsReference)
        {
            var removeRefBtn = AppDialogs.CreateSecondaryButton(owner, "移除引用行", minWidth: 82);
            removeRefBtn.Padding = new Thickness(8, 3, 8, 3);
            removeRefBtn.FontSize = 11.5;
            removeRefBtn.Margin = new Thickness(4, 1, 0, 1);
            removeRefBtn.Click += (_, _) =>
            {
                workingLines.RemoveAt(part.StartLine);
                checkedParts.Clear();
                onMutated();
            };
            rightActions.Children.Add(removeRefBtn);
        }
        else
        {
            var dupBtn = AppDialogs.CreateSecondaryButton(owner, "复制段", minWidth: 56);
            dupBtn.Padding = new Thickness(8, 3, 8, 3);
            dupBtn.FontSize = 11.5;
            dupBtn.Margin = new Thickness(4, 1, 0, 1);
            dupBtn.ToolTip = "在当前段落紧邻下方复制出一份完全相同的段落（含和弦）";
            dupBtn.Click += (_, _) =>
            {
                PartAnalyzer.DuplicatePart(workingLines, part.PartIndex, includeChords: true);
                checkedParts.Clear();
                onMutated();
            };
            rightActions.Children.Add(dupBtn);

            if (part.PartIndex > 0 && !allParts[part.PartIndex - 1].IsReference)
            {
                var mergePrevBtn = AppDialogs.CreateSecondaryButton(owner, "↑ 并入上段", minWidth: 82);
                mergePrevBtn.Padding = new Thickness(8, 3, 8, 3);
                mergePrevBtn.FontSize = 11.5;
                mergePrevBtn.Margin = new Thickness(4, 1, 0, 1);
                mergePrevBtn.ToolTip = "取消当前段落分隔，并入上一段";
                mergePrevBtn.Click += (_, _) =>
                {
                    firstLine.PartTitle = "";
                    firstLine.RepeatCount = 1;
                    checkedParts.Clear();
                    onMutated();
                };
                rightActions.Children.Add(mergePrevBtn);
            }
        }

        if (allParts.Count > 1)
        {
            var moveUpBtn = AppDialogs.CreateSecondaryButton(owner, "↑", minWidth: 28);
            moveUpBtn.Padding = new Thickness(6, 3, 6, 3);
            moveUpBtn.FontSize = 11.5;
            moveUpBtn.Margin = new Thickness(4, 1, 0, 1);
            moveUpBtn.IsEnabled = part.PartIndex > 0;
            moveUpBtn.ToolTip = "将此段上移";
            moveUpBtn.Click += (_, _) =>
            {
                PartAnalyzer.MovePart(workingLines, part.PartIndex, -1);
                checkedParts.Clear();
                onMutated();
            };

            var moveDownBtn = AppDialogs.CreateSecondaryButton(owner, "↓", minWidth: 28);
            moveDownBtn.Padding = new Thickness(6, 3, 6, 3);
            moveDownBtn.FontSize = 11.5;
            moveDownBtn.Margin = new Thickness(3, 1, 0, 1);
            moveDownBtn.IsEnabled = part.PartIndex < allParts.Count - 1;
            moveDownBtn.ToolTip = "将此段下移";
            moveDownBtn.Click += (_, _) =>
            {
                PartAnalyzer.MovePart(workingLines, part.PartIndex, +1);
                checkedParts.Clear();
                onMutated();
            };

            rightActions.Children.Add(moveUpBtn);
            rightActions.Children.Add(moveDownBtn);
        }

        DockPanel.SetDock(rightActions, Dock.Right);
        headerDock.Children.Add(rightActions);

        var leftHeader = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };

        if (!part.IsReference)
        {
            var checkBox = new CheckBox
            {
                IsChecked = checkedParts.Contains(part.PartIndex),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            checkBox.Checked += (_, _) => { checkedParts.Add(part.PartIndex); onSelectionChanged(); };
            checkBox.Unchecked += (_, _) => { checkedParts.Remove(part.PartIndex); onSelectionChanged(); };
            leftHeader.Children.Add(checkBox);

            var titleBox = new ComboBox
            {
                Width = 112,
                IsEditable = true,
                ItemsSource = PresetPartTitles,
                Text = string.IsNullOrWhiteSpace(firstLine.PartTitle) ? part.Title : firstLine.PartTitle,
                FontSize = 12,
                Padding = new Thickness(6, 3, 6, 3),
                Margin = new Thickness(0, 1, 6, 1),
                ToolTip = "修改段落名称（如：主歌、副歌、桥段）"
            };
            void CommitTitle()
            {
                var next = (titleBox.Text ?? "").Trim();
                firstLine.PartTitle = next;
            }
            titleBox.SelectionChanged += (_, _) =>
            {
                if (titleBox.SelectedItem is string s)
                    firstLine.PartTitle = s;
            };
            titleBox.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => CommitTitle()));
            titleBox.LostFocus += (_, _) => CommitTitle();
            leftHeader.Children.Add(titleBox);

            var repeatBox = new ComboBox
            {
                Width = 58,
                FontSize = 11.5,
                Padding = new Thickness(5, 3, 5, 3),
                Margin = new Thickness(0, 1, 8, 1),
                ToolTip = "设置段落反复次数标记 (×1 ~ ×4)"
            };
            for (var r = 1; r <= 4; r++)
                repeatBox.Items.Add($"×{r}");
            repeatBox.SelectedIndex = Math.Clamp(firstLine.RepeatCount, 1, 4) - 1;
            repeatBox.SelectionChanged += (_, _) =>
            {
                if (repeatBox.SelectedIndex >= 0)
                    firstLine.RepeatCount = repeatBox.SelectedIndex + 1;
            };
            leftHeader.Children.Add(repeatBox);
        }
        else
        {
            leftHeader.Children.Add(new TextBlock
            {
                Text = "反复引用",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = AppDialogs.AccentHoverBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 8, 0)
            });
        }

        leftHeader.Children.Add(BuildMiniBadge($"{part.LineCount} 行", AppDialogs.SubtleBorderBrush, AppDialogs.BodyBrush));

        if (part.HasStacked)
        {
            var maxVerses = 1 + Enumerable.Range(part.StartLine, part.LineCount)
                .Max(idx => workingLines[idx].AltLyrics?.Count ?? 0);
            leftHeader.Children.Add(BuildMiniBadge($"已叠 {maxVerses} 遍", AppDialogs.AccentLightBrush, AppDialogs.AccentHoverBrush));
        }

        if (part.ChordCount > 0)
            leftHeader.Children.Add(BuildMiniBadge($"{part.ChordCount} 和弦", AppDialogs.SubtleBorderBrush, AppDialogs.MutedBrush));

        headerDock.Children.Add(leftHeader);
        headerBorder.Child = headerDock;
        cardStack.Children.Add(headerBorder);

        // ─── Card Body: Lines + Split Strips ───
        var bodyStack = new StackPanel { Margin = new Thickness(12, 8, 12, 8) };
        for (var rowIdx = 0; rowIdx < part.LineCount; rowIdx++)
        {
            var line = workingLines[part.StartLine + rowIdx];
            var lineBox = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };

            var primaryRow = new DockPanel();
            var numText = new TextBlock
            {
                Text = $"{rowIdx + 1}.",
                Width = 22,
                FontSize = 11.5,
                Foreground = AppDialogs.MutedBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(numText, Dock.Left);
            primaryRow.Children.Add(numText);

            if (line.Chords.Count > 0)
            {
                var chordHint = string.Join(" ", line.Chords.Take(6).Select(c => c.Name)) + (line.Chords.Count > 6 ? " …" : "");
                var chordTag = new TextBlock
                {
                    Text = chordHint,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = AppDialogs.AccentBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0)
                };
                DockPanel.SetDock(chordTag, Dock.Right);
                primaryRow.Children.Add(chordTag);
            }

            primaryRow.Children.Add(new TextBlock
            {
                Text = line.Text,
                FontSize = 13,
                FontWeight = part.IsReference ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = part.IsReference ? AppDialogs.AccentHoverBrush : AppDialogs.TitleBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });
            lineBox.Children.Add(primaryRow);

            if (line.AltLyrics is { Count: > 0 })
            {
                for (var altIdx = 0; altIdx < line.AltLyrics.Count; altIdx++)
                {
                    lineBox.Children.Add(new TextBlock
                    {
                        Text = $"  [{altIdx + 2}] {line.AltLyrics[altIdx]}",
                        FontSize = 12,
                        Foreground = AppDialogs.MutedBrush,
                        Margin = new Thickness(20, 1, 0, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });
                }
            }

            bodyStack.Children.Add(lineBox);

            if (!part.IsReference && rowIdx < part.LineCount - 1)
            {
                var splitTargetIdx = part.StartLine + rowIdx + 1;
                var splitBtn = new Button
                {
                    Content = "✂ 在此处切分为新段落",
                    FontSize = 10.5,
                    Foreground = AppDialogs.MutedBrush,
                    Background = Brushes.Transparent,
                    BorderBrush = AppDialogs.DividerBrush,
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(22, 2, 0, 2),
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Cursor = Cursors.Hand,
                    Opacity = 0.65
                };
                splitBtn.MouseEnter += (_, _) => { splitBtn.Opacity = 1.0; splitBtn.Foreground = AppDialogs.AccentBrush; };
                splitBtn.MouseLeave += (_, _) => { splitBtn.Opacity = 0.65; splitBtn.Foreground = AppDialogs.MutedBrush; };
                splitBtn.Click += (_, _) =>
                {
                    workingLines[splitTargetIdx].PartTitle = $"段落 {allParts.Count + 1}";
                    onMutated();
                };
                bodyStack.Children.Add(splitBtn);
            }
        }

        cardStack.Children.Add(bodyStack);
        return card;
    }

    private static Border BuildMiniBadge(string text, Brush background, Brush foreground) => new()
    {
        Background = background,
        CornerRadius = new CornerRadius(9),
        Padding = new Thickness(7, 2, 7, 2),
        Margin = new Thickness(0, 1, 5, 1),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = foreground
        }
    };
}
