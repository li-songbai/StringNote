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
            if (!File.Exists(LibraryPath)) return new LibraryData();
            var loaded = JsonSerializer.Deserialize<LibraryData>(File.ReadAllText(LibraryPath), JsonOptions) ?? new LibraryData();
            loaded.Categories ??= [];
            loaded.Songs ??= [];
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

    private static void NormalizeSong(SongModel song)
    {
        song.Lines ??= [];
        foreach (var line in song.Lines)
        {
            line.Text ??= "";
            line.Chords ??= [];
            var length = line.Text.Length;
            foreach (var chord in line.Chords)
            {
                chord.Position = Math.Clamp(chord.Position, 0, length);
                chord.Name = ChordCatalog.NormalizeChordName(chord.Name);
                if (string.IsNullOrWhiteSpace(chord.ShapeId))
                    chord.ShapeId = ChordCatalog.ByName(chord.Name)?.Id ?? "";
            }
        }
    }
}
