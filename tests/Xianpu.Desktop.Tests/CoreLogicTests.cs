using System.IO;
using Xianpu.Desktop;
using Xunit;

namespace Xianpu.Desktop.Tests;

public sealed class CoreLogicTests
{
    [Fact]
    public void ChordTranspose_ShiftsBasicAndSlashChordsCorrectly()
    {
        Assert.Equal("G", ChordTranspose.Shift("C", 7, flats: false));
        Assert.Equal("Em7", ChordTranspose.Shift("Am7", 7, flats: false));
        Assert.Equal("G/B", ChordTranspose.Shift("C/E", 7, flats: false));
        Assert.Equal("Bb", ChordTranspose.Shift("C", -2, flats: true));
    }

    [Fact]
    public void ChordTranspose_TransposeSongKeys_PreservesPositionsAndUpdatesSelectedKey()
    {
        var song = new SongModel
        {
            Title = "移调测试",
            OriginalKey = "C",
            SelectedKey = "C",
            Lines =
            [
                new LyricLine
                {
                    Text = "故事的小黄花从出生那年就飘着",
                    Chords =
                    [
                        new ChordPlacement { Position = 0, Name = "C" },
                        new ChordPlacement { Position = 3, Name = "G" },
                        new ChordPlacement { Position = 6, Name = "Am" },
                        new ChordPlacement { Position = 9, Name = "F" },
                        new ChordPlacement { Position = 12, Name = "C/E" }
                    ]
                }
            ]
        };

        var ok = ChordTranspose.TransposeSongKeys(song, "C", "G");

        Assert.True(ok);
        Assert.Equal("G", song.SelectedKey);
        Assert.Equal(["G", "D", "Em", "C", "G/B"], song.Lines[0].Chords.Select(c => c.Name).ToArray());
        Assert.Equal([0, 3, 6, 9, 12], song.Lines[0].Chords.Select(c => c.Position).ToArray());
    }

    [Fact]
    public void ChordTranspose_ToDegree_MapsDiatonicChords()
    {
        Assert.Equal("1", ChordTranspose.ToDegree("C", "C"));
        Assert.Equal("5", ChordTranspose.ToDegree("G", "C"));
        Assert.Equal("6m", ChordTranspose.ToDegree("Am", "C"));
        Assert.Equal("4", ChordTranspose.ToDegree("F", "C"));
    }

    [Fact]
    public void LrcImporter_ExtractsMetadataFiltersCreditsAndStripsTimestamps()
    {
        var lrc = """
            [ti:秋日木吉他]
            [ar:测试歌手]
            [00:00.00]作词：测试作者
            [00:01.00]作曲：测试作者
            [00:05.12]晚风吹过旧窗台
            [00:10.50]木吉他<00:11.00>轻轻响起来
            """;

        var song = LrcImporter.ImportText(lrc, "fallback");

        Assert.Equal("秋日木吉他", song.Title);
        Assert.Equal(2, song.Lines.Count);
        Assert.Equal("晚风吹过旧窗台", song.Lines[0].Text);
        Assert.Equal("木吉他轻轻响起来", song.Lines[1].Text);
    }

    [Fact]
    public void LrcImporter_ThrowsOnEmptyOrCreditsOnly()
    {
        var onlyCredits = """
            [00:00.00]作词：张三
            [00:01.00]作曲：李四
            """;

        Assert.Throws<InvalidDataException>(() => LrcImporter.ImportText(onlyCredits, "empty"));
    }

    [Fact]
    public void PartAnalyzer_FoldAndExpandStackedParts_RoundTripsLyrics()
    {
        var lines = new List<LyricLine>
        {
            new() { PartTitle = "主歌 1", Text = "第一段第一句", Chords = [new ChordPlacement { Position = 0, Name = "C" }] },
            new() { Text = "第一段第二句", Chords = [new ChordPlacement { Position = 0, Name = "G" }] },
            new() { PartTitle = "主歌 2", Text = "第二段第一句" },
            new() { Text = "第二段第二句" }
        };

        PartAnalyzer.FoldPartsAsStacked(lines, [0, 1]);

        Assert.Equal(2, lines.Count);
        Assert.True(lines[0].HasStackedLyrics);
        Assert.Single(lines[0].AltLyrics);
        Assert.Equal("第二段第一句", lines[0].AltLyrics[0]);

        PartAnalyzer.ExpandStackedPart(lines, partIndex: 0, copyChords: true);

        Assert.Equal(4, lines.Count);
        Assert.False(lines[0].HasStackedLyrics);
        Assert.Equal("第二段第一句", lines[2].Text);
        Assert.Equal("C", lines[2].Chords[0].Name);
    }

    [Fact]
    public void AiScoreImporter_ParsesMarkdownWrappedVocalJson()
    {
        var aiResponse = """
            好的，已为你识别出以下弹唱谱 JSON：
            ```json
            {
              "title": "风吹过的夏天",
              "originalKey": "G",
              "selectedKey": "G",
              "capo": "2 品",
              "lines": [
                {
                  "partTitle": "主歌",
                  "text": "风吹过夏天的傍晚",
                  "chords": [
                    { "position": 0, "name": "G" },
                    { "position": 4, "name": "Em" }
                  ]
                }
              ]
            }
            ```
            祝练琴愉快！
            """;

        var result = AiScoreImporter.Parse(aiResponse);

        Assert.Equal("风吹过的夏天", result.Song.Title);
        Assert.Equal("G", result.Song.SelectedKey);
        Assert.Equal(1, result.LineCount);
        Assert.Equal(2, result.ChordCount);
        Assert.Equal("Em", result.Song.Lines[0].Chords[1].Name);
    }

    [Fact]
    public void LibraryStore_SaveLoadBackupAndSnapshots_WorkInIsolatedDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "StringNoteTests_" + Guid.NewGuid().ToString("N"));
        var prevEnv = Environment.GetEnvironmentVariable("XIANPU_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("XIANPU_DATA_DIR", tempDir);
            var store = new LibraryStore();

            var song = new SongModel
            {
                Title = "备份测试曲",
                OriginalKey = "C",
                SelectedKey = "C",
                Bpm = 92,
                Lines = [new LyricLine { Text = "测试歌词行", Chords = [new ChordPlacement { Position = 0, Name = "C" }] }]
            };
            var library = new LibraryData { Songs = [song] };

            store.Save(library);
            Assert.True(File.Exists(store.LibraryPath));
            Assert.Equal(1, LibraryStore.PeekLibrarySongCount(store.LibraryPath));

            var snapshotPath = store.CreateManualSnapshot("unit");
            Assert.NotNull(snapshotPath);
            Assert.True(File.Exists(snapshotPath!));
            Assert.NotEmpty(store.ListRollingBackups());

            var exportPath = Path.Combine(tempDir, "full-export.xianpu-library.json");
            LibraryStore.ExportLibraryBackup(library, exportPath);
            var restored = LibraryStore.LoadLibraryBackup(exportPath);
            Assert.Single(restored.Songs);
            Assert.Equal(92, restored.Songs[0].Bpm);

            // Undo/Redo snapshot ignores UpdatedAt changes
            var snap1 = LibraryStore.SerializeSongSnapshot(song);
            song.UpdatedAt = DateTime.Now.AddMinutes(5);
            var snap2 = LibraryStore.SerializeSongSnapshot(song);
            Assert.Equal(snap1, snap2);

            // NormalizeSong clamps out-of-range BPM
            var badBpm = new SongModel { Bpm = 999 };
            LibraryStore.NormalizeSong(badBpm);
            Assert.Equal(240, badBpm.Bpm);

            var zeroBpm = new SongModel { Bpm = -10 };
            LibraryStore.NormalizeSong(zeroBpm);
            Assert.Equal(0, zeroBpm.Bpm);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XIANPU_DATA_DIR", prevEnv);
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void LibraryStore_QuarantineCorruptedLibrary_PreservesCorruptedFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "StringNoteCorruptTest_" + Guid.NewGuid().ToString("N"));
        var prevEnv = Environment.GetEnvironmentVariable("XIANPU_DATA_DIR");
        try
        {
            Directory.CreateDirectory(tempDir);
            Environment.SetEnvironmentVariable("XIANPU_DATA_DIR", tempDir);
            var store = new LibraryStore();

            const string brokenJson = "{ invalid json from sync conflict";
            File.WriteAllText(store.LibraryPath, brokenJson);

            Assert.Throws<InvalidDataException>(() => store.Load());

            var quarantinedPath = store.QuarantineCorruptedLibrary();
            Assert.NotNull(quarantinedPath);
            Assert.True(File.Exists(quarantinedPath!));
            Assert.Equal(brokenJson, File.ReadAllText(quarantinedPath!));
            Assert.False(File.Exists(store.LibraryPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("XIANPU_DATA_DIR", prevEnv);
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
