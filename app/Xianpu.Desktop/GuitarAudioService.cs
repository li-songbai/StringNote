using System.IO;
using System.Runtime.InteropServices;

namespace Xianpu.Desktop;

/// <summary>
/// Zero-dependency acoustic guitar plucked-string synthesizer using Karplus-Strong physical modeling
/// played directly from pinned in-memory WAV buffers via winmm.dll PlaySound.
/// </summary>
public sealed class GuitarAudioService : IDisposable
{
    private const uint SndSync = 0x0000;
    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndMemory = 0x0004;
    private const int SampleRate = 44100;

    [DllImport("winmm.dll", SetLastError = false)]
    private static extern bool PlaySound(IntPtr pszSound, IntPtr hmod, uint fdwSound);

    private readonly object _lock = new();
    private byte[]? _currentWav;
    private GCHandle _currentHandle;
    private bool _disposed;

    public bool IsMuted { get; set; }
    public double Volume { get; set; } = 0.78;

    public void PlayNote(int midiNote, int durationMs = 1150)
    {
        if (_disposed || IsMuted) return;
        var wav = SynthesizeMixedNotes([(midiNote, 0, durationMs)], Volume);
        PlayWavBytes(wav);
    }

    public void PlayChord(IReadOnlyList<int> midiNotes, bool arpeggio = false)
    {
        if (_disposed || IsMuted || midiNotes.Count == 0) return;
        var stepMs = arpeggio ? 155 : 32;
        var tailMs = arpeggio ? 1100 : 1450;
        var events = new List<(int Midi, int OffsetMs, int DurationMs)>(midiNotes.Count);
        for (var i = 0; i < midiNotes.Count; i++)
            events.Add((midiNotes[i], i * stepMs, tailMs));

        var wav = SynthesizeMixedNotes(events, Volume);
        PlayWavBytes(wav);
    }

    public void PlayScaleSequence(IReadOnlyList<int> midiNotes, int stepMs = 200)
    {
        if (_disposed || IsMuted || midiNotes.Count == 0) return;
        var events = new List<(int Midi, int OffsetMs, int DurationMs)>(midiNotes.Count);
        for (var i = 0; i < midiNotes.Count; i++)
            events.Add((midiNotes[i], i * stepMs, 520));

        var wav = SynthesizeMixedNotes(events, Volume);
        PlayWavBytes(wav);
    }

    /// <summary>
    /// Plays a single real-time drill step mixing optional guitar pluck (with accent punch)
    /// and optional woodblock metronome click into a single WAV buffer so winmm PlaySound never cuts off either layer.
    /// </summary>
    public void PlayDrillStep(
        int? midiNote,
        bool isNoteAccent,
        bool playClick,
        bool isDownbeatClick,
        int durationMs = 420)
    {
        if (_disposed || IsMuted) return;
        if (midiNote is null && !playClick) return;

        var wav = SynthesizeDrillStepWav(midiNote, isNoteAccent, playClick, isDownbeatClick, durationMs, Volume);
        PlayWavBytes(wav);
    }

    public static byte[] SynthesizeDrillStepWav(
        int? midiNote,
        bool isNoteAccent,
        bool playClick,
        bool isDownbeatClick,
        int durationMs = 420,
        double masterVolume = 0.78)
    {
        var clampedMs = Math.Clamp(durationMs, 90, 1800);
        var totalSamples = (int)(SampleRate * clampedMs / 1000.0);
        var mix = new double[totalSamples];

        if (midiNote is { } midi)
        {
            var freq = 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);
            if (freq is >= 55.0 and <= 2200.0)
            {
                var gain = isNoteAccent ? 0.96 : 0.62;
                RenderKarplusStrongPluck(mix, 0, totalSamples, freq, gain);
                if (isNoteAccent)
                {
                    // Crisp pick attack transient for accented notes (>)
                    var transientLen = Math.Min(totalSamples, SampleRate * 12 / 1000);
                    for (var i = 0; i < transientLen; i++)
                    {
                        var env = Math.Exp(-i / (SampleRate * 0.0028));
                        mix[i] += Math.Sin(2.0 * Math.PI * freq * 2.0 * i / SampleRate) * 0.24 * env;
                    }
                }
            }
        }

        if (playClick)
        {
            // Clean woodblock click (1250 Hz accent beat 1, 880 Hz normal beat)
            var clickFreq = isDownbeatClick ? 1260.0 : 880.0;
            var clickGain = isDownbeatClick ? 0.56 : 0.36;
            var clickSamples = Math.Min(totalSamples, (int)(SampleRate * 0.032));
            for (var i = 0; i < clickSamples; i++)
            {
                var t = (double)i / SampleRate;
                var env = Math.Exp(-t * 145.0);
                var body = Math.Sin(2.0 * Math.PI * clickFreq * t) + 0.32 * Math.Sin(2.0 * Math.PI * clickFreq * 2.42 * t);
                mix[i] += body * clickGain * env;
            }
        }

        var peak = 0.001;
        for (var i = 0; i < totalSamples; i++)
        {
            var abs = Math.Abs(mix[i]);
            if (abs > peak) peak = abs;
        }
        var normGain = Math.Clamp(masterVolume, 0.0, 1.0) * (peak > 0.94 ? 0.94 / peak : 1.0);

        var fadeSamples = Math.Min(totalSamples, SampleRate * 12 / 1000);
        for (var i = 0; i < fadeSamples; i++)
            mix[totalSamples - fadeSamples + i] *= 1.0 - (double)i / fadeSamples;

        return EncodePcm16Wav(mix, normGain);
    }

    public void Stop()
    {
        lock (_lock)
        {
            try { PlaySound(IntPtr.Zero, IntPtr.Zero, SndSync); } catch { }
            ReleasePinned();
        }
    }

    private void PlayWavBytes(byte[] wav)
    {
        lock (_lock)
        {
            if (_disposed) return;
            try
            {
                // Stop previous sound cleanly before releasing its pinned buffer
                PlaySound(IntPtr.Zero, IntPtr.Zero, SndSync);
                ReleasePinned();
                _currentWav = wav;
                _currentHandle = GCHandle.Alloc(_currentWav, GCHandleType.Pinned);
                PlaySound(_currentHandle.AddrOfPinnedObject(), IntPtr.Zero, SndMemory | SndAsync | SndNoDefault);
            }
            catch
            {
                // Never crash UI on missing/busy audio devices
            }
        }
    }

    private void ReleasePinned()
    {
        if (_currentHandle.IsAllocated)
            _currentHandle.Free();
        _currentWav = null;
    }

    public static byte[] SynthesizeMixedNotes(
        IReadOnlyList<(int Midi, int OffsetMs, int DurationMs)> events,
        double masterVolume = 0.78)
    {
        var totalMs = 0;
        foreach (var ev in events)
        {
            var end = ev.OffsetMs + ev.DurationMs;
            if (end > totalMs) totalMs = end;
        }
        totalMs = Math.Clamp(totalMs, 120, 8000);
        var totalSamples = (int)(SampleRate * totalMs / 1000.0);
        var mix = new double[totalSamples];

        var perNoteGain = events.Count <= 1 ? 0.85 : Math.Min(0.55, 1.35 / Math.Sqrt(events.Count));

        foreach (var ev in events)
        {
            var freq = 440.0 * Math.Pow(2.0, (ev.Midi - 69) / 12.0);
            if (freq < 55.0 || freq > 2200.0) continue;

            var startSample = Math.Max(0, (int)(SampleRate * ev.OffsetMs / 1000.0));
            var lenSamples = Math.Min(totalSamples - startSample, (int)(SampleRate * ev.DurationMs / 1000.0));
            if (lenSamples <= 16) continue;

            RenderKarplusStrongPluck(mix, startSample, lenSamples, freq, perNoteGain);
        }

        // Normalize softly to prevent clipping
        var peak = 0.001;
        for (var i = 0; i < totalSamples; i++)
        {
            var abs = Math.Abs(mix[i]);
            if (abs > peak) peak = abs;
        }
        var normGain = Math.Clamp(masterVolume, 0.0, 1.0) * (peak > 0.92 ? 0.92 / peak : 1.0);

        // Soft fade-out on final 15ms to guarantee zero click
        var fadeSamples = Math.Min(totalSamples, SampleRate * 15 / 1000);
        for (var i = 0; i < fadeSamples; i++)
        {
            var factor = 1.0 - (double)i / fadeSamples;
            mix[totalSamples - fadeSamples + i] *= factor;
        }

        return EncodePcm16Wav(mix, normGain);
    }

    private static void RenderKarplusStrongPluck(double[] mix, int start, int length, double freq, double gain)
    {
        var delayLen = Math.Max(2, (int)Math.Round(SampleRate / freq));
        var ring = new double[delayLen];

        // Deterministic mellow acoustic pick burst (warm low-pass filtered noise + soft fundamental)
        var rng = new Random(delayLen * 7919 + 13);
        var prev = 0.0;
        for (var i = 0; i < delayLen; i++)
        {
            var white = rng.NextDouble() * 2.0 - 1.0;
            var fundamental = Math.Sin(2.0 * Math.PI * i / delayLen) * 0.35;
            prev = prev * 0.45 + (white * 0.65 + fundamental) * 0.55;
            ring[i] = prev;
        }

        // Remove DC offset from initial burst
        var mean = 0.0;
        for (var i = 0; i < delayLen; i++) mean += ring[i];
        mean /= delayLen;
        for (var i = 0; i < delayLen; i++) ring[i] -= mean;

        // Higher strings decay slightly faster like a real acoustic guitar
        var damping = freq > 350 ? 0.9945 : (freq > 180 ? 0.9962 : 0.9974);
        var idx = 0;

        for (var n = 0; n < length; n++)
        {
            var sample = ring[idx];
            var nextIdx = (idx + 1) % delayLen;
            // Two-point averaging low-pass filter gives realistic warm nylon/steel decay
            ring[idx] = 0.5 * (sample + ring[nextIdx]) * damping;

            var attack = n < 45 ? n / 45.0 : 1.0;
            mix[start + n] += sample * gain * attack;
            idx = nextIdx;
        }
    }

    private static byte[] EncodePcm16Wav(double[] samples, double gain)
    {
        var dataSize = samples.Length * 2;
        using var ms = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(ms);

        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write((short)1); // Mono
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataSize);

        for (var i = 0; i < samples.Length; i++)
        {
            var clamped = Math.Clamp(samples[i] * gain, -1.0, 1.0);
            writer.Write((short)(clamped * 32767.0));
        }

        return ms.ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
