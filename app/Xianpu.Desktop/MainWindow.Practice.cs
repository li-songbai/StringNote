using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Xianpu.Desktop;

public partial class MainWindow
{
    private void BpmBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null || BpmBox is null) return;
        var text = BpmBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            _selectedSong.Bpm = 0;
            return;
        }
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 30 and <= 240)
        {
            _selectedSong.Bpm = parsed;
            if (_metronome.IsRunning)
                _metronome.Bpm = parsed;
        }
    }

    private void BpmBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_initializing || _selectedSong is null || BpmBox is null) return;
        var text = BpmBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            _selectedSong.Bpm = 0;
        }
        else if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            var clamped = Math.Clamp(parsed, 30, 240);
            _selectedSong.Bpm = clamped;
            _initializing = true;
            BpmBox.Text = clamped.ToString(CultureInfo.InvariantCulture);
            _initializing = false;
            if (_metronome.IsRunning)
                _metronome.Bpm = clamped;
        }
        else
        {
            _initializing = true;
            BpmBox.Text = _selectedSong.Bpm > 0 ? _selectedSong.Bpm.ToString(CultureInfo.InvariantCulture) : "";
            _initializing = false;
        }
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
            AutoScrollToggle.Content = "⏸ 暂停";
            AutoScrollToggle.Background = AppDialogs.AccentLightBrush;
            AutoScrollToggle.BorderBrush = AppDialogs.AccentBrush;
            AutoScrollToggle.Foreground = AppDialogs.AccentHoverBrush;
            SetStatus("已开启自动滚屏（按 Space 空格键可随时暂停/继续）。");
        }
        else
        {
            _autoScrollTimer.Stop();
            _autoScrollClock.Stop();
            AutoScrollToggle.Content = "▶ 滚屏";
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
            var (label, beats) = MetronomeMeters[_metronomeMeterIndex];
            _metronome.Start(bpm, beats);
            MetronomeToggle.Content = $"■ {bpm}";
            MetronomeToggle.Background = AppDialogs.AccentLightBrush;
            MetronomeToggle.BorderBrush = AppDialogs.AccentBrush;
            MetronomeToggle.Foreground = AppDialogs.AccentHoverBrush;
            SetStatus($"节拍器已开启：{bpm} BPM（{label} 拍）。");
        }
        else
        {
            _metronome.Stop();
            MetronomeToggle.Content = "♩ 节拍器";
            MetronomeToggle.Background = Brushes.White;
            MetronomeToggle.BorderBrush = AppDialogs.InputBorderBrush;
            MetronomeToggle.Foreground = AppDialogs.BodyBrush;
            if (MetronomeBeatText is not null)
                MetronomeBeatText.Text = "";
            SetStatus("节拍器已停止。");
        }
    }

    private void MetronomeMeter_Click(object sender, RoutedEventArgs e)
    {
        _metronomeMeterIndex = (_metronomeMeterIndex + 1) % MetronomeMeters.Length;
        var (label, beats) = MetronomeMeters[_metronomeMeterIndex];
        if (MetronomeMeterButton is not null)
            MetronomeMeterButton.Content = label;
        _metronome.BeatsPerBar = beats;
        SetStatus($"节拍器拍号已切换为 {label}。");
    }

    private void OnMetronomeBeatTick(int beat, int beatsPerBar)
    {
        if (MetronomeBeatText is null) return;
        MetronomeBeatText.Text = $"● {beat}/{beatsPerBar}";
        MetronomeBeatText.Foreground = beat == 1 ? AppDialogs.AccentBrush : AppDialogs.MutedBrush;
        if (MetronomeToggle is { IsChecked: true })
            MetronomeToggle.Content = $"■ {_metronome.Bpm}";
    }
}
