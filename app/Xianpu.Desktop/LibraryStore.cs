using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xianpu.Desktop;

public sealed class LibraryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string LibraryPath { get; } = GetLibraryPath();

    private static string GetLibraryPath()
    {
        var overrideDirectory = Environment.GetEnvironmentVariable("XIANPU_DATA_DIR");
        var directory = string.IsNullOrWhiteSpace(overrideDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xianpu")
            : overrideDirectory;
        return Path.Combine(directory, "library.json");
    }

    public LibraryData Load()
    {
        try
        {
            if (!File.Exists(LibraryPath))
            {
                var empty = new LibraryData();
                ChordCatalog.LoadCustomData(empty.CustomChordsByKey, empty.CustomChordShapes);
                return empty;
            }
            var loaded = JsonSerializer.Deserialize<LibraryData>(File.ReadAllText(LibraryPath), JsonOptions) ?? new LibraryData();
            loaded.Categories ??= [];
            loaded.Songs ??= [];
            loaded.CustomChordsByKey = new Dictionary<string, List<string>>(
                loaded.CustomChordsByKey ?? new Dictionary<string, List<string>>(),
                StringComparer.OrdinalIgnoreCase);
            loaded.CustomChordShapes ??= [];
            ChordCatalog.LoadCustomData(loaded.CustomChordsByKey, loaded.CustomChordShapes);
            foreach (var song in loaded.Songs)
                NormalizeSong(song);
            return loaded;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"曲谱库无法读取：{LibraryPath}", ex);
        }
    }

    public void Save(LibraryData library)
    {
        var directory = Path.GetDirectoryName(LibraryPath)!;
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(library, JsonOptions);
        var tempPath = LibraryPath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, LibraryPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            File.WriteAllText(LibraryPath, json);
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch
            {
                // Ignore cleanup failure on temp file
            }
        }
    }

    public static void SaveSongProject(SongModel song, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(song, JsonOptions));

    public static SongModel LoadSongProject(string path)
    {
        var song = JsonSerializer.Deserialize<SongModel>(File.ReadAllText(path), JsonOptions)
                   ?? throw new InvalidDataException("曲谱项目文件为空或格式不正确。");
        NormalizeSong(song);
        return song;
    }

    public static void NormalizeSong(SongModel song)
    {
        song.Lines ??= [];
        foreach (var line in song.Lines)
        {
            line.Text ??= "";
            line.PartTitle = (line.PartTitle ?? "").Trim();
            line.RepeatCount = Math.Clamp(line.RepeatCount <= 0 ? 1 : line.RepeatCount, 1, 16);
            line.AltLyrics ??= [];
            line.Chords ??= [];

            if (line.IsReferenceLine)
            {
                line.AltLyrics.Clear();
                line.Chords.Clear();
            }
            else
            {
                for (var i = 0; i < line.AltLyrics.Count; i++)
                    line.AltLyrics[i] = (line.AltLyrics[i] ?? "").Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);

                while (line.AltLyrics.Count > 0 && string.IsNullOrWhiteSpace(line.AltLyrics[^1]))
                    line.AltLyrics.RemoveAt(line.AltLyrics.Count - 1);

                if (line.AltLyrics.Count > 0 && line.AltLyrics.All(string.IsNullOrWhiteSpace))
                    line.AltLyrics.Clear();
            }

            var length = line.Text.Length;
            foreach (var chord in line.Chords)
            {
                chord.Position = Math.Clamp(chord.Position, 0, length);
                chord.Name = ChordCatalog.NormalizeChordName(chord.Name);
                if (string.IsNullOrWhiteSpace(chord.ShapeId))
                    chord.ShapeId = ChordCatalog.ByName(chord.Name)?.Id ?? "";
            }
        }

        song.TabSections ??= [];
        foreach (var section in song.TabSections)
        {
            section.Id = string.IsNullOrWhiteSpace(section.Id) ? Guid.NewGuid().ToString("N") : section.Id;
            section.Title = string.IsNullOrWhiteSpace(section.Title)
                ? section.Kind switch
                {
                    TabSectionKind.Intro => "前奏",
                    TabSectionKind.Outro => "尾奏",
                    _ => "间奏"
                }
                : section.Title.Trim();
            section.TimeSignature = string.IsNullOrWhiteSpace(section.TimeSignature) ? "4/4" : section.TimeSignature.Trim();
            section.ColumnsPerBar = Math.Clamp(section.ColumnsPerBar <= 0 ? 8 : section.ColumnsPerBar, 4, 16);
            section.InsertAfterLineIndex = section.Kind switch
            {
                TabSectionKind.Intro => -1,
                TabSectionKind.Outro => song.Lines.Count > 0 ? song.Lines.Count - 1 : -1,
                _ => song.Lines.Count > 0 ? Math.Clamp(section.InsertAfterLineIndex, 0, song.Lines.Count - 1) : -1
            };

            section.Bars ??= [];
            foreach (var bar in section.Bars)
            {
                bar.ColumnCount = Math.Clamp(bar.ColumnCount <= 0 ? section.ColumnsPerBar : bar.ColumnCount, 1, 16);
                bar.Notes ??= [];
                bar.Notes = bar.Notes
                    .Where(n => n.StringIndex is >= 0 and <= 5 && n.Column >= 0 && n.Column < bar.ColumnCount && !string.IsNullOrWhiteSpace(n.Fret))
                    .GroupBy(n => (n.Column, n.StringIndex))
                    .Select(g =>
                    {
                        var last = g.Last();
                        last.Fret = last.Fret.Trim();
                        return last;
                    })
                    .ToList();

                bar.ChordMarks ??= [];
                bar.ChordMarks = bar.ChordMarks
                    .Where(m => m.Column >= 0 && m.Column < bar.ColumnCount && !string.IsNullOrWhiteSpace(m.Text))
                    .GroupBy(m => m.Column)
                    .Select(g =>
                    {
                        var last = g.Last();
                        last.Text = last.Text.Trim();
                        return last;
                    })
                    .ToList();
            }
        }
    }
}
