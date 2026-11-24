using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Xianpu.Desktop;

public partial class MainWindow
{
    private void RefreshQuickChordBar()
    {
        if (QuickChordPanel is null) return;
        QuickChordPanel.Children.Clear();

        var key = string.IsNullOrWhiteSpace(_selectedSong?.SelectedKey)
            ? (string.IsNullOrWhiteSpace(_selectedSong?.OriginalKey) ? "C" : _selectedSong!.OriginalKey)
            : _selectedSong!.SelectedKey;

        PaletteKeyLabel.Text = $"{key} 调常用和弦：";
        var chords = ChordCatalog.GetCommonChordsForKey(key);
        var activeBrush = ScoreSurface?.ActiveChordBrush;

        foreach (var chordName in chords)
        {
            var isSelected = ScoreSurface?.ContinuousPaintMode == true &&
                             string.Equals(activeBrush, chordName, StringComparison.OrdinalIgnoreCase);
            var isCustom = ChordCatalog.IsCustomChordInKey(key, chordName);

            var btn = new Button
            {
                Content = chordName,
                Tag = chordName,
                Style = (Style)FindResource("ChordChipButton"),
                ToolTip = ScoreSurface?.ContinuousPaintMode == true
                    ? $"选中 {chordName} 后，可连续点击歌词字位放置（右键可自定义指法）"
                    : $"将 {chordName} 填入当前光标字位（右键可自定义指法{(isCustom ? "或移除" : "")}）"
            };
            if (isSelected)
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(47, 107, 72));
                btn.Foreground = Brushes.White;
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(47, 107, 72));
            }

            var menu = new ContextMenu();
            var editShapeItem = new MenuItem { Header = $"自定义「{chordName}」左手按法..." };
            editShapeItem.Click += (_, _) => EditCustomChordShape(key, chordName);
            menu.Items.Add(editShapeItem);
            if (isCustom)
            {
                var removeItem = new MenuItem { Header = $"从 {ChordCatalog.NormalizeKeyBucket(key)} 调移除「{chordName}」" };
                removeItem.Click += (_, _) => RemoveCustomChordFromKey(key, chordName);
                menu.Items.Add(removeItem);
            }
            btn.ContextMenu = menu;

            btn.Click += QuickChordChip_Click;
            QuickChordPanel.Children.Add(btn);
        }

        var addBtn = new Button
        {
            Content = "+ 加和弦",
            Style = (Style)FindResource("ChordChipButton"),
            Background = new SolidColorBrush(Color.FromRgb(234, 243, 237)),
            Foreground = new SolidColorBrush(Color.FromRgb(36, 85, 56)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(148, 191, 164)),
            ToolTip = $"在 {ChordCatalog.NormalizeKeyBucket(key)} 调下新增左手和弦（支持自定义名称与指法图）"
        };
        addBtn.Click += (_, _) => AddCustomChordForKey(key);
        QuickChordPanel.Children.Add(addBtn);
    }

    private void AddCustomChordForKey(string key, ChordAnchor? applyToPopoverAnchor = null)
    {
        var result = CustomChordDialog.Show(this, key, KeyNames);
        if (!result.Saved || string.IsNullOrWhiteSpace(result.ChordName))
            return;

        ApplyCustomChordResult(result);

        if (applyToPopoverAnchor is { } anchor && ChordPopover.IsOpen)
        {
            ScoreSurface.SetChordAt(anchor, result.ChordName);
            ChordPopover.IsOpen = false;
            SetStatus($"已在 {result.TargetKey} 调新增左手和弦「{result.ChordName}」并填入当前字位。");
            ScoreSurface.Focus();
        }
        else
        {
            SetStatus($"已在 {result.TargetKey} 调新增左手和弦「{result.ChordName}」。");
        }
    }

    private void EditCustomChordShape(string key, string chordName)
    {
        var result = CustomChordDialog.Show(this, key, KeyNames, chordName, lockChordName: true);
        if (!result.Saved || string.IsNullOrWhiteSpace(result.ChordName))
            return;

        ApplyCustomChordResult(result, addToKeyPalette: false);
        SetStatus($"已更新和弦「{result.ChordName}」的左手按法。");
    }

    private void ApplyCustomChordResult(CustomChordDialogResult result, bool addToKeyPalette = true)
    {
        if (addToKeyPalette)
        {
            var bucket = ChordCatalog.NormalizeKeyBucket(result.TargetKey);
            if (!_library.CustomChordsByKey.TryGetValue(bucket, out var list))
            {
                list = [];
                _library.CustomChordsByKey[bucket] = list;
            }
            if (!list.Contains(result.ChordName, StringComparer.OrdinalIgnoreCase))
                list.Add(result.ChordName);
        }

        if (result.CustomShape is not null)
        {
            _library.CustomChordShapes.RemoveAll(s => string.Equals(s.Name, result.ChordName, StringComparison.OrdinalIgnoreCase));
            _library.CustomChordShapes.Add(result.CustomShape);
        }

        ChordCatalog.LoadCustomData(_library.CustomChordsByKey, _library.CustomChordShapes);
        SaveLibraryQuietly();
        RefreshQuickChordBar();
        ScoreSurface.Rebuild();
    }

    private void RemoveCustomChordFromKey(string key, string chordName)
    {
        var bucket = ChordCatalog.NormalizeKeyBucket(key);
        if (_library.CustomChordsByKey.TryGetValue(bucket, out var list))
        {
            list.RemoveAll(c => string.Equals(c, chordName, StringComparison.OrdinalIgnoreCase));
            if (list.Count == 0)
                _library.CustomChordsByKey.Remove(bucket);
        }

        ChordCatalog.LoadCustomData(_library.CustomChordsByKey, _library.CustomChordShapes);
        SaveLibraryQuietly();
        RefreshQuickChordBar();
        SetStatus($"已从 {bucket} 调移除自定义和弦「{chordName}」。");
    }

    private void QuickChordChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string chordName } || _selectedSong is null) return;

        CommitInlineLyricEdit(refocusSurface: false);
        if (LyricEditModeToggle?.IsChecked == true)
            LyricEditModeToggle.IsChecked = false;

        if (ScoreSurface.ContinuousPaintMode)
        {
            if (string.Equals(ScoreSurface.ActiveChordBrush, chordName, StringComparison.OrdinalIgnoreCase))
            {
                ScoreSurface.ActiveChordBrush = null;
                SetStatus("已取消连点画笔。");
            }
            else
            {
                ScoreSurface.ActiveChordBrush = chordName;
                SetStatus($"连点填谱：已选【{chordName}】，直接依次点击歌词上的进唱字位即可放置。");
            }
            RefreshQuickChordBar();
            ScoreSurface.Focus();
            return;
        }

        if (ScoreSurface.Caret is { } caret)
        {
            ScoreSurface.SetChordAt(caret, chordName);
            SetStatus($"已在当前字位填入和弦 {chordName}（可按左右方向键移动到下一进唱字位）。");
            ScoreSurface.Focus();
        }
        else
        {
            ScoreSurface.ActiveChordBrush = chordName;
            SetStatus($"已选和弦【{chordName}】，请点击歌词上的进唱字位放置。");
            ScoreSurface.Focus();
        }
    }

    private void ContinuousPaint_Changed(object sender, RoutedEventArgs e)
    {
        if (ScoreSurface is null) return;
        var enabled = ContinuousPaintToggle.IsChecked == true;
        if (enabled && LyricEditModeToggle?.IsChecked == true)
            LyricEditModeToggle.IsChecked = false;
        ScoreSurface.ContinuousPaintMode = enabled;

        if (!enabled)
        {
            ScoreSurface.ActiveChordBrush = null;
            SetStatus("已关闭连点填谱：点击任意歌词字位可直接输入或点选和弦。");
        }
        else
        {
            var firstChord = ChordCatalog.GetCommonChordsForKey(_selectedSong?.SelectedKey).FirstOrDefault() ?? "C";
            ScoreSurface.ActiveChordBrush ??= firstChord;
            SetStatus($"已开启连点填谱：当前画笔【{ScoreSurface.ActiveChordBrush}】，点击歌词字位可连续放置，点上方和弦按钮可切换。");
        }
        RefreshQuickChordBar();
    }

    private void OnCharacterSlotClicked(ChordAnchor anchor, Rect anchorRect, string? existingChord)
    {
        OpenChordPopover(anchor, anchorRect, existingChord ?? "", selectAll: true);
    }

    private void OnQuickTypeStarted(ChordAnchor anchor, Rect anchorRect, string initialText)
    {
        OpenChordPopover(anchor, anchorRect, initialText, selectAll: false);
    }

    private void OpenChordPopover(ChordAnchor anchor, Rect anchorRect, string initialText, bool selectAll)
    {
        if (_selectedSong is null || anchor.LineIndex < 0 || anchor.LineIndex >= _selectedSong.Lines.Count)
            return;

        PopulatePopoverContent(anchor, initialText, selectAll);

        ChordPopover.PlacementRectangle = new Rect(
            anchorRect.X,
            anchorRect.Y,
            Math.Max(22, anchorRect.Width),
            Math.Max(28, anchorRect.Height));
        ChordPopover.IsOpen = true;
    }

    private void PopulatePopoverContent(ChordAnchor anchor, string initialText, bool selectAll)
    {
        if (_selectedSong is null || anchor.LineIndex < 0 || anchor.LineIndex >= _selectedSong.Lines.Count)
            return;

        _popoverAnchor = anchor;
        var currentChord = ScoreSurface.GetChordAt(anchor);
        _popoverInitialChord = currentChord ?? "";

        _syncingPopoverPosition = true;
        try
        {
            PosOnCharRadio.IsChecked = !anchor.BeforeChar;
            PosBeforeCharRadio.IsChecked = anchor.BeforeChar;
        }
        finally
        {
            _syncingPopoverPosition = false;
        }

        var lineText = _selectedSong.Lines[anchor.LineIndex].Text;
        var charLabel = GetCharacterLabelAt(lineText, anchor.Position);
        PopoverTargetLabel.Text = anchor.BeforeChar
            ? $"进唱字位：「{charLabel}」字前"
            : $"进唱字位：「{charLabel}」上方";

        DeleteInlineChordButton.Visibility = !string.IsNullOrWhiteSpace(currentChord)
            ? Visibility.Visible
            : Visibility.Collapsed;

        BuildPopoverCharStrip(anchor);

        PopoverChordWrap.Children.Clear();
        var key = string.IsNullOrWhiteSpace(_selectedSong.SelectedKey)
            ? (string.IsNullOrWhiteSpace(_selectedSong.OriginalKey) ? "C" : _selectedSong.OriginalKey)
            : _selectedSong.SelectedKey;

        foreach (var chord in ChordCatalog.GetCommonChordsForKey(key))
        {
            var isCurrent = string.Equals(currentChord, chord, StringComparison.OrdinalIgnoreCase);
            var chip = new Button
            {
                Content = chord,
                Tag = chord,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 4, 4),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Background = isCurrent ? new SolidColorBrush(Color.FromRgb(47, 107, 72)) : new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                Foreground = isCurrent ? Brushes.White : new SolidColorBrush(Color.FromRgb(36, 85, 56)),
                BorderBrush = isCurrent ? new SolidColorBrush(Color.FromRgb(47, 107, 72)) : new SolidColorBrush(Color.FromRgb(203, 213, 225))
            };
            chip.Click += PopoverChordChip_Click;
            PopoverChordWrap.Children.Add(chip);
        }

        var addPopoverChordBtn = new Button
        {
            Content = "+ 加和弦",
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 4, 4),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Background = new SolidColorBrush(Color.FromRgb(234, 243, 237)),
            Foreground = new SolidColorBrush(Color.FromRgb(36, 85, 56)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(148, 191, 164)),
            ToolTip = $"在 {ChordCatalog.NormalizeKeyBucket(key)} 调下新增左手和弦并填入"
        };
        addPopoverChordBtn.Click += (_, _) => AddCustomChordForKey(key, anchor);
        PopoverChordWrap.Children.Add(addPopoverChordBtn);

        InlineChordBox.Text = initialText;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            InlineChordBox.Focus();
            Keyboard.Focus(InlineChordBox);
            if (selectAll)
                InlineChordBox.SelectAll();
            else
                InlineChordBox.CaretIndex = InlineChordBox.Text.Length;
        });
    }

    private void PopoverPosition_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncingPopoverPosition || _selectedSong is null) return;
        var beforeChar = PosBeforeCharRadio.IsChecked == true;
        if (_popoverAnchor is not { } oldAnchor || oldAnchor.BeforeChar == beforeChar)
            return;

        var newAnchor = oldAnchor with { BeforeChar = beforeChar };
        _popoverAnchor = newAnchor;

        var existingAtOld = ScoreSurface.GetChordAt(oldAnchor);
        var typed = InlineChordBox.Text.Trim();
        var chordToPlace = !string.IsNullOrWhiteSpace(typed)
            ? ChordCatalog.NormalizeChordName(typed)
            : existingAtOld;

        if (!string.IsNullOrWhiteSpace(chordToPlace))
        {
            ScoreSurface.SetChordPlacement(oldAnchor, newAnchor, chordToPlace);
            _popoverInitialChord = chordToPlace;
            DeleteInlineChordButton.Visibility = Visibility.Visible;
        }
        else
        {
            var existingAtNew = ScoreSurface.GetChordAt(newAnchor);
            _popoverInitialChord = existingAtNew ?? "";
            if (!string.IsNullOrWhiteSpace(existingAtNew))
                InlineChordBox.Text = existingAtNew;
            DeleteInlineChordButton.Visibility = !string.IsNullOrWhiteSpace(existingAtNew)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        ScoreSurface.SelectAnchor(newAnchor);

        var lineText = newAnchor.LineIndex >= 0 && newAnchor.LineIndex < _selectedSong.Lines.Count
            ? _selectedSong.Lines[newAnchor.LineIndex].Text
            : "";
        var charLabel = GetCharacterLabelAt(lineText, newAnchor.Position);
        PopoverTargetLabel.Text = newAnchor.BeforeChar
            ? $"进唱字位：「{charLabel}」字前"
            : $"进唱字位：「{charLabel}」上方";

        BuildPopoverCharStrip(newAnchor);
    }

    private void BuildPopoverCharStrip(ChordAnchor activeAnchor)
    {
        PopoverCharStrip.Children.Clear();
        if (_selectedSong is null || activeAnchor.LineIndex < 0 || activeAnchor.LineIndex >= _selectedSong.Lines.Count)
            return;

        var line = _selectedSong.Lines[activeAnchor.LineIndex];
        var elements = new List<(int Start, string Text)>();
        var enumerator = StringInfo.GetTextElementEnumerator(line.Text);
        while (enumerator.MoveNext())
        {
            elements.Add((enumerator.ElementIndex, enumerator.GetTextElement()));
        }

        if (line.Chords.Any(c => c.Position >= line.Text.Length) || activeAnchor.Position >= line.Text.Length)
        {
            elements.Add((line.Text.Length, "尾"));
        }

        var chordsByPos = line.Chords
            .GroupBy(c => c.Position)
            .ToDictionary(g => g.Key, g => g.ToList());

        Button? activeButton = null;
        foreach (var (start, text) in elements)
        {
            chordsByPos.TryGetValue(start, out var chordsAtPos);
            var hasChord = chordsAtPos is { Count: > 0 };
            var chordDisplay = hasChord
                ? string.Join(" ", chordsAtPos!.Select(c => c.BeforeChar ? $"↗{c.Name}" : c.Name))
                : "·";
            var isActive = start == activeAnchor.Position;

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = chordDisplay,
                FontSize = 11,
                FontWeight = hasChord ? FontWeights.Bold : FontWeights.Normal,
                Foreground = hasChord
                    ? new SolidColorBrush(Color.FromRgb(47, 107, 72))
                    : new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(text) ? "␣" : text,
                FontSize = 13,
                FontWeight = (isActive || hasChord) ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isActive
                    ? new SolidColorBrush(Color.FromRgb(15, 23, 42))
                    : (hasChord ? new SolidColorBrush(Color.FromRgb(30, 41, 59)) : new SolidColorBrush(Color.FromRgb(71, 85, 105))),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            var cell = new Button
            {
                Content = stack,
                Margin = new Thickness(0, 0, 3, 0),
                Padding = new Thickness(5, 3, 5, 3),
                MinWidth = 28,
                Cursor = Cursors.Hand,
                Background = isActive
                    ? new SolidColorBrush(Color.FromRgb(234, 243, 237))
                    : (hasChord ? new SolidColorBrush(Color.FromRgb(248, 250, 252)) : Brushes.White),
                BorderBrush = isActive
                    ? new SolidColorBrush(Color.FromRgb(47, 107, 72))
                    : (hasChord ? new SolidColorBrush(Color.FromRgb(148, 191, 164)) : new SolidColorBrush(Color.FromRgb(226, 232, 240))),
                BorderThickness = isActive ? new Thickness(1.5) : new Thickness(1),
                ToolTip = hasChord ? $"字位「{text}」当前和弦：{chordDisplay}（点击切换）" : $"点击切换到字位「{text}」"
            };

            var targetBeforeChar = chordsAtPos is { Count: > 0 }
                ? (chordsAtPos.Any(c => c.BeforeChar == activeAnchor.BeforeChar)
                    ? activeAnchor.BeforeChar
                    : chordsAtPos[0].BeforeChar)
                : false;
            var targetAnchor = new ChordAnchor(activeAnchor.LineIndex, start, targetBeforeChar);
            cell.Click += (_, _) =>
            {
                ScoreSurface.SelectAnchor(targetAnchor);
                var nextChord = ScoreSurface.GetChordAt(targetAnchor) ?? "";
                PopulatePopoverContent(targetAnchor, nextChord, selectAll: true);
            };

            PopoverCharStrip.Children.Add(cell);
            if (isActive)
                activeButton = cell;
        }

        if (activeButton is not null)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => activeButton.BringIntoView());
        }
    }

    private static string GetCharacterLabelAt(string text, int position)
    {
        if (string.IsNullOrEmpty(text) || position >= text.Length)
            return "句尾";
        if (position < 0)
            position = 0;
        var info = StringInfo.GetNextTextElement(text, position);
        return string.IsNullOrWhiteSpace(info) ? "空格" : info;
    }

    private void PopoverChordChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string chordName } || _popoverAnchor is not { } anchor)
            return;

        ScoreSurface.SetChordAt(anchor, chordName);
        ChordPopover.IsOpen = false;
        SetStatus($"已填入和弦 {chordName}。");
        ScoreSurface.Focus();
    }

    private void CommitInlineChord_Click(object sender, RoutedEventArgs e)
    {
        CommitInlineChordAndClose();
    }

    private void DeleteInlineChord_Click(object sender, RoutedEventArgs e)
    {
        if (_popoverAnchor is { } anchor)
        {
            ScoreSurface.SetChordAt(anchor, null);
            SetStatus("已删除该字位上的和弦。");
        }
        ChordPopover.IsOpen = false;
        ScoreSurface.Focus();
    }

    private void CommitInlineChordAndClose()
    {
        if (_popoverAnchor is { } anchor)
        {
            var raw = InlineChordBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                if (!string.IsNullOrWhiteSpace(_popoverInitialChord))
                {
                    ScoreSurface.SetChordAt(anchor, null);
                    SetStatus("已清除该字位上的和弦。");
                }
            }
            else
            {
                var normalized = ChordCatalog.NormalizeChordName(raw);
                ScoreSurface.SetChordAt(anchor, normalized);
                SetStatus($"已在进唱字位填入和弦 {normalized}。");
            }
        }
        ChordPopover.IsOpen = false;
        ScoreSurface.Focus();
    }

    private void InlineChordBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2 && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (TriggerInlineLyricEditForCurrentSelection())
                e.Handled = true;
            return;
        }

        if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None)
        {
            ChordPopover.IsOpen = false;
            _vm.TogglePracticeModeCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitInlineChordAndClose();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ChordPopover.IsOpen = false;
            ScoreSurface.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            if (_popoverAnchor is { } anchor)
            {
                var raw = InlineChordBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(raw))
                    ScoreSurface.SetChordAt(anchor, ChordCatalog.NormalizeChordName(raw));
                else if (!string.IsNullOrWhiteSpace(_popoverInitialChord))
                    ScoreSurface.SetChordAt(anchor, null);
            }

            var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            if (ScoreSurface.MoveCaret(shift ? -1 : 1, 0) && ScoreSurface.Caret is { } nextCaret)
            {
                var nextChord = ScoreSurface.GetChordAt(nextCaret) ?? "";
                OpenChordPopover(nextCaret, ScoreSurface.GetAnchorRect(nextCaret), nextChord, selectAll: true);
            }
            else
            {
                ChordPopover.IsOpen = false;
                ScoreSurface.Focus();
            }
            e.Handled = true;
        }
    }

    private void ChordPopover_Closed(object sender, EventArgs e)
    {
        _popoverAnchor = null;
    }

    private void NewSong_Click(object sender, RoutedEventArgs e)
    {
        var song = new SongModel
        {
            Title = "未命名歌曲",
            OriginalKey = "C",
            SelectedKey = "C",
            Capo = "",
            ColumnMode = 0
        };
        SelectSong(song);
        TitleBox.Focus();
        TitleBox.SelectAll();
        SetStatus("已创建空白曲谱：可点「导入 LRC」或「编辑歌词」铺开歌词。");
    }

    private void ImportLrc_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "LRC 歌词与文本 (*.lrc;*.txt)|*.lrc;*.txt|所有文件 (*.*)|*.*",
            Title = "导入 LRC 歌词文件"
        };
        if (dialog.ShowDialog(this) != true) return;
        ImportLrcFile(dialog.FileName);
    }

    private void AiImport_Click(object sender, RoutedEventArgs e)
    {
        var result = AiImportDialog.Show(this, hasCurrentSong: _selectedSong is not null);
        if (result is not { Accepted: true, Parsed: { } parsed })
            return;

        if (result.AppendToCurrent && _selectedSong is not null)
        {
            CommitFields();
            AiScoreImporter.MergeIntoSong(_selectedSong, parsed.Song);
            SelectSong(_selectedSong);
            SetStatus($"AI 追加导入成功（+{parsed.LineCount} 句歌词、+{parsed.ChordCount} 个和弦、+{parsed.TabBarCount} 个六线谱小节），点击「保存」可更新到本地曲库。");
        }
        else
        {
            SelectSong(parsed.Song);
            SetStatus($"AI 导入成功：《{parsed.Song.Title}》（{parsed.DetectedKind} · {parsed.LineCount} 句歌词、{parsed.ChordCount} 个和弦、{parsed.TabBarCount} 个六线谱小节），点击「保存」可加入本地曲库。");
        }
    }

    private void ImportLrcFile(string filePath)
    {
        try
        {
            var song = LrcImporter.Import(LrcImporter.ReadFile(filePath), filePath);
            if (string.IsNullOrWhiteSpace(song.SelectedKey) && !string.IsNullOrWhiteSpace(song.OriginalKey))
                song.SelectedKey = song.OriginalKey;
            if (string.IsNullOrWhiteSpace(song.SelectedKey))
                song.SelectedKey = "C";
            SelectSong(song);
            var summary = PartAnalyzer.SummarizeCandidates(song);
            if (summary.DuplicateGroups > 0 || summary.StackableGroups > 0)
            {
                SetStatus($"已导入《{song.Title}》（共 {song.Lines.Count} 行，预分 {summary.PartCount} 段，检测到 {summary.DuplicateGroups} 组同词、{summary.StackableGroups} 组可叠行段落，可点「段落编排」一键整理）。");
            }
            else if (summary.PartCount > 1)
            {
                SetStatus($"已导入《{song.Title}》（共 {song.Lines.Count} 行，已自动分为 {summary.PartCount} 段），点击进唱字位即可填入左手和弦。");
            }
            else
            {
                SetStatus($"已导入《{song.Title}》（共 {song.Lines.Count} 行歌词），点击歌词上的进唱字位即可添加左手和弦。");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            AppDialogs.ShowError(this, "导入歌词失败", ex.Message);
        }
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "可编辑弦谱文件 (*.xianpu.json;*.json)|*.xianpu.json;*.json|所有文件 (*.*)|*.*",
            Title = "导入弦谱文件"
        };
        if (dialog.ShowDialog(this) != true) return;
        OpenProjectFile(dialog.FileName);
    }

    private void OpenProjectFile(string filePath)
    {
        try
        {
            var song = LibraryStore.LoadSongProject(filePath);
            song.Id = Guid.NewGuid().ToString("N");
            SelectSong(song);
            SetStatus($"已导入曲谱《{song.Title}》（完整保留歌词、和弦与六线谱），点击「保存」可加入本地曲库。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            AppDialogs.ShowError(this, "打开曲谱失败", ex.Message);
        }
    }

    private void LoadFromFilePath(string filePath)
    {
        if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            OpenProjectFile(filePath);
        else
            ImportLrcFile(filePath);
    }
}
