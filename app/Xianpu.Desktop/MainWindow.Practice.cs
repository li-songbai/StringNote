using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Xianpu.Desktop;

public partial class MainWindow
{
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

        ChordPopover.IsOpen = false;
        StopAutoScroll(clearSession: false);

        try
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true)
                return;

            var pageSize = new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
            var paginator = ScoreSurface.CreatePrintPaginator(pageSize);
            var title = string.IsNullOrWhiteSpace(_selectedSong.Title) ? "弦笺曲谱" : _selectedSong.Title.Trim();
            dialog.PrintDocument(paginator, $"弦笺 - {title}");
            SetStatus($"已发送打印 / PDF 任务（共 {paginator.PageCount} 页）：{title}");
        }
        catch (Exception ex)
        {
            AppDialogs.ShowError(this, "打印 / 导出 PDF 失败", ex.Message);
        }
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
