using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Xianpu.Desktop;

internal sealed class MetronomeService : IDisposable
{
    private const uint SndSync = 0x0000;
    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndMemory = 0x0004;

    [DllImport("winmm.dll", SetLastError = false)]
    private static extern bool PlaySound(byte[]? pszSound, IntPtr hmod, uint fdwSound);

    private readonly byte[] _accentClickWav;
    private readonly byte[] _normalClickWav;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();

    private int _bpm = 80;
    private int _beatsPerBar = 4;
    private int _currentBeat;
    private double _nextBeatMs;
    private bool _running;
    private bool _disposed;

    public event Action<int, int>? BeatTick;

    public bool IsRunning => _running;

    public int Bpm
    {
        get => _bpm;
        set
        {
            var clamped = Math.Clamp(value, 30, 240);
            if (_bpm == clamped) return;
            _bpm = clamped;
            if (_running)
            {
                // Reschedule next beat cleanly from now without abrupt double-hit
                _nextBeatMs = _clock.Elapsed.TotalMilliseconds + BeatIntervalMs;
            }
        }
    }

    public int BeatsPerBar
    {
        get => _beatsPerBar;
        set
        {
            var clamped = value is 2 or 3 or 4 or 6 ? value : 4;
            _beatsPerBar = clamped;
            if (_currentBeat > _beatsPerBar)
                _currentBeat = 0;
        }
    }

    private double BeatIntervalMs => 60000.0 / _bpm;

    public MetronomeService()
    {
        _accentClickWav = BuildWoodblockWav(frequencyHz: 1120.0, durationMs: 28, amplitude: 0.72);
        _normalClickWav = BuildWoodblockWav(frequencyHz: 780.0, durationMs: 24, amplitude: 0.52);

        _timer = new DispatcherTimer(DispatcherPriority.Send)
        {
            Interval = TimeSpan.FromMilliseconds(8)
        };
        _timer.Tick += OnTimerTick;
    }

    public void Start(int bpm, int beatsPerBar = 4)
    {
        if (_disposed) return;
        _bpm = Math.Clamp(bpm <= 0 ? 80 : bpm, 30, 240);
        _beatsPerBar = beatsPerBar is 2 or 3 or 4 or 6 ? beatsPerBar : 4;
        _currentBeat = 0;
        _running = true;
        _clock.Restart();
        _nextBeatMs = 0;
        TriggerBeat();
        _nextBeatMs = BeatIntervalMs;
        _timer.Start();
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _timer.Stop();
        _clock.Stop();
        _currentBeat = 0;
        try
        {
            PlaySound(null, IntPtr.Zero, SndSync);
        }
        catch
        {
            // Ignore audio stop errors
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_running || _disposed) return;

        var elapsed = _clock.Elapsed.TotalMilliseconds;
        if (elapsed + 2.0 < _nextBeatMs)
            return;

        TriggerBeat();

        var interval = BeatIntervalMs;
        _nextBeatMs += interval;
        if (_nextBeatMs < elapsed)
            _nextBeatMs = elapsed + interval;
    }

    private void TriggerBeat()
    {
        _currentBeat = (_currentBeat % _beatsPerBar) + 1;
        var wav = _currentBeat == 1 ? _accentClickWav : _normalClickWav;
        try
        {
            PlaySound(wav, IntPtr.Zero, SndMemory | SndAsync | SndNoDefault);
        }
        catch
        {
            // Ignore audio device errors so practice mode never crashes
        }
        BeatTick?.Invoke(_currentBeat, _beatsPerBar);
    }

    private static byte[] BuildWoodblockWav(double frequencyHz, int durationMs, double amplitude)
    {
        const int sampleRate = 44100;
        const short bitsPerSample = 16;
        const short channels = 1;
        var sampleCount = (int)(sampleRate * durationMs / 1000.0);
        var dataSize = sampleCount * channels * (bitsPerSample / 8);

        using var ms = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(ms);

        // RIFF WAV header
        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * (bitsPerSample / 8));
        writer.Write((short)(channels * (bitsPerSample / 8)));
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataSize);

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (double)sampleRate;
            var attack = i < 36 ? i / 36.0 : 1.0;
            var decay = Math.Exp(-t * 155.0);
            var tone = Math.Sin(2.0 * Math.PI * frequencyHz * t) * 0.78
                       + Math.Sin(2.0 * Math.PI * frequencyHz * 2.04 * t) * 0.22;
            var sample = Math.Clamp(tone * attack * decay * amplitude, -1.0, 1.0);
            writer.Write((short)(sample * short.MaxValue));
        }

        return ms.ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _timer.Tick -= OnTimerTick;
    }
}
