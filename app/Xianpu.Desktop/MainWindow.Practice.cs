using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Xianpu.Desktop;

public partial class MainWindow
{
    private bool _libraryWasOpenBeforePractice = true;

    private void BpmBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        _vm.CommitBpmText();
        _headerSaveDebounceTimer.Stop();
        SaveLibraryQuietly();
    }

    private void PrintPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        CommitFields();

        ChordPopover.IsOpen = false;
        StopAutoScroll(clearSession: false);

        try
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true)
                return;

            var pageSize = new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
            var paginator = ScoreSurface.CreatePrintPaginator(pageSize);
            try
            {
                var title = string.IsNullOrWhiteSpace(_selectedSong.Title) ? "弦笺曲谱" : _selectedSong.Title.Trim();
                dialog.PrintDocument(paginator, $"弦笺 - {title}");
                SetStatus($"已发送打印 / PDF 任务（共 {paginator.PageCount} 页）：{title}");
            }
            finally
            {
                ScoreSurface.EndPrintSession();
            }
        }
        catch (Exception ex)
        {
            AppDialogs.ShowError(this, "打印 / 导出 PDF 失败", ex.Message);
        }
    }

    private void ImportMenu_Click(object sender, RoutedEventArgs e)
    {
        if (ImportMenuButton is null) return;
        var menu = new ContextMenu { PlacementTarget = ImportMenuButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };

        var lrcItem = new MenuItem { Header = "导入 LRC / 文本歌词 (.lrc / .txt)..." };
        lrcItem.Click += ImportLrc_Click;
        menu.Items.Add(lrcItem);

        var aiItem = new MenuItem { Header = "✨ AI 识谱导入（弹唱谱 / 指弹谱）..." };
        aiItem.Click += AiImport_Click;
        menu.Items.Add(aiItem);

        var projectItem = new MenuItem { Header = "导入好友分享谱子 (.xianpu.json)..." };
        projectItem.Click += OpenProject_Click;
        menu.Items.Add(projectItem);

        menu.Items.Add(new Separator());

        var newItem = new MenuItem { Header = "+ 新建空白曲谱" };
        newItem.Click += NewSong_Click;
        menu.Items.Add(newItem);

        menu.IsOpen = true;
    }

    private void ArrangeMenu_Click(object sender, RoutedEventArgs e)
    {
        if (PartArrangeButton is null) return;
        var menu = new ContextMenu { PlacementTarget = PartArrangeButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };

        var editLyricsItem = new MenuItem { Header = "批量编辑整首歌词（保留和弦）..." };
        editLyricsItem.Click += EditAllLyrics_Click;
        menu.Items.Add(editLyricsItem);

        var partItem = new MenuItem { Header = "段落编排 / 多段叠行歌词..." };
        partItem.Click += PartArrange_Click;
        menu.Items.Add(partItem);

        menu.Items.Add(new Separator());

        var tabItem = new MenuItem { Header = "+ 添加 / 管理六线谱 (TAB)..." };
        tabItem.Click += TabSectionMenu_Click;
        menu.Items.Add(tabItem);

        var transposeItem = new MenuItem { Header = "按调名批量移调..." };
        transposeItem.Click += TransposeDialog_Click;
        menu.Items.Add(transposeItem);

        menu.IsOpen = true;
    }

    private void ExportMenu_Click(object sender, RoutedEventArgs e)
    {
        if (ExportMenuButton is null) return;
        var menu = new ContextMenu { PlacementTarget = ExportMenuButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };

        var pngItem = new MenuItem { Header = "🖼 导出高清图片 (PNG)..." };
        pngItem.Click += ExportPng_Click;
        menu.Items.Add(pngItem);

        var pdfItem = new MenuItem { Header = "🖨 打印 / 导出 PDF (A4 分页)..." };
        pdfItem.Click += PrintPdf_Click;
        menu.Items.Add(pdfItem);

        menu.Items.Add(new Separator());

        var shareItem = new MenuItem { Header = "📤 导出可编辑谱子 (.xianpu.json)..." };
        shareItem.Click += SaveProject_Click;
        menu.Items.Add(shareItem);

        menu.IsOpen = true;
    }

    private void ChordOpsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (ChordOpsMenuButton is null) return;
        var menu = new ContextMenu { PlacementTarget = ChordOpsMenuButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };

        var copyItem = new MenuItem { Header = "复制当前句和弦 (Ctrl+C)", IsEnabled = ScoreSurface.SelectedLineHasChords };
        copyItem.Click += CopyLineChords_Click;
        menu.Items.Add(copyItem);

        var canPaste = ScoreSurface.HasCopiedLineChords &&
                       ScoreSurface.ActiveLineIndex is { } lineIdx &&
                       _selectedSong is not null &&
                       lineIdx >= 0 &&
                       lineIdx < _selectedSong.Lines.Count &&
                       !_selectedSong.Lines[lineIdx].IsReferenceLine;
        var pasteItem = new MenuItem { Header = "粘贴句和弦 (Ctrl+V)", IsEnabled = canPaste };
        pasteItem.Click += PasteLineChords_Click;
        menu.Items.Add(pasteItem);

        menu.Items.Add(new Separator());

        var removeItem = new MenuItem { Header = "删除选中和弦 (Delete)", IsEnabled = ScoreSurface.HasSelectedChord };
        removeItem.Click += RemoveChord_Click;
        menu.Items.Add(removeItem);

        var clearItem = new MenuItem { Header = "清空整首歌词全部和弦..." };
        clearItem.Click += ClearAllChords_Click;
        menu.Items.Add(clearItem);

        menu.IsOpen = true;
    }

    private void ApplyPracticeMode(bool isPractice)
    {
        CommitInlineLyricEdit(refocusSurface: false);
        ChordPopover.IsOpen = false;

        if (isPractice)
        {
            _libraryWasOpenBeforePractice = LibraryToggle?.IsChecked == true;
            if (LibraryToggle is not null && LibraryToggle.IsChecked == true)
                LibraryToggle.IsChecked = false;
            if (ContinuousPaintToggle is not null && ContinuousPaintToggle.IsChecked == true)
                ContinuousPaintToggle.IsChecked = false;
            if (LyricEditModeToggle is not null && LyricEditModeToggle.IsChecked == true)
                LyricEditModeToggle.IsChecked = false;
        }
        else
        {
            if (LibraryToggle is not null && _libraryWasOpenBeforePractice && LibraryToggle.IsChecked != true)
                LibraryToggle.IsChecked = true;
        }

        if (FloatingChordBar is not null)
            FloatingChordBar.Visibility = isPractice ? Visibility.Collapsed : Visibility.Visible;
        if (InlineTransposeControls is not null)
            InlineTransposeControls.Visibility = isPractice ? Visibility.Collapsed : Visibility.Visible;
        if (PracticeFloatingDock is not null)
            PracticeFloatingDock.Visibility = isPractice ? Visibility.Visible : Visibility.Collapsed;

        if (EditModeButton is not null && PracticeModeButton is not null)
        {
            EditModeButton.Background = !isPractice ? Brushes.White : Brushes.Transparent;
            EditModeButton.Foreground = !isPractice ? AppDialogs.TitleBrush : AppDialogs.MutedBrush;
            EditModeButton.FontWeight = !isPractice ? FontWeights.SemiBold : FontWeights.Normal;

            PracticeModeButton.Background = isPractice ? Brushes.White : Brushes.Transparent;
            PracticeModeButton.Foreground = isPractice ? AppDialogs.AccentHoverBrush : AppDialogs.MutedBrush;
            PracticeModeButton.FontWeight = isPractice ? FontWeights.SemiBold : FontWeights.Normal;
        }

        UpdateViewportScale();
        SetStatus(isPractice
            ? "已进入「练琴专注模式」（按 F5 可随时切回编辑模式，Space 控制滚屏暂停/继续）。"
            : "已切回「编辑制谱模式」。");
    }

    private void DockToggleScroll_Click(object sender, RoutedEventArgs e)
    {
        if (AutoScrollToggle is not null)
            AutoScrollToggle.IsChecked = !(AutoScrollToggle.IsChecked == true);
    }

    private void DockToggleMetronome_Click(object sender, RoutedEventArgs e)
    {
        if (MetronomeToggle is not null)
            MetronomeToggle.IsChecked = !(MetronomeToggle.IsChecked == true);
    }

    private void AutoScrollToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (AutoScrollToggle is null) return;
        var active = AutoScrollToggle.IsChecked == true;
        if (active)
        {
            if (ScoreScrollViewer is null || ScoreScrollViewer.ScrollableHeight <= 1.0)
            {
                AutoScrollToggle.IsChecked = false;
                SetStatus("当前曲谱已完整显示在视野内，无需滚屏。");
                return;
            }

            if (ScoreScrollViewer.VerticalOffset >= ScoreScrollViewer.ScrollableHeight - 1.0)
                ScoreScrollViewer.ScrollToTop();

            _autoScrollEngaged = true;
            _autoScrollClock.Restart();
            _autoScrollTimer.Start();
            _vm.Practice.SetAutoScrollState(true);
            AutoScrollToggle.Background = AppDialogs.AccentLightBrush;
            AutoScrollToggle.BorderBrush = AppDialogs.AccentBrush;
            AutoScrollToggle.Foreground = AppDialogs.AccentHoverBrush;
            SetStatus("已开启自动滚屏（按 Space 空格键可随时暂停/继续）。");
        }
        else
        {
            _autoScrollTimer.Stop();
            _autoScrollClock.Stop();
            _vm.Practice.SetAutoScrollState(false);
            AutoScrollToggle.Background = Brushes.White;
            AutoScrollToggle.BorderBrush = AppDialogs.InputBorderBrush;
            AutoScrollToggle.Foreground = AppDialogs.BodyBrush;
            if (_autoScrollEngaged)
                SetStatus("自动滚屏已暂停（按 Space 空格键可继续）。");
        }
    }

    private void StopAutoScroll(bool clearSession)
    {
        _autoScrollTimer.Stop();
        _autoScrollClock.Stop();
        if (clearSession)
            _autoScrollEngaged = false;
        if (AutoScrollToggle is not null && AutoScrollToggle.IsChecked == true)
            AutoScrollToggle.IsChecked = false;
    }

    private void OnAutoScrollTick(object? sender, EventArgs e)
    {
        if (ScoreScrollViewer is null || AutoScrollToggle?.IsChecked != true)
        {
            _autoScrollTimer.Stop();
            return;
        }

        var dt = _autoScrollClock.Elapsed.TotalSeconds;
        _autoScrollClock.Restart();
        if (dt <= 0 || dt > 0.5)
            return;

        var speedPxPerSec = 38.0;
        if (AutoScrollSpeedBox?.SelectedValue is string speedTag &&
            double.TryParse(speedTag, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedSpeed))
        {
            speedPxPerSec = parsedSpeed;
        }

        var nextOffset = ScoreScrollViewer.VerticalOffset + speedPxPerSec * dt;
        if (nextOffset >= ScoreScrollViewer.ScrollableHeight)
        {
            ScoreScrollViewer.ScrollToVerticalOffset(ScoreScrollViewer.ScrollableHeight);
            StopAutoScroll(clearSession: true);
            SetStatus("已滚动至曲谱末尾，自动滚屏已停止。");
            return;
        }

        ScoreScrollViewer.ScrollToVerticalOffset(nextOffset);
    }

    private void MetronomeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (MetronomeToggle is null) return;
        var active = MetronomeToggle.IsChecked == true;
        if (active)
        {
            var bpm = _selectedSong is { Bpm: >= 30 and <= 240 } ? _selectedSong.Bpm : 80;
            var beats = _vm.Practice.CurrentBeatsPerBar;
            var label = _vm.Practice.MetronomeMeterLabel;
            _metronome.Start(bpm, beats);
            _vm.Practice.SetMetronomeRunning(bpm);
            MetronomeToggle.Background = AppDialogs.AccentLightBrush;
            MetronomeToggle.BorderBrush = AppDialogs.AccentBrush;
            MetronomeToggle.Foreground = AppDialogs.AccentHoverBrush;
            SetStatus($"节拍器已开启：{bpm} BPM（{label} 拍）。");
        }
        else
        {
            _metronome.Stop();
            _vm.Practice.SetMetronomeStopped();
            MetronomeToggle.Background = Brushes.White;
            MetronomeToggle.BorderBrush = AppDialogs.InputBorderBrush;
            MetronomeToggle.Foreground = AppDialogs.BodyBrush;
            SetStatus("节拍器已停止。");
        }
    }

    private void MetronomeMeter_Click(object sender, RoutedEventArgs e)
    {
        var (label, beats) = _vm.Practice.CycleMeter();
        _metronome.BeatsPerBar = beats;
        SetStatus($"节拍器拍号已切换为 {label}。");
    }

    private void OnMetronomeBeatTick(int beat, int beatsPerBar)
    {
        _vm.Practice.UpdateBeat(beat, beatsPerBar, _metronome.Bpm);
        if (MetronomeBeatText is not null)
            MetronomeBeatText.Foreground = beat == 1 ? AppDialogs.AccentBrush : AppDialogs.MutedBrush;
    }
}
