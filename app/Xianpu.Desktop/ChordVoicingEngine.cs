using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Xianpu.Desktop;

internal sealed record ParsedChord(
    string DisplayName,
    int RootPitch,
    int BassPitch,
    bool HasSlashBass,
    string CanonicalQuality,
    HashSet<int> AllPitches,
    HashSet<int> RequiredPitches,
    bool IsPowerChord);

internal static class ChordVoicingEngine
{
    // Standard tuning E2 A2 D3 G3 B3 E4 (pitch classes mod 12, C = 0)
    private static readonly int[] OpenPitches = [4, 9, 2, 7, 11, 4];

    private static readonly ConcurrentDictionary<string, IReadOnlyList<ChordShape>> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex RootAndSlashRegex = new(
        @"^([A-Ga-g])([#b]?)(.*?)(?:/([A-Ga-g])([#b]?))?$",
        RegexOptions.Compiled);

    public static bool TryParseChord(string? rawName, out ParsedChord parsed)
    {
        parsed = null!;
        if (string.IsNullOrWhiteSpace(rawName))
            return false;

        var cleaned = rawName.Trim()
            .Replace('♯', '#')
            .Replace('♭', 'b')
            .Replace(" ", "", StringComparison.Ordinal);

        var match = RootAndSlashRegex.Match(cleaned);
        if (!match.Success)
            return false;

        var rootPitch = ParseNotePitch(match.Groups[1].Value[0], match.Groups[2].Value);
        if (rootPitch < 0)
            return false;

        var hasSlash = match.Groups[4].Success && !string.IsNullOrEmpty(match.Groups[4].Value);
        var bassPitch = hasSlash
            ? ParseNotePitch(match.Groups[4].Value[0], match.Groups[5].Value)
            : rootPitch;
        if (bassPitch < 0)
            return false;

        var rawSuffix = match.Groups[3].Value.Trim();
        var suffix = rawSuffix
            .Replace("(", "", StringComparison.Ordinal)
            .Replace(")", "", StringComparison.Ordinal);

        if (!TryParseSuffixIntervals(suffix, out var intervals, out var omissible, out var canonicalQuality, out var isPower))
            return false;

        var allPitches = new HashSet<int>();
        var requiredPitches = new HashSet<int>();
        foreach (var iv in intervals)
        {
            var pc = (rootPitch + iv) % 12;
            allPitches.Add(pc);
            if (!omissible.Contains(iv))
                requiredPitches.Add(pc);
        }

        // Always include root & explicit slash bass in pitch sets
        allPitches.Add(rootPitch);
        requiredPitches.Add(rootPitch);
        allPitches.Add(bassPitch);
        requiredPitches.Add(bassPitch);

        parsed = new ParsedChord(
            DisplayName: ChordCatalog.NormalizeChordName(rawName),
            RootPitch: rootPitch,
            BassPitch: bassPitch,
            HasSlashBass: hasSlash && bassPitch != rootPitch,
            CanonicalQuality: canonicalQuality,
            AllPitches: allPitches,
            RequiredPitches: requiredPitches,
            IsPowerChord: isPower);
        return true;
    }

    public static IReadOnlyList<ChordShape> DeriveCandidates(string? chordName, int maxResults = 6)
    {
        if (string.IsNullOrWhiteSpace(chordName))
            return [];

        var normalized = ChordCatalog.NormalizeChordName(chordName);
        if (string.IsNullOrWhiteSpace(normalized))
            return [];

        var cached = Cache.GetOrAdd(normalized, static key => ComputeCandidates(key, 6));
        return cached.Count <= maxResults ? cached : cached.Take(maxResults).ToList();
    }

    public static BarreShape? InferBarre(int?[] frets, int[] fingers)
    {
        var fretted = new List<(int Str, int Fret)>();
        for (var s = 0; s < 6; s++)
        {
            if (frets[s] is > 0 and var f)
                fretted.Add((s, f));
        }

        if (fretted.Count < 2)
            return null;

        var minFret = fretted.Min(x => x.Fret);
        var onMin = fretted.Where(x => x.Fret == minFret && fingers[x.Str] == 1).Select(x => x.Str).ToList();
        if (onMin.Count < 2)
            return null;

        var fromStr = onMin[0];
        var endStr = onMin[^1];
        for (var s = fromStr; s <= endStr; s++)
        {
            if (frets[s] == 0 || (frets[s] is > 0 and var f && f < minFret))
                return null;
        }

        return new BarreShape(minFret, fromStr, endStr);
    }

    private static IReadOnlyList<ChordShape> ComputeCandidates(string normalizedName, int maxResults)
    {
        if (!TryParseChord(normalizedName, out var parsed))
            return [];

        var results = new List<(ChordShape Shape, double Score)>();
        var seenFrets = new HashSet<string>(StringComparer.Ordinal);

        void TryAdd(ChordShape shape, double score)
        {
            var fp = Fingerprint(shape.Frets);
            if (seenFrets.Add(fp))
                results.Add((shape, score));
        }

        // 1. Proven movable CAGED/barre templates (for non-slash chords)
        if (!parsed.HasSlashBass)
        {
            foreach (var (shape, score) in GenerateMovableTemplates(parsed, normalizedName))
                TryAdd(shape, score);
        }

        // 2. General ergonomic fretboard search across open & movable windows
        foreach (var (shape, score) in SolveErgonomicVoicings(parsed, normalizedName))
            TryAdd(shape, score);

        return results
            .OrderBy(x => x.Score)
            .Take(maxResults)
            .Select((x, idx) => new ChordShape
            {
                Id = $"auto:{normalizedName}:{idx}",
                Name = normalizedName,
                Type = x.Shape.Type,
                Frets = x.Shape.Frets,
                Fingers = x.Shape.Fingers,
                BaseFret = x.Shape.BaseFret,
                Barre = x.Shape.Barre,
                Label = x.Shape.Label
            })
            .ToList();
    }

    private static List<(ChordShape Shape, double Score)> GenerateMovableTemplates(ParsedChord parsed, string displayName)
    {
        var list = new List<(ChordShape, double)>();
        var q = parsed.CanonicalQuality;

        // 6th-string root (E-shape family)
        var eFret = (parsed.RootPitch - OpenPitches[0] + 12) % 12;
        if (eFret >= 1 && eFret <= 10)
        {
            AddMovableIfMatches(list, parsed, displayName, q, eFret, isAShape: false);
        }

        // 5th-string root (A-shape family)
        var aFret = (parsed.RootPitch - OpenPitches[1] + 12) % 12;
        if (aFret >= 1 && aFret <= 10)
        {
            AddMovableIfMatches(list, parsed, displayName, q, aFret, isAShape: true);
        }

        return list;
    }

    private static void AddMovableIfMatches(
        List<(ChordShape Shape, double Score)> list,
        ParsedChord parsed,
        string displayName,
        string q,
        int f,
        bool isAShape)
    {
        int?[]? frets = null;
        int[]? fingers = null;
        BarreShape? barre = null;
        string? label = null;

        if (!isAShape)
        {
            // E-shape family (6th string root = fret f)
            switch (q)
            {
                case "":
                    frets = [f, f + 2, f + 2, f + 1, f, f];
                    fingers = [1, 3, 4, 2, 1, 1];
                    barre = new(f, 0, 5);
                    label = "E 形横按";
                    break;
                case "m":
                    frets = [f, f + 2, f + 2, f, f, f];
                    fingers = [1, 3, 4, 1, 1, 1];
                    barre = new(f, 0, 5);
                    label = "Em 形横按";
                    break;
                case "7":
                    frets = [f, f + 2, f, f + 1, f, f];
                    fingers = [1, 3, 1, 2, 1, 1];
                    barre = new(f, 0, 5);
                    label = "E7 形横按";
                    break;
                case "m7":
                    frets = [f, f + 2, f, f, f, f];
                    fingers = [1, 3, 1, 1, 1, 1];
                    barre = new(f, 0, 5);
                    label = "Em7 形横按";
                    break;
                case "maj7":
                    frets = [f, null, f + 1, f + 1, f, null];
                    fingers = [1, 0, 3, 4, 2, 0];
                    label = $"{f}品把位";
                    break;
                case "sus4":
                    frets = [f, f + 2, f + 2, f + 2, f, f];
                    fingers = [1, 2, 3, 4, 1, 1];
                    barre = new(f, 0, 5);
                    label = "Esus4 形横按";
                    break;
                case "7sus4":
                    frets = [f, f + 2, f, f + 2, f, f];
                    fingers = [1, 3, 1, 4, 1, 1];
                    barre = new(f, 0, 5);
                    label = "E7sus4 形横按";
                    break;
                case "m7b5" when f >= 2:
                    frets = [f, null, f, f, f - 1, null];
                    fingers = [2, 0, 3, 4, 1, 0];
                    label = "半减七把位";
                    break;
                case "dim7" when f >= 2:
                    frets = [f, null, f - 1, f, f - 1, null];
                    fingers = [2, 0, 1, 3, 1, 0];
                    label = "减七把位";
                    break;
                case "6" when f >= 2:
                    frets = [f, null, f - 1, f + 1, f, null];
                    fingers = [2, 0, 1, 4, 3, 0];
                    label = $"{f}品把位";
                    break;
                case "m6" when f >= 2:
                    frets = [f, null, f - 1, f, f, null];
                    fingers = [2, 0, 1, 3, 4, 0];
                    label = $"{f}品把位";
                    break;
                case "5":
                    frets = [f, f + 2, f + 2, null, null, null];
                    fingers = [1, 3, 4, 0, 0, 0];
                    label = "强力和弦";
                    break;
            }
        }
        else
        {
            // A-shape family (5th string root = fret f)
            switch (q)
            {
                case "":
                    frets = [null, f, f + 2, f + 2, f + 2, f];
                    fingers = [0, 1, 3, 3, 3, 1];
                    barre = new(f, 1, 5);
                    label = "A 形横按";
                    break;
                case "m":
                    frets = [null, f, f + 2, f + 2, f + 1, f];
                    fingers = [0, 1, 3, 4, 2, 1];
                    barre = new(f, 1, 5);
                    label = "Am 形横按";
                    break;
                case "7":
                    frets = [null, f, f + 2, f, f + 2, f];
                    fingers = [0, 1, 3, 1, 4, 1];
                    barre = new(f, 1, 5);
                    label = "A7 形横按";
                    break;
                case "m7":
                    frets = [null, f, f + 2, f, f + 1, f];
                    fingers = [0, 1, 3, 1, 2, 1];
                    barre = new(f, 1, 5);
                    label = "Am7 形横按";
                    break;
                case "maj7":
                    frets = [null, f, f + 2, f + 1, f + 2, f];
                    fingers = [0, 1, 3, 2, 4, 1];
                    barre = new(f, 1, 5);
                    label = "Amaj7 形横按";
                    break;
                case "sus2":
                    frets = [null, f, f + 2, f + 2, f, f];
                    fingers = [0, 1, 3, 4, 1, 1];
                    barre = new(f, 1, 5);
                    label = "Asus2 形横按";
                    break;
                case "sus4":
                    frets = [null, f, f + 2, f + 2, f + 3, f];
                    fingers = [0, 1, 2, 3, 4, 1];
                    barre = new(f, 1, 5);
                    label = "Asus4 形横按";
                    break;
                case "7sus4":
                    frets = [null, f, f + 2, f, f + 3, f];
                    fingers = [0, 1, 3, 1, 4, 1];
                    barre = new(f, 1, 5);
                    label = "A7sus4 形横按";
                    break;
                case "9" when f >= 2:
                    frets = [null, f, f - 1, f, f, f];
                    fingers = [0, 2, 1, 3, 3, 3];
                    label = "C9 形把位";
                    break;
                case "m9" when f >= 3:
                    frets = [null, f, f - 2, f, f, null];
                    fingers = [0, 2, 1, 3, 4, 0];
                    label = "Dm9 形把位";
                    break;
                case "maj9" when f >= 2:
                    frets = [null, f, f - 1, f + 1, f, null];
                    fingers = [0, 2, 1, 4, 3, 0];
                    label = "Cmaj9 形把位";
                    break;
                case "m7b5":
                    frets = [null, f, f + 1, f, f + 1, null];
                    fingers = [0, 1, 3, 2, 4, 0];
                    label = "半减七把位";
                    break;
                case "dim7" when f >= 2:
                    frets = [null, f, f + 1, f - 1, f + 1, null];
                    fingers = [0, 2, 3, 1, 4, 0];
                    label = "减七把位";
                    break;
                case "aug" when f >= 3:
                    frets = [null, f, f - 1, f - 2, f - 2, null];
                    fingers = [0, 3, 2, 1, 1, 0];
                    barre = new(f - 2, 3, 4);
                    label = "增三把位";
                    break;
                case "5":
                    frets = [null, f, f + 2, f + 2, null, null];
                    fingers = [0, 1, 3, 4, 0, 0];
                    label = "强力和弦";
                    break;
            }
        }

        if (frets is null || fingers is null)
            return;

        // Verify pitch integrity and diagram 4-fret fit
        var minFret = 99;
        var maxFret = 0;
        for (var s = 0; s < 6; s++)
        {
            if (frets[s] is not { } fr) continue;
            if (fr < 0 || fr > 15) return;
            var pc = (OpenPitches[s] + fr) % 12;
            if (!parsed.AllPitches.Contains(pc)) return;
            if (fr > 0)
            {
                if (fr < minFret) minFret = fr;
                if (fr > maxFret) maxFret = fr;
            }
        }

        if (maxFret - minFret > 3)
            return;

        var baseFret = maxFret <= 4 ? 1 : minFret;
        var shape = new ChordShape
        {
            Id = $"template-{(isAShape ? "A" : "E")}-{displayName}",
            Name = displayName,
            Type = barre is not null ? "barre" : "auto",
            Frets = frets,
            Fingers = fingers,
            BaseFret = baseFret,
            Barre = barre,
            Label = label ?? "自动推导"
        };

        // Give curated movable templates a slight ergonomic preference over generic solver voicings at similar fret heights
        var score = minFret * 2.8 + (barre is not null ? 1.5 : 0.0) + (isAShape ? 0.3 : 0.0);
        list.Add((shape, score));
    }

    private static List<(ChordShape Shape, double Score)> SolveErgonomicVoicings(ParsedChord parsed, string displayName)
    {
        var candidates = new Dictionary<string, (int?[] Frets, double Score)>(StringComparer.Ordinal);
        var minStrings = parsed.IsPowerChord ? 2 : 3;

        // Window 0 = open position (frets 0..4), windows 1..9 = movable 4-fret spans [w .. w+3]
        for (var winStart = 0; winStart <= 9; winStart++)
        {
            var maxFretInWin = winStart == 0 ? 4 : winStart + 3;
            var minFretInWin = winStart == 0 ? 1 : winStart;

            var choicesPerString = new List<int?>[6];
            for (var s = 0; s < 6; s++)
            {
                var opts = new List<int?>(4) { null };
                // Allow open string in open window (or low movable window up to fret 2 for folk resonance)
                if (winStart <= 1 && parsed.AllPitches.Contains(OpenPitches[s]))
                    opts.Add(0);

                for (var f = minFretInWin; f <= maxFretInWin; f++)
                {
                    if (parsed.AllPitches.Contains((OpenPitches[s] + f) % 12))
                        opts.Add(f);
                }
                choicesPerString[s] = opts;
            }

            var current = new int?[6];
            SearchString(0, current, choicesPerString, parsed, minStrings, candidates);
        }

        var ranked = candidates.Values
            .OrderBy(x => x.Score)
            .Take(10)
            .Select(x => (BuildShapeFromFrets(displayName, x.Frets), x.Score))
            .ToList();

        return ranked;
    }

    private static void SearchString(
        int stringIdx,
        int?[] current,
        List<int?>[] choicesPerString,
        ParsedChord parsed,
        int minStrings,
        Dictionary<string, (int?[] Frets, double Score)> outCandidates)
    {
        if (stringIdx == 6)
        {
            EvaluateCandidate(current, parsed, minStrings, outCandidates);
            return;
        }

        foreach (var opt in choicesPerString[stringIdx])
        {
            current[stringIdx] = opt;
            SearchString(stringIdx + 1, current, choicesPerString, parsed, minStrings, outCandidates);
        }
    }

    private static void EvaluateCandidate(
        int?[] current,
        ParsedChord parsed,
        int minStrings,
        Dictionary<string, (int?[] Frets, double Score)> outCandidates)
    {
        var lowestStr = -1;
        var highestStr = -1;
        var soundingCount = 0;
        var openCount = 0;
        var frettedCount = 0;
        var minFret = 99;
        var maxFret = 0;
        var pitchMask = 0;

        for (var s = 0; s < 6; s++)
        {
            if (current[s] is not { } f) continue;
            if (lowestStr < 0) lowestStr = s;
            highestStr = s;
            soundingCount++;
            var pc = (OpenPitches[s] + f) % 12;
            pitchMask |= 1 << pc;
            if (f == 0)
            {
                openCount++;
            }
            else
            {
                frettedCount++;
                if (f < minFret) minFret = f;
                if (f > maxFret) maxFret = f;
            }
        }

        if (soundingCount < minStrings)
            return;

        // Bass constraint: lowest sounding string MUST play BassPitch
        var bassPc = (OpenPitches[lowestStr] + current[lowestStr]!.Value) % 12;
        if (bassPc != parsed.BassPitch)
            return;

        // Every required pitch class must be covered
        foreach (var req in parsed.RequiredPitches)
        {
            if ((pitchMask & (1 << req)) == 0)
                return;
        }

        // Reject unplayable interior muted strings:
        // Allow at most 1 interior muted string, and ONLY on string 1 when string 0 is bass (thumb/index shell voicings like 2-x-2-2-1-0)
        var interiorMutes = 0;
        for (var s = lowestStr + 1; s < highestStr; s++)
        {
            if (current[s] is null)
            {
                interiorMutes++;
                if (interiorMutes > 1 || !(lowestStr == 0 && s == 1))
                    return;
            }
        }

        // Diagram 4-fret fit & human hand span check
        var span = frettedCount > 0 ? maxFret - minFret : 0;
        if (span > 3)
            return;

        // Check finger budget (<= 4 independent fingers, accounting for possible index barre on minFret)
        var needsBarre = false;
        if (frettedCount > 4)
        {
            if (!CanBarreAtMinFret(current, minFret, out var extraFingers) || extraFingers > 3)
                return;
            needsBarre = true;
        }
        else if (frettedCount >= 2 && openCount == 0 && CanBarreAtMinFret(current, minFret, out var extraF) && extraF <= 3)
        {
            // Natural barre when minFret covers multiple strings with no open strings
            var onMinCount = CountAtFret(current, minFret);
            if (onMinCount >= 2 && frettedCount >= 4)
                needsBarre = true;
        }

        // Ergonomic cost scoring (lower = better guitarist voicing)
        var baseFret = frettedCount == 0 || maxFret <= 4 ? 1 : minFret;
        var score = 0.0;

        // Strong preference for low / open positions
        score += baseFret == 1 ? 0.0 : (minFret * 3.6);
        score += span * 2.1;
        score -= openCount * 1.4;

        // Prefer fuller 4–6 string voicings (except power chords which prefer 2–3 strings)
        if (parsed.IsPowerChord)
        {
            score += Math.Abs(soundingCount - 3) * 2.5;
            if (highestStr > 3) score += 4.0;
        }
        else
        {
            score += (6 - soundingCount) * 2.4;
            if (soundingCount == 3) score += 5.5;
            // Prefer bass on 6th / 5th / 4th string rather than thin treble-only voicings
            if (lowestStr >= 3) score += 7.0;
            // Mild penalty for muting high e string when a full top voicing exists
            if (current[5] is null) score += 1.6;
        }

        // Bonus for covering all optional chord tones (e.g. keeping perfect 5th when possible)
        var missingOptional = 0;
        foreach (var pc in parsed.AllPitches)
        {
            if ((pitchMask & (1 << pc)) == 0)
                missingOptional++;
        }
        score += missingOptional * 1.8;

        // Penalty for interior mute & barre difficulty
        score += interiorMutes * 3.2;
        score += needsBarre ? 2.4 : 0.0;
        score += frettedCount * 0.55;

        // Slight bonus for root in open string or low fret
        if (baseFret == 1 && openCount > 0 && !needsBarre)
            score -= 2.2;

        var fp = Fingerprint(current);
        if (!outCandidates.TryGetValue(fp, out var existing) || score < existing.Score)
        {
            outCandidates[fp] = ((int?[])current.Clone(), score);
        }
    }

    private static ChordShape BuildShapeFromFrets(string displayName, int?[] frets)
    {
        var minFret = 99;
        var maxFret = 0;
        var frettedCount = 0;
        var openCount = 0;

        for (var s = 0; s < 6; s++)
        {
            if (frets[s] is not { } f) continue;
            if (f == 0) openCount++;
            else
            {
                frettedCount++;
                if (f < minFret) minFret = f;
                if (f > maxFret) maxFret = f;
            }
        }

        var baseFret = frettedCount == 0 || maxFret <= 4 ? 1 : minFret;
        var fingers = new int[6];
        BarreShape? barre = null;

        var useBarre = frettedCount >= 2 &&
                       CanBarreAtMinFret(frets, minFret, out var extraCount) &&
                       extraCount <= 3 &&
                       (frettedCount > 4 || (CountAtFret(frets, minFret) >= 2 && openCount == 0 && frettedCount >= 4));

        var nextFinger = 1;
        if (useBarre)
        {
            var firstMin = -1;
            var lastMin = -1;
            for (var s = 0; s < 6; s++)
            {
                if (frets[s] == minFret)
                {
                    if (firstMin < 0) firstMin = s;
                    lastMin = s;
                    fingers[s] = 1;
                }
            }
            if (firstMin >= 0 && lastMin > firstMin)
            {
                barre = new BarreShape(minFret, firstMin, lastMin);
                nextFinger = 2;
            }
        }

        // Assign remaining fingers in ergonomic fret-ascending, bass-to-treble order
        var remaining = new List<(int Str, int Fret)>();
        for (var s = 0; s < 6; s++)
        {
            if (frets[s] is > 0 and var f && fingers[s] == 0)
                remaining.Add((s, f));
        }

        remaining.Sort((a, b) =>
        {
            var fc = a.Fret.CompareTo(b.Fret);
            return fc != 0 ? fc : a.Str.CompareTo(b.Str);
        });

        foreach (var (s, _) in remaining)
        {
            fingers[s] = Math.Min(4, nextFinger);
            if (nextFinger < 4) nextFinger++;
        }

        var label = barre is not null
            ? $"推导 · {baseFret}品横按"
            : (baseFret == 1 ? "推导 · 开放把位" : $"推导 · {baseFret}品把位");

        return new ChordShape
        {
            Id = $"auto-{displayName}",
            Name = displayName,
            Type = barre is not null ? "barre" : "auto",
            Frets = frets,
            Fingers = fingers,
            BaseFret = baseFret,
            Barre = barre,
            Label = label
        };
    }

    private static bool CanBarreAtMinFret(int?[] frets, int minFret, out int aboveMinCount)
    {
        aboveMinCount = 0;
        var firstMin = -1;
        var lastMin = -1;
        for (var s = 0; s < 6; s++)
        {
            if (frets[s] == minFret)
            {
                if (firstMin < 0) firstMin = s;
                lastMin = s;
            }
            else if (frets[s] is > 0 and var f && f > minFret)
            {
                aboveMinCount++;
            }
        }

        if (firstMin < 0 || lastMin <= firstMin)
            return false;

        // No open string can sound underneath an index barre
        for (var s = firstMin; s <= lastMin; s++)
        {
            if (frets[s] == 0)
                return false;
        }

        return true;
    }

    private static int CountAtFret(int?[] frets, int fret)
    {
        var count = 0;
        for (var s = 0; s < 6; s++)
        {
            if (frets[s] == fret) count++;
        }
        return count;
    }

    private static string Fingerprint(int?[] frets) =>
        string.Join(',', frets.Select(f => f.HasValue ? f.Value.ToString() : "x"));

    private static int ParseNotePitch(char letter, string accidental)
    {
        var basePitch = char.ToUpperInvariant(letter) switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => -1
        };
        if (basePitch < 0) return -1;
        var offset = accidental switch
        {
            "#" => 1,
            "b" => -1,
            _ => 0
        };
        return (basePitch + offset + 12) % 12;
    }

    private static bool TryParseSuffixIntervals(
        string suffix,
        out HashSet<int> intervals,
        out HashSet<int> omissible,
        out string canonicalQuality,
        out bool isPower)
    {
        intervals = [0];
        omissible = [];
        canonicalQuality = "";
        isPower = false;

        var s = suffix.Trim();
        if (s.Length == 0 || s.Equals("maj", StringComparison.OrdinalIgnoreCase))
        {
            intervals = [0, 4, 7];
            canonicalQuality = "";
            return true;
        }

        // Power chord: C5 / G5
        if (s == "5")
        {
            intervals = [0, 7];
            canonicalQuality = "5";
            isPower = true;
            return true;
        }

        var pos = 0;
        var isMinor = false;
        var isDim = false;
        var isAug = false;
        var isHalfDim = false;
        var hasMajorSeventhPrefix = false;
        var third = 4; // default major 3rd
        var fifth = 7; // default perfect 5th

        // 1. Base quality prefix
        if (StartsWithCI(s, pos, "maj"))
        {
            hasMajorSeventhPrefix = true;
            pos += 3;
        }
        else if (StartsWithExact(s, pos, "M") && (pos + 1 == s.Length || char.IsDigit(s[pos + 1])))
        {
            if (pos + 1 < s.Length)
                hasMajorSeventhPrefix = true;
            pos += 1;
        }
        else if (StartsWithExact(s, pos, "Δ"))
        {
            hasMajorSeventhPrefix = true;
            pos += 1;
        }
        else if (StartsWithCI(s, pos, "min"))
        {
            isMinor = true;
            third = 3;
            pos += 3;
        }
        else if (StartsWithExact(s, pos, "m") && !StartsWithCI(s, pos, "maj"))
        {
            isMinor = true;
            third = 3;
            pos += 1;
        }
        else if (StartsWithCI(s, pos, "dim"))
        {
            isDim = true;
            third = 3;
            fifth = 6;
            pos += 3;
        }
        else if (StartsWithExact(s, pos, "°"))
        {
            isDim = true;
            third = 3;
            fifth = 6;
            pos += 1;
        }
        else if (StartsWithExact(s, pos, "ø"))
        {
            isHalfDim = true;
            isMinor = true;
            third = 3;
            fifth = 6;
            pos += 1;
        }
        else if (StartsWithCI(s, pos, "aug"))
        {
            isAug = true;
            fifth = 8;
            pos += 3;
        }
        else if (StartsWithExact(s, pos, "+") && (pos + 1 == s.Length || char.IsDigit(s[pos + 1])))
        {
            isAug = true;
            fifth = 8;
            pos += 1;
        }

        // Handle minor-major seventh: mmaj7 / mM7
        if (isMinor && pos < s.Length)
        {
            if (StartsWithCI(s, pos, "maj"))
            {
                hasMajorSeventhPrefix = true;
                pos += 3;
            }
            else if (StartsWithExact(s, pos, "M") && pos + 1 < s.Length && char.IsDigit(s[pos + 1]))
            {
                hasMajorSeventhPrefix = true;
                pos += 1;
            }
        }

        // 2. Primary extension number right after quality (6, 6/9, 7, 9, 11, 13)
        var extension = 0;
        var hasSixNine = false;
        if (pos < s.Length && char.IsDigit(s[pos]))
        {
            if (StartsWithExact(s, pos, "6/9") || StartsWithExact(s, pos, "69"))
            {
                extension = 6;
                hasSixNine = true;
                pos += StartsWithExact(s, pos, "6/9") ? 3 : 2;
            }
            else if (StartsWithExact(s, pos, "13"))
            {
                extension = 13;
                pos += 2;
            }
            else if (StartsWithExact(s, pos, "11"))
            {
                extension = 11;
                pos += 2;
            }
            else if (s[pos] is '6' or '7' or '9')
            {
                extension = s[pos] - '0';
                pos += 1;
            }
        }
        else if (hasMajorSeventhPrefix && pos == s.Length)
        {
            // Bare "CΔ" means Cmaj7
            extension = 7;
        }
        else if (isHalfDim && extension == 0)
        {
            extension = 7;
        }

        // Build base intervals
        var hasThird = true;
        var hasFifth = true;
        var extraIntervals = new HashSet<int>();
        var susType = 0; // 0 = none, 2 = sus2, 4 = sus4

        if (extension == 6)
        {
            extraIntervals.Add(9);
            if (hasSixNine) extraIntervals.Add(2);
        }
        else if (extension >= 7)
        {
            var seventh = isDim ? 9 : (hasMajorSeventhPrefix ? 11 : 10);
            extraIntervals.Add(seventh);
            if (extension >= 9)
                extraIntervals.Add(2);
            if (extension >= 11)
                extraIntervals.Add(5);
            if (extension >= 13)
                extraIntervals.Add(9);
        }

        // 3. Parse remaining modifiers (sus2/sus4/add9/b5/-5/#5/b9/#9/#11/b13/no3/no5)
        var guard = 0;
        while (pos < s.Length && guard++ < 16)
        {
            if (StartsWithCI(s, pos, "sus4"))
            {
                susType = 4;
                third = 5;
                pos += 4;
            }
            else if (StartsWithCI(s, pos, "sus2"))
            {
                susType = 2;
                third = 2;
                pos += 4;
            }
            else if (StartsWithCI(s, pos, "sus"))
            {
                susType = 4;
                third = 5;
                pos += 3;
            }
            else if (StartsWithCI(s, pos, "add13"))
            {
                extraIntervals.Add(9);
                pos += 5;
            }
            else if (StartsWithCI(s, pos, "add11"))
            {
                extraIntervals.Add(5);
                pos += 5;
            }
            else if (StartsWithCI(s, pos, "add9"))
            {
                extraIntervals.Add(2);
                pos += 4;
            }
            else if (StartsWithCI(s, pos, "add4"))
            {
                extraIntervals.Add(5);
                pos += 4;
            }
            else if (StartsWithCI(s, pos, "add2"))
            {
                extraIntervals.Add(2);
                pos += 4;
            }
            else if (StartsWithCI(s, pos, "no3") || StartsWithCI(s, pos, "omit3"))
            {
                hasThird = false;
                pos += StartsWithCI(s, pos, "no3") ? 3 : 5;
            }
            else if (StartsWithCI(s, pos, "no5") || StartsWithCI(s, pos, "omit5"))
            {
                hasFifth = false;
                pos += StartsWithCI(s, pos, "no5") ? 3 : 5;
            }
            else if (StartsWithCI(s, pos, "b5") || StartsWithExact(s, pos, "-5"))
            {
                fifth = 6;
                pos += 2;
            }
            else if (StartsWithCI(s, pos, "#5") || StartsWithExact(s, pos, "+5"))
            {
                fifth = 8;
                pos += 2;
            }
            else if (StartsWithCI(s, pos, "b9") || StartsWithExact(s, pos, "-9"))
            {
                extraIntervals.Remove(2);
                extraIntervals.Add(1);
                pos += 2;
            }
            else if (StartsWithCI(s, pos, "#9") || StartsWithExact(s, pos, "+9"))
            {
                extraIntervals.Remove(2);
                extraIntervals.Add(3);
                pos += 2;
            }
            else if (StartsWithCI(s, pos, "#11") || StartsWithExact(s, pos, "+11"))
            {
                extraIntervals.Remove(5);
                extraIntervals.Add(6);
                pos += 3;
            }
            else if (StartsWithCI(s, pos, "b13") || StartsWithExact(s, pos, "-13"))
            {
                extraIntervals.Remove(9);
                extraIntervals.Add(8);
                pos += 3;
            }
            else if (StartsWithExact(s, pos, "2") && pos == 0)
            {
                // Shorthand like "C2" -> Csus2 / Cadd9 (treat as sus2)
                susType = 2;
                third = 2;
                pos += 1;
            }
            else if (StartsWithExact(s, pos, "4") && pos == 0)
            {
                susType = 4;
                third = 5;
                pos += 1;
            }
            else
            {
                // Unrecognized garbage token -> do not guess wildly
                return false;
            }
        }

        if (pos < s.Length)
            return false;

        intervals = [0];
        if (hasThird) intervals.Add(third);
        if (hasFifth) intervals.Add(fifth);
        foreach (var ex in extraIntervals)
            intervals.Add(ex);

        // On a 6-string guitar, perfect 5th can be omitted when chord has >= 4 distinct tones (e.g. C9, G13, E7#9)
        if (hasFifth && fifth == 7 && intervals.Count >= 4)
            omissible.Add(7);
        // For 11th / 13th chords, natural 9th is also commonly omitted on 6 strings
        if (extension >= 11 && intervals.Contains(2))
            omissible.Add(2);
        // For 11th dominant chords, major 3rd often clashes with 11th (4th) and may be omitted
        if (extension == 11 && !isMinor && intervals.Contains(4) && intervals.Contains(5))
            omissible.Add(4);

        canonicalQuality = BuildCanonicalQuality(isMinor, isDim, isAug, hasMajorSeventhPrefix, extension, susType, fifth, extraIntervals, suffix);
        return true;
    }

    private static string BuildCanonicalQuality(
        bool isMinor,
        bool isDim,
        bool isAug,
        bool hasMaj7,
        int extension,
        int susType,
        int fifth,
        HashSet<int> extra,
        string rawSuffix)
    {
        if (isDim && extension == 7) return "dim7";
        if (isDim) return "dim";
        if (isAug && extension == 0) return "aug";
        if (isMinor && extension == 7 && fifth == 6 && susType == 0) return "m7b5";
        if (susType == 2 && extension == 0 && extra.Count == 0) return "sus2";
        if (susType == 4 && extension == 0 && extra.Count == 0) return "sus4";
        if (susType == 4 && extension == 7 && !hasMaj7 && fifth == 7) return "7sus4";

        if (susType == 0 && fifth == 7)
        {
            if (!isMinor && !hasMaj7 && extension == 0 && extra.Count == 0) return "";
            if (isMinor && !hasMaj7 && extension == 0 && extra.Count == 0) return "m";
            if (!isMinor && !hasMaj7 && extension == 7 && extra.Count == 1) return "7";
            if (isMinor && !hasMaj7 && extension == 7 && extra.Count == 1) return "m7";
            if (!isMinor && hasMaj7 && extension == 7 && extra.Count == 1) return "maj7";
            if (!isMinor && !hasMaj7 && extension == 6 && extra.Count == 1) return "6";
            if (isMinor && !hasMaj7 && extension == 6 && extra.Count == 1) return "m6";
            if (!isMinor && !hasMaj7 && extension == 9 && extra.Count == 2) return "9";
            if (isMinor && !hasMaj7 && extension == 9 && extra.Count == 2) return "m9";
            if (!isMinor && hasMaj7 && extension == 9 && extra.Count == 2) return "maj9";
        }

        return rawSuffix.ToLowerInvariant();
    }

    private static bool StartsWithCI(string s, int pos, string token) =>
        pos + token.Length <= s.Length &&
        string.Compare(s, pos, token, 0, token.Length, StringComparison.OrdinalIgnoreCase) == 0;

    private static bool StartsWithExact(string s, int pos, string token) =>
        pos + token.Length <= s.Length &&
        string.Compare(s, pos, token, 0, token.Length, StringComparison.Ordinal) == 0;
}
