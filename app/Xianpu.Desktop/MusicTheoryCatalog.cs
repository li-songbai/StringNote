using System.Text.RegularExpressions;

namespace Xianpu.Desktop;

public sealed record GuitarTuning(string Id, string Name, string Category, int[] OpenMidi);

public sealed record ScaleDefinition(
    string Id,
    string Name,
    string Category,
    int[] Intervals,
    bool IsMinorFamily,
    int[] TriadIntervals);

public sealed record CagedBoxInstance(
    string ShapeLetter,      // "C", "A", "G", "E", "D"
    string ColorHex,
    int OctaveOffset,
    int MinFret,
    int MaxFret,
    int[] LeftFrets,         // length 6 (string 0=6E .. 5=1e), left pentatonic note fret
    int[] RightFrets,        // length 6, right pentatonic note fret
    int?[] TriadFrets);      // length 6, embedded triad shape

public sealed record DiatonicChordInfo(
    int DegreeNumber,        // 1..7
    string RomanNumeral,     // I, ii, iii, IV, V, vi, vii°
    string RootName,
    string TriadSymbol,
    string SeventhSymbol,
    int RootPitch);

public sealed record ChordStringDecode(
    int StringIndex,         // 0=6E .. 5=1e
    int? Fret,
    string IntervalLabel,    // R, 3, 5, b7 ... or "—"
    string IntervalRole,     // "root", "third", "fifth", "color", "mute"
    string ScaleDegreeLabel, // 1..7 or "—"
    string NoteWithOctave);  // e.g. "C3" or "×"

public sealed record CagedChordVoicing(
    ChordShape Shape,
    string? CagedLetter,     // "C" | "A" | "G" | "E" | "D" | null
    string BadgeText,        // e.g. "C 形" or "把位 1"
    string PositionHint,     // e.g. "开放" or "8品"
    string ColorHex);        // CAGED signature color or Pine Green

public sealed record ProgressionStep(string ChordName, string Roman, int Beats = 4);

public sealed record ProgressionPreset(string Id, string Name, string Category, string KeyName, int Bpm, List<ProgressionStep> Steps);

public sealed record ProgressionSuggestion(string Label, string Reason, List<string> Chords);

public sealed record TheoryLessonStep(
    string StepTitle,
    string Body,
    int TonicPitch,
    string ScaleId,
    bool ShowCaged,
    string CagedFilter,      // "ALL", "C", "A", "G", "E", "D", or ""
    bool ShowCapsules,
    string HighlightChord = "",
    int Capo = 0,
    string TuningId = "standard");

public sealed record TheoryLesson(
    string Id,
    string UnitName,
    string Title,
    string Summary,
    List<TheoryLessonStep> Steps);

public static class MusicTheoryCatalog
{
    private static readonly string[] SharpNotes = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    private static readonly string[] FlatNotes = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];

    public static readonly string[] TonicLabels = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    private static readonly string[] DegreeLabels = ["1", "b2", "2", "b3", "3", "4", "#4", "5", "b6", "6", "b7", "7"];
    private static readonly string[] IntervalLabels = ["R", "b2", "2", "b3", "3", "4", "b5", "5", "#5", "6", "b7", "7"];

    public static readonly IReadOnlyList<GuitarTuning> Tunings =
    [
        new("standard", "标准调弦 (EADGBE)", "标准", [40, 45, 50, 55, 59, 64]),
        new("eb", "降半音 (Eb Ab Db Gb Bb Eb)", "降音", [39, 44, 49, 54, 58, 63]),
        new("d_std", "降全音 (DGCFAD)", "降音", [38, 43, 48, 53, 57, 62]),
        new("drop_d", "Drop D (DADGBE)", "Drop", [38, 45, 50, 55, 59, 64]),
        new("drop_c", "Drop C (CGCFAD)", "Drop", [36, 43, 48, 53, 57, 62]),
        new("open_d", "Open D (DADF#AD)", "开放", [38, 45, 50, 54, 57, 62]),
        new("open_g", "Open G (DGDGBD)", "开放", [38, 43, 50, 55, 59, 62]),
        new("dadgad", "DADGAD", "特殊", [38, 45, 50, 55, 57, 62]),
        new("facgce", "FACGCE (中西部情绪)", "特殊", [41, 45, 48, 55, 60, 64])
    ];

    public static readonly IReadOnlyList<ScaleDefinition> Scales =
    [
        // 常用
        new("major", "自然大调 (Major)", "常用", [0, 2, 4, 5, 7, 9, 11], false, [0, 4, 7]),
        new("natural_minor", "自然小调 (Natural Minor)", "常用", [0, 2, 3, 5, 7, 8, 10], true, [0, 3, 7]),
        new("major_pent", "大调五声音阶 (Major Pentatonic)", "常用", [0, 2, 4, 7, 9], false, [0, 4, 7]),
        new("minor_pent", "小调五声音阶 (Minor Pentatonic)", "常用", [0, 3, 5, 7, 10], true, [0, 3, 7]),
        new("major_blues", "大调 Blues", "常用", [0, 2, 3, 4, 7, 9], false, [0, 4, 7]),
        new("minor_blues", "小调 Blues", "常用", [0, 3, 5, 6, 7, 10], true, [0, 3, 7]),
        // 调式
        new("dorian", "多利亚 (Dorian)", "调式", [0, 2, 3, 5, 7, 9, 10], true, [0, 3, 7]),
        new("phrygian", "弗里吉亚 (Phrygian)", "调式", [0, 1, 3, 5, 7, 8, 10], true, [0, 3, 7]),
        new("lydian", "利底亚 (Lydian)", "调式", [0, 2, 4, 6, 7, 9, 11], false, [0, 4, 7]),
        new("mixolydian", "混合利底亚 (Mixolydian)", "调式", [0, 2, 4, 5, 7, 9, 10], false, [0, 4, 7]),
        new("locrian", "洛克里亚 (Locrian)", "调式", [0, 1, 3, 5, 6, 8, 10], true, [0, 3, 6]),
        // 和声与旋律小调
        new("harmonic_minor", "和声小调 (Harmonic Minor)", "和声/旋律小调", [0, 2, 3, 5, 7, 8, 11], true, [0, 3, 7]),
        new("melodic_minor", "爵士旋律小调 (Melodic Minor)", "和声/旋律小调", [0, 2, 3, 5, 7, 9, 11], true, [0, 3, 7]),
        new("phrygian_dom", "弗里吉亚属音阶", "和声/旋律小调", [0, 1, 4, 5, 7, 8, 10], false, [0, 4, 7]),
        new("lydian_dom", "利底亚属音阶", "和声/旋律小调", [0, 2, 4, 6, 7, 9, 10], false, [0, 4, 7]),
        new("altered", "变化属音阶 (Altered)", "和声/旋律小调", [0, 1, 3, 4, 6, 8, 10], false, [0, 4, 10]),
        // 对称与其他
        new("whole_tone", "全音音阶 (Whole Tone)", "对称/其他", [0, 2, 4, 6, 8, 10], false, [0, 4, 8]),
        new("wh_dim", "全半减音阶", "对称/其他", [0, 2, 3, 5, 6, 8, 9, 11], true, [0, 3, 6]),
        new("hw_dim", "半全减音阶", "对称/其他", [0, 1, 3, 4, 6, 7, 9, 10], false, [0, 4, 7]),
        new("chromatic", "半音阶 (Chromatic)", "对称/其他", [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11], false, [0, 4, 7])
    ];

    // Base C Major CAGED shapes on Standard tuning (string 0 = 6E .. string 5 = 1e)
    // Each shape's RightFrets coincides with the next shape's LeftFrets, creating interlocking C-A-G-E-D puzzle boxes!
    private static readonly (string Letter, string ColorHex, int[] LeftFrets, int[] RightFrets, int?[] TriadFrets)[] BaseCMajorCaged =
    [
        ("C", "#7C9A46", [0, 0, 0, 0, 1, 0], [3, 3, 2, 2, 3, 3], [null, 3, 2, 0, 1, 0]),
        ("A", "#D96B8A", [3, 3, 2, 2, 3, 3], [5, 5, 5, 5, 5, 5], [null, 3, 5, 5, 5, 3]),
        ("G", "#8B74C4", [5, 5, 5, 5, 5, 5], [8, 7, 7, 7, 8, 8], [8, 7, 5, 5, 5, 8]),
        ("E", "#4A8FD2", [8, 7, 7, 7, 8, 8], [10, 10, 10, 9, 10, 10], [8, 10, 10, 9, 8, 8]),
        ("D", "#39A089", [10, 10, 10, 9, 10, 10], [12, 12, 12, 12, 13, 12], [null, null, 10, 12, 13, 12])
    ];

    public static readonly IReadOnlyList<ProgressionPreset> ProgressionPresets =
    [
        new("pop_1564", "I–V–vi–IV · 流行万能", "常用循环", "C", 90, [new("C", "I"), new("G", "V"), new("Am", "vi"), new("F", "IV")]),
        new("ballad_4536251", "IV–V–iii–vi–ii–V–I · 抒情华语", "常用循环", "C", 80, [new("F", "IV"), new("G", "V"), new("Em", "iii"), new("Am", "vi"), new("Dm", "ii"), new("G", "V"), new("C", "I")]),
        new("canon", "I–V–vi–iii–IV–I–IV–V · 卡农走向", "常用循环", "C", 84, [new("C", "I"), new("G", "V"), new("Am", "vi"), new("Em", "iii"), new("F", "IV"), new("C", "I"), new("F", "IV"), new("G", "V")]),
        new("pop_1645", "I–vi–IV–V · 50s 经典", "常用循环", "G", 96, [new("G", "I"), new("Em", "vi"), new("C", "IV"), new("D", "V")]),
        new("minor_6415", "vi–IV–I–V · 燃系副歌", "小调走向", "G", 104, [new("Em", "vi"), new("C", "IV"), new("G", "I"), new("D", "V")]),
        new("andalusian", "i–bVII–bVI–V7 · 西班牙下行", "小调走向", "Am", 92, [new("Am", "i"), new("G", "bVII"), new("F", "bVI"), new("E7", "V7")]),
        new("jazz_251", "ii7–V7–Imaj7 · 爵士 2-5-1", "七和弦", "C", 88, [new("Dm7", "ii7"), new("G7", "V7"), new("Cmaj7", "Imaj7"), new("Cmaj7", "Imaj7")]),
        new("blues_12", "I7–IV7–V7 · 布鲁斯循环", "摇滚蓝调", "A", 96, [new("A7", "I7"), new("D7", "IV7"), new("A7", "I7"), new("E7", "V7"), new("D7", "IV7"), new("A7", "I7")])
    ];

    public static GuitarTuning GetTuning(string? id) =>
        Tunings.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Tunings[0];

    public static ScaleDefinition GetScale(string? id) =>
        Scales.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Scales[0];

    public static string NoteName(int pitchClass, bool preferFlats = false)
    {
        var pc = ((pitchClass % 12) + 12) % 12;
        return preferFlats ? FlatNotes[pc] : SharpNotes[pc];
    }

    public static string MidiToNoteWithOctave(int midi, bool preferFlats = false)
    {
        var pc = ((midi % 12) + 12) % 12;
        var octave = (midi / 12) - 1;
        return $"{NoteName(pc, preferFlats)}{octave}";
    }

    public static string GetDegreeLabel(int pitchClass, int tonicPitch) =>
        DegreeLabels[((pitchClass - tonicPitch) % 12 + 12) % 12];

    public static string GetIntervalLabel(int pitchClass, int rootPitch) =>
        IntervalLabels[((pitchClass - rootPitch) % 12 + 12) % 12];

    public static bool PreferFlatsForTonic(int tonicPitch) =>
        tonicPitch is 1 or 3 or 5 or 8 or 10; // Db, Eb, F, Ab, Bb

    public static int ParseCapoNumber(string? capoText)
    {
        if (string.IsNullOrWhiteSpace(capoText)) return 0;
        var match = Regex.Match(capoText, @"(\d{1,2})");
        return match.Success && int.TryParse(match.Groups[1].Value, out var n) ? Math.Clamp(n, 0, 11) : 0;
    }

    public static List<CagedBoxInstance> ComputeCagedBoxes(
        int tonicPitch,
        bool isMinorScale,
        GuitarTuning tuning,
        int maxFret = 17)
    {
        // CAGED 5 interlocking shapes are defined for standard-interval relative tunings (Standard, Eb, D)
        // Compute relative major tonic for minor scales so Am shares C Major's exact 5 interlocking shapes!
        var majorTonic = isMinorScale ? (tonicPitch + 3) % 12 : ((tonicPitch % 12) + 12) % 12;
        var tuningShift = 40 - tuning.OpenMidi[0]; // e.g. Eb standard shifts frets +1
        var isUniformStandardIntervals =
            tuning.OpenMidi[1] - tuning.OpenMidi[0] == 5 &&
            tuning.OpenMidi[2] - tuning.OpenMidi[1] == 5 &&
            tuning.OpenMidi[3] - tuning.OpenMidi[2] == 5 &&
            tuning.OpenMidi[4] - tuning.OpenMidi[3] == 4 &&
            tuning.OpenMidi[5] - tuning.OpenMidi[4] == 5;

        if (!isUniformStandardIntervals)
            return [];

        var baseShift = (majorTonic + tuningShift + 24) % 12;
        var result = new List<CagedBoxInstance>();

        for (var octaveShift = -12; octaveShift <= 12; octaveShift += 12)
        {
            var delta = baseShift + octaveShift;
            foreach (var (letter, color, lefts, rights, triad) in BaseCMajorCaged)
            {
                var shiftedLeft = new int[6];
                var shiftedRight = new int[6];
                var shiftedTriad = new int?[6];
                var minF = 99;
                var maxF = -99;

                for (var s = 0; s < 6; s++)
                {
                    shiftedLeft[s] = lefts[s] + delta;
                    shiftedRight[s] = rights[s] + delta;
                    shiftedTriad[s] = triad[s].HasValue ? triad[s]!.Value + delta : null;
                    if (shiftedLeft[s] < minF) minF = shiftedLeft[s];
                    if (shiftedRight[s] > maxF) maxF = shiftedRight[s];
                }

                // Include shapes that sit cleanly inside 0..maxFret
                if (minF >= 0 && maxF <= maxFret)
                {
                    result.Add(new CagedBoxInstance(
                        letter,
                        color,
                        octaveShift,
                        minF,
                        maxF,
                        shiftedLeft,
                        shiftedRight,
                        shiftedTriad));
                }
            }
        }

        return result.OrderBy(b => b.MinFret).ThenBy(b => b.MaxFret).ToList();
    }

    public static List<DiatonicChordInfo> GetDiatonicChords(int tonicPitch, bool isMinor, bool preferFlats)
    {
        int[] intervals = isMinor ? [0, 2, 3, 5, 7, 8, 10] : [0, 2, 4, 5, 7, 9, 11];
        string[] romans = isMinor
            ? ["i", "ii°", "III", "iv", "v", "VI", "VII"]
            : ["I", "ii", "iii", "IV", "V", "vi", "vii°"];
        string[] triadQual = isMinor
            ? ["m", "dim", "", "m", "m", "", ""]
            : ["", "m", "m", "", "", "m", "dim"];
        string[] sevQual = isMinor
            ? ["m7", "m7-5", "maj7", "m7", "m7", "maj7", "7"]
            : ["maj7", "m7", "m7", "maj7", "7", "m7", "m7-5"];

        var list = new List<DiatonicChordInfo>(7);
        for (var i = 0; i < 7; i++)
        {
            var pc = (tonicPitch + intervals[i]) % 12;
            var root = NoteName(pc, preferFlats);
            list.Add(new DiatonicChordInfo(
                i + 1,
                romans[i],
                root,
                root + triadQual[i],
                root + sevQual[i],
                pc));
        }
        return list;
    }

    public static List<ChordStringDecode> DecodeChordStrings(
        ChordShape shape,
        int tonicPitch,
        GuitarTuning tuning,
        int capo,
        bool preferFlats)
    {
        var hasParsed = ChordVoicingEngine.TryParseChord(shape.Name, out var parsed);
        var chordRoot = hasParsed ? parsed.RootPitch : tonicPitch;
        var rows = new List<ChordStringDecode>(6);

        for (var s = 0; s < 6; s++)
        {
            var fret = s < shape.Frets.Length ? shape.Frets[s] : null;
            if (fret is null)
            {
                rows.Add(new ChordStringDecode(s, null, "—", "mute", "—", "×"));
                continue;
            }

            var midi = tuning.OpenMidi[s] + capo + fret.Value;
            var pc = ((midi % 12) + 12) % 12;
            var ivSemi = ((pc - chordRoot) % 12 + 12) % 12;
            var ivLabel = IntervalLabels[ivSemi];
            var role = ivSemi switch
            {
                0 => "root",
                3 or 4 => "third",
                7 => "fifth",
                _ => "color"
            };
            var degLabel = GetDegreeLabel(pc, tonicPitch);
            var noteLabel = MidiToNoteWithOctave(midi, preferFlats);
            rows.Add(new ChordStringDecode(s, fret, ivLabel, role, degLabel, noteLabel));
        }

        return rows;
    }

    public static List<int> GetChordMidiNotes(ChordShape shape, GuitarTuning tuning, int capo = 0)
    {
        var notes = new List<int>(6);
        for (var s = 0; s < 6; s++)
        {
            if (s < shape.Frets.Length && shape.Frets[s] is { } f)
                notes.Add(tuning.OpenMidi[s] + capo + f);
        }
        return notes;
    }

    public static List<ProgressionStep> ExtractSongProgression(SongModel? song, int maxChords = 8)
    {
        if (song is null) return [];
        var chords = new List<string>();
        foreach (var line in song.Lines)
        {
            if (line.IsReferenceLine) continue;
            foreach (var c in line.Chords)
            {
                var norm = ChordCatalog.NormalizeChordName(c.Name);
                if (string.IsNullOrWhiteSpace(norm)) continue;
                if (chords.Count == 0 || !string.Equals(chords[^1], norm, StringComparison.OrdinalIgnoreCase))
                    chords.Add(norm);
                if (chords.Count >= maxChords) break;
            }
            if (chords.Count >= maxChords) break;
        }

        if (chords.Count == 0) return [];
        var effectiveKey = string.IsNullOrWhiteSpace(song.SelectedKey) ? song.OriginalKey : song.SelectedKey;
        return chords.Select(c => new ProgressionStep(c, ChordTranspose.ToDegree(c, effectiveKey), 4)).ToList();
    }

    public static List<ProgressionSuggestion> SuggestSmootherProgressions(IReadOnlyList<string> chords, bool preferFlats)
    {
        if (chords.Count < 2) return [];

        // Strategy A: Smooth bass voice-leading using first/second inversion slash chords to keep bass steps <= 3 semitones
        var smoothA = new List<string>(chords);
        var changedA = 0;
        for (var i = 1; i < chords.Count; i++)
        {
            if (!ChordVoicingEngine.TryParseChord(smoothA[i - 1], out var prev)) continue;
            if (!ChordVoicingEngine.TryParseChord(smoothA[i], out var curr)) continue;
            if (curr.HasSlashBass) continue;

            var dist = CircularDist(prev.BassPitch, curr.BassPitch);
            if (dist <= 2) continue;

            // Try 3rd or 5th of curr as bass if it steps closer (1–2 semitones) to prev bass
            var bestAltBass = -1;
            var bestDist = dist;
            foreach (var pc in curr.AllPitches)
            {
                if (pc == curr.RootPitch) continue;
                var d = CircularDist(prev.BassPitch, pc);
                if (d < bestDist && d <= 2)
                {
                    bestDist = d;
                    bestAltBass = pc;
                }
            }

            if (bestAltBass >= 0)
            {
                smoothA[i] = $"{curr.DisplayName}/{NoteName(bestAltBass, preferFlats)}";
                changedA++;
            }
        }

        // Strategy B: Bass pedal / walking descent on chord #2 or #3
        var smoothB = new List<string>(chords);
        var changedB = 0;
        for (var i = 0; i < chords.Count - 1; i++)
        {
            if (!ChordVoicingEngine.TryParseChord(smoothB[i], out var curr)) continue;
            if (!ChordVoicingEngine.TryParseChord(smoothB[i + 1], out var next)) continue;
            if (curr.HasSlashBass) continue;

            // If curr -> next jumps by 4–5 semitones, invert curr on its 3rd or 5th closer to next bass
            var third = curr.AllPitches.FirstOrDefault(p => ((p - curr.RootPitch + 12) % 12) is 3 or 4);
            if (third != curr.RootPitch && CircularDist(third, next.BassPitch) is 1 or 2 && CircularDist(curr.BassPitch, next.BassPitch) > 2)
            {
                smoothB[i] = $"{curr.DisplayName}/{NoteName(third, preferFlats)}";
                changedB++;
                break;
            }
        }

        var suggestions = new List<ProgressionSuggestion>();
        if (changedA > 0)
            suggestions.Add(new ProgressionSuggestion("方案 A · 级进低音转位", "利用和弦三音/五音作斜杠低音，让根音走向变成半音/全音平滑衔接", smoothA));
        if (changedB > 0 && !smoothB.SequenceEqual(smoothA, StringComparer.OrdinalIgnoreCase))
            suggestions.Add(new ProgressionSuggestion("方案 B · 过渡转位连接", "在跳进和弦前使用第一转位牵引至下一个根音，听感更自然", smoothB));

        if (suggestions.Count == 0 && chords.Count >= 2 && ChordVoicingEngine.TryParseChord(chords[0], out var first))
        {
            // Fallback pedal bass suggestion
            var pedal = new List<string>(chords);
            var bassName = NoteName(first.RootPitch, preferFlats);
            if (pedal.Count > 1 && !pedal[1].Contains('/'))
                pedal[1] = $"{pedal[1]}/{bassName}";
            suggestions.Add(new ProgressionSuggestion("方案 A · 主音持续低音 (Pedal)", $"在第二和弦保留 {bassName} 低音形成平稳铺底感", pedal));
        }

        return suggestions;
    }

    public static List<ProgressionSuggestion> SuggestRicherProgressions(IReadOnlyList<string> chords, int tonicPitch, bool isMinor)
    {
        if (chords.Count == 0) return [];
        var diatonic = GetDiatonicChords(tonicPitch, isMinor, PreferFlatsForTonic(tonicPitch));

        string EnrichColor(string raw, bool addNinthStyle)
        {
            if (!ChordVoicingEngine.TryParseChord(raw, out var parsed) || parsed.HasSlashBass)
                return raw;

            var matchedDiatonic = diatonic.FirstOrDefault(d => d.RootPitch == parsed.RootPitch);
            if (parsed.CanonicalQuality == "")
            {
                if (matchedDiatonic is not null)
                {
                    if (matchedDiatonic.DegreeNumber == 5)
                        return addNinthStyle ? $"{matchedDiatonic.RootName}6" : $"{matchedDiatonic.RootName}7";
                    if (matchedDiatonic.DegreeNumber is 1 or 4)
                        return addNinthStyle ? $"{matchedDiatonic.RootName}add9" : $"{matchedDiatonic.RootName}maj7";
                }
                return addNinthStyle ? $"{raw}add9" : $"{raw}maj7";
            }
            if (parsed.CanonicalQuality == "m")
            {
                return addNinthStyle ? $"{raw}9" : $"{raw}7";
            }
            return raw;
        }

        var seventhVer = chords.Select(c => EnrichColor(c, addNinthStyle: false)).ToList();
        var folkAdd9Ver = chords.Select((c, idx) => EnrichColor(c, addNinthStyle: idx % 2 == 0)).ToList();
        var lushVer = chords.Select(c => EnrichColor(c, addNinthStyle: true)).ToList();

        var list = new List<ProgressionSuggestion>
        {
            new("方案 A · 流行七和弦色彩", "将主/下属大三和弦升级为 maj7、小三和弦升级为 m7，瞬间更有温暖氛围感", seventhVer),
            new("方案 B · 民谣 add9 / 6 混合", "交替加入 add9 与 6 和弦，保留清澈木吉他共鸣同时更有层次", folkAdd9Ver),
            new("方案 C · 现代九和弦延伸", "大量融入 add9 / m9 / 6 色彩音，适合 R&B / 抒情流行分解伴奏", lushVer)
        };
        return list;
    }

    private static int CircularDist(int a, int b)
    {
        var d = Math.Abs(a - b) % 12;
        return Math.Min(d, 12 - d);
    }

    public static IReadOnlyList<TheoryLesson> Lessons { get; } = BuildLessons();

    private static List<TheoryLesson> BuildLessons() =>
    [
        new("unit1_notes", "1. 音高与指板", "十二个音名与指板规律", "理解半音/全音与空弦音名，告别盲按",
        [
            new("每品相差一个半音", "吉他指板相邻两品永远相差 1 个半音（12 品刚好高八度循环）。记住全半音规律：E-F、B-C 之间是天生半音（紧挨 1 品），其余自然音之间都隔 1 品（全音）。", 0, "major", false, "", false),
            new("从 6 弦与 5 弦快速认音", "先记熟 6 弦（E）和 5 弦（A）上 3、5、7、8、10 品的音名，就能定位绝大多数横按和弦与 CAGED 根音位置。点击上方指板任意品格可以直接听音！", 0, "major", false, "", false, HighlightChord: "C")
        ]),

        new("unit2_intervals", "2. 音程与三和弦", "大三度与小三度决定明暗", "为什么大调明亮、小调忧伤",
        [
            new("大三和弦骨架：1 - 3 - 5", "上方指板中，橙色大圆是根音 1，紫色/青色大圆是 3 音与 5 音。大三度（隔 4 个半音）赋予 C 和弦明亮稳定的听感。", 0, "major_pent", true, "C", false, HighlightChord: "C"),
            new("小三和弦骨架：1 - b3 - 5", "把三音降半音变成 b3（隔 3 个半音），就变成自然小调与 Am 和弦的忧郁色彩。对比点击下方「听里面的和弦」感受明暗差异。", 9, "minor_pent", true, "G", false, HighlightChord: "Am")
        ]),

        new("unit3_scales", "3. 音阶与五声音阶", "去掉 4 和 7 就变成万能五声音阶", "为什么五声音阶怎么弹都好听",
        [
            new("自然大调 7 个音", "自然大调包含 1 2 3 4 5 6 7 七个音，其中 4 级和 7 级构成“三全音”张力——用来制造推进感很棒，但停在上面容易觉得“悬着”。", 0, "major", false, "", false),
            new("大调五声音阶：1 2 3 5 6", "去掉 4 和 7 两个半音碰撞点，剩下的 1 2 3 5 6 就是大调五声音阶，每根弦正好 2 个音，是吉他即兴与加花最顺手的武器。", 0, "major_pent", true, "ALL", true)
        ]),

        new("unit4_caged_intro", "4. CAGED 核心系统", "第 1 课：CAGED 是什么，有什么用", "用 5 个开放和弦形状打通整根指板",
        [
            new("整根琴颈其实只有 5 块拼图", "CAGED 不是新乐理，而是把你早就背过的 5 个开放和弦（C、A、G、E、D）当作可移动的“骨架”，按 C→A→G→E→D 顺序首尾相接，刚好无缝铺满整根指板！", 0, "major_pent", true, "ALL", false),
            new("相邻两块永远共享一条边", "仔细看上方 5 种颜色的轮廓框：C 形的右边界，就是 A 形的左边界；A 形的右边界，就是 G 形的左边界……记熟连接点，你就能随时跨把位滑行，不再被锁在 1–3 品。", 0, "major_pent", true, "ALL", true)
        ]),

        new("unit4_caged_shapes", "4. CAGED 核心系统", "第 2 课：五个音阶形状（每块藏着一个和弦）", "逐个看透 C / A / G / E / D 五个胶囊指型",
        [
            new("C 形（0–3 品 · 根音在 5 弦与 2 弦）", "看上方绿色胶囊框：大圆点 1、3、5 组成的正是你最熟悉的开放 C 和弦（x32010）；周围小白点 2 和 6 补全了这一块的五声音阶。", 0, "major_pent", true, "C", true, HighlightChord: "C"),
            new("A 形（2–5 品 · 根音在 5 弦 3 品与 3 弦 5 品）", "玫瑰粉色块：它直接接着 C 形的 5 弦 3 品根音向右展开，里面藏着 A 形横按的 C 和弦（x35553）。", 0, "major_pent", true, "A", true),
            new("G 形（5–8 品 · 也就是经典小调五声第一把位！）", "薰衣草紫色块（5–8 品）：藏着 G 形骨架，同时它就是吉他手最熟悉的 Am 小调五声音阶第 1 把位！大圆点 1/3/5 帮你一眼定位落脚音。", 0, "major_pent", true, "G", true),
            new("E 形（7–10 品）与 D 形（9–13 品）", "天蓝色 E 形以 6 弦 8 品为根音（也就是 8 品大横按 C 和弦），再往右接青绿色 D 形（4 弦 10 品根音），到 12 品后又回到 C 形开始新八度循环！", 0, "major_pent", true, "E", true)
        ]),

        new("unit4_caged_transpose", "4. CAGED 核心系统", "第 3 课：换调其实只是把 5 块一起平移", "一套指型通吃 12 个调",
        [
            new("把刚才的 C 大调平移到 G 大调", "看上方指板切到了 G 大调：C→A→G→E→D 的拼接顺序一模一样，只是整条彩带一起平移了！此时 0–3 品刚好落在了 G 形（开放 G 和弦）。", 7, "major_pent", true, "ALL", true, HighlightChord: "G"),
            new("再切到 D 大调验证一下", "D 大调的 0–3 品刚好是 D 形（开放 D 和弦），往右依次接 C 形、A 形、G 形、E 形——只要在任意一根弦上找到当前调的根音，整根指板的 5 块拼图立刻浮现。", 2, "major_pent", true, "ALL", true, HighlightChord: "D")
        ]),

        new("unit4_caged_improv", "4. CAGED 核心系统", "第 4 课：用 CAGED 找靶向音即兴", "不再漫无目的爬音阶",
        [
            new("停在彩色大圆（和弦内音）上永远好听", "即兴时把小白点（2、6）当作经过的台阶，乐句结尾尽量落在当前和弦的橙色根音或三音/五音大圆上，旋律立刻就有“高级感”。", 7, "major_pent", true, "G", true, HighlightChord: "G"),
            new("同一把位不用乱跑，直接切换和弦内音", "切到「和弦进行」标签，打开「靶向音四色提示」播放 1645，试着只留在同一个 4 品方框内，跟着红/金亮点弹 Solo，这就是职业乐手的“就近解决”秘诀。", 7, "major", true, "E", false, HighlightChord: "Em")
        ]),

        new("unit5_diatonic", "5. 顺阶和弦与变调夹", "一个调里哪 7 个和弦最和谐", "顺阶和弦规律 + 变调夹实战",
        [
            new("大调顺阶规律：大大 小小 大大 减", "任意大调里，1/4/5 级天然是大三和弦（I、IV、V），2/3/6 级是小三和弦（ii、iii、vi），7 级是减三和弦（vii°）。切到「和弦解剖库 → 调内」即可一键逐个试听。", 0, "major", false, "", false, HighlightChord: "Fmaj7"),
            new("变调夹的本质：把开放把位整体搬家", "在下方给指板夹上 2 品变调夹（Capo 2），用 C 调开放指法弹出的实际就是 D 大调！指板会自动隐藏变调夹左侧品格，同时保留准确的实际音名对照。", 2, "major", true, "ALL", false, HighlightChord: "C", Capo: 2)
        ])
    ];

    /// <summary>
    /// Returns all playable neck positions for a chord, prioritizing the 5 textbook CAGED hand shapes
    /// (C / A / G / E / D) ordered smoothly from the nut up the neck for major/minor/7/maj7/m7 chords,
    /// plus ergonomic derived voicings for extended, altered, or slash chords.
    /// </summary>
    public static List<CagedChordVoicing> GetCagedChordVoicings(string chordSymbol)
    {
        var normalized = ChordCatalog.NormalizeChordName(chordSymbol);
        if (string.IsNullOrWhiteSpace(normalized))
            return [];

        var results = new List<CagedChordVoicing>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        if (ChordVoicingEngine.TryParseChord(normalized, out var parsed) &&
            !parsed.HasSlashBass &&
            parsed.CanonicalQuality is "" or "m" or "7" or "maj7" or "m7")
        {
            var cagedDefs = BuildCagedFiveTemplates(parsed.RootPitch, parsed.CanonicalQuality, normalized);
            foreach (var item in cagedDefs.OrderBy(x => GetMinFretSortKey(x.Shape.Frets)))
            {
                var fp = string.Join(",", item.Shape.Frets.Select(f => f?.ToString() ?? "x"));
                if (seenKeys.Add(fp))
                    results.Add(item);
            }
        }

        // Supplement with ChordCatalog candidates (or use them exclusively for slash / extended chords)
        var candidates = ChordCatalog.GetCandidates(normalized, maxResults: 8);
        var extraIndex = 1;
        foreach (var cand in candidates)
        {
            var fp = string.Join(",", cand.Frets.Select(f => f?.ToString() ?? "x"));
            if (!seenKeys.Add(fp))
                continue;
            if (results.Count >= 8)
                break;

            var posHint = FormatPositionHint(cand.Frets);
            results.Add(new CagedChordVoicing(
                cand,
                CagedLetter: null,
                BadgeText: results.Count < 5 && !string.IsNullOrWhiteSpace(cand.Label) ? cand.Label : $"把位 {extraIndex}",
                PositionHint: posHint,
                ColorHex: "#2F6B48"));
            extraIndex++;
        }

        return results;
    }

    private static List<CagedChordVoicing> BuildCagedFiveTemplates(int rootPitch, string quality, string displayName)
    {
        // Base open root pitches for CAGED: C=0, A=9, G=7, E=4, D=2
        (string Letter, string ColorHex, int BaseRoot)[] shapes =
        [
            ("C", "#7C9A46", 0),
            ("A", "#D96B8A", 9),
            ("G", "#8B74C4", 7),
            ("E", "#4A8FD2", 4),
            ("D", "#39A089", 2)
        ];

        var list = new List<CagedChordVoicing>(5);
        foreach (var (letter, colorHex, baseRoot) in shapes)
        {
            var d = (rootPitch - baseRoot + 12) % 12;
            int?[]? frets = null;
            int[]? fingers = null;
            BarreShape? barre = null;

            switch (quality)
            {
                case "":
                    switch (letter)
                    {
                        case "C":
                            frets = [null, d + 3, d + 2, d, d + 1, d];
                            fingers = d == 0 ? [0, 3, 2, 0, 1, 0] : [0, 4, 3, 1, 2, 1];
                            barre = d > 0 ? new(d, 3, 5) : null;
                            break;
                        case "A":
                            frets = [null, d, d + 2, d + 2, d + 2, d];
                            fingers = d == 0 ? [0, 0, 1, 2, 3, 0] : [0, 1, 3, 3, 3, 1];
                            barre = d > 0 ? new(d, 1, 5) : null;
                            break;
                        case "G":
                            frets = [d + 3, d + 2, d, d, d, d + 3];
                            fingers = d == 0 ? [2, 1, 0, 0, 0, 3] : [3, 2, 1, 1, 1, 4];
                            barre = d > 0 ? new(d, 2, 4) : null;
                            break;
                        case "E":
                            frets = [d, d + 2, d + 2, d + 1, d, d];
                            fingers = d == 0 ? [0, 2, 3, 1, 0, 0] : [1, 3, 4, 2, 1, 1];
                            barre = d > 0 ? new(d, 0, 5) : null;
                            break;
                        case "D":
                            frets = [null, null, d, d + 2, d + 3, d + 2];
                            fingers = d == 0 ? [0, 0, 0, 1, 3, 2] : [0, 0, 1, 2, 4, 3];
                            break;
                    }
                    break;

                case "m":
                    switch (letter)
                    {
                        case "C":
                            frets = [null, d + 3, d + 1, d, d + 1, null];
                            fingers = d == 0 ? [0, 3, 1, 0, 2, 0] : [0, 4, 2, 1, 3, 0];
                            break;
                        case "A":
                            frets = [null, d, d + 2, d + 2, d + 1, d];
                            fingers = d == 0 ? [0, 0, 2, 3, 1, 0] : [0, 1, 3, 4, 2, 1];
                            barre = d > 0 ? new(d, 1, 5) : null;
                            break;
                        case "G":
                            frets = [d + 3, d + 1, d, d, null, d + 3];
                            fingers = d == 0 ? [3, 1, 0, 0, 0, 4] : [3, 2, 1, 1, 0, 4];
                            barre = d > 0 ? new(d, 2, 3) : null;
                            break;
                        case "E":
                            frets = [d, d + 2, d + 2, d, d, d];
                            fingers = d == 0 ? [0, 2, 3, 0, 0, 0] : [1, 3, 4, 1, 1, 1];
                            barre = d > 0 ? new(d, 0, 5) : null;
                            break;
                        case "D":
                            frets = [null, null, d, d + 2, d + 3, d + 1];
                            fingers = d == 0 ? [0, 0, 0, 2, 3, 1] : [0, 0, 1, 3, 4, 2];
                            break;
                    }
                    break;

                case "7":
                    switch (letter)
                    {
                        case "C":
                            frets = d == 0 ? [null, 3, 2, 3, 1, 0] : [null, d + 3, d + 2, d + 3, d + 1, null];
                            fingers = [0, 3, 2, 4, 1, 0];
                            break;
                        case "A":
                            frets = [null, d, d + 2, d, d + 2, d];
                            fingers = d == 0 ? [0, 0, 2, 0, 3, 0] : [0, 1, 3, 1, 4, 1];
                            barre = d > 0 ? new(d, 1, 5) : null;
                            break;
                        case "G":
                            frets = d == 0 ? [3, 2, 0, 0, 0, 1] : [d + 3, null, d + 3, d + 4, d + 3, null];
                            fingers = d == 0 ? [3, 2, 0, 0, 0, 1] : [2, 0, 3, 4, 1, 0];
                            break;
                        case "E":
                            frets = [d, d + 2, d, d + 1, d, d];
                            fingers = d == 0 ? [0, 2, 0, 1, 0, 0] : [1, 3, 1, 2, 1, 1];
                            barre = d > 0 ? new(d, 0, 5) : null;
                            break;
                        case "D":
                            frets = [null, null, d, d + 2, d + 1, d + 2];
                            fingers = d == 0 ? [0, 0, 0, 2, 1, 3] : [0, 0, 1, 3, 2, 4];
                            break;
                    }
                    break;

                case "maj7":
                    switch (letter)
                    {
                        case "C":
                            frets = [null, d + 3, d + 2, d, d, d];
                            fingers = d == 0 ? [0, 3, 2, 0, 0, 0] : [0, 4, 3, 1, 1, 1];
                            barre = d > 0 ? new(d, 3, 5) : null;
                            break;
                        case "A":
                            frets = [null, d, d + 2, d + 1, d + 2, d];
                            fingers = d == 0 ? [0, 0, 2, 1, 3, 0] : [0, 1, 3, 2, 4, 1];
                            barre = d > 0 ? new(d, 1, 5) : null;
                            break;
                        case "G":
                            frets = d == 0 ? [3, 2, 0, 0, 0, 2] : [d + 3, d + 2, d, d, d, d + 2];
                            fingers = d == 0 ? [2, 1, 0, 0, 0, 3] : [4, 2, 1, 1, 1, 3];
                            barre = d > 0 ? new(d, 2, 4) : null;
                            break;
                        case "E":
                            frets = d == 0 ? [0, 2, 1, 1, 0, 0] : [d, null, d + 1, d + 1, d, null];
                            fingers = d == 0 ? [0, 3, 1, 2, 0, 0] : [1, 0, 3, 4, 2, 0];
                            break;
                        case "D":
                            frets = [null, null, d, d + 2, d + 2, d + 2];
                            fingers = d == 0 ? [0, 0, 0, 1, 2, 3] : [0, 0, 1, 2, 3, 4];
                            break;
                    }
                    break;

                case "m7":
                    switch (letter)
                    {
                        case "C":
                            frets = [null, d + 3, d + 1, d + 3, d + 1, d + 3];
                            fingers = [0, 2, 1, 3, 1, 4];
                            barre = new(d + 1, 2, 4);
                            break;
                        case "A":
                            frets = [null, d, d + 2, d, d + 1, d];
                            fingers = d == 0 ? [0, 0, 2, 0, 1, 0] : [0, 1, 3, 1, 2, 1];
                            barre = d > 0 ? new(d, 1, 5) : null;
                            break;
                        case "G":
                            frets = [d + 3, null, d + 3, d + 3, d + 3, null];
                            fingers = [1, 0, 2, 3, 4, 0];
                            break;
                        case "E":
                            frets = [d, d + 2, d, d, d, d];
                            fingers = d == 0 ? [0, 2, 0, 0, 0, 0] : [1, 3, 1, 1, 1, 1];
                            barre = d > 0 ? new(d, 0, 5) : null;
                            break;
                        case "D":
                            frets = [null, null, d, d + 2, d + 1, d + 1];
                            fingers = d == 0 ? [0, 0, 0, 2, 1, 1] : [0, 0, 1, 4, 2, 3];
                            barre = d == 0 ? new(1, 4, 5) : null;
                            break;
                    }
                    break;
            }

            if (frets is null || fingers is null)
                continue;

            var posFrets = frets.Where(f => f is > 0).Select(f => f!.Value).ToArray();
            var hasOpen = frets.Any(f => f == 0);
            var minPos = posFrets.Length > 0 ? posFrets.Min() : 1;
            var maxPos = posFrets.Length > 0 ? posFrets.Max() : 1;
            if (maxPos > 16)
                continue;

            var baseFret = (hasOpen && maxPos <= 4) || maxPos <= 4 ? 1 : minPos;
            var posHint = hasOpen && baseFret == 1 ? "开放" : $"{minPos}品";
            var shapeLabel = $"{letter} 形 · {(barre is not null ? $"{minPos}品横按" : posHint)}";

            var shape = new ChordShape
            {
                Id = $"caged:{displayName}:{letter}:{string.Join("-", frets.Select(f => f?.ToString() ?? "x"))}",
                Name = displayName,
                Type = barre is not null ? "barre" : "open",
                Frets = frets,
                Fingers = fingers,
                BaseFret = baseFret,
                Barre = barre,
                Label = shapeLabel
            };

            list.Add(new CagedChordVoicing(shape, letter, $"{letter} 形", posHint, colorHex));
        }

        return list;
    }

    private static int GetMinFretSortKey(int?[] frets)
    {
        if (frets.Any(f => f == 0)) return 0;
        var pos = frets.Where(f => f is > 0).Select(f => f!.Value).ToArray();
        return pos.Length > 0 ? pos.Min() : 0;
    }

    private static string FormatPositionHint(int?[] frets)
    {
        var hasOpen = frets.Any(f => f == 0);
        var pos = frets.Where(f => f is > 0).Select(f => f!.Value).ToArray();
        if (hasOpen && (pos.Length == 0 || pos.Max() <= 4)) return "开放";
        return pos.Length > 0 ? $"{pos.Min()}品" : "开放";
    }

    public static List<(string Label, string Symbol)> GetChordColorVariations(string chordSymbol, bool preferFlats)
    {
        if (!ChordVoicingEngine.TryParseChord(chordSymbol, out var parsed))
            return [];

        var root = NoteName(parsed.RootPitch, preferFlats);
        var third = NoteName((parsed.RootPitch + (parsed.CanonicalQuality.StartsWith("m") && !parsed.CanonicalQuality.StartsWith("maj") ? 3 : 4)) % 12, preferFlats);
        var fifth = NoteName((parsed.RootPitch + 7) % 12, preferFlats);
        var isMin = parsed.CanonicalQuality is "m" or "m7" or "m9" or "m6" or "m7b5";

        var list = isMin
            ? new List<(string, string)>
            {
                ("小三", root + "m"),
                ("小七", root + "m7"),
                ("小九", root + "m9"),
                ("挂二", root + "sus2"),
                ("挂四", root + "sus4"),
                ("第一转位", $"{root}m/{third}"),
                ("五音低音", $"{root}m/{fifth}")
            }
            : new List<(string, string)>
            {
                ("大三", root),
                ("大七", root + "maj7"),
                ("加九", root + "add9"),
                ("挂四", root + "sus4"),
                ("属七", root + "7"),
                ("大六", root + "6"),
                ("第一转位", $"{root}/{third}"),
                ("五音低音", $"{root}/{fifth}")
            };

        return list.Where(x => !string.Equals(x.Item2, chordSymbol, StringComparison.OrdinalIgnoreCase)).Take(7).ToList();
    }

    public static string DescribeDiatonicFunction(string chordSymbol, int tonicPitch, bool isMinorScale)
    {
        if (!ChordVoicingEngine.TryParseChord(chordSymbol, out var parsed))
            return "自由色彩和弦，可用于离调过渡或营造独特氛围。";

        var semi = ((parsed.RootPitch - tonicPitch) % 12 + 12) % 12;
        if (!isMinorScale)
        {
            return semi switch
            {
                0 => "I 级主和弦（Tonic）· 全曲最稳定的归宿点，常作为乐句起点与终止落点。",
                2 => "ii 级上主和弦（Subdominant）· 柔和推进感，极常接 V 级构成黄金「ii–V–I」。",
                4 => "iii 级中音和弦 · 带一丝淡淡忧伤的过渡桥梁，常在 I→iii→IV 或 IV→V→iii→vi 中出现。",
                5 => "IV 级下属和弦（Subdominant）· 开阔明亮的展开感，副歌起句与情绪抬升利器。",
                7 => "V 级属和弦（Dominant）· 强烈牵引力，拉着旋律回到 I 级主和弦完成解决。",
                9 => "vi 级关系小调主和弦 · 抒情核心（流行 1564 / 6415 灵魂），可与 I 级无缝互替。",
                11 => "vii° 级导音和弦 · 紧张度高，现代弹唱常替换为 V/7（如 G/B）做平滑低音过渡。",
                _ => "调外借用 / 离调色彩和弦 · 可制造惊喜转折，常用作重属和弦或平行小调借用。"
            };
        }

        return semi switch
        {
            0 => "i 级小调主和弦 · 忧郁沉稳的核心落脚点。",
            2 => "ii° 级半减和弦 · 强张力过渡，常接 V7 制造爵士或悲情推进。",
            3 => "III 级关系大调主和弦 · 瞬间拨云见日的明亮色彩，副歌转亮常用。",
            5 => "iv 级小调下属和弦 · 深情厚重，非常适合抒情副歌铺底。",
            7 => "v / V7 级属和弦 · 换成大三或属七（如 E7）能产生极强回归主和弦的张力。",
            8 => "VI 级大三和弦 · 史诗感与燃系走向核心（i–VI–III–VII / i–VII–VI–V7）。",
            10 => "VII 级大三和弦 · 摇滚与民谣小调灵魂过渡和弦，下行衔接顺滑有力。",
            _ => "调外借用色彩和弦 · 可作为过渡和弦丰富小调和声层次。"
        };
    }

    public static (List<int> RootPcs, List<int> ChordPcs, List<int> ColorPcs) GetJamTargetPitchClasses(
        string chordSymbol,
        int tonicPitch,
        ScaleDefinition scale)
    {
        if (!ChordVoicingEngine.TryParseChord(chordSymbol, out var parsed))
            return ([tonicPitch], [], []);

        var scaleSet = new HashSet<int>(scale.Intervals.Select(iv => (tonicPitch + iv) % 12));
        var roots = new List<int> { parsed.RootPitch };
        var chordTones = parsed.AllPitches.Where(p => p != parsed.RootPitch).Distinct().ToList();
        var colorTones = new List<int>();
        foreach (var pc in scaleSet)
        {
            if (pc == parsed.RootPitch || parsed.AllPitches.Contains(pc)) continue;
            var iv = ((pc - parsed.RootPitch) % 12 + 12) % 12;
            if (iv is 2 or 9 or 10 or 11)
                colorTones.Add(pc);
        }
        return (roots, chordTones, colorTones);
    }
}
