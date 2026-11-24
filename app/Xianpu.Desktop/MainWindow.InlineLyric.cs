using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Xianpu.Desktop;

public partial class MainWindow
{
    private (int LineIndex, int VerseIndex)? _activeInlineLyric;
    private string _inlineLyricOriginalText = "";
    private bool _closingInlineLyricEditor;

    private bool IsInlineLyricEditorOpen =>
        _activeInlineLyric is not null && InlineLyricOverlayCanvas?.Visibility == Visibility.Visible;

    private bool TriggerInlineLyricEditForCurrentSelection()
    {
        if (_selectedSong is null || ScoreSurface is null)
            return false;

        if (_selectedSong.Lines.Count == 0)
        {
            _selectedSong.Lines.Add(new LyricLine { Text = "" });
            ScoreSurface.Rebuild();
            if (ScoreSurface.GetInlineLyricTarget(0, 0, 0) is { } firstTarget)
            {
                OpenInlineLyricEditor(firstTarget);
                return true;
            }
            return false;
        }

        int? lineIdx = null;
        int? charIdx = null;

        if (ChordPopover.IsOpen && _popoverAnchor is { } popoverAnchor)
        {
            lineIdx = popoverAnchor.LineIndex;
            charIdx = popoverAnchor.Position;
        }
        else if (ScoreSurface.ActiveLineIndex is { } activeIdx)
        {
            lineIdx = activeIdx;
            charIdx = ScoreSurface.Caret?.LineIndex == activeIdx ? ScoreSurface.Caret.Position : null;
        }
        else
        {
            for (var i = 0; i < _selectedSong.Lines.Count; i++)
            {
                if (!_selectedSong.Lines[i].IsReferenceLine)
                {
                    lineIdx = i;
                    charIdx = 0;
                    break;
                }
            }
        }

        if (lineIdx is not { } idx || idx < 0 || idx >= _selectedSong.Lines.Count)
            return false;

        if (ScoreSurface.GetInlineLyricTarget(idx, 0, charIdx) is not { } target)
            return false;

        OpenInlineLyricEditor(target);
        return true;
    }

    private void LyricEditMode_Changed(object sender, RoutedEventArgs e)
    {
        if (LyricEditModeToggle is null || ScoreSurface is null) return;
        var enabled = LyricEditModeToggle.IsChecked == true;

        if (enabled)
        {
            ChordPopover.IsOpen = false;
            if (ContinuousPaintToggle?.IsChecked == true)
                ContinuousPaintToggle.IsChecked = false;
            ScoreSurface.ActiveChordBrush = null;
            ScoreSurface.SelectedShapeId = null;
        }
        else
        {
            CommitInlineLyricEdit(refocusSurface: false);
        }

        ScoreSurface.LyricEditMode = enabled;
        ScoreSurface.InvalidateVisual();

        if (!_initializing)
        {
            SetStatus(enabled
                ? "已开启原位改词模式：单击谱面任意歌词即可直接打字修改（Enter 保存，Shift+Enter 断句，↑/↓ 切换上下句）。"
                : "已切回标和弦模式：单击歌词字位标和弦，双击歌词或按 F2 仍可随时原位改词。");
        }
    }

    private void OpenInlineLyricEditor(LyricInlineTarget target)
    {
        if (_selectedSong is null || InlineLyricOverlayCanvas is null || InlineLyricBox is null)
            return;

        if (_activeInlineLyric is { } prev && prev != (target.LineIndex, target.VerseIndex))
        {
            var prevLineCount = _selectedSong.Lines.Count;
            var prevLineIdx = prev.LineIndex;
            CommitInlineLyricEdit(refocusSurface: false);
            var adjustedLineIdx = target.LineIndex;
            if (_selectedSong.Lines.Count < prevLineCount && prevLineIdx < target.LineIndex)
                adjustedLineIdx = Math.Max(0, adjustedLineIdx - (prevLineCount - _selectedSong.Lines.Count));

            if (ScoreSurface.GetInlineLyricTarget(adjustedLineIdx, target.VerseIndex, target.CharIndex) is not { } refreshed)
                return;
            target = refreshed;
        }

        if (target.LineIndex < 0 || target.LineIndex >= _selectedSong.Lines.Count)
            return;

        var line = _selectedSong.Lines[target.LineIndex];
        if (line.IsReferenceLine)
        {
            OpenPartArrangeDialog();
            return;
        }

        ChordPopover.IsOpen = false;

        var verseIdx = Math.Clamp(target.VerseIndex, 0, line.AltLyrics.Count);
        var currentText = verseIdx == 0 ? line.Text : line.AltLyrics[verseIdx - 1];

        _activeInlineLyric = (target.LineIndex, verseIdx);
        _inlineLyricOriginalText = currentText;
        ScoreSurface.ActiveInlineLyricEdit = _activeInlineLyric;
        ScoreSurface.InvalidateVisual();

        if (InlineLyricBadgeText is not null)
        {
            InlineLyricBadgeText.Text = target.IsStacked || verseIdx > 0
                ? $"第 {target.LineIndex + 1} 句 · 第 {verseIdx + 1} 遍"
                : $"第 {target.LineIndex + 1} 句歌词";
        }

        if (InlineLyricHintText is not null)
        {
            InlineLyricHintText.Text = verseIdx == 0
                ? "Enter 保存 · Shift+Enter 断句 · ↑↓ 切句 · Esc 取消"
                : "Enter 保存 · ↑↓ 切句 · 留空删除本遍 · Esc 取消";
        }

        var left = Math.Max(8.0, target.Bounds.X - 6.0);
        var top = Math.Max(4.0, target.Bounds.Y - 3.0);
        Canvas.SetLeft(InlineLyricEditorContainer, left);
        Canvas.SetTop(InlineLyricEditorContainer, top);

        var minWidth = Math.Max(180.0, Math.Min(target.MaxWidth, target.Bounds.Width + 24.0));
        InlineLyricCard.MinWidth = minWidth;
        InlineLyricCard.MaxWidth = Math.Max(minWidth, target.MaxWidth + 12.0);

        InlineLyricBox.Foreground = verseIdx == 0 ? AppDialogs.TitleBrush : AppDialogs.BodyBrush;
        InlineLyricBox.Text = currentText;
        InlineLyricOverlayCanvas.Visibility = Visibility.Visible;

        EnsureInlineEditorVisible(target.Bounds);

        var targetCharIndex = Math.Clamp(target.CharIndex, 0, currentText.Length);
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!IsInlineLyricEditorOpen) return;
            InlineLyricBox.Focus();
            Keyboard.Focus(InlineLyricBox);
            var safeCaret = Math.Clamp(targetCharIndex, 0, InlineLyricBox.Text.Length);
            InlineLyricBox.Select(safeCaret, 0);
        });
    }

    private void CommitInlineLyricEdit(bool refocusSurface = false)
    {
        if (_closingInlineLyricEditor || _activeInlineLyric is not { } active)
            return;

        _closingInlineLyricEditor = true;
        try
        {
            var editedRaw = InlineLyricBox?.Text ?? "";
            HideInlineLyricOverlay();

            if (_selectedSong is null || active.LineIndex < 0 || active.LineIndex >= _selectedSong.Lines.Count)
            {
                ScoreSurface.Rebuild();
                return;
            }

            var line = _selectedSong.Lines[active.LineIndex];
            var changed = false;

            if (string.IsNullOrWhiteSpace(editedRaw))
            {
                LyricLineEditHelper.RemoveVerseOrLine(_selectedSong, active.LineIndex, active.VerseIndex);
                changed = true;
            }
            else if (active.VerseIndex == 0)
            {
                var multiLines = editedRaw
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace('\r', '\n')
                    .Split('\n')
                    .Select(s => s.TrimEnd())
                    .Where((s, idx) => idx == 0 || !string.IsNullOrWhiteSpace(s))
                    .ToList();

                var primaryNew = multiLines.Count > 0 ? multiLines[0] : "";
                if (!string.Equals(line.Text, primaryNew, StringComparison.Ordinal) || multiLines.Count > 1)
                {
                    LyricLineEditHelper.ApplyMainLyricEdit(line, primaryNew);
                    if (multiLines.Count > 1)
                    {
                        for (var i = 1; i < multiLines.Count; i++)
                        {
                            _selectedSong.Lines.Insert(active.LineIndex + i, new LyricLine
                            {
                                Text = multiLines[i].Trim()
                            });
                        }

                        var extraCount = multiLines.Count - 1;
                        foreach (var tab in _selectedSong.TabSections)
                        {
                            if (tab.Kind == TabSectionKind.Interlude && tab.InsertAfterLineIndex >= active.LineIndex)
                                tab.InsertAfterLineIndex += extraCount;
                        }
                    }

                    LibraryStore.NormalizeSong(_selectedSong);
                    changed = true;
                }
            }
            else
            {
                var altIdx = active.VerseIndex - 1;
                var cleanAlt = editedRaw
                    .Replace("\r\n", " ", StringComparison.Ordinal)
                    .Replace('\r', ' ')
                    .Replace('\n', ' ')
                    .TrimEnd();

                if (altIdx >= 0 && altIdx < line.AltLyrics.Count &&
                    !string.Equals(line.AltLyrics[altIdx], cleanAlt, StringComparison.Ordinal))
                {
                    line.AltLyrics[altIdx] = cleanAlt;
                    LibraryStore.NormalizeSong(_selectedSong);
                    changed = true;
                }
            }

            ScoreSurface.Rebuild();
            UpdateViewportScale();

            if (changed)
            {
                SaveLibraryQuietly();
                RefreshLibraryList();
                UpdatePartArrangeButtonCue();
                UpdateActionStates();
                SetStatus("已原位更新谱面歌词（和弦位置已自动跟随）。");
            }
        }
        finally
        {
            _closingInlineLyricEditor = false;
            if (refocusSurface)
                ScoreSurface?.Focus();
        }
    }

    private void CancelInlineLyricEdit()
    {
        if (_closingInlineLyricEditor || _activeInlineLyric is not { } active)
            return;

        _closingInlineLyricEditor = true;
        try
        {
            HideInlineLyricOverlay();

            // Clean up temporary empty placeholder if the user opened an empty line/verse and hit Esc without typing
            if (_selectedSong is not null &&
                active.LineIndex >= 0 &&
                active.LineIndex < _selectedSong.Lines.Count &&
                string.IsNullOrEmpty(_inlineLyricOriginalText))
            {
                var line = _selectedSong.Lines[active.LineIndex];
                if (active.VerseIndex > 0 &&
                    active.VerseIndex - 1 < line.AltLyrics.Count &&
                    string.IsNullOrEmpty(line.AltLyrics[active.VerseIndex - 1]))
                {
                    line.AltLyrics.RemoveAt(active.VerseIndex - 1);
                }
                else if (active.VerseIndex == 0 &&
                         string.IsNullOrEmpty(line.Text) &&
                         line.Chords.Count == 0 &&
                         line.AltLyrics.Count == 0)
                {
                    _selectedSong.Lines.RemoveAt(active.LineIndex);
                }
            }

            ScoreSurface.Rebuild();
            UpdateViewportScale();
        }
        finally
        {
            _closingInlineLyricEditor = false;
            ScoreSurface?.Focus();
        }
    }

    private void HideInlineLyricOverlay()
    {
        _activeInlineLyric = null;
        if (ScoreSurface is not null)
        {
            ScoreSurface.ActiveInlineLyricEdit = null;
            ScoreSurface.InvalidateVisual();
        }
        if (InlineLyricOverlayCanvas is not null)
            InlineLyricOverlayCanvas.Visibility = Visibility.Collapsed;
    }

    private void InlineLyricBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.ImeProcessed)
            return;

        if (e.Key == Key.Escape)
        {
            CancelInlineLyricEdit();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Return)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                SplitInlineLyricAtCaret();
            else
                CommitInlineLyricEdit(refocusSurface: true);
            e.Handled = true;
            return;
        }

        if ((e.Key == Key.Up || e.Key == Key.Down) && Keyboard.Modifiers == ModifierKeys.None)
        {
            NavigateInlineLyricRow(e.Key == Key.Up ? -1 : 1);
            e.Handled = true;
        }
    }

    private void InlineLyricBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_closingInlineLyricEditor || !IsInlineLyricEditorOpen)
            return;

        if (e.NewFocus is DependencyObject next && InlineLyricEditorContainer is not null && IsElementInside(next, InlineLyricEditorContainer))
            return;

        CommitInlineLyricEdit(refocusSurface: false);
    }

    private void SplitInlineLyricAtCaret()
    {
        if (_closingInlineLyricEditor || _selectedSong is null || _activeInlineLyric is not { } active || InlineLyricBox is null)
            return;

        if (active.VerseIndex > 0)
        {
            NavigateInlineLyricRow(1);
            return;
        }

        var caret = InlineLyricBox.CaretIndex;
        var currentText = InlineLyricBox.Text ?? "";

        if (string.IsNullOrWhiteSpace(currentText))
        {
            CommitInlineLyricEdit(refocusSurface: true);
            return;
        }

        _closingInlineLyricEditor = true;
        int nextLineIndex;
        try
        {
            HideInlineLyricOverlay();
            nextLineIndex = LyricLineEditHelper.SplitLineAt(_selectedSong, active.LineIndex, currentText, caret);
            ScoreSurface.Rebuild();
            UpdateViewportScale();
            SaveLibraryQuietly();
            RefreshLibraryList();
            UpdatePartArrangeButtonCue();
            UpdateActionStates();
            SetStatus("已从光标处断句拆分为两行（后半句和弦已自动平移到新行）。");
        }
        finally
        {
            _closingInlineLyricEditor = false;
        }

        if (ScoreSurface.GetInlineLyricTarget(nextLineIndex, 0, 0) is { } nextTarget)
            OpenInlineLyricEditor(nextTarget);
    }

    private void NavigateInlineLyricRow(int delta)
    {
        if (_selectedSong is null || _activeInlineLyric is not { } active || InlineLyricBox is null)
            return;

        var preferredCharIdx = InlineLyricBox.CaretIndex;
        var wasEmptyRemoved = string.IsNullOrWhiteSpace(InlineLyricBox.Text);

        var targetsBefore = EnumerateEditableVersePairs(_selectedSong);
        var currentFlatIdx = targetsBefore.FindIndex(p => p == active);

        CommitInlineLyricEdit(refocusSurface: false);

        var targetsAfter = EnumerateEditableVersePairs(_selectedSong);
        if (targetsAfter.Count == 0)
        {
            ScoreSurface?.Focus();
            return;
        }

        int nextFlatIdx;
        if (currentFlatIdx < 0)
        {
            nextFlatIdx = delta > 0 ? 0 : targetsAfter.Count - 1;
        }
        else if (wasEmptyRemoved && targetsAfter.Count < targetsBefore.Count)
        {
            nextFlatIdx = delta > 0
                ? Math.Min(currentFlatIdx, targetsAfter.Count - 1)
                : Math.Max(0, currentFlatIdx - 1);
        }
        else
        {
            nextFlatIdx = currentFlatIdx + delta;
        }

        if (nextFlatIdx < 0 || nextFlatIdx >= targetsAfter.Count)
        {
            ScoreSurface?.Focus();
            return;
        }

        var next = targetsAfter[nextFlatIdx];
        if (ScoreSurface.GetInlineLyricTarget(next.LineIndex, next.VerseIndex, preferredCharIdx) is { } target)
            OpenInlineLyricEditor(target);
    }

    private void InlineAddAlt_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null || _activeInlineLyric is not { } active)
            return;

        var lineIndex = active.LineIndex;
        if (string.IsNullOrWhiteSpace(InlineLyricBox?.Text) && active.VerseIndex == 0)
            return;

        CommitInlineLyricEdit(refocusSurface: false);
        AddAltLyricInline(lineIndex);
    }

    private void AddAltLyricInline(int lineIndex)
    {
        if (_selectedSong is null || lineIndex < 0 || lineIndex >= _selectedSong.Lines.Count)
            return;

        CommitInlineLyricEdit(refocusSurface: false);
        if (lineIndex >= _selectedSong.Lines.Count)
            return;

        var line = _selectedSong.Lines[lineIndex];
        if (line.IsReferenceLine)
        {
            OpenPartArrangeDialog();
            return;
        }

        line.AltLyrics.Add("");
        var newVerseIdx = line.AltLyrics.Count;
        ScoreSurface.Rebuild();
        UpdateViewportScale();

        if (ScoreSurface.GetInlineLyricTarget(lineIndex, newVerseIdx, 0) is { } target)
            OpenInlineLyricEditor(target);
    }

    private void EnsureInlineEditorVisible(Rect surfaceBounds)
    {
        if (ScoreScrollViewer is null || ScoreSurface is null)
            return;

        try
        {
            var topLeftInViewer = ScoreSurface.TranslatePoint(new Point(surfaceBounds.Left, surfaceBounds.Top - 24), ScoreScrollViewer);
            var bottomRightInViewer = ScoreSurface.TranslatePoint(new Point(surfaceBounds.Right, surfaceBounds.Bottom + 42), ScoreScrollViewer);

            if (topLeftInViewer.Y < 0)
                ScoreScrollViewer.ScrollToVerticalOffset(Math.Max(0, ScoreScrollViewer.VerticalOffset + topLeftInViewer.Y - 16));
            else if (bottomRightInViewer.Y > ScoreScrollViewer.ViewportHeight)
                ScoreScrollViewer.ScrollToVerticalOffset(ScoreScrollViewer.VerticalOffset + (bottomRightInViewer.Y - ScoreScrollViewer.ViewportHeight) + 24);
        }
        catch
        {
            // Layout not yet ready; safe to ignore
        }
    }

    private static List<(int LineIndex, int VerseIndex)> EnumerateEditableVersePairs(SongModel song)
    {
        var result = new List<(int LineIndex, int VerseIndex)>();
        for (var i = 0; i < song.Lines.Count; i++)
        {
            var line = song.Lines[i];
            if (line.IsReferenceLine) continue;
            result.Add((i, 0));
            for (var v = 1; v <= line.AltLyrics.Count; v++)
                result.Add((i, v));
        }
        return result;
    }

    private static bool IsElementInside(DependencyObject? element, DependencyObject container)
    {
        var current = element;
        while (current is not null)
        {
            if (ReferenceEquals(current, container))
                return true;
            current = GetVisualOrLogicalParent(current);
        }
        return false;
    }
}
