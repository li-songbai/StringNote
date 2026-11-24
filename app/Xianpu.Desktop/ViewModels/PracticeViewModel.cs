using CommunityToolkit.Mvvm.ComponentModel;

namespace Xianpu.Desktop.ViewModels;

/// <summary>
/// Reactive state for bottom practice & viewport controls (auto-scroll, metronome, zoom, status).
/// </summary>
public sealed partial class PracticeViewModel : ObservableObject
{
    private static readonly (string Label, int Beats)[] Meters =
    [
        ("4/4", 4),
        ("3/4", 3),
        ("6/8", 6)
    ];

    private int _meterIndex;

    [ObservableProperty]
    private bool _isAutoScrollActive;

    [ObservableProperty]
    private string _autoScrollButtonText = "▶ 滚屏";

    [ObservableProperty]
    private bool _isMetronomeActive;

    [ObservableProperty]
    private string _metronomeButtonText = "♩ 节拍器";

    [ObservableProperty]
    private string _metronomeMeterLabel = "4/4";

    [ObservableProperty]
    private string _metronomeBeatText = "";

    [ObservableProperty]
    private bool _isAccentBeat;

    [ObservableProperty]
    private string _zoomPercentText = "适应";

    [ObservableProperty]
    private string _statusMessage = "提示：点击歌词「进唱字位」输入和弦；空白处拖拽可移动页面；顶部可拖动窗口，右下可调整大小。";

    public int CurrentBeatsPerBar => Meters[_meterIndex].Beats;

    public (string Label, int Beats) CycleMeter()
    {
        _meterIndex = (_meterIndex + 1) % Meters.Length;
        var current = Meters[_meterIndex];
        MetronomeMeterLabel = current.Label;
        return current;
    }

    public void UpdateBeat(int beat, int beatsPerBar, int bpm)
    {
        MetronomeBeatText = $"● {beat}/{beatsPerBar}";
        IsAccentBeat = beat == 1;
        if (IsMetronomeActive)
            MetronomeButtonText = $"■ {bpm}";
    }

    public void SetMetronomeStopped()
    {
        IsMetronomeActive = false;
        MetronomeButtonText = "♩ 节拍器";
        MetronomeBeatText = "";
        IsAccentBeat = false;
    }

    public void SetMetronomeRunning(int bpm)
    {
        IsMetronomeActive = true;
        MetronomeButtonText = $"■ {bpm}";
    }

    public void SetAutoScrollState(bool active)
    {
        IsAutoScrollActive = active;
        AutoScrollButtonText = active ? "⏸ 暂停" : "▶ 滚屏";
    }
}
