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

public sealed record ThreeNpsBoxInstance(
    int PositionNumber,      // 1 .. 7
    string DegreeStartLabel, // e.g. "1级起", "2级起"
    string ColorHex,
    int MinFret,
    int MaxFret,
    int[] LeftFrets,         // length 6 (0=6E .. 5=1e), 1st note fret on string
    int[] RightFrets,        // length 6, 3rd note fret on string
    int[][] StringFrets);    // length 6, each length 3 exact frets on string

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

public sealed record LessonCompareOption(
    string Label,
    string Hint,
    string ChordSymbol = "",
    int? TonicPitch = null,
    string ScaleId = "");

public sealed record LessonFretQuiz(
    string Prompt,
    int TargetPitchClass,
    int? RequiredStringIdx = null, // 0=6E .. 5=1e
    int? MinFret = null,
    int? MaxFret = null,
    string SuccessMessage = "精准命中目标音！");

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
    string TuningId = "standard",
    string KeyFormula = "",
    bool ShowThreeNps = false,
    int ThreeNpsPos = 0,
    FretboardLabelMode? PreferredLabelMode = null,
    IReadOnlyList<LessonCompareOption>? CompareItems = null,
    LessonFretQuiz? Quiz = null);

public sealed record TheoryLesson(
    string Id,
    string UnitName,
    string CodeBadge,
    string Title,
    string Summary,
    List<TheoryLessonStep> Steps,
    string PracticeDrillId = "",
    string PracticeChordSymbol = "",
    string PracticeProgressionId = "",
    string CustomProgressionChords = "");

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

    private static readonly string[] ThreeNpsColors =
    [
        "#2F6B48", "#3B82F6", "#8B5CF6", "#D97706", "#DB2777", "#0891B2", "#65A30D"
    ];

    /// <summary>
    /// Computes the 7 diagonal 3-Notes-Per-String (3NPS) positions across the neck for any 7-note scale
    /// (automatically promotes 5/6-note scales to their parent 7-note major/natural-minor scale).
    /// </summary>
    public static List<ThreeNpsBoxInstance> ComputeThreeNpsBoxes(
        int tonicPitch,
        ScaleDefinition scale,
        GuitarTuning tuning,
        int maxFret = 17)
    {
        var effScale = scale.Intervals.Length == 7
            ? scale
            : GetScale(scale.IsMinorFamily ? "natural_minor" : "major");

        var pcs = effScale.Intervals.Select(iv => ((tonicPitch + iv) % 12 + 12) % 12).ToArray();
        var pcSet = new HashSet<int>(pcs);
        var results = new List<ThreeNpsBoxInstance>(7);

        for (var posIdx = 0; posIdx < 7; posIdx++)
        {
            var startPc = pcs[posIdx];
            var baseFret6 = -1;
            for (var f = 0; f < 12; f++)
            {
                if (((tuning.OpenMidi[0] + f) % 12 + 12) % 12 == startPc)
                {
                    baseFret6 = f;
                    break;
                }
            }
            if (baseFret6 < 0) continue;

            // Build strictly 3 ascending scale notes per string from string 0 (6E) to string 5 (1e)
            var lefts = new int[6];
            var rights = new int[6];
            var strFrets = new int[6][];
            var valid = true;
            var lastMidi = tuning.OpenMidi[0] + baseFret6 - 1;
            var minF = 99;
            var maxF = -99;

            for (var s = 0; s < 6; s++)
            {
                var three = new List<int>(3);
                for (var f = 0; f <= maxFret + 4; f++)
                {
                    var midi = tuning.OpenMidi[s] + f;
                    if (midi <= lastMidi) continue;
                    if (!pcSet.Contains(((midi % 12) + 12) % 12)) continue;
                    three.Add(f);
                    lastMidi = midi;
                    if (three.Count == 3) break;
                }
                if (three.Count < 3 || three[2] > maxFret)
                {
                    valid = false;
                    break;
                }
                strFrets[s] = [three[0], three[1], three[2]];
                lefts[s] = three[0];
                rights[s] = three[2];
                if (three[0] < minF) minF = three[0];
                if (three[2] > maxF) maxF = three[2];
            }

            if (!valid) continue;

            var degLbl = GetDegreeLabel(startPc, tonicPitch);
            results.Add(new ThreeNpsBoxInstance(
                PositionNumber: posIdx + 1,
                DegreeStartLabel: $"{degLbl}级起",
                ColorHex: ThreeNpsColors[posIdx % ThreeNpsColors.Length],
                MinFret: minF,
                MaxFret: maxF,
                LeftFrets: lefts,
                RightFrets: rights,
                StringFrets: strFrets));
        }

        return results.OrderBy(x => x.MinFret).ToList();
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
        // ═══════════════════════════════════════════════════════════════════════════
        // 单元 1：指板几何与秒认音名（告别盲按）
        // ═══════════════════════════════════════════════════════════════════════════
        new("u1_01_semitones", "1. 指板几何与秒认音名", "01", "十二平均律与 E-F / B-C 天生半音",
            "理解半音=1品、全音=2品与空弦推算逻辑，彻底告别死记硬背",
            [
                new("吉他指板铁律：相邻 1 品永远相差半音",
                    "吉他是最直观的半音阶乐器：顺着同一根弦往琴身方向每挪 1 品就升高 1 个半音，走到第 12 品（双圆点品标）刚好循环满 12 个半音，回到高八度的同名音！先把上方指板切换到「音名」视角观察。",
                    0, "major", false, "", false,
                    KeyFormula: "1 品 = 1 个半音（Semitone） · 2 品 = 1 个全音（Whole Tone） · 12 品 = 高八度完整循环",
                    PreferredLabelMode: FretboardLabelMode.NoteName),
                new("为什么只有 E-F 与 B-C 紧挨不隔品？",
                    "在七个自然音名 C D E F G A B 里，绝大多数相邻字母之间都隔着 1 个升降音（相差 2 品全音），唯独『E 与 F（3-4）』、『B 与 C（7-1）』之间是天生半音，在指板上紧挨着只差 1 品！",
                    0, "major", false, "", false,
                    KeyFormula: "口诀：『3-4（E-F）、7-1（B-C）肩并肩只差 1 品，其余相邻自然音统统隔 1 品（差 2 品）』",
                    PreferredLabelMode: FretboardLabelMode.NoteName,
                    CompareItems:
                    [
                        new("C → D（全音 · 隔 2 品）", "5弦3品 C 到 5品 D 中间夹着 C#/Db，听感舒展稳健", TonicPitch: 0, ScaleId: "major"),
                        new("E → F（半音 · 紧挨 1 品）", "6弦空弦 E 到 1品 F 紧贴无空隙，半音上行极具趋近感", ChordSymbol: "Fmaj7")
                    ]),
                new("顺着 6 弦从 0 品推到 12 品实战",
                    "以最粗的 6 弦空弦 E（0品）为例：E→F 紧挨在 1 品，F→G 隔品到 3 品，G→A 到 5 品，A→B 到 7 品，B→C 紧挨在 8 品，C→D 到 10 品，D→E 回到 12 品高八度！",
                    0, "major", false, "", false,
                    KeyFormula: "6 弦自然音品位链：0(E) - 1(F) - 3(G) - 5(A) - 7(B) - 8(C) - 10(D) - 12(E)",
                    PreferredLabelMode: FretboardLabelMode.NoteName,
                    Quiz: new("🎯 随堂闯关：根据『E-F 天生半音紧挨』规律，请在上方指板的「6 弦（最下方粗弦）」上点击找出 F 音！",
                        TargetPitchClass: 5, RequiredStringIdx: 0, MaxFret: 13,
                        SuccessMessage: "太稳了！6 弦 1 品（或 13 品）就是 F 音——大横按 F 和弦的根音正是从这儿来的！"))
            ],
            PracticeDrillId: "u1_spider_1234"),

        new("u1_02_landmarks", "1. 指板几何与秒认音名", "02", "6 弦与 5 弦「黄金路标品」秒认法",
            "借琴颈珍珠品标（3/5/7/8/10品）一秒锁定全谱横按与把位根音",
            [
                new("6 弦黄金路标：3G · 5A · 7B · 8C · 10D",
                    "不需要背完整张 102 格指板！职业乐手 90% 的大横按、Power Chord 和把位音阶都以 6 弦或 5 弦做地基。先利用 3/5/7 品珍珠圆点记住 6 弦上的 G(3品)、A(5品)、B(7品)，再顺手捎上紧挨 7 品的 8 品 C！",
                    0, "major", false, "", false, HighlightChord: "G",
                    KeyFormula: "6 弦路标口诀：『三 G 五 A 七是 B，八品紧挨就是 C，十品 D 来十二 E』",
                    PreferredLabelMode: FretboardLabelMode.NoteName),
                new("5 弦黄金路标：3C · 5D · 7E · 8F · 10G",
                    "5 弦空弦是 A，由于吉他 6→5 弦相差纯四度，同一品位上 5 弦永远比 6 弦高一个纯四度：6 弦 3 品是 G，5 弦 3 品就是 C；6 弦 5 品是 A，5 弦 5 品就是 D；5 弦 8 品则是 F！",
                    0, "major", false, "", false, HighlightChord: "C",
                    KeyFormula: "5 弦路标口诀：『三 C 五 D 七是 E，八品紧挨就是 F，十品 G 来十二 A』",
                    PreferredLabelMode: FretboardLabelMode.NoteName),
                new("升降音怎么找？先钉自然音，左降 b 右升 #",
                    "遇到 Bb、F#、Eb 千万别慌：先找旁边的自然音路标，往琴头退 1 品就是降（b），往琴身进 1 品就是升（#）！例如找 5 弦上的 Eb：先钉死 5 弦 5 品 D（或 7 品 E），退 1 品即 6 品 Eb！",
                    3, "major", false, "", false,
                    KeyFormula: "升降音秒杀法：6 弦 1 品 F → 2 品即 F#；5 弦 3 品 C → 1 品是 Bb、4 品是 C#/Db、6 品是 Eb",
                    PreferredLabelMode: FretboardLabelMode.NoteName,
                    Quiz: new("🎯 随堂闯关：利用 5 弦路标口诀，请在上方指板的「5 弦」上点击找出 C 音或 F 音中的『F 音』（8品）！",
                        TargetPitchClass: 5, RequiredStringIdx: 1, MinFret: 6, MaxFret: 10,
                        SuccessMessage: "一击即中！5 弦 8 品正是 F 音（紧挨 7 品 E）！"))
            ]),

        new("u1_03_octaves", "1. 指板几何与秒认音名", "03", "八度几何与「B 弦右偏 1 品」铁律",
            "掌握两套八度三角手型，3 秒钟从低音弦顺藤摸瓜找出全指板同名音",
            [
                new("揭秘吉他调弦的唯一“拐点”：3 弦到 2 弦是大三度！",
                    "标准调弦 EADGBE 中，6→5、5→4、4→3、2→1 相邻弦全都是『纯四度（差 5 个半音）』，唯独『3 弦(G) → 2 弦(B)』只有『大三度（差 4 个半音）』！这个设计是为了让开放和弦好按，但也导致任何指型一旦跨过 2 弦就必须往右补偿 1 品！",
                    0, "major", false, "", false,
                    KeyFormula: "调弦音程链：E —(纯四)— A —(纯四)— D —(纯四)— G —【大三度！右偏1品】— B —(纯四)— E",
                    PreferredLabelMode: FretboardLabelMode.NoteName),
                new("6 弦与 5 弦起点的「隔一弦、进两品」八度框",
                    "只要没碰到 2 弦（B弦），找高八度超级工整：6 弦 n 品的音 = 4 弦 (n+2) 品；5 弦 n 品的音 = 3 弦 (n+2) 品！比如 5 弦 3 品 C，隔开 4 弦往右数 2 品，3 弦 5 品必然也是高八度 C（流行摇滚最爱的八度双音手型）！",
                    0, "major_pent", true, "A", false,
                    KeyFormula: "低音区八度公式（不跨 2 弦）：6 弦 → 4 弦（+2 品） · 5 弦 → 3 弦（+2 品）",
                    PreferredLabelMode: FretboardLabelMode.NoteName),
                new("跨过 2 弦的「隔一弦、进三品」与反向三角",
                    "一旦跨过 2 弦，把刚才的 +2 补偿 1 品变成『+3 品』即可：4 弦 n 品的音 = 2 弦 (n+3) 品；3 弦 n 品的音 = 1 弦 (n+3) 品！例如 4 弦 2 品是 E，2 弦 (2+3)=5 品也是 E！",
                    0, "major_pent", true, "C", false,
                    KeyFormula: "高音区八度公式（跨过 2 弦）：4 弦 → 2 弦（+3 品） · 3 弦 → 1 弦（+3 品） · 6 弦与 1 弦永远同品同名！",
                    PreferredLabelMode: FretboardLabelMode.NoteName,
                    Quiz: new("🎯 随堂闯关：已知 4 弦 10 品（或空弦旁）推导，请在上方指板的「2 弦（B弦）」上点击找出任意一个 C 音！",
                        TargetPitchClass: 0, RequiredStringIdx: 4,
                        SuccessMessage: "完美验证！2 弦 1 品（或 13 品）就是 C 音——从 5 弦 3 品 C 往左退 2 品到 2 弦 1 品正是 C 形骨架！"))
            ]),

        // ═══════════════════════════════════════════════════════════════════════════
        // 单元 2：音程听感与和弦色彩家族
        // ═══════════════════════════════════════════════════════════════════════════
        new("u2_04_intervals", "2. 音程听感与和弦色彩", "04", "音程距离：协和、空灵与张力的源头",
            "先在单根弦与相邻弦听懂 3 度、5 度、7 度，不再盲背和弦图",
            [
                new("纯五度（1 - 5）：摇滚强力和弦的钢铁地基",
                    "把上方指板切到「音程」视角：根音 R（橙色）与纯五度 5（隔 7 个半音，绿色）融合度极高、毫无甜腻或忧郁倾向——电吉他失真下的 Power Chord（如 C5、G5）只弹 1 和 5，靠的就是这种纯粹的力量感。",
                    0, "major_pent", true, "C", false, HighlightChord: "C",
                    KeyFormula: "纯五度 = 相差 7 个半音（指板几何：6/5/4 弦到高一弦 +2 品，正是强力和弦 1-5 手型！）",
                    PreferredLabelMode: FretboardLabelMode.Interval),
                new("大三度（3）vs 小三度（b3）：决定和弦明暗的唯一开关",
                    "既然 1 和 5 只负责搭骨架，是谁决定了开心还是悲伤？答案全在『三音』！大三度（距根音 4 个半音）明亮温暖；把三音往左退 1 品变成小三度 b3（距根音 3 个半音），色彩立刻转为内敛忧郁。",
                    0, "major", false, "", false, HighlightChord: "C",
                    KeyFormula: "大三度 (R→3) = 4 个半音（温暖明亮） · 小三度 (R→b3) = 3 个半音（忧郁叙事）",
                    PreferredLabelMode: FretboardLabelMode.Interval,
                    CompareItems:
                    [
                        new("C 大三和弦 (1-3-5)", "含大三音 E，听感阳光、开阔、尘埃落定", ChordSymbol: "C"),
                        new("Cm 小三和弦 (1-b3-5)", "三音退半音变为 Eb，瞬间染上电影悲剧与思念色彩", ChordSymbol: "Cm")
                    ]),
                new("三全音（b5 / #4）与小二度：推动音乐前进的引擎",
                    "音乐不能只有协和，否则像白开水！相差 6 个半音的『三全音（增四/减五度，指板相邻弦斜对角 1 品）』天生紧绷不安，急需向内收缩或向外张开解决到稳定的三度——这正是所有属七和弦（V7）催人落泪回家的物理秘密。",
                    0, "minor_blues", true, "G", false,
                    KeyFormula: "协和音程（1/3/5/6）负责『落脚好听』 · 不协和音程（b2 / 4 / b5 / 7）负责『制造悬念与推进』",
                    PreferredLabelMode: FretboardLabelMode.Interval,
                    Quiz: new("🎯 随堂闯关：观察上方 C 调指板的紫色圆点，请点击任意一个大三度『3 音（E）』听听它与根音的融合感！",
                        TargetPitchClass: 4,
                        SuccessMessage: "找对啦！E 正是 C 的大三度（3音），停在它上面既有旋律甜味又无比稳定！"))
            ]),

        new("u2_05_triads_sus", "2. 音程听感与和弦色彩", "05", "三和弦四大家族与 sus2 / sus4 / add9 神器",
            "彻底搞懂民谣弹唱谱里最常出现的挂留与加九色彩怎么按、怎么用",
            [
                new("大三、小三、减三（dim）与增三（aug）",
                    "三和弦由两个三度叠置而成：大+小=大三(1-3-5)；小+大=小三(1-b3-5)；小+小=减三 dim(1-b3-b5，紧缩悬疑，常作七级或半音过渡)；大+大=增三 aug(1-3-#5，奇幻漂浮，常作副歌前蓄力)。",
                    0, "major", false, "", false, HighlightChord: "C",
                    KeyFormula: "大三 = 1-3-5 · 小三 = 1-b3-5 · 减三(dim) = 1-b3-b5 · 增三(aug) = 1-3-#5",
                    CompareItems:
                    [
                        new("C (1-3-5)", "稳定晴朗的大三基准", ChordSymbol: "C"),
                        new("Cm (1-b3-5)", "柔和伤感的小三色彩", ChordSymbol: "Cm"),
                        new("Cdim (1-b3-b5)", "五音也降半音，营造悬疑转折张力", ChordSymbol: "Cdim"),
                        new("Caug (1-3-#5)", "五音升半音，充满梦幻膨胀推进感", ChordSymbol: "Caug")
                    ]),
                new("挂留和弦 sus2 与 sus4：抽掉三音后的呼吸感",
                    "『sus（Suspended 挂留）』的意思是：把决定悲喜的三音（3）暂时拿掉，换成 2 音（sus2 = 1-2-5）或 4 音（sus4 = 1-4-5）！因为没有三音，它不悲不喜、仙气飘飘；弹唱里在 D → Dsus4 → D → Dsus2 之间动一根小指/中指，伴奏立刻活起来！",
                    2, "major", false, "", false, HighlightChord: "Dsus2",
                    KeyFormula: "sus2 = 1 - 2 - 5（空灵清澈） · sus4 = 1 - 4 - 5（蓄势待发，极想回落到 3 音！）",
                    CompareItems:
                    [
                        new("D (1-3-5)", "标准明亮的 D 大三和弦", ChordSymbol: "D"),
                        new("Dsus4 (1-4-5)", "小指按住 1 弦 3 品 G，营造悬停期待感", ChordSymbol: "Dsus4"),
                        new("Dsus2 (1-2-5)", "松开 1 弦露出空弦 E，现代民谣标志性的通透呼吸感", ChordSymbol: "Dsus2")
                    ]),
                new("民谣清澈密码 add9：保留三音再添一颗高音 9 度",
                    "千万别把 add9 和 sus2 搞混！sus2 丢掉了 3 音，而『add9（1-3-5-9）』既保留了温暖的大三音 3，又在高音区点缀了一颗高八度的 2 音（9音）——像 Cadd9、Gadd9 既有大调的甜美又有晶莹泛音，是木吉他分解和弦的王牌！",
                    0, "major", false, "", false, HighlightChord: "Cadd9",
                    KeyFormula: "sus2 (1-2-5，无3音，中性空灵)  ≠  add9 (1-3-5-9，有3音，甜美丰满)  ≠  9和弦 (1-3-5-b7-9，含七音)",
                    CompareItems:
                    [
                        new("C 标准三和弦", "干净朴素的基础伴奏声响", ChordSymbol: "C"),
                        new("Csus2 (1-2-5)", "拿掉三音 E，声音中性空旷", ChordSymbol: "Csus2"),
                        new("Cadd9 (1-3-5-9)", "保留 E 同时在 2 弦 3 品加入九音 D，层次瞬间丰富！", ChordSymbol: "Cadd9")
                    ],
                    Quiz: new("🎯 随堂闯关：Cadd9 是在 C (C-E-G) 基础上加了九音（即高八度的 2 级音）。请在上方指板点击找出 C 调的『2/9 音（D）』！",
                        TargetPitchClass: 2,
                        SuccessMessage: "答对了！D 音正是 C 调的 2 音（高八度即 9 音），2 弦 3 品那颗清脆的高音就是它！"))
            ],
            PracticeChordSymbol: "Cadd9"),

        new("u2_06_seventh_chords", "2. 音程听感与和弦色彩", "06", "四大七和弦：流行、爵士与蓝调的灵魂",
            "在三和弦顶上再叠一个七音，解锁都市流行、R&B 与爵士的微醺质感",
            [
                new("大七和弦 maj7（1 - 3 - 5 - 7）：都市夜晚与温柔滤镜",
                    "在大三和弦（1-3-5）上叠加『大七度 7（离高八度根音只差半音）』，轻微的半音摩擦非但不刺耳，反而产生极其浪漫、松弛、略带怀旧的高级感！开放 Cmaj7（x32000）和 Fmaj7（xx3210）好按又惊艳。",
                    0, "major", false, "", false, HighlightChord: "Cmaj7",
                    KeyFormula: "maj7 = 大三和弦 (1-3-5) + 大七度 (7) · 常用于调内 I 级（Cmaj7）与 IV 级（Fmaj7）"),
                new("属七和弦 7（1 - 3 - 5 - b7）：强烈想“回家”的推手",
                    "注意写法：单写一个数字『7』（如 G7、C7、E7）代表大三和弦加『小七度 b7』！此时大三音（3）与小七音（b7）刚好构成不稳定的三全音，像拉满的弓弦一样渴望解决回主和弦（如 G7 → C）！而在布鲁斯里，全曲都可以用属七制造粗犷律动。",
                    0, "mixolydian", false, "", false, HighlightChord: "G7",
                    KeyFormula: "属七 (X7) = 1 - 3 - 5 - b7（内置 3 与 b7 三全音张力，天然驱动 V7 → I 解决！）"),
                new("小七 m7 与半减七 m7b5：丝绒叙事与爵士小调二级",
                    "小三和弦加 b7 得到『小七 m7（1-b3-5-b7）』，棱角全消、温柔如丝绒（R&B / Neo-Soul 满地都是 Am7、Dm7）；若再把五音降半音变成『m7b5（又称 ø7，1-b3-b5-b7）』，则是顺阶第 7 级与小调 2-5-1（Bm7b5 → E7 → Am）的催泪利器！",
                    0, "major", false, "", false, HighlightChord: "Dm7",
                    KeyFormula: "四大七和弦速查：maj7(1-3-5-7) · 7(1-3-5-b7) · m7(1-b3-5-b7) · m7b5(1-b3-b5-b7)",
                    CompareItems:
                    [
                        new("Cmaj7 (1-3-5-7)", "温暖梦幻、慵懒漂浮的大七色彩", ChordSymbol: "Cmaj7"),
                        new("C7 (1-3-5-b7)", "蓝调张力十足、急切想往 F 解决的属七", ChordSymbol: "C7"),
                        new("Cm7 (1-b3-5-b7)", "柔和都市 R&B 质感的小七和弦", ChordSymbol: "Cm7"),
                        new("Cm7b5 (1-b3-b5-b7)", "深邃凝重的半减七色彩", ChordSymbol: "Cm7b5")
                    ],
                    Quiz: new("🎯 随堂闯关：G7 (G-B-D-F) 与 Gmaj7 (G-B-D-F#) 只差七音！请在上方 C 大调指板点击找出 G7 里的『小七音 F』！",
                        TargetPitchClass: 5,
                        SuccessMessage: "精准！F 正是 G 的小七音（b7），它和 G7 里的 B 音构成三全音，推着耳朵回到 C 和弦！"))
            ],
            PracticeDrillId: "u4_54_7th_up",
            PracticeChordSymbol: "Cmaj7",
            PracticeProgressionId: "jazz_251"),

        // ═══════════════════════════════════════════════════════════════════════════
        // 单元 3：大小调、五声、蓝调与调式色彩
        // ═══════════════════════════════════════════════════════════════════════════
        new("u3_07_pentatonic_secret", "3. 大小调、五声与调式色彩", "07", "自然大调全半音骨架与五声音阶万能法则",
            "看透『全全半全全全半』与为什么要去掉 4 和 7 两个敏感音",
            [
                new("自然大调的基因密码：全 - 全 - 半 - 全 - 全 - 全 - 半",
                    "无论从哪颗音出发，只要严格按照『2品-2品-1品-2品-2品-2品-1品』的步长走，弹出来的永远是 Do Re Mi Fa Sol La Si Do 的明亮大调！其中 3-4 级与 7-1 级两处半音正是调性感最强的锚点。",
                    0, "major", false, "", false,
                    KeyFormula: "自然大调半音距：[0, 2, 4, 5, 7, 9, 11] · 半音位置锁定在 3↔4 与 7↔1"),
                new("为什么新手拿七声音阶即兴容易“踩雷”？",
                    "在大调主和弦（1-3-5）铺底时，如果你不小心重重停在『4 音』上，它会和大三音『3』产生尖锐的小二度半音打架（俗称 Avoid Note 避用音）；停在『7 音』上则会悬在半空落不下来。",
                    0, "major", true, "G", false, HighlightChord: "C",
                    KeyFormula: "4 级（Fa）建议作为经过音快速滑向 3 音（Mi）；7 级（Si）建议向上半音解决回 1 音（Do）"),
                new("抽掉 4 和 7 = 万能五声音阶（每弦恰好 2 个音！）",
                    "把 4 和 7 两个半音碰撞源头一删，剩下的『1 - 2 - 3 - 5 - 6』没有任何半音关系，随便停在哪个音上都与背景和弦和谐共鸣！更妙的是，在吉他上它变成了极其规整的『每根弦 2 个音（双音胶囊）』，推弦、滑音、击勾弦顺手到飞起！",
                    0, "major_pent", true, "ALL", true,
                    KeyFormula: "大调五声音阶 = 1 - 2 - 3 - 5 - 6（中国宫商角徵羽同构 · 零半音冲突 · 吉他每弦 2 音）",
                    CompareItems:
                    [
                        new("C 自然大调 (7音)", "含 4(F) 与 7(B)，叙事完整但落音需讲究解决", TonicPitch: 0, ScaleId: "major"),
                        new("C 大调五声 (5音)", "去掉 F 与 B，粒粒清澈协和，民谣/乡村加花神器", TonicPitch: 0, ScaleId: "major_pent")
                    ],
                    Quiz: new("🎯 随堂闯关：五声音阶由 1-2-3-5-6 组成。请在上方 C 大调五声指板中点击找出甜美的『6 音（A）』！",
                        TargetPitchClass: 9,
                        SuccessMessage: "漂亮！6 音（A）是大调五声里最有民谣歌唱感的一颗色彩音！"))
            ],
            PracticeDrillId: "u1_box_pent"),

        new("u3_08_relative_minor", "3. 大小调、五声与调式色彩", "08", "关系大小调同源揭秘：为什么 C 大调 = A 小调",
            "不背第二套指型！只换落脚重心，一套手型同时通吃大调与小调",
            [
                new("退 3 品找“亲兄妹”：大调的 6 级就是关系小调主音",
                    "C 自然大调（C D E F G A B）与 A 自然小调（A B C D E F G）用的完完全全是同一批音！在吉他指板上，任意大调根音往琴头方向退 3 品（小三度，即 6 级 La），就是它的关系小调主音（如 C→Am、G→Em、F→Dm、D→Bm）。",
                    0, "major", true, "G", false,
                    KeyFormula: "关系小调主音 = 大调主音往下退 3 品（小三度 / 第 6 级） · 常用对：C↔Am · G↔Em · D↔Bm · F↔Dm · A↔F#m"),
                new("手型一模一样，为什么听起来一个阳光、一个忧伤？",
                    "切到上方 5–8 品的薰衣草紫框看看：同一套按法（6弦 5-8、5弦 5-7、4弦 5-7...），如果你围绕『C-E-G』起止并垫 C 和弦，它就是阳光的 C 大调五声；如果你围绕『A-C-E（6弦5品/4弦7品）』起止并垫 Am 和弦，它瞬间变成酷炫忧伤的 A 小调五声第 1 把位！",
                    9, "minor_pent", true, "G", true, HighlightChord: "Am",
                    KeyFormula: "小调五声公式 = 1 - b3 - 4 - 5 - b7（与关系大调五声 1-2-3-5-6 指板坐标 100% 重合，唯独根音重心不同！）",
                    CompareItems:
                    [
                        new("垫 C 和弦 · C 大调重心", "以 C 为家，同指型听起来温暖田园", ChordSymbol: "C", TonicPitch: 0, ScaleId: "major_pent"),
                        new("垫 Am 和弦 · A 小调重心", "以 6 弦 5 品 A 为家，同一套手型瞬间变为摇滚抒情小调！", ChordSymbol: "Am", TonicPitch: 9, ScaleId: "minor_pent")
                    ]),
                new("实战背图偷懒秘籍：五套指型直接翻倍成十套！",
                    "这意味着你只要练熟 CAGED 五个五声指型，就同时学会了全部大调五声 + 全部小调五声！弹小调歌曲时，既可以直接把调号选到 Am 看『1 b3 4 5 b7』，也可以在脑中借关系大调 C 定位整根指板。",
                    9, "minor_pent", true, "ALL", true,
                    KeyFormula: "指板记忆减负定律：学会 5 个 CAGED 把位 = 同时掌握 12 个大调 + 12 个关系小调！",
                    Quiz: new("🎯 随堂闯关：如果一首歌是 G 大调，它的关系小调主音（退 3 品 / 第 6 级）是谁？请在指板上点击找出这颗音（E）！",
                        TargetPitchClass: 4,
                        SuccessMessage: "完全正确！G 往左退 3 品正是 E——所以 G 大调与 Em 小调共用全套顺阶和弦与音阶指型！"))
            ],
            PracticeDrillId: "u1_slide_mpent",
            PracticeProgressionId: "minor_6415"),

        new("u3_09_blues_note", "3. 大小调、五声与调式色彩", "09", "布鲁斯蓝调音 (b5) 的灵魂哭腔与微推弦",
            "在五声音阶里点入一颗『魔鬼半音』，瞬间解锁 Blues、摇滚与流行加花味道",
            [
                new("小调 Blues 音阶：在 4 与 5 之间塞入『b5』",
                    "观察上方 A 小调 Blues 指板（5–8 品）：它在小调五声『1 - b3 - 4 - 5 - b7』的 4 音(D)与 5 音(E)正中间，塞进了一颗降五音『b5（Eb，如 5 弦 6 品、3 弦 8 品）』，形成连续三个半音（4 - b5 - 5）的丝滑阶梯！",
                    9, "minor_blues", true, "G", false,
                    KeyFormula: "小调布鲁斯音阶 = 1 - b3 - 4 -【b5 蓝调音】- 5 - b7（共 6 个音）"),
                new("蓝调音使用铁律：『路过即惊艳，久留变车祸』",
                    "b5 与根音构成极不稳定的三全音——千万不要在拍头死死停在 b5 上不动！正确用法是：从 4 音半音推弦/滑音经过 b5 冲向 5 音（4→b5→5），或者下行时从 5 音顺着 b5 滑落到 4 音再回 b3，一瞬间就有老派黑人吉他手的沧桑叹息感。",
                    9, "minor_blues", true, "G", false, HighlightChord: "A7",
                    KeyFormula: "经典蓝调连复段路径：5 → b5(滑) → 4 → b3 → 1，或 3 弦 7 品(4音)推半音摸到 b5 再回落！",
                    CompareItems:
                    [
                        new("A 小调五声（纯净版）", "干脆利落，但少了半音摩擦的泥土腥辣味", TonicPitch: 9, ScaleId: "minor_pent"),
                        new("A 小调 Blues（加 b5）", "多了 Eb 蓝调经过音，立刻带出浓郁 Blues 哭腔！", TonicPitch: 9, ScaleId: "minor_blues"),
                        new("A 大调 Blues（加 b3）", "在大调五声 2 与 3 之间加入 #2/b3 滑音，轻快俏皮的乡村爵士味", TonicPitch: 9, ScaleId: "major_blues")
                    ]),
                new("大调 Blues 的甜辣秘方：从 b3 滑向大三音 3",
                    "除了忧郁小调，欢快曲风常用『大调 Blues（1 - 2 - b3 - 3 - 5 - 6）』：每次弹到大三音 3 之前，故意先在低半音的 b3 上蹭一下滑过去（b3→3），哪怕在普通民谣弹唱的 C 和弦、G 和弦空隙加这一招，老手味直接拉满！",
                    0, "major_blues", true, "C", false, HighlightChord: "C7",
                    KeyFormula: "大调 Blues = 1 - 2 -【b3→3 滑音】- 5 - 6（与关系小调 Blues 同样 100% 同音同指型！）",
                    Quiz: new("🎯 随堂闯关：回到 A 小调 Blues（或 C 大调 Blues），请在指板上点击找出那颗灵魂蓝调音『Eb / D#』！",
                        TargetPitchClass: 3,
                        SuccessMessage: "稳准狠！Eb/D# 正是 A 小调的 b5 蓝调音（也是 C 大调 Blues 滑向 E 的 b3 甜味音）！"))
            ],
            PracticeProgressionId: "blues_12"),

        new("u3_10_modes", "3. 大小调、五声与调式色彩", "10", "三大实战教会调式：用「平行特征音」一秒听懂",
            "抛弃天书式死记硬背！只改 1 颗特征音，掌握 Dorian、Mixolydian 与 Lydian",
            [
                new("调式防晕秘诀：别按关系调背，按『同主音加减滤镜』听！",
                    "很多教材讲『D Dorian 是从 D 弹到 D 的 C 大调』，结果弹出来还是满脑子 C 大调！真正大师级的思维叫『平行对比法』：全部固定以 A（或 C）作主音，把调式看成是在普通大/小调上『拧动了唯一一颗特征色彩旋钮』！",
                    9, "natural_minor", false, "", false, HighlightChord: "Am7",
                    KeyFormula: "平行调式记忆法：小调阵营看 6 级/2 级变化 · 大调阵营看 4 级/7 级变化"),
                new("① Dorian（多利亚）：最酷的明亮小调（把 b6 升成 6！）",
                    "普通自然小调（1-2-b3-4-5-b6-b7）里的 b6 非常悲情压抑；把 b6 升高半音变成自然大六度『6』（在 A Dorian 里就是把 F 升成 F#），立刻去掉了苦情味，变得时尚、律动、充满都市感！Santana《Oye Como Va》、Pink Floyd 与无数 Funk 伴奏（Am7 → D7）全靠这颗 6 音！",
                    9, "dorian", true, "G", false, HighlightChord: "Am7",
                    KeyFormula: "Dorian = 自然小调 + 升 6 级（1 - 2 - b3 - 4 - 5 -【6】- b7） · 标志和弦进行：im7 ↔ IV7（如 Am7 ↔ D7）"),
                new("② Mixolydian（混合利底亚）：带蓝调野性的摇滚大调（把 7 降成 b7！）",
                    "把自然大调温顺的导音 7 降半音变成『b7』（1-2-3-4-5-6-b7），大调明亮的三音依然在，但结尾少了学院派乖巧感，多了一股洒脱不羁的公路摇滚劲儿（Beatles《Norwegian Wood》、AC/DC、Grateful Dead 常用，核心走向 I → bVII → IV，如 A → G → D）！",
                    9, "mixolydian", true, "E", false, HighlightChord: "A7",
                    KeyFormula: "Mixolydian = 自然大调 + 降 7 级（1 - 2 - 3 - 4 - 5 - 6 -【b7】） · 完美契合属七和弦 X7 Solo"),
                new("③ Lydian（利底亚）：失重漂浮的电影太空大调（把 4 升成 #4！）",
                    "把自然大调里容易打架的 4 音升高半音变成『#4』（1-2-3-#4-5-6-7），不仅消除了与 3 音的半音冲突，还凭空造出久石让、好莱坞科幻大片与 Joe Satriani 标志性的梦幻悬浮感（典型伴奏 Cmaj7 → D/C）！点下方按钮一秒对比四种人格：",
                    0, "lydian", false, "", false, HighlightChord: "Cmaj7",
                    KeyFormula: "三大调式一句话：Dorian 小调亮 6（Funk/Santana） · Mixolydian 大调降 b7（摇滚/属七） · Lydian 大调升 #4（梦幻电影）",
                    CompareItems:
                    [
                        new("A 自然小调 (含 b6=F)", "传统伤感叙事小调", TonicPitch: 9, ScaleId: "natural_minor"),
                        new("A Dorian (特征 6=F#)", "注意听 F# 带来的摩登灵动感！", TonicPitch: 9, ScaleId: "dorian"),
                        new("C Mixolydian (特征 b7=Bb)", "大调骨架配上摇滚 b7 洒脱尾韵", TonicPitch: 0, ScaleId: "mixolydian"),
                        new("C Lydian (特征 #4=F#)", "注意第 4 个音 F# 腾空而起的科幻仙气！", TonicPitch: 0, ScaleId: "lydian")
                    ],
                    Quiz: new("🎯 随堂闯关：当前上方是 C Lydian（1-2-3-#4-5-6-7），请在指板上点击找出它最具科幻感的特征音『#4（F#）』！",
                        TargetPitchClass: 6,
                        SuccessMessage: "耳朵和眼睛都太敏锐了！F# 正是 C Lydian 的 #4 特征音，垫在 Cmaj7 上仙气十足！"))
            ]),

        // ═══════════════════════════════════════════════════════════════════════════
        // 单元 4：CAGED 全景系统（大调 + 小调贯通）
        // ═══════════════════════════════════════════════════════════════════════════
        new("u4_11_caged_core", "4. CAGED 全景系统", "11", "CAGED 底层逻辑：5 个开放和弦无缝铺满琴颈",
            "看懂为什么偏偏是 C-A-G-E-D 这五个字母，彻底打通把位隔阂",
            [
                new("为什么叫 CAGED，不叫 BDFGA？",
                    "在吉他开放把位（0–3品）里，只有 C、A、G、E、D 这 5 个大三和弦不用大横按就能完整弹出开放骨架（B 和 F 天生需要横按，本质就是平移后的 A 形和 E 形）！把这 5 个开放手型往高把位推，就成了覆盖全指板的 5 种基础模具。",
                    0, "major_pent", true, "ALL", false,
                    KeyFormula: "CAGED 本质 = 5 个开放和弦模具（食指充当移动琴枕 Barre）在整根琴颈上的无限循环拼接"),
                new("咬合铁律：前一块的右边，永远等于下一块的左边！",
                    "观察上方 5 种颜色的轮廓框：橄榄绿 C 形的右侧音列，分毫不差就是樱桃粉 A 形的左侧音列；A 形右边咬着薰衣草紫 G 形；G 形右边咬着钴蓝 E 形；E 形右边咬着青碧 D 形；到 12 品后 D 形右边又接回高八度 C 形！",
                    0, "major_pent", true, "ALL", true,
                    KeyFormula: "永久循环顺序（不论什么调永远不变！）：C → A → G → E → D → C → A ..."),
                new("点按顶栏徽章：既能单块放大深挖，又能全指板鸟瞰",
                    "点击指板正上方的彩色胶囊徽章（或本卡片下方的五把位速览卡），可以单独点亮任意一块把位框，再点一次恢复全景。记住这个顺序后，哪怕闭着眼睛弹到 9 品，你也知道左边是谁、右边是谁！",
                    0, "major_pent", true, "ALL", true,
                    KeyFormula: "定位秘诀：在 5 弦找到根音，往左看是 C 形、往右看是 A 形；在 6 弦找到根音，往左看是 G 形、往右看是 E 形！",
                    Quiz: new("🎯 随堂闯关：在 C 大调中，6 弦 8 品是根音 C（E 形起点）。请点击上方指板「6 弦 8 品」亲自听听这颗核心锚点！",
                        TargetPitchClass: 0, RequiredStringIdx: 0, MinFret: 7, MaxFret: 9,
                        SuccessMessage: "定位精准！6 弦 8 品往左铺开就是 G 形（5–8品），往右铺开就是 E 形（8–10品）！"))
            ]),

        new("u4_12_caged_five_shapes", "4. CAGED 全景系统", "12", "五大把位逐块拆解：和弦骨架就藏在音阶里",
            "逐个吃透 C / A / G / E / D 五块的根音弦、横按指法与加花甜点区",
            [
                new("① C 形把位（C 调 0–3 品 · 根音在 5 弦与 2 弦）",
                    "看上方橄榄绿框：橙色根音 1 与紫/绿内音 3、5 围成的正是开放 C 和弦（x32010）！移到高把位时，用无名指踩住 5 弦根音、食指躺平按 3/2 弦，非常适合做清脆的民谣击勾弦加花（如在 4 弦 0→2 品、2 弦 1→3 品击弦）。",
                    0, "major_pent", true, "C", true, HighlightChord: "C",
                    KeyFormula: "C 形锚点：主根音在 5 弦（无名指/小指侧）+ 副根音在 2 弦（食指侧）"),
                new("② A 形把位（C 调 3–5 品 · 根音在 5 弦与 3 弦）",
                    "樱桃粉框：与左侧 C 形共用『5 弦 3 品根音』，但手掌向右展开，里面藏着五弦根音大横按和弦（x35553，即 A 形平移 3 品）！它的 4/3/2 弦同一品刚好是 5-1-3 三和弦小横按，伴奏切音极其干脆。",
                    0, "major_pent", true, "A", true,
                    KeyFormula: "A 形锚点：主根音在 5 弦（食指侧）+ 副根音在 3 弦（无名指侧 +2 品八度）"),
                new("③ G 形把位（C 调 5–8 品 · 吉他手最熟的国民指型！）",
                    "薰衣草紫框：全横按 G 形完整 6 弦较难按，实战通常只抓中间 432 弦同品小横按（xx5558）；但它的五声音阶框（6弦5-8、5弦5-7、4弦5-7、3弦5-7、2弦5-8、1弦5-8）正是天下吉他手第一天背的黄金指型！",
                    0, "major_pent", true, "G", true,
                    KeyFormula: "G 形锚点：主根音在 6 弦与 1 弦（小指侧）+ 副根音在 3 弦（食指小横按）"),
                new("④ E 形把位（C 调 7–10 品 · 六弦大横按主阵地）",
                    "钴蓝框：以『6 弦 8 品』为食指根音，里面藏着最经典的 E 形大横按 C 和弦（8-10-10-9-8-8）！在电吉他与木吉他高把位编配中，取它的 4-3-2-1 弦高音小四弦（xx-10-9-8-8）既省力又清亮穿透。",
                    0, "major_pent", true, "E", true,
                    KeyFormula: "E 形锚点：主根音在 6 弦与 1 弦（食指横按）+ 副根音在 4 弦（无名指 +2 品）"),
                new("⑤ D 形把位（C 调 10–13 品 · 高音晶莹区）",
                    "青碧框：以『4 弦 10 品』为食指根音，把开放 D 和弦（xx0232）平移到高把位变成（xx-10-12-13-12）！再往右迈一步，12–15 品就完美接回了高八度 C 形，整根琴颈正式闭环！",
                    0, "major_pent", true, "D", true,
                    KeyFormula: "D 形锚点：主根音在 4 弦（食指）+ 副根音在 2 弦（无名指/小指 +3 品，因跨 B 弦补偿！）")
            ],
            PracticeDrillId: "u1_slide_majpent"),

        new("u4_13_minor_caged", "4. CAGED 全景系统", "13", "小调 CAGED 实战三剑客（Am / Em / Dm 形）",
            "把大三音退半音，掌握小调伴奏与 Solo 最顺手的三大黄金手型",
            [
                new("小调也有五块拼图，但实战最常用『Am · Em · Dm』三剑客",
                    "理论上小调也有 Cm-Am-Gm-Em-Dm 五个手型，但 Cm 形与 Gm 形按起来反人类，职业吉他手 95% 的小调高把位伴奏都靠最符合人体工学的『Em 形（6弦根音横按）』、『Am 形（5弦根音横按）』与『Dm 形（4弦根音高音簇）』完成！",
                    9, "minor_pent", true, "ALL", false, HighlightChord: "Am",
                    KeyFormula: "小调高把位伴奏主力：6 弦找根音按 Em 形 · 5 弦找根音按 Am 形 · 4 弦高音点缀按 Dm 形"),
                new("看懂命名小细节：为什么 A 小调五声第 1 把位标着『G 形』？",
                    "观察上方 A 小调 5–8 品：它之所以对应薰衣草紫框，是因为 A 小调与 C 大调同源（5–8 品正是 C 大调 G 形，同时也是 A 小调以 6 弦 5 品为食指根音的 Em 横按和弦所在区）！把和弦高音与五声框叠在一起看，一秒通透。",
                    9, "minor_pent", true, "G", true, HighlightChord: "Am",
                    KeyFormula: "同把位对照：6 弦食指小调横按（Em 手型，如 5 品 Am）完美嵌在小调五声第 1 把位正中央！",
                    CompareItems:
                    [
                        new("Am 开放把位 (Am形 · 0-3品)", "5弦空弦根音，温暖木吉他分解首选", ChordSymbol: "Am"),
                        new("Am 五品横按 (Em形 · 5-8品)", "6弦5品根音，完美重合 5-8 品黄金 Solo 把位", ChordSymbol: "Am")
                    ]),
                new("同根音大小调原地变形：只动三音 1 品！",
                    "在同一个把位里，从 A 大调（E 形横按 577655）切到 A 小调（Em 形横按 577555），仅仅只是把中指松开，让 3 弦 6 品的大三音 C# 退 1 品变成 5 品的小三音 C！记住这 1 品之差，转调借用信手拈来。",
                    9, "minor_pent", true, "G", false,
                    KeyFormula: "大小调原地变形公式：根音 R 与五音 5 纹丝不动，仅把大三音 (3) 往琴头退 1 品即变为小三音 (b3)！",
                    Quiz: new("🎯 随堂闯关：当前是 A 小调（根音 A、小三音 C、五音 E）。请在 5–8 品把位内点击任意一颗小三音『C』！",
                        TargetPitchClass: 0, MinFret: 5, MaxFret: 8,
                        SuccessMessage: "太棒了！无论是 6 弦 8 品还是 3 弦 5 品，都是决定 Am 忧郁色彩的小三音 C！"))
            ],
            PracticeChordSymbol: "Am7"),

        new("u4_14_caged_transpose_target", "4. CAGED 全景系统", "14", "12 调平移与「同一把位」靶向内音即兴",
            "换调如平移积木，换和弦不换把位——揭秘职业乐手 Solo 好听的核心",
            [
                new("整条彩带平移：从 C 大调推到 G 大调与 D 大调",
                    "看上方指板切到了 G 大调：C→A→G→E→D 的颜色咬合顺序丝毫未变，仅仅是整条五色拼图一起顺移，让 0–3 品刚好落在了 G 形（开放 G 和弦）！无论乐队喊出什么调，先在 6 弦或 5 弦钉住一颗根音，五块地图瞬间自动展开。",
                    7, "major_pent", true, "ALL", true, HighlightChord: "G",
                    KeyFormula: "全调通吃法则：调号变了，C→A→G→E→D 的前后邻居关系永远不变！"),
                new("告别“爬音阶机器”：为什么你弹五声像做广播体操？",
                    "很多琴友背熟了指型，Solo 却干瘪乏味，原因只有一个：不管背景伴奏换成了什么和弦，手指都在机械上下爬同一条音阶！真正好听的旋律，会在和弦切换瞬间把落脚音精准停在『当拍和弦的 1/3/5 内音（Chord Tones）』上！",
                    7, "major_pent", true, "G", true, HighlightChord: "G",
                    KeyFormula: "即兴黄金定律：『拍间经过用音阶音（2/4/6/7），长音落脚盯死当前和弦内音（1/3/5）』"),
                new("终极空间掌控：死锁 5–8 品，原地切换和弦与调性！",
                    "不需要满指板乱窜！哪怕左手死锁在 5–8 品不动：背景弹 G 时你落在 G 形内音，背景换 C 时原地切到 C 大调 G 形内音，背景换 D 时原地切到 D 大调 C 形内音——去「和弦进行」开启『靶向音染色』，或去「练功房」挑战第 58 课同一把位五度圈，亲手打通任督二脉！",
                    7, "major", true, "E", false, HighlightChord: "C",
                    KeyFormula: "任何 4 品窗口内，都同时躺着全部 12 个和弦的就近转位内音——就近解决才是最丝滑的声部连接！",
                    CompareItems:
                    [
                        new("G 和弦内音 (G-B-D)", "在 5-8 品内锚定 D 形/E 形交界处的 G、B、D", ChordSymbol: "G"),
                        new("Em 和弦内音 (E-G-B)", "手腕不动，重心滑向 5 弦 7 品 E，情绪瞬间转深沉", ChordSymbol: "Em"),
                        new("C 和弦内音 (C-E-G)", "仍在同一把位（5弦7品E、4弦5品G、3弦5品C）无缝衔接！", ChordSymbol: "C"),
                        new("D 和弦内音 (D-F#-A)", "落向 3 弦 7 品 D 或 2 弦 7 品 F#，制造强烈回归推力！", ChordSymbol: "D")
                    ])
            ],
            PracticeDrillId: "u5_58_cof_pent",
            PracticeProgressionId: "pop_1645"),

        // ═══════════════════════════════════════════════════════════════════════════
        // 单元 5：CAGED vs 3NPS（每弦三音）双核系统
        // ═══════════════════════════════════════════════════════════════════════════
        new("u5_15_three_nps", "5. CAGED 与 3NPS 双核系统", "15", "3NPS 七把位斜向几何：速弹与模进的高速公路",
            "开启每弦严格 3 个音的对称矩阵，让三连音、四音模进与击勾连奏快如闪电",
            [
                new("传统 CAGED 指型的唯一痛点：每根弦音数不对称",
                    "弹七声音阶时，传统纵向 CAGED 指型有的弦弹 3 个音、有的弦（如 B 弦或 G 弦）只弹 2 个音——一旦速度飙起来做三音模进或交替拨弦，右手换弦节奏就会一脚深一脚浅。为此，现代吉他手发明了『3NPS（3 Notes Per String 每弦三音）』系统！",
                    7, "major", false, "", false,
                    ShowThreeNps: true, ThreeNpsPos: 0,
                    KeyFormula: "3NPS 核心定义：6 根弦 × 每弦严格 3 个音 = 18 个音（跨越两个多八度，拨弦规律 100% 镜像对称！）"),
                new("七个斜向把位框：从 6 弦 1～7 级顺次出发",
                    "看上方自动点亮的 3NPS 彩色斜框！因为自然大调有 7 个音级，从 6 弦的第 1、2、3、4、5、6、7 级分别起步，每弦往上铺满 3 个音，就自然向琴身方向倾斜延伸出『1把 ～ 7把』共 7 个把位（点指板顶部的 1把～7把 徽章可逐把单独检视）！",
                    7, "major", false, "", false,
                    ShowThreeNps: true, ThreeNpsPos: 1,
                    KeyFormula: "3NPS 把位口诀：1把起于 1 级（Ionian） · 2把起于 2 级（Dorian） · ... · 6把起于 6 级（关系小调第 1 把位！）"),
                new("左手只有 3 种指距模具：全全 · 半全 · 全半",
                    "别被斜框吓到！在任意一根弦上按 3 个自然音阶音，手指间距永远逃不出 3 种模板：①『全+全』（如 3-5-7 品，微张手型）；②『半+全』（如 5-6-8 品，1-2-4 指）；③『全+半』（如 5-7-8 品，1-3-4 指）！而且相邻两弦经常出现完全相同的手型复制！",
                    7, "major", false, "", false,
                    ShowThreeNps: true, ThreeNpsPos: 1,
                    KeyFormula: "单弦三音仅 3 种手型：『全全（隔1隔1）』、『半全（紧挨+隔1）』、『全半（隔1+紧挨）』",
                    Quiz: new("🎯 随堂闯关：当前聚焦在 G 大调 3NPS 第 1 把位（6弦从 3 品 G 出发：3-5-7品）。请点击 6 弦第 3 个音『7 品 B』感受张手跨度！",
                        TargetPitchClass: 11, RequiredStringIdx: 0, MinFret: 6, MaxFret: 8,
                        SuccessMessage: "手型到位！G 大调 6 弦 3-5-7 品与 5 弦 3-5-7 品连续两根弦都是『全+全』对称张手！"))
            ],
            PracticeDrillId: "u1_3nps_major"),

        new("u5_16_hybrid_navigation", "5. CAGED 与 3NPS 双核系统", "16", "双核融合：实战什么时候用 CAGED，什么时候切 3NPS？",
            "高手从不二选一！教你把纵向和弦锚点与斜向滑音高速路焊成一张网",
            [
                new("场景一：抒情歌唱、双音加花、推弦揉弦 → 选 CAGED！",
                    "当你在民谣弹唱间奏加花、弹 R&B 双音（Double Stops），或者需要精准停在当前和弦的三音/五音上做深情推弦时，纵向紧凑的『CAGED 五声/七声框』不用大劈叉，食指还能随时横按垫出和弦底色，稳如泰山。",
                    7, "major_pent", true, "G", true, HighlightChord: "G",
                    KeyFormula: "CAGED 最佳主场：伴奏加花（Chord Melody） · 五声蓝调推弦 · 同一把位内音勾勒"),
                new("场景二：三连音/十六分模进、大跨度冲刺上行 → 切 3NPS！",
                    "当音乐推向高潮，需要从低音 6 弦一口气用三音模进、四音模进或三连音冲上高音 1 弦时，立刻切换成『3NPS 斜向视角』！不仅每根弦固定 3 音永不卡壳，还顺势把你的左手从低把位自然带到了高把位甜点区！",
                    7, "major", false, "", false,
                    ShowThreeNps: true, ThreeNpsPos: 0,
                    KeyFormula: "3NPS 最佳主场：三音/四音/五音模进 · 高速交替拨弦与连音（Legato） · 两个八度长线条冲刺"),
                new("终极黏合剂：「对角线滑音高速公路」无缝穿梭",
                    "怎么把两个系统彻底打通？秘诀是利用无名指或食指『顺弦滑音 2 品（Slide）』作为换挡离合器！比如 A 小调五声从 6 弦 3→5 品滑入第 1 把位，到 3 弦 7→9 品一滑直接飞进高把位——点击下方「去练功房实操」即可跟着动态滑音谱面练熟这条黄金高速路！",
                    9, "minor_pent", true, "ALL", true,
                    KeyFormula: "小调五声黄金对角线（A小调为例）：6弦(3s5) → 5弦(5-7) → 4弦(5s7) → 3弦(5-7s9) → 2弦(8-10) → 1弦(8-10s12)",
                    Quiz: new("🎯 随堂闯关：在 A 小调对角线滑音路线上，3 弦 7 品 D 往右滑 2 品到『3 弦 9 品』是什么音？请在指板上点击 3 弦 9 品验证！",
                        TargetPitchClass: 4, RequiredStringIdx: 3, MinFret: 8, MaxFret: 10,
                        SuccessMessage: "丝滑切入！3 弦 9 品正是五音 E，这一滑直接把你送进了 8–10 品高音歌唱区！"))
            ],
            PracticeDrillId: "u1_slide_mpent"),

        // ═══════════════════════════════════════════════════════════════════════════
        // 单元 6：顺阶和弦、和声功能与五度圈密码
        // ═══════════════════════════════════════════════════════════════════════════
        new("u6_17_diatonic_family", "6. 顺阶和弦与五度圈", "17", "顺阶三和弦与顺阶七和弦全景推导",
            "为什么一个调里偏偏是这 7 个和弦是一家？三度叠置一秒看穿",
            [
                new("隔一个音抓一个（1-3-5, 2-4-6...）：自动长出顺阶七兄弟",
                    "不用死背！只用 C 大调内部的白键音（C D E F G A B）每隔一个音叠三层：1-3-5 得到大三 C(I)；2-4-6 因 D→F 是小三度，天然长成小三 Dm(ii)；依此类推，7 个音级必然长出『大 · 小 · 小 · 大 · 大 · 小 · 减』的固定家族！",
                    0, "major", false, "", false, HighlightChord: "C",
                    KeyFormula: "大调顺阶三和弦铁律：I(大) · ii(小) · iii(小) · IV(大) · V(大) · vi(小) · vii°(减)"),
                new("再往上叠一层七音：流行与爵士顺阶七和弦全表",
                    "在刚才的三和弦顶上再隔一音抓第 4 颗音（1-3-5-7, 2-4-6-1...）：只看 I 级与 IV 级天然是温暖的『maj7』，唯有 V 级天然长成带张力的『属七 V7（G7）』，ii/iii/vi 全是柔和的『m7』，vii 级则是『m7b5』！",
                    0, "major", false, "", false, HighlightChord: "G7",
                    KeyFormula: "大调顺阶七和弦铁律：Imaj7 · ii7 · iii7 · IVmaj7 · V7（唯一属七！）· vi7 · viiø7(m7b5)",
                    CompareItems:
                    [
                        new("Imaj7 (Cmaj7)", "1-3-5-7 · 安宁明亮的主和弦终点", ChordSymbol: "Cmaj7"),
                        new("ii7 (Dm7)", "2-4-6-1 · 柔和推进的下属预备", ChordSymbol: "Dm7"),
                        new("V7 (G7)", "5-7-2-4 · 全调唯一内置三全音的属七引擎！", ChordSymbol: "G7"),
                        new("vi7 (Am7)", "6-1-3-5 · 温柔叙事的关系小主和弦", ChordSymbol: "Am7")
                    ]),
                new("关系小调顺阶排序：从第 6 级开始重排编号即可",
                    "自然小调（如 A 小调）的顺阶和弦完全就是把 C 大调那 7 个兄弟从 Am 开始排队：i(Am) · ii°(Bdim) · bIII(C) · iv(Dm) · v(Em，实战常升导音改用强力 E7！) · bVI(F) · bVII(G)！",
                    9, "natural_minor", false, "", false, HighlightChord: "Am7",
                    KeyFormula: "自然小调顺阶：im7 · iiø7 · bIIImaj7 · ivm7 · vm7(常改 V7!) · bVImaj7 · bVII7",
                    Quiz: new("🎯 随堂闯关：在 C 大调顺阶七和弦中，唯一天然是『属七和弦（X7）』的是第 5 级。请点击指板找出它的根音『G』！",
                        TargetPitchClass: 7,
                        SuccessMessage: "完全正确！第 5 级 G7 是调内唯一的天然属七和弦，所以 V7→I 才具有无可替代的归属感！"))
            ],
            PracticeDrillId: "u4_50_triad_up",
            PracticeChordSymbol: "Dm7"),

        new("u6_18_harmonic_function", "6. 顺阶和弦与五度圈", "18", "和声功能引擎：T（主）- S（下属）- D（属）情绪推拉",
            "搞懂写歌与编曲底层剧本：为什么 1645、4536251 听一万遍都不腻",
            [
                new("把 7 个顺阶和弦分成三大职能部门：家 · 旅途 · 悬崖",
                    "7 个和弦其实只干三件事：\n① 主功能 T（Tonic：I、vi、iii）——稳定如『家』，适合开头与收尾；\n② 下属功能 S（Subdominant：IV、ii）——离开家门向远方『展开叙事』；\n③ 属功能 D（Dominant：V/V7、vii°）——情绪拉到『悬崖顶点』，强烈渴望跳回主和弦回家！",
                    0, "major", false, "", false, HighlightChord: "C",
                    KeyFormula: "经典和声叙事闭环：T（稳定出发：I/vi） → S（展开铺垫：IV/ii） → D（制造高潮悬念：V7） → T（圆满回家：I）"),
                new("同部门自由代班：为什么 Dm7 能替 F，Am 能替 C？",
                    "仔细比对组成音：F(F-A-C) 与 Dm7(D-F-A-C) 有 3 个音完全重合！C(C-E-G) 与 Am7(A-C-E-G) 也有 3 个音完全重合！所以伴奏嫌单调时，第 1 小节弹 F、第 2 小节换 Dm7，或者把结尾的 C 换成惊喜的 Am（阻碍终止），既通顺又高级！",
                    0, "major", false, "", false, HighlightChord: "Dm7",
                    KeyFormula: "同功能替换表：T 组【I ↔ vi ↔ iii】 · S 组【IV ↔ ii】 · D 组【V7 ↔ vii°】",
                    CompareItems:
                    [
                        new("正格终止 G7 → C", "V7 直接回到 I 级，坚定圆满地画上句号", ChordSymbol: "C"),
                        new("阻碍终止 G7 → Am", "V7 故意拐进同属 T 组的 vi 级 Am，制造意犹未尽的反转！", ChordSymbol: "Am")
                    ]),
                new("解码华语黄金套路 4-5-3-6-2-5-1 与 2-5-1",
                    "现在再看周杰伦/林俊杰式抒情神级走向『4 - 5 - 3 - 6 - 2 - 5 - 1（F-G-Em-Am-Dm-G-C）』：开头 IV→V 本想回 I，却故意用 iii→vi 接住延续情绪，最后再用最经典的『ii(S) → V7(D) → I(T)』稳稳落地！点下方按钮可直接送入进行台跟弹！",
                    0, "major", false, "", false, HighlightChord: "G",
                    KeyFormula: "万能金曲密码：1-5-6-4（T-D-T-S 无限循环） · 2-5-1（S-D-T 黄金解决） · 4-5-3-6-2-5-1（抒情大满贯）")
            ],
            PracticeProgressionId: "ballad_4536251"),

        new("u6_19_circle_of_fifths", "6. 顺阶和弦与五度圈", "19", "五度圈与四度圈：吉他手的瑞士军刀",
            "一张圆盘同时算清 12 调升降号、6 个顺阶和弦抱团规律与逆五度解决链",
            [
                new("顺时针五度升（#），逆时针四度降（b）",
                    "从 12 点钟的 C（0升降）出发：顺时针每次跨一个纯五度『C → G → D → A → E → B → F#』，调号依次多 1 个 # 号（G调1#、D调2#、A调3#、E调4#）；逆时针每次跨一个纯四度『C → F → Bb → Eb → Ab → Db』，依次多 1 个 b 号！",
                    7, "major", false, "", false,
                    KeyFormula: "顺五度口诀：C - G(1#) - D(2#) - A(3#) - E(4#) - B(5#) · 逆五度/纯四度：C - F(1b) - Bb(2b) - Eb(3b) - Ab(4b)"),
                new("神奇相邻抱团：任意调的 6 个核心顺阶和弦在盘上紧挨在一起！",
                    "在五度圈上随便点一个大调主音（比如 C）：它左边的邻居就是下属 IV 级（F），右边的邻居就是属 V 级（G），而紧贴这三格内圈的三个关系小调（Dm、Am、Em）恰好就是 ii、vi、iii 级！一秒钟框出全调 6 个常用和弦！",
                    0, "major", false, "", false, HighlightChord: "F",
                    KeyFormula: "五度圈三邻格速查法：【左邻 = IV 级】 — 【中心 = I 级】 — 【右邻 = V 级】（如 G 调左邻是 C、右邻是 D！）"),
                new("为什么流行与爵士和弦总爱按『逆时针（四度上行/五度下行）』走？",
                    "因为属和弦 V→I 本身就是逆时针走一格（G→C）！把链条拉长：『Bø7 → E7/Em → Am → Dm → G7 → C』（7-3-6-2-5-1），每一步都在五度圈上逆时针走一格，耳朵听起来像多米诺骨牌一样丝滑必然！",
                    0, "major", false, "", false, HighlightChord: "Am",
                    KeyFormula: "逆五度黄金链（每步上行纯四度）：iii(Em) → vi(Am) → ii(Dm) → V(G) → I(C)",
                    Quiz: new("🎯 随堂闯关：用『左 IV 中心 I 右 V』法则算一算：D 大调顺时针右邻的『属和弦 V 级』根音是谁？请在指板上点击它（A）！",
                        TargetPitchClass: 9,
                        SuccessMessage: "秒算成功！D 调左邻是 G(IV)、中心是 D(I)、右邻正是 A(V)——所以 D 调三大件就是 D-G-A！"))
            ],
            PracticeDrillId: "u5_60_cof5_pent"),

        // ═══════════════════════════════════════════════════════════════════════════
        // 单元 7：流行编配进阶：顺滑低音线与离调催泪弹
        // ═══════════════════════════════════════════════════════════════════════════
        new("u7_20_slash_bassline", "7. 流行编配进阶：离调与低音线", "20", "斜杠转位和弦（Slash Chords）与顺滑级进低音",
            "看懂 G/B、C/E、Am/G，把生硬跳跃的伴奏升级成会唱歌的大提琴低音线",
            [
                new("斜杠记号『分子 / 分母』到底怎么按？",
                    "看到『G/B』或『D/F#』千万别当成两个和弦相除！斜杠左边（分子）是右手整体的和弦名字，斜杠右边（分母）指定『最粗那根最低音必须弹哪个音』！例如 G/B（x20033）：不弹 6 弦 G，改用 5 弦 2 品的三音 B 做最低音（即第一转位）！",
                    0, "major", false, "", false, HighlightChord: "G/B",
                    KeyFormula: "X / Y 读法 = 弹 X 和弦，但把最低根音换成 Y（常见如 C/E、G/B、D/F#、Am/G、F/C）"),
                new("化大跳为半音/全音漫步：卡农式顺滑下行低音",
                    "原本弹『C → G → Am → Em → F』，低音在 C→G→A→E→F 之间东一榔头西一棒子；只要把 G 换成 G/B、Em 换成 Am/G、C 换成 C/E，低音立刻变成『C → B → A → G → F → E → D → G』一条连贯下行旋律线，木吉他一把琴弹出交响层次！",
                    0, "major", false, "", false, HighlightChord: "C/E",
                    KeyFormula: "C 调顺滑低音神级模板：C(1) → G/B(7) → Am(6) → Am/G(5) → F(4) → C/E(3) → Dm7(2) → G(5)",
                    CompareItems:
                    [
                        new("C (低音 C)", "5弦3品 C 起步，稳重扎实", ChordSymbol: "C"),
                        new("G/B (低音下行到 B)", "5弦2品 B 顺阶往下走半音，比原位 G 温柔百倍！", ChordSymbol: "G/B"),
                        new("Am (低音落到 A)", "5弦空弦 A 自然承接，低音如流水般连贯", ChordSymbol: "Am"),
                        new("D/F# (G调下行过渡)", "G → D/F# → Em 中拇指扣 6 弦 2 品 F# 的民谣必杀技！", ChordSymbol: "D/F#")
                    ]),
                new("低音持续音（Pedal Point）：不动如山的电影高级感",
                    "反过来还有一种斜杠如『F/G』、『Dm7/G』：上面和弦在换，最低音却死死钉在 5 级 G 上不动，蓄满张力直到最后落回 C！点击下方按钮，直接把顺滑低音走向送进「和弦进行台」整段播放感受！",
                    0, "major", false, "", false, HighlightChord: "Dm7/G",
                    KeyFormula: "属持续低音万能结尾：Dm7/G（或 F/G，常简写为 G11） → Cmaj7",
                    Quiz: new("🎯 随堂闯关：在 G 大调经典连接『G → D/F# → Em』中，D/F# 的最低音需要按在 6 弦 2 品。请点击指板「6 弦 2 品」听听这颗过渡音！",
                        TargetPitchClass: 6, RequiredStringIdx: 0, MinFret: 1, MaxFret: 3,
                        SuccessMessage: "就是它！6 弦 3 品(G) → 2 品(F#) → 0 品(E)，拇指顺势一扣，民谣前奏精髓到手！"))
            ],
            PracticeChordSymbol: "G/B",
            CustomProgressionChords: "C,G/B,Am,Am/G,F,C/E,Dm7,G"),

        new("u7_21_secondary_dominants", "7. 流行编配进阶：离调与低音线", "21", "副属和弦（Secondary Dominants）：调外催泪推进器",
            "揭秘 C 调歌里为什么突然蹦出 E7、D7、A7、C7，一招制造副歌揪心高潮",
            [
                new("什么是副属和弦？『给配角和弦也配一位专属保镖 V7！』",
                    "原本调内只有主和弦 C 拥有专属推进器 G7（V7→I）。如果接下来要进 6 级 Am，嫌普通 Em→Am 太温吞怎么办？临时把 Am 当作新老大，借用 A 小调自己的五级属七『E7（E-G#-B-D）』狠狠推它一把——这个 E7 就叫『V7/vi（六级的副属和弦）』！",
                    0, "major", false, "", false, HighlightChord: "E7",
                    KeyFormula: "副属和弦推导公式：想强烈推进目标和弦 X → 在 X 前面塞入『高纯五度（或退 5 品）的属七和弦』！"),
                new("华语与日系流行四大高频副属武器",
                    "记住 C 大调里最出彩的 4 颗调外副属炸弹：\n• 『E7 → Am（III7→vi）』：瞬间揪心催泪（王菲/陈奕迅/久石让标配）；\n• 『C7 → F（I7→IV）』：主歌进副歌前把主和弦 C 加个 b7 变 C7，气势瞬间抬升推入 F！\n• 『A7 → Dm7（VI7→ii）』与『D7 → G7（II7→V）』：爵士与 R&B 连环推进器！",
                    0, "major", false, "", false, HighlightChord: "C7",
                    KeyFormula: "C 调四大副属速查：C7 → F(IV) · E7 → Am(vi) · A7 → Dm(ii) · D7 → G(V)",
                    CompareItems:
                    [
                        new("调内温顺 Em → Am", "含自然 G 音，平和但缺少戏剧冲突", ChordSymbol: "Em"),
                        new("副属催泪 E7 → Am", "3弦1品升成调外音 G#，半音直刺主音 A，情绪瞬间决堤！", ChordSymbol: "E7"),
                        new("立即落回 Am 感受解决", "听刚才 E7 里的 G# 是如何完美融化进 Am 的！", ChordSymbol: "Am")
                    ]),
                new("遇到副属和弦怎么即兴加花？盯紧那颗『调外升半音三音』！",
                    "当伴奏弹到 E7 时，C 大调原本的 G 音已经临时变成了『G#』！此时只要在 Solo 或分解加花里精准点响这颗 G#（如 3 弦 1 品或 1 弦 4 品），再顺着半音滑回 A，听众鸡皮疙瘩立刻起来！",
                    9, "harmonic_minor", false, "", false, HighlightChord: "E7",
                    KeyFormula: "副属 Solo 秘诀：E7 小节临时启用『A 和声小调（含 #7=G#）』，精准命中导音 G# → A！",
                    Quiz: new("🎯 随堂闯关：E7 之所以比 Em 催泪十倍，全靠那颗调外大三音『G# / Ab』。请在上方指板点击找出任意一颗 G#/Ab 音！",
                        TargetPitchClass: 8,
                        SuccessMessage: "灵魂命中！G# 往上走半音就是 Am 的根音 A，这就是和声小调与副属和弦最迷人的张力！"))
            ],
            PracticeChordSymbol: "E7",
            CustomProgressionChords: "C,E7,Am,C7,F,D7,G7,C"),

        new("u7_22_modal_interchange", "7. 流行编配进阶：离调与低音线", "22", "同主音小调借用（Modal Interchange）：IV → iv 落幕叹息",
            "学会『F → Fm → C』与『bVI → bVII → I』，给编曲加上电影片尾字幕滤镜",
            [
                new("什么是调式互换借用？向“平行世界的自己”借色彩",
                    "C 大调与『C 自然小调（C D Eb F G Ab Bb）』共享同一个主音 C。写在大调里的歌，在结尾或副歌转折处临时从 C 小调借一两个和弦过来用完再回 C 大调，就叫『同主音小调借用（Modal Interchange）』！",
                    0, "natural_minor", false, "", false, HighlightChord: "Fm",
                    KeyFormula: "大调最常向同主音小调借用的三张王牌：四级小 iv(Fm) · 降六级 bVI(Ab) · 降七级 bVII(Bb)"),
                new("第一催泪杀手锏：四级大变四级小『F → Fm → C』",
                    "无数经典情歌（从 Beatles《In My Life》、Radiohead《Creep》到周杰伦《晴天》/华语芭乐片尾）都在最后一句用『F → Fm → C』！秘密在于内声部那条半音叹息线：C 和弦的 5 音(G) → 升到 F 的大三音(A) → 借用 Fm 变成降六音(Ab) → 顺滑落回 C 的 5 音(G)！",
                    0, "major", false, "", false, HighlightChord: "Fm",
                    KeyFormula: "四级小半音声部线（C调）：F（含 A 音） → Fm（降为 Ab 音，遗憾叹息） → C（落回 G 音，释怀收尾）",
                    CompareItems:
                    [
                        new("① 先听四级大 F", "温暖明亮的期待感（内含 A 音）", ChordSymbol: "F"),
                        new("② 借用四级小 Fm", "A 音塌陷半音变成 Ab，瞬间涌上一股温柔的遗憾与不舍！", ChordSymbol: "Fm"),
                        new("③ 回到主和弦 Cmaj7", "Ab 再落半音回到 G，乌云散开、释然落幕", ChordSymbol: "Cmaj7")
                    ]),
                new("史诗电影感闯关终止：『bVI → bVII → I（Ab → Bb → C）』",
                    "游戏配乐、动漫燃曲与摇滚尾奏最爱的『马里奥通关进行』：不走普通 Dm→G→C，而是从同主音小调借来『Ab → Bb → C』（在 G 调里则是 Eb → F → G），全音阶连续上扬两级冲回大三主和弦，热血澎湃！",
                    0, "major", false, "", false, HighlightChord: "Bb",
                    KeyFormula: "史诗全音上扬终止式：bVI（大三） → bVII（大三） → I（大三）· 例：C 调为 Ab → Bb → C",
                    Quiz: new("🎯 随堂闯关：在『F → Fm → C』叹息线（A → Ab → G）中，Fm 借来的关键离调音是『Ab / G#』。请在指板上点击找出它！",
                        TargetPitchClass: 8,
                        SuccessMessage: "太有乐感了！试着在吉他上连弹 3 弦 2 品(A) → 1 品(Ab) → 0 品(G)，就能亲耳听到这条经典叹息线！"))
            ],
            PracticeChordSymbol: "Fm",
            CustomProgressionChords: "C,E7,Am,C7,F,Fm,Cmaj7"),

        // ═══════════════════════════════════════════════════════════════════════════
        // 单元 8：弹唱实战定调、变调夹推算与扒谱入门
        // ═══════════════════════════════════════════════════════════════════════════
        new("u8_23_capo_math", "8. 弹唱定调与扒谱入门", "23", "变调夹（Capo）双向换算与男女调实战选调",
            "彻底算清『原曲调 = 选调 + Capo』，遇到降 E 调/升 F 调一秒挑出最省力指法",
            [
                new("变调夹黄金等式：原曲实际音高 = 谱面选调指法 + 变调夹品位",
                    "变调夹相当于把吉他琴枕往右搬了 N 个半音！如果你谱面选『C 调指法』，变调夹夹在第 2 品（Capo 2），C 往上数 2 个半音是 D——所以你手按的是轻松的 C-G-Am-F，耳朵听到的实际已经是『D 大调』！",
                    2, "major", true, "ALL", false, HighlightChord: "C", Capo: 2,
                    KeyFormula: "正向公式：实际调 = 指法选调 + Capo 品数（例：C 调指法 + Capo 3 = Eb 大调 · G 调指法 + Capo 2 = A 大调）"),
                new("逆向实战：原曲是可怕的 Eb 调、B 调、F# 调，老手怎么夹？",
                    "木吉他弹唱界有个公开秘密：90% 的谱子只用最好按、空弦共鸣最美的『C 调指法』或『G 调指法』！\n• 遇到原曲 Eb 调（比 C 高 3 半音）→ 夹 Capo 3 用 C 调指法！\n• 遇到原曲 Bb 调（比 G 高 3 半音）→ 夹 Capo 3 用 G 调指法！\n• 遇到原曲 B 调（比 G 高 4 半音）→ 夹 Capo 4 用 G 调指法，全程零大横按！",
                    3, "major", true, "ALL", false, HighlightChord: "C", Capo: 3,
                    KeyFormula: "选调黄金偏好：实际调在 Db/D/Eb/E 优先夹 1~4 品弹『C 调指法』；实际调在 Ab/A/Bb/B 优先夹 1~4 品弹『G 调指法』！"),
                new("唱歌高音劈叉 / 太低沉？用变调夹与弦笺一键升降调自救",
                    "跟着原唱唱不上去时，直接把变调夹往琴头方向退 2～3 品（若原本不夹，可在弦笺编辑制谱顶栏点『b 降调』把 C 调谱一键转成 G 调再夹 Capo 2 = A 调，净降小三度）！男女生对唱同一首歌，通常相差 4～5 个半音最舒适。",
                    3, "major", false, "", false, Capo: 3,
                    KeyFormula: "人声适配经验：副歌最高音吃力 → Capo 退 2 品（降全音） · 女歌男唱 / 男歌女唱 → 常升降 4～5 个半音（纯四度左右）",
                    Quiz: new("🎯 随堂闯关：当前指板已夹上『Capo 3』。当你用 C 调开放手型按 5 弦相对第 3 品（即琴颈第 6 品）时，实际发出的主音是 Eb！请点击「5 弦 6 品」验证！",
                        TargetPitchClass: 3, RequiredStringIdx: 1, MinFret: 5, MaxFret: 7,
                        SuccessMessage: "一算即通！琴颈 5 弦 6 品正是 Eb3——手上是熟悉的 C 指法，嘴里唱的已是原汁原味的降 E 大调！"))
            ]),

        new("u8_24_ear_transcribing", "8. 弹唱定调与扒谱入门", "24", "零基础听音定调与扒谱四步法（从耳朵到落谱）",
            "不靠绝对音感！教你顺着『尾音抓主音 → 辨明暗 → 摸贝斯根音 → 套级数』亲手扒出第一首歌",
            [
                new("第 1 步 · 哼尾音抓主音（Home Note）：1 分钟锁定全曲落脚点",
                    "扒谱根本不需要天生的绝对音感！播放你想扒的歌（尤其是副歌最后一句结束处），心里顺着歌曲把那个最稳定、感觉“彻底唱完回家了”的长音『嗯——』哼出来；然后在吉他 1 弦或 2 弦上逐品滑音比对，直到找到和嘴里哼的一模一样的那一品——90% 的情况下，它就是大调的『1(Do)』或小调的『6(La)』！",
                    7, "major", false, "", false,
                    PreferredLabelMode: FretboardLabelMode.NoteName,
                    KeyFormula: "定调第 1 铁律：找全曲或段落『最有终结感的收尾音』→ 在单弦滑音撞出同音高 → 锁定候选主音！"),
                new("第 2 步 · 一扫辨明暗：是阳光大调，还是叙事小调？",
                    "假设刚才在吉他上摸出的收尾回家音是『G』：接下来直接在这颗根音上分别扫一下『G 大三和弦』和『Gm 小三和弦』跟原曲核对！如果 G 大三贴合明亮，全曲就是 G 大调（顺阶家族：G Am Bm C D Em）；如果 Gm 贴合忧郁，全曲就是 G 小调（关系大调为往右进 3 品的 Bb 大调）！",
                    7, "major", false, "", false, HighlightChord: "G",
                    KeyFormula: "定调第 2 铁律：主音配大三和弦贴合 = X 大调 · 主音配小三和弦贴合 = X 小调（往高音数 3 品即同源关系大调）",
                    CompareItems:
                    [
                        new("试扫 G 大三和弦", "若与原曲收尾气质严丝合缝 → 锁定 G 大调！", ChordSymbol: "G"),
                        new("试扫 Gm 小三和弦", "若原曲收尾偏冷峻忧伤 → 锁定 G 小调（同 Bb 大调音库）！", ChordSymbol: "Gm")
                    ]),
                new("第 3 步 · 盯死贝斯抓根音：套入高频级数公式一试一个准",
                    "调一定好，和弦候选范围瞬间从几百个缩小到『调内 6 个常用和弦』！戴上耳机重点听每小节第 1 拍最低沉的贝斯/吉他根音落点，在 6/5 弦摸出级数数字，再结合常见走向验证：\n• 听到 1 → 5 → 6 → 4（G-D-Em-C）或 6 → 4 → 1 → 5（Em-C-G-D）；\n• 听到连贯下行多半是卡农或斜杠转位（G - D/F# - Em - Em/D - C）！",
                    7, "major", false, "", false, HighlightChord: "Em",
                    KeyFormula: "流行扒谱四大盲猜命中王：1-5-6-4 · 6-4-1-5 · 4-5-3-6-2-5-1 · 1-6-4-5（遇到意外揪心感优先排查 III7 副属与 iv 小调借用！）"),
                new("第 4 步 · 前奏/间奏单音怎么扒？『和弦手型里藏着 80% 的旋律！』",
                    "扒指弹或前奏 Solo 时千万别一个音一个音大海捞针！先把刚才扒出来的背景和弦按在手上（或看上方指板的 CAGED 把位框）——原曲绝大部分旋律音和加花击勾弦，其实就乖乖躺在你左手正按着的和弦指型与双音胶囊里！",
                    7, "major_pent", true, "G", true, HighlightChord: "G",
                    KeyFormula: "扒谱终极闭环：定主音 → 辨大小调 → 抓低音定和弦级数 → 在当拍和弦 CAGED 指型内顺手摸出前奏单音与加花！",
                    Quiz: new("🎯 毕业闯关：假设你听一首歌锁死了是 G 大调，某小节贝斯低音落在了第 4 级（IV级）。请在上方 G 大调指板点击找出这颗『4 级根音（C）』！",
                        TargetPitchClass: 0,
                        SuccessMessage: "恭喜通关全部 24 节实用乐理课！G 大调 4 级正是 C 和弦——快去扒一首你最爱的歌落进弦笺曲库吧！"))
            ],
            PracticeProgressionId: "pop_1645")
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
