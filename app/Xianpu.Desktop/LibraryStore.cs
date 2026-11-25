using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xianpu.Desktop;

public sealed class AppSettings
{
    public string CustomDataDirectory { get; set; } = "";
    public int MaxRollingBackups { get; set; } = 5;
}

public sealed record LibraryBackupInfo(
    string FilePath,
    string FileName,
    DateTime CreatedAt,
    long SizeBytes,
    int? SongCount);

public sealed class LibraryStore
{
    private const int DefaultMaxRollingBackups = 5;
    private static readonly TimeSpan AutoBackupInterval = TimeSpan.FromMinutes(3);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private AppSettings _settings;
    private DateTime _lastAutoBackupUtc = DateTime.MinValue;

    public LibraryStore()
    {
        _settings = LoadSettings();
    }

    public static string DefaultDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xianpu");

    public static string SettingsPath =>
        Path.Combine(DefaultDataDirectory, "settings.json");

    public static bool IsDataDirOverriddenByEnv =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("XIANPU_DATA_DIR"));

    public bool IsUsingCustomDataDirectory =>
        !IsDataDirOverriddenByEnv && !string.IsNullOrWhiteSpace(_settings.CustomDataDirectory);

    public int MaxRollingBackups =>
        Math.Clamp(_settings.MaxRollingBackups <= 0 ? DefaultMaxRollingBackups : _settings.MaxRollingBackups, 1, 20);

    public string DataDirectory
    {
        get
        {
            var overrideDirectory = Environment.GetEnvironmentVariable("XIANPU_DATA_DIR");
            if (!string.IsNullOrWhiteSpace(overrideDirectory))
                return overrideDirectory.Trim();

            if (!string.IsNullOrWhiteSpace(_settings.CustomDataDirectory))
                return _settings.CustomDataDirectory.Trim();

            return DefaultDataDirectory;
        }
    }

    public string LibraryPath => Path.Combine(DataDirectory, "library.json");

    public string BackupsDirectory => Path.Combine(DataDirectory, "backups");

    public void SetCustomDataDirectory(string? directoryPath)
    {
        var normalized = string.IsNullOrWhiteSpace(directoryPath)
            ? ""
            : Path.GetFullPath(directoryPath.Trim());

        if (!string.IsNullOrEmpty(normalized) &&
            string.Equals(
                normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(DefaultDataDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            normalized = "";
        }

        _settings.CustomDataDirectory = normalized;
        _lastAutoBackupUtc = DateTime.MinValue;
        SaveSettings(_settings);
    }

    private static AppSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions) ?? new AppSettings();
            loaded.CustomDataDirectory = (loaded.CustomDataDirectory ?? "").Trim();
            loaded.MaxRollingBackups = Math.Clamp(loaded.MaxRollingBackups <= 0 ? DefaultMaxRollingBackups : loaded.MaxRollingBackups, 1, 20);
            return loaded;
        }
        catch
        {
            return new AppSettings();
        }
    }

    private static void SaveSettings(AppSettings settings)
    {
        Directory.CreateDirectory(DefaultDataDirectory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var tempPath = SettingsPath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, SettingsPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            File.WriteAllText(SettingsPath, json);
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    public LibraryData Load() => LoadFromFile(LibraryPath, applyCustomChords: true);

    public static LibraryData LoadLibraryBackup(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("找不到指定的曲库备份文件。", filePath);

        return LoadFromFile(filePath, applyCustomChords: false);
    }

    private static LibraryData LoadFromFile(string filePath, bool applyCustomChords)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                var empty = new LibraryData();
                if (applyCustomChords)
                    ChordCatalog.LoadCustomData(empty.CustomChordsByKey, empty.CustomChordShapes);
                return empty;
            }
            var raw = File.ReadAllText(filePath);
            var loaded = JsonSerializer.Deserialize<LibraryData>(raw, JsonOptions)
                         ?? throw new InvalidDataException("曲库文件内容为空或格式无效。");
            loaded.Categories ??= [];
            loaded.Songs ??= [];
            loaded.CustomChordsByKey = new Dictionary<string, List<string>>(
                loaded.CustomChordsByKey ?? new Dictionary<string, List<string>>(),
                StringComparer.OrdinalIgnoreCase);
            loaded.CustomChordShapes ??= [];
            loaded.TheoryNotes ??= [];
            if (applyCustomChords)
                ChordCatalog.LoadCustomData(loaded.CustomChordsByKey, loaded.CustomChordShapes);
            foreach (var song in loaded.Songs)
                NormalizeSong(song);
            return loaded;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"曲谱库无法读取：{filePath}", ex);
        }
    }

    public static int? PeekLibrarySongCount(string libraryFilePath)
    {
        try
        {
            if (!File.Exists(libraryFilePath))
                return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(libraryFilePath));
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, "Songs", StringComparison.OrdinalIgnoreCase) &&
                    prop.Value.ValueKind == JsonValueKind.Array)
                {
                    return prop.Value.GetArrayLength();
                }
            }
            return 0;
        }
        catch
        {
            return null;
        }
    }

    public void Save(LibraryData library)
    {
        var directory = Path.GetDirectoryName(LibraryPath)!;
        Directory.CreateDirectory(directory);
        TryCreateRollingBackup();

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

    public static string CrashLogPath =>
        Path.Combine(DefaultDataDirectory, "logs", "crash.log");

    public string? QuarantineCorruptedLibrary()
    {
        try
        {
            if (!File.Exists(LibraryPath))
                return null;

            var directory = Path.GetDirectoryName(LibraryPath)!;
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var dest = Path.Combine(directory, $"library.corrupted-{stamp}.json");
            File.Move(LibraryPath, dest, overwrite: true);
            return dest;
        }
        catch
        {
            try
            {
                if (!File.Exists(LibraryPath))
                    return null;
                var directory = Path.GetDirectoryName(LibraryPath)!;
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var dest = Path.Combine(directory, $"library.corrupted-{stamp}.json");
                File.Copy(LibraryPath, dest, overwrite: true);
                return dest;
            }
            catch
            {
                return null;
            }
        }
    }

    public static void AppendCrashLog(string source, Exception ex)
    {
        try
        {
            var logPath = CrashLogPath;
            var dir = Path.GetDirectoryName(logPath)!;
            Directory.CreateDirectory(dir);

            // Keep crash.log under ~512 KB to avoid unbounded growth
            if (File.Exists(logPath) && new FileInfo(logPath).Length > 512 * 1024)
            {
                var prevPath = logPath + ".1";
                try { File.Move(logPath, prevPath, overwrite: true); } catch { }
            }

            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] [v{UpdateService.CurrentVersion}]{Environment.NewLine}{ex}{Environment.NewLine}{new string('-', 60)}{Environment.NewLine}";
            File.AppendAllText(logPath, entry);
        }
        catch
        {
            // Logging must never throw a secondary exception
        }
    }

    public string? CreateManualSnapshot(string tag = "manual")
    {
        try
        {
            if (!File.Exists(LibraryPath))
                return null;

            Directory.CreateDirectory(BackupsDirectory);
            var safeTag = string.IsNullOrWhiteSpace(tag) ? "manual" : tag.Trim();
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var dest = Path.Combine(BackupsDirectory, $"library-{stamp}-{safeTag}.json");
            File.Copy(LibraryPath, dest, overwrite: true);
            PruneRollingBackups();
            return dest;
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<LibraryBackupInfo> ListRollingBackups()
    {
        try
        {
            if (!Directory.Exists(BackupsDirectory))
                return [];

            return Directory.GetFiles(BackupsDirectory, "library-*.json")
                .Select(path =>
                {
                    var info = new FileInfo(path);
                    return new LibraryBackupInfo(
                        path,
                        info.Name,
                        info.LastWriteTime,
                        info.Length,
                        PeekLibrarySongCount(path));
                })
                .OrderByDescending(b => b.CreatedAt)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private void TryCreateRollingBackup()
    {
        try
        {
            if (!File.Exists(LibraryPath))
                return;

            var nowUtc = DateTime.UtcNow;
            if (_lastAutoBackupUtc != DateTime.MinValue && (nowUtc - _lastAutoBackupUtc) < AutoBackupInterval)
                return;

            Directory.CreateDirectory(BackupsDirectory);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var backupPath = Path.Combine(BackupsDirectory, $"library-{stamp}.json");
            File.Copy(LibraryPath, backupPath, overwrite: true);
            _lastAutoBackupUtc = nowUtc;
            PruneRollingBackups();
        }
        catch
        {
            // Never let backup housekeeping block normal saving.
        }
    }

    private void PruneRollingBackups()
    {
        try
        {
            if (!Directory.Exists(BackupsDirectory))
                return;

            var files = new DirectoryInfo(BackupsDirectory)
                .GetFiles("library-*.json")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            var keep = MaxRollingBackups;
            for (var i = keep; i < files.Count; i++)
            {
                try { files[i].Delete(); } catch { }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    public static void ExportLibraryBackup(LibraryData library, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(library, JsonOptions));

    public static string SerializeSongSnapshot(SongModel song)
    {
        var savedAt = song.UpdatedAt;
        try
        {
            song.UpdatedAt = DateTime.MinValue;
            return JsonSerializer.Serialize(song, CompactJsonOptions);
        }
        finally
        {
            song.UpdatedAt = savedAt;
        }
    }

    public static SongModel? DeserializeSongSnapshot(string json)
    {
        var song = JsonSerializer.Deserialize<SongModel>(json, CompactJsonOptions);
        if (song is not null)
            NormalizeSong(song);
        return song;
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

    public static void SaveTabSection(TabSection section, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(section, JsonOptions));

    public static TabSection LoadTabSection(string path)
    {
        var section = JsonSerializer.Deserialize<TabSection>(File.ReadAllText(path), JsonOptions)
                      ?? throw new InvalidDataException("六线谱片段文件为空或格式不正确。");
        NormalizeSong(new SongModel { TabSections = [section] });
        section.Id = Guid.NewGuid().ToString("N");
        return section;
    }

    public static void NormalizeSong(SongModel song)
    {
        song.Artist = (song.Artist ?? "").Trim();
        song.Bpm = song.Bpm <= 0 ? 0 : Math.Clamp(song.Bpm, 30, 240);
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
                        last.GraceFret = (last.GraceFret ?? "").Trim();
                        if (string.IsNullOrEmpty(last.GraceFret))
                            last.GraceLink = TabLinkKind.None;
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

                bar.ColumnMetas ??= [];
                bar.ColumnMetas = bar.ColumnMetas
                    .Where(m => m.Column >= 0 && m.Column < bar.ColumnCount)
                    .GroupBy(m => m.Column)
                    .Select(g => g.Last())
                    .Where(m => m.Duration != TabDuration.Auto || m.Dotted || m.IsRest || m.Strum != TabStrumKind.None || m.PalmMute)
                    .OrderBy(m => m.Column)
                    .ToList();
            }
        }
    }
}
