using System.Text.RegularExpressions;

namespace Xianpu.Desktop;

public static class ChordTranspose
{
    private const string Notes = "C D EF G A B";
    private static readonly string[] Sharps = "C C# D D# E F F# G G# A A# B".Split(' ');
    private static readonly string[] Flats = "C Db D Eb E F Gb G Ab A Bb B".Split(' ');
    private static readonly string[] CanonicalMajorKeys = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];
    private static readonly string[] CanonicalMinorKeys = ["Cm", "C#m", "Dm", "Ebm", "Em", "Fm", "F#m", "Gm", "G#m", "Am", "Bbm", "Bm"];

    private static readonly string[] SharpDegrees = ["1", "#1", "2", "b3", "3", "4", "#4", "5", "#5", "6", "b7", "7"];
    private static readonly string[] FlatDegrees = ["1", "b2", "2", "b3", "3", "4", "b5", "5", "b6", "6", "b7", "7"];

    private static readonly HashSet<string> FlatKeySet = new(StringComparer.OrdinalIgnoreCase)
    {
        "F", "Bb", "Eb", "Ab", "Db", "Gb",
        "Dm", "Gm", "Cm", "Fm", "Bbm", "Ebm"
    };

    private static int Pitch(string note)
    {
        var natural = Notes.IndexOf(char.ToUpperInvariant(note[0]));
        var accidental = note.Length == 1 ? 0 : note[1] == '#' ? 1 : -1;
        return (natural + accidental + 12) % 12;
    }

    public static (int Pitch, bool Minor)? ReadKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var cleaned = Normalize(key);
        cleaned = Regex.Replace(cleaned, @"(大调|小调|调)$", "", RegexOptions.CultureInvariant).Trim();
        var match = Regex.Match(cleaned, @"^([A-Ga-g][#b]?)(m?)$");
        return match.Success ? (Pitch(match.Groups[1].Value), match.Groups[2].Length > 0) : null;
    }

    public static bool PreferFlats(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        if (key.Contains('♭')) return true;
        var cleaned = Normalize(key);
        if (FlatKeySet.Contains(cleaned)) return true;
        var match = Regex.Match(cleaned, @"^([A-Ga-g])([#b]?)(m?)$");
        return match.Success && match.Groups[2].Value.Equals("b", StringComparison.OrdinalIgnoreCase);
    }

    public static string? Shift(string chord, int shift, bool flats)
    {
        if (string.IsNullOrWhiteSpace(chord)) return null;
        var match = Regex.Match(Normalize(chord), @"^([A-Ga-g][#b]?)(.*)$");
        if (!match.Success) return null;
        var notes = flats ? Flats : Sharps;
        string Move(string note) => notes[(Pitch(note) + (shift % 12) + 24) % 12];
        var suffix = Regex.Replace(match.Groups[2].Value, @"/([A-Ga-g][#b]?)$", bass => "/" + Move(bass.Groups[1].Value));
        return Move(match.Groups[1].Value) + suffix;
    }

    public static string ShiftKey(string key, int semitones)
    {
        var parsed = ReadKey(key);
        if (parsed is null) return key;
        var newPitch = (parsed.Value.Pitch + (semitones % 12) + 24) % 12;
        return parsed.Value.Minor ? CanonicalMinorKeys[newPitch] : CanonicalMajorKeys[newPitch];
    }

    public static void TransposeSongInPlace(SongModel song, int semitones, bool updateSelectedKey = true)
    {
        if (song is null) return;
        var targetKey = !string.IsNullOrWhiteSpace(song.SelectedKey)
            ? ShiftKey(song.SelectedKey, semitones)
            : (!string.IsNullOrWhiteSpace(song.OriginalKey) ? ShiftKey(song.OriginalKey, semitones) : "C");

        var useFlats = PreferFlats(targetKey);
        foreach (var line in song.Lines)
        {
            foreach (var chord in line.Chords)
            {
                var shifted = Shift(chord.Name, semitones, useFlats);
                if (shifted is null) continue;
                var normalized = ChordCatalog.NormalizeChordName(shifted);
                chord.Name = normalized;
                var shape = ChordCatalog.ByName(normalized)
                            ?? ChordCatalog.ByName(Shift(normalized, 0, !useFlats));
                chord.ShapeId = shape?.Id ?? "";
            }
        }

        if (updateSelectedKey)
            song.SelectedKey = targetKey;
    }

    public static bool TransposeSongKeys(SongModel song, string fromKey, string toKey)
    {
        if (song is null) return false;
        var from = ReadKey(fromKey);
        var to = ReadKey(toKey);
        if (from is null || to is null) return false;

        var shift = ((to.Value.Pitch - from.Value.Pitch) % 12 + 12) % 12;
        var useFlats = PreferFlats(toKey);
        foreach (var line in song.Lines)
        {
            foreach (var chord in line.Chords)
            {
                var shifted = Shift(chord.Name, shift, useFlats);
                if (shifted is null) continue;
                var normalized = ChordCatalog.NormalizeChordName(shifted);
                chord.Name = normalized;
                var shape = ChordCatalog.ByName(normalized)
                            ?? ChordCatalog.ByName(Shift(normalized, 0, !useFlats));
                chord.ShapeId = shape?.Id ?? "";
            }
        }

        var cleanedTo = Regex.Replace(Normalize(toKey), @"(大调|小调|调)$", "", RegexOptions.CultureInvariant).Trim();
        var toMatch = Regex.Match(cleanedTo, @"^([A-Ga-g])([#b]?)(m?)$");
        song.SelectedKey = toMatch.Success
            ? char.ToUpperInvariant(toMatch.Groups[1].Value[0]) + toMatch.Groups[2].Value + toMatch.Groups[3].Value
            : ShiftKey(toKey, 0);
        return true;
    }

    public static string ToDegree(string chordName, string? key)
    {
        if (string.IsNullOrWhiteSpace(chordName)) return "";
        var effectiveKey = string.IsNullOrWhiteSpace(key) ? "C" : key;
        var parsedKey = ReadKey(effectiveKey) ?? (0, false);
        var majorPitch = parsedKey.Minor ? (parsedKey.Pitch + 3) % 12 : parsedKey.Pitch;
        var degrees = PreferFlats(effectiveKey) ? FlatDegrees : SharpDegrees;

        var match = Regex.Match(Normalize(chordName), @"^([A-Ga-g][#b]?)(.*)$");
        if (!match.Success) return chordName;

        string DegreeFor(string note) => degrees[(Pitch(note) - majorPitch + 12) % 12];
        var suffix = Regex.Replace(match.Groups[2].Value, @"/([A-Ga-g][#b]?)$", bass => "/" + DegreeFor(bass.Groups[1].Value));
        return DegreeFor(match.Groups[1].Value) + suffix;
    }

    private static string Normalize(string value) => value.Trim().Replace('♯', '#').Replace('♭', 'b');
}
