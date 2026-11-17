using System.Text.RegularExpressions;

namespace Xianpu.Desktop;

public static class ChordCatalog
{
    private static readonly string[] CommonTemplate =
    [
        "C", "Dm", "Em", "F", "G", "Am",
        "G7", "Fmaj7", "Em7", "Dm7", "Am7", "Cadd9", "G/B", "Cmaj7"
    ];

    private static readonly List<ChordShape> Shapes = Build();
    private static readonly Dictionary<string, ChordShape> ById = Shapes.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ChordShape> FirstByName = Shapes
        .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, List<string>> CustomChordsByKey = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ChordShape> CustomByName = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ChordShape> CustomById = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ChordShape> All => Shapes;

    public static ChordShape? ByShapeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (CustomById.TryGetValue(id, out var custom)) return custom;
        return ById.TryGetValue(id, out var shape) ? shape : null;
    }

    public static ChordShape? ByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var normalized = NormalizeChordName(name);
        if (CustomByName.TryGetValue(normalized, out var custom)) return custom;
        return FirstByName.TryGetValue(normalized, out var shape) ? shape : null;
    }

    public static ChordShape? BuiltInByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var normalized = NormalizeChordName(name);
        return FirstByName.TryGetValue(normalized, out var shape) ? shape : null;
    }

    public static string NormalizeKeyBucket(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "C";
        var parsed = ChordTranspose.ReadKey(key);
        if (parsed is null) return key.Trim();
        var cleaned = Regex.Replace(key.Trim().Replace('♯', '#').Replace('♭', 'b'), @"(大调|小调|调)$", "", RegexOptions.CultureInvariant).Trim();
        var match = Regex.Match(cleaned, @"^([A-Ga-g])([#b]?)(m?)$");
        if (!match.Success) return key.Trim();
        return char.ToUpperInvariant(match.Groups[1].Value[0]) + match.Groups[2].Value + match.Groups[3].Value;
    }

    public static void LoadCustomData(
        IDictionary<string, List<string>>? chordsByKey,
        IEnumerable<CustomChordDefinition>? customShapes)
    {
        CustomChordsByKey.Clear();
        if (chordsByKey is not null)
        {
            foreach (var (rawKey, list) in chordsByKey)
            {
                if (list is null) continue;
                var bucket = NormalizeKeyBucket(rawKey);
                if (!CustomChordsByKey.TryGetValue(bucket, out var target))
                {
                    target = [];
                    CustomChordsByKey[bucket] = target;
                }
                foreach (var item in list)
                {
                    var normalized = NormalizeChordName(item);
                    if (!string.IsNullOrWhiteSpace(normalized) &&
                        !target.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        target.Add(normalized);
                    }
                }
            }
        }

        CustomByName.Clear();
        CustomById.Clear();
        if (customShapes is not null)
        {
            foreach (var def in customShapes)
            {
                var normalized = NormalizeChordName(def.Name);
                if (string.IsNullOrWhiteSpace(normalized)) continue;
                var frets = new int?[6];
                var fingers = new int[6];
                for (var i = 0; i < 6; i++)
                {
                    frets[i] = def.Frets is not null && i < def.Frets.Length ? def.Frets[i] : null;
                    fingers[i] = def.Fingers is not null && i < def.Fingers.Length ? Math.Clamp(def.Fingers[i], 0, 4) : 0;
                }
                var id = $"custom-{normalized}";
                var shape = new ChordShape
                {
                    Id = id,
                    Name = normalized,
                    Type = "custom",
                    Frets = frets,
                    Fingers = fingers,
                    BaseFret = Math.Clamp(def.BaseFret <= 0 ? 1 : def.BaseFret, 1, 15),
                    Barre = null,
                    Label = string.IsNullOrWhiteSpace(def.Label) ? "自定义和弦" : def.Label.Trim()
                };
                CustomByName[normalized] = shape;
                CustomById[id] = shape;
            }
        }
    }

    public static string NormalizeChordName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var trimmed = raw.Trim().Replace('♯', '#').Replace('♭', 'b').Replace('m', 'm');
        var match = Regex.Match(trimmed, @"^([A-Ga-g])([#b]?)(.*?)(?:/([A-Ga-g])([#b]?))?$");
        if (!match.Success) return trimmed;

        var root = char.ToUpperInvariant(match.Groups[1].Value[0]) + NormalizeAccidental(match.Groups[2].Value);
        var quality = NormalizeQuality(match.Groups[3].Value.Trim());
        if (!match.Groups[4].Success || string.IsNullOrEmpty(match.Groups[4].Value))
            return root + quality;

        var bass = char.ToUpperInvariant(match.Groups[4].Value[0]) + NormalizeAccidental(match.Groups[5].Value);
        return $"{root}{quality}/{bass}";
    }

    private static string NormalizeAccidental(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value[0] is '#' ? "#" : "b";
    }

    private static string NormalizeQuality(string quality)
    {
        if (string.IsNullOrEmpty(quality)) return "";
        quality = Regex.Replace(quality, @"^maj", "maj", RegexOptions.IgnoreCase);
        quality = Regex.Replace(quality, @"^min", "m", RegexOptions.IgnoreCase);
        quality = Regex.Replace(quality, @"^sus", "sus", RegexOptions.IgnoreCase);
        quality = Regex.Replace(quality, @"^add", "add", RegexOptions.IgnoreCase);
        quality = Regex.Replace(quality, @"^dim", "dim", RegexOptions.IgnoreCase);
        quality = Regex.Replace(quality, @"^aug", "aug", RegexOptions.IgnoreCase);
        if (quality.StartsWith("M", StringComparison.Ordinal) && !quality.StartsWith("Maj", StringComparison.Ordinal))
            quality = "m" + quality[1..];
        return quality;
    }

    public static bool IsCustomChordInKey(string? key, string chordName)
    {
        var bucket = NormalizeKeyBucket(key);
        var normalized = NormalizeChordName(chordName);
        return CustomChordsByKey.TryGetValue(bucket, out var list) &&
               list.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> GetCommonChordsForKey(string? key)
    {
        var parsed = ChordTranspose.ReadKey(key);
        var result = new List<string>(CommonTemplate.Length + 4);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (parsed is null)
        {
            foreach (var item in CommonTemplate)
            {
                if (seen.Add(item))
                    result.Add(item);
            }
        }
        else
        {
            var shift = parsed.Value.Minor ? (parsed.Value.Pitch + 3) % 12 : parsed.Value.Pitch;
            var flats = ChordTranspose.PreferFlats(key);
            foreach (var item in CommonTemplate)
            {
                var shifted = ChordTranspose.Shift(item, shift, flats) ?? item;
                var normalized = NormalizeChordName(shifted);
                if (seen.Add(normalized))
                    result.Add(normalized);
            }
        }

        var bucket = NormalizeKeyBucket(key);
        if (CustomChordsByKey.TryGetValue(bucket, out var customList))
        {
            foreach (var customChord in customList)
            {
                var normalized = NormalizeChordName(customChord);
                if (!string.IsNullOrWhiteSpace(normalized) && seen.Add(normalized))
                    result.Add(normalized);
            }
        }

        return result;
    }

    private static List<ChordShape> Build()
    {
        var shapes = new List<ChordShape>();
        void Add(string id, string name, string type, int?[] frets, int[] fingers, int baseFret = 1, BarreShape? barre = null, string label = "开放") =>
            shapes.Add(new ChordShape { Id = id, Name = name, Type = type, Frets = frets, Fingers = fingers, BaseFret = baseFret, Barre = barre, Label = label });

        Add("C", "C", "open", [null, 3, 2, 0, 1, 0], [0, 3, 2, 0, 1, 0]);
        Add("D", "D", "open", [null, null, 0, 2, 3, 2], [0, 0, 0, 1, 3, 2]);
        Add("E", "E", "open", [0, 2, 2, 1, 0, 0], [0, 2, 3, 1, 0, 0]);
        Add("F", "F", "barre", [1, 3, 3, 2, 1, 1], [1, 3, 4, 2, 1, 1], 1, new(1, 0, 5), "E 形横按");
        Add("G", "G", "open", [3, 2, 0, 0, 0, 3], [2, 1, 0, 0, 0, 3]);
        Add("A", "A", "open", [null, 0, 2, 2, 2, 0], [0, 0, 1, 2, 3, 0]);
        Add("Am", "Am", "open", [null, 0, 2, 2, 1, 0], [0, 0, 2, 3, 1, 0]);
        Add("Dm", "Dm", "open", [null, null, 0, 2, 3, 1], [0, 0, 0, 2, 3, 1]);
        Add("Em", "Em", "open", [0, 2, 2, 0, 0, 0], [0, 2, 3, 0, 0, 0]);
        Add("A7", "A7", "open", [null, 0, 2, 0, 2, 0], [0, 0, 2, 0, 3, 0]);
        Add("B7", "B7", "open", [null, 2, 1, 2, 0, 2], [0, 2, 1, 3, 0, 4]);
        Add("D7", "D7", "open", [null, null, 0, 2, 1, 2], [0, 0, 0, 2, 1, 3]);
        Add("E7", "E7", "open", [0, 2, 0, 1, 0, 0], [0, 2, 0, 1, 0, 0]);
        Add("G7", "G7", "open", [3, 2, 0, 0, 0, 1], [3, 2, 0, 0, 0, 1]);
        Add("C7", "C7", "open", [null, 3, 2, 3, 1, 0], [0, 3, 2, 4, 1, 0]);
        Add("Am7", "Am7", "open", [null, 0, 2, 0, 1, 0], [0, 0, 2, 0, 1, 0]);
        Add("Dm7", "Dm7", "open", [null, null, 0, 2, 1, 1], [0, 0, 0, 2, 1, 1], 1, new(1, 4, 5));
        Add("Em7", "Em7", "open", [0, 2, 2, 0, 3, 0], [0, 2, 3, 0, 4, 0]);
        Add("Bm7", "Bm7", "barre", [null, 2, 4, 2, 3, 2], [0, 1, 3, 1, 2, 1], 2, new(2, 1, 5), "Am7 形横按");
        Add("F#m7", "F#m7", "barre", [2, 4, 2, 2, 2, 2], [1, 3, 1, 1, 1, 1], 2, new(2, 0, 5), "Em7 形横按");
        Add("Cmaj7", "Cmaj7", "open", [null, 3, 2, 0, 0, 0], [0, 3, 2, 0, 0, 0]);
        Add("Fmaj7", "Fmaj7", "open", [null, null, 3, 2, 1, 0], [0, 0, 3, 2, 1, 0]);
        Add("Gmaj7", "Gmaj7", "open", [3, 2, 0, 0, 0, 2], [2, 1, 0, 0, 0, 3]);
        Add("Dmaj7", "Dmaj7", "open", [null, null, 0, 2, 2, 2], [0, 0, 0, 1, 2, 3]);
        Add("Amaj7", "Amaj7", "open", [null, 0, 2, 1, 2, 0], [0, 0, 2, 1, 3, 0]);
        Add("Cadd9", "Cadd9", "open", [null, 3, 2, 0, 3, 0], [0, 2, 1, 0, 3, 0]);
        Add("Fadd9", "Fadd9", "open", [null, null, 3, 2, 1, 3], [0, 0, 3, 2, 1, 4]);
        Add("G/B", "G/B", "open", [null, 2, 0, 0, 3, 3], [0, 1, 0, 0, 3, 4], 1, null, "转位和弦");
        Add("D/F#", "D/F#", "open", [2, 0, 0, 2, 3, 2], [1, 0, 0, 2, 4, 3], 1, null, "转位和弦");
        Add("C/E", "C/E", "open", [0, 3, 2, 0, 1, 0], [0, 3, 2, 0, 1, 0], 1, null, "转位和弦");
        Add("C/G", "C/G", "open", [3, 3, 2, 0, 1, 0], [3, 4, 2, 0, 1, 0], 1, null, "转位和弦");
        Add("Am/G", "Am/G", "open", [3, 0, 2, 2, 1, 0], [3, 0, 2, 4, 1, 0], 1, null, "转位和弦");
        Add("F#m7-5", "F#m7-5", "open", [2, null, 2, 2, 1, 0], [2, 0, 3, 4, 1, 0], 1, null, "半减七");
        Add("Bm7-5", "Bm7-5", "open", [null, 2, 3, 2, 3, null], [0, 1, 3, 2, 4, 0], 1, null, "半减七");
        Add("Asus2", "Asus2", "open", [null, 0, 2, 2, 0, 0], [0, 0, 1, 2, 0, 0]);
        Add("Asus4", "Asus4", "open", [null, 0, 2, 2, 3, 0], [0, 0, 1, 2, 3, 0]);
        Add("Dsus2", "Dsus2", "open", [null, null, 0, 2, 3, 0], [0, 0, 0, 1, 3, 0]);
        Add("Dsus4", "Dsus4", "open", [null, null, 0, 2, 3, 3], [0, 0, 0, 1, 3, 4]);
        Add("Esus4", "Esus4", "open", [0, 2, 2, 2, 0, 0], [0, 1, 2, 3, 0, 0]);

        var roots = new[] { "C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B" };
        var eRootIndex = Array.IndexOf(roots, "E");
        var eShapeRoots = new HashSet<string>(["F", "F#", "G", "Ab", "Bb", "B", "C", "C#", "D", "Eb"], StringComparer.OrdinalIgnoreCase);
        for (var fret = 1; fret <= 11; fret++)
        {
            var root = roots[(eRootIndex + fret) % 12];
            if (!eShapeRoots.Contains(root)) continue;
            if (root != "F")
                Add($"barre-E-{root}", root, "barre", [fret, fret + 2, fret + 2, fret + 1, fret, fret], [1, 3, 4, 2, 1, 1], fret, new(fret, 0, 5), "E 形横按");
            Add($"barre-Em-{root}", root + "m", "barre", [fret, fret + 2, fret + 2, fret, fret, fret], [1, 3, 4, 1, 1, 1], fret, new(fret, 0, 5), "Em 形横按");
        }

        var aRootIndex = Array.IndexOf(roots, "A");
        var aShapeRoots = new HashSet<string>(["Bb", "B", "C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab"], StringComparer.OrdinalIgnoreCase);
        for (var fret = 1; fret <= 11; fret++)
        {
            var root = roots[(aRootIndex + fret) % 12];
            if (!aShapeRoots.Contains(root)) continue;
            Add($"barre-A-{root}", root, "barre", [null, fret, fret + 2, fret + 2, fret + 2, fret], [0, 1, 3, 3, 3, 1], fret, new(fret, 1, 5), "A 形横按");
            Add($"barre-Am-{root}", root + "m", "barre", [null, fret, fret + 2, fret + 2, fret + 1, fret], [0, 1, 3, 4, 2, 1], fret, new(fret, 1, 5), "Am 形横按");
        }
        return shapes;
    }
}
