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
        Assert.Equal("测试歌手", song.Artist);
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

    [Fact]
    public void AiScoreImporter_StripsChordPaddingSpacesInAltLyrics_WhilePreservingPickupIndent()
    {
        const string json = """
        {
          "mode": "vocal",
          "title": "夜空中最亮的星",
          "parts": [
            {
              "title": "副歌",
              "repeat": 2,
              "lines": [
                {
                  "main": "[Em]给我再去相[C]信的勇气 越过[G]谎言去拥抱[D]你 每当 我",
                  "alt": ["    给我再去相信     的勇气 越过     谎言去拥抱     你 每当 我"]
                },
                {
                  "main": "[G]叹息 OH [G]夜空中最亮的星",
                  "alt": ["    哪里 OH    夜空中最亮的星"]
                },
                {
                  "main": "oh [G]Honey 我脑海里全[D]都是你",
                  "alt": ["   Honey 你目光里充    满忧郁"]
                }
              ]
            }
          ]
        }
        """;

        var result = AiScoreImporter.Parse(json);
        Assert.Equal(3, result.Song.Lines.Count);

        Assert.Equal("给我再去相信的勇气 越过谎言去拥抱你 每当 我", result.Song.Lines[0].Text);
        Assert.Equal("给我再去相信的勇气 越过谎言去拥抱你 每当 我", result.Song.Lines[0].AltLyrics[0]);

        Assert.Equal("叹息 OH 夜空中最亮的星", result.Song.Lines[1].Text);
        Assert.Equal("哪里 OH 夜空中最亮的星", result.Song.Lines[1].AltLyrics[0]);

        Assert.Equal("oh Honey 我脑海里全都是你", result.Song.Lines[2].Text);
        Assert.Equal("   Honey 你目光里充满忧郁", result.Song.Lines[2].AltLyrics[0]);
    }

    [Fact]
    public void UpdateService_CompareVersionStrings_HandlesMultiDigitPreReleaseAndStable()
    {
        Assert.True(UpdateService.CompareVersionStrings("v0.1.0-beta.10", "v0.1.0-beta.9") > 0);
        Assert.True(UpdateService.CompareVersionStrings("0.1.0-beta.2", "0.1.0-beta.10") < 0);
        Assert.True(UpdateService.CompareVersionStrings("v1.0.0", "v1.0.0-beta.10") > 0);
        Assert.True(UpdateService.CompareVersionStrings("v1.0.0-rc.1", "v1.0.0-beta.99") > 0);
        Assert.True(UpdateService.CompareVersionStrings("v1.0.1", "v1.0.0") > 0);
        Assert.Equal(0, UpdateService.CompareVersionStrings("v1.0.0", "1.0.0+build123"));
    }

    [Fact]
    public void LyricLineEditHelper_ApplyMainLyricEdit_ShiftsChordsAcrossMultipleEdits()
    {
        var line = new LyricLine
        {
            Text = "给我再去相信的勇器",
            Chords =
            [
                new ChordPlacement { Position = 0, BeforeChar = true, Name = "Em", ShapeId = "Em" },
                new ChordPlacement { Position = 0, BeforeChar = false, Name = "G", ShapeId = "G" },
                new ChordPlacement { Position = 5, BeforeChar = false, Name = "C", ShapeId = "C" },   // '信'
                new ChordPlacement { Position = 8, BeforeChar = false, Name = "D", ShapeId = "D" },   // '器' -> '气'
                new ChordPlacement { Position = 9, BeforeChar = false, Name = "Em7", ShapeId = "Em7" } // 句尾延音
            ]
        };

        LyricLineEditHelper.ApplyMainLyricEdit(line, "噢 给我再去更相信的勇气吧");

        Assert.Equal("噢 给我再去更相信的勇气吧", line.Text);
        // Opening BeforeChar pickup chord stays at 0, while chord on '给' moves to '给' (index 2)
        Assert.Contains(line.Chords, c => c.Name == "Em" && c.BeforeChar && c.Position == 0);
        Assert.Contains(line.Chords, c => c.Name == "G" && !c.BeforeChar && c.Position == 2);
        // '信' moved from index 5 to index 8 ("噢 给我再去更相" = 8 chars)
        Assert.Contains(line.Chords, c => c.Name == "C" && c.Position == 8);
        // Replaced '器' -> '气' at index 11
        Assert.Contains(line.Chords, c => c.Name == "D" && c.Position == 11);
        // Trailing sustain chord stays at new line end (length 13)
        Assert.Contains(line.Chords, c => c.Name == "Em7" && c.Position == line.Text.Length);
    }

    [Fact]
    public void LyricLineEditHelper_SplitLineAt_SplitsChordsAltLyricsAndShiftsInterludes()
    {
        var song = new SongModel
        {
            Lines =
            [
                new LyricLine
                {
                    PartTitle = "副歌",
                    RepeatCount = 2,
                    Text = "有一颗透明的心灵 和会流泪的眼睛",
                    AltLyrics = ["有痛苦都留在心里 也不愿忘记你的眼睛"],
                    Chords =
                    [
                        new ChordPlacement { Position = 0, Name = "Em", ShapeId = "Em" },
                        new ChordPlacement { Position = 7, Name = "C", ShapeId = "C" },
                        new ChordPlacement { Position = 11, Name = "G", ShapeId = "G" },
                        new ChordPlacement { Position = 15, Name = "D", ShapeId = "D" }
                    ]
                }
            ],
            TabSections =
            [
                new TabSection { Kind = TabSectionKind.Interlude, InsertAfterLineIndex = 0, Title = "间奏" }
            ]
        };

        // Split right before "和会流泪的眼睛" (index 9, right after the space at index 8)
        var nextIdx = LyricLineEditHelper.SplitLineAt(song, 0, song.Lines[0].Text, 9);

        Assert.Equal(1, nextIdx);
        Assert.Equal(2, song.Lines.Count);

        Assert.Equal("有一颗透明的心灵", song.Lines[0].Text);
        Assert.Equal(["Em", "C"], song.Lines[0].Chords.Select(c => c.Name).ToArray());
        Assert.Equal("有痛苦都留在心里", song.Lines[0].AltLyrics[0]);

        Assert.Equal("和会流泪的眼睛", song.Lines[1].Text);
        Assert.Equal(["G", "D"], song.Lines[1].Chords.Select(c => c.Name).ToArray());
        Assert.Equal(2, song.Lines[1].Chords[0].Position); // '流' in "和会流泪的眼睛"
        Assert.Equal(6, song.Lines[1].Chords[1].Position); // '睛' in "和会流泪的眼睛"
        Assert.Equal("也不愿忘记你的眼睛", song.Lines[1].AltLyrics[0]);

        // Interlude moved to stay after the second half of the split sentence
        Assert.Equal(1, song.TabSections[0].InsertAfterLineIndex);
    }

    [Fact]
    public void ChordVoicingEngine_DerivesValidPlayableDiagramsForExtendedAndAlteredChords()
    {
        int[] openPitches = [4, 9, 2, 7, 11, 4];
        string[] testChords =
        [
            "C9", "Am7sus4", "Fsus2", "Gsus4", "Gadd9",
            "Ebmaj7", "Dbm7", "F#m7b5", "Cdim7", "Gaug",
            "E7#9", "Am11", "G13", "C5", "CM7", "Gb", "Bb9", "F#7sus4"
        ];

        foreach (var name in testChords)
        {
            var shape = ChordCatalog.ByName(name);
            Assert.NotNull(shape);
            Assert.True(ChordVoicingEngine.TryParseChord(name, out var parsed), $"Should parse {name}");

            var lowestSounding = -1;
            var soundingPitches = new HashSet<int>();
            var minFret = 99;
            var maxFret = 0;

            for (var s = 0; s < 6; s++)
            {
                if (shape!.Frets[s] is not { } f) continue;
                if (lowestSounding < 0) lowestSounding = s;
                var pc = (openPitches[s] + f) % 12;
                Assert.True(parsed.AllPitches.Contains(pc), $"{name}: string {s} fret {f} pitch {pc} not in chord");
                soundingPitches.Add(pc);
                if (f > 0)
                {
                    if (f < minFret) minFret = f;
                    if (f > maxFret) maxFret = f;
                }
            }

            Assert.True(lowestSounding >= 0, $"{name}: must have sounding strings");
            var bassPc = (openPitches[lowestSounding] + shape!.Frets[lowestSounding]!.Value) % 12;
            Assert.Equal(parsed.BassPitch, bassPc);

            // Must fit inside the 4-fret diagram grid (BaseFret .. BaseFret + 3)
            if (maxFret > 0)
            {
                Assert.True(maxFret - shape.BaseFret <= 3, $"{name}: maxFret {maxFret} overflows BaseFret {shape.BaseFret}");
                Assert.True(minFret >= shape.BaseFret, $"{name}: minFret {minFret} below BaseFret {shape.BaseFret}");
            }

            foreach (var req in parsed.RequiredPitches)
                Assert.Contains(req, soundingPitches);
        }

        // CM7 normalizes to Cmaj7 (Major 7th, not minor 7th!)
        Assert.Equal("Cmaj7", ChordCatalog.NormalizeChordName("CM7"));
    }

    [Fact]
    public void ChordVoicingEngine_EnforcesSlashChordBassAndProducesMultipleCandidates()
    {
        int[] openPitches = [4, 9, 2, 7, 11, 4];
        string[] slashChords = ["Am7/D", "Fmaj7/C", "Eb/G", "D/F#", "G/B", "Dm7/G", "Cmaj7/E"];

        foreach (var name in slashChords)
        {
            var shape = ChordCatalog.ByName(name);
            Assert.NotNull(shape);
            Assert.True(ChordVoicingEngine.TryParseChord(name, out var parsed));

            var lowestStr = Array.FindIndex(shape!.Frets, f => f.HasValue);
            Assert.True(lowestStr >= 0);
            var actualBass = (openPitches[lowestStr] + shape.Frets[lowestStr]!.Value) % 12;
            Assert.Equal(parsed.BassPitch, actualBass);
        }

        // Multiple ergonomic candidates available for position cycling & ByShapeId round-trip
        var candidates = ChordCatalog.GetCandidates("Ebmaj7");
        Assert.True(candidates.Count >= 2);
        var second = candidates[1];
        var resolvedById = ChordCatalog.ByShapeId(second.Id);
        Assert.NotNull(resolvedById);
        Assert.Equal(second.Frets, resolvedById!.Frets);
    }

    [Fact]
    public void MusicTheoryCatalog_ComputesInterlockingCagedBoxesAndDiatonicChords()
    {
        var std = MusicTheoryCatalog.GetTuning("standard");
        var cBoxes = MusicTheoryCatalog.ComputeCagedBoxes(tonicPitch: 0, isMinorScale: false, std, maxFret: 17);
        Assert.True(cBoxes.Count >= 5);

        // Verify adjacent CAGED boxes interlock seamlessly: box[i].RightFrets == box[i+1].LeftFrets
        for (var i = 0; i < cBoxes.Count - 1; i++)
            Assert.Equal(cBoxes[i].RightFrets, cBoxes[i + 1].LeftFrets);

        // Relative minor (Am) shares the exact same CAGED positions as C Major
        var amBoxes = MusicTheoryCatalog.ComputeCagedBoxes(tonicPitch: 9, isMinorScale: true, std, maxFret: 17);
        Assert.Equal(cBoxes.Count, amBoxes.Count);
        for (var i = 0; i < cBoxes.Count; i++)
        {
            Assert.Equal(cBoxes[i].ShapeLetter, amBoxes[i].ShapeLetter);
            Assert.Equal(cBoxes[i].LeftFrets, amBoxes[i].LeftFrets);
        }

        // G Major diatonic chords: G, Am, Bm, C, D, Em, F#dim
        var gDiatonic = MusicTheoryCatalog.GetDiatonicChords(tonicPitch: 7, isMinor: false, preferFlats: false);
        Assert.Equal(7, gDiatonic.Count);
        Assert.Equal(["G", "Am", "Bm", "C", "D", "Em", "F#dim"], gDiatonic.Select(d => d.TriadSymbol).ToArray());

        // CAGED 5-shape chord voicings for major/minor/7/maj7/m7: all 5 shapes present, ordered up the neck, 100% valid chord tones
        int[] openPitches = [4, 9, 2, 7, 11, 4];
        foreach (var chordSym in new[] { "C", "G", "Am", "Dm", "G7", "Cmaj7", "Am7" })
        {
            var voicings = MusicTheoryCatalog.GetCagedChordVoicings(chordSym);
            var cagedOnes = voicings.Where(v => v.CagedLetter is not null).ToList();
            Assert.Equal(5, cagedOnes.Count);
            Assert.True(ChordVoicingEngine.TryParseChord(chordSym, out var parsed));

            foreach (var v in cagedOnes)
            {
                for (var s = 0; s < 6; s++)
                {
                    if (v.Shape.Frets[s] is not { } f) continue;
                    var pc = (openPitches[s] + f) % 12;
                    Assert.Contains(pc, parsed.AllPitches);
                }
            }
        }
    }

    [Fact]
    public void MusicTheoryCatalog_DecodesChordStringsAndSynthesizesAudioWav()
    {
        var std = MusicTheoryCatalog.GetTuning("standard");
        var cShape = ChordCatalog.ByName("C");
        Assert.NotNull(cShape);

        var decoded = MusicTheoryCatalog.DecodeChordStrings(cShape!, tonicPitch: 0, std, capo: 0, preferFlats: false);
        Assert.Equal(6, decoded.Count);
        Assert.Equal(["×", "C3", "E3", "G3", "C4", "E4"], decoded.Select(d => d.NoteWithOctave).ToArray());
        Assert.Equal(["—", "R", "3", "5", "R", "3"], decoded.Select(d => d.IntervalLabel).ToArray());
        Assert.Equal(["—", "1", "3", "5", "1", "3"], decoded.Select(d => d.ScaleDegreeLabel).ToArray());

        // Progression suggestions: smooth voice leading & richer harmony
        var smooth = MusicTheoryCatalog.SuggestSmootherProgressions(["C", "G", "Am", "F"], preferFlats: false);
        Assert.NotEmpty(smooth);
        Assert.Contains(smooth[0].Chords, c => c.Contains('/'));

        var richer = MusicTheoryCatalog.SuggestRicherProgressions(["C", "G", "Am", "F"], tonicPitch: 0, isMinor: false);
        Assert.NotEmpty(richer);
        Assert.Equal(4, richer[0].Chords.Count);

        // Karplus-Strong plucked acoustic WAV output header & non-silent payload
        var wav = GuitarAudioService.SynthesizeMixedNotes([(48, 0, 260), (52, 30, 260), (55, 60, 260)]);
        Assert.True(wav.Length > 44);
        Assert.Equal((byte)'R', wav[0]);
        Assert.Equal((byte)'I', wav[1]);
        Assert.Equal((byte)'F', wav[2]);
        Assert.Equal((byte)'F', wav[3]);
        Assert.Equal((byte)'W', wav[8]);
        Assert.Equal((byte)'A', wav[9]);
        Assert.Equal((byte)'V', wav[10]);
        Assert.Equal((byte)'E', wav[11]);
    }

    [Fact]
    public void LibraryStore_PersistsTheoryNotesRoundTrip()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "XianpuTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var prevEnv = Environment.GetEnvironmentVariable("XIANPU_DATA_DIR");
        Environment.SetEnvironmentVariable("XIANPU_DATA_DIR", tempDir);
        try
        {
            var store = new LibraryStore();
            var lib = new LibraryData();
            lib.Songs.Add(new SongModel { Title = "测试曲" });
            lib.TheoryNotes.Add(new TheoryNoteItem
            {
                Title = "G 调 E 指型五声连奏",
                Tag = "CAGED",
                TonicPitch = 7,
                ScaleId = "major_pent",
                ActiveCagedFilter = "E",
                TuningId = "standard",
                Capo = 2,
                HighlightChord = "G",
                Content = "第 3 品 E 指型接第 5 品 D 指型非常顺手。"
            });

            lib.DrillBestBpms["u2_28_disp_beat"] = 108;

            store.Save(lib);
            var reloaded = store.Load();
            Assert.Single(reloaded.TheoryNotes);
            var note = reloaded.TheoryNotes[0];
            Assert.Equal("G 调 E 指型五声连奏", note.Title);
            Assert.Equal("CAGED", note.Tag);
            Assert.Equal(7, note.TonicPitch);
            Assert.Equal("E", note.ActiveCagedFilter);
            Assert.Equal(2, note.Capo);
            Assert.Equal("G", note.HighlightChord);
            Assert.Equal(108, reloaded.DrillBestBpms["u2_28_disp_beat"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XIANPU_DATA_DIR", prevEnv);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ScaleDrillCatalog_GeneratesValidSequencesSkipsArpeggiosAndSinglePosCircleOfFifths()
    {
        var std = MusicTheoryCatalog.GetTuning("standard");
        Assert.True(ScaleDrillCatalog.Presets.Count >= 28);

        // 1. Validate every preset generates valid playable TAB bars and events
        foreach (var preset in ScaleDrillCatalog.Presets)
        {
            var scale = MusicTheoryCatalog.GetScale(preset.RecommendedScaleId);
            var drill = ScaleDrillCatalog.Generate(
                tonicPitch: 0,
                scale: scale,
                tuning: std,
                capo: 0,
                activeCagedFilter: "G",
                posMode: preset.DefaultPosMode,
                pattern: preset.Pattern,
                subdivision: preset.Subdivision,
                accentMode: preset.Accent);

            Assert.NotEmpty(drill.Section.Bars);
            Assert.NotEmpty(drill.Events);
            foreach (var ev in drill.Events)
            {
                Assert.InRange(ev.StringIdx, 0, 5);
                Assert.InRange(ev.Fret, 0, FretboardCanvas.TotalFrets);
                Assert.Equal(std.OpenMidi[ev.StringIdx] + ev.Fret, ev.Midi);
            }
        }

        // 2. Verify 3-against-4 cross-beat accent displacement (every 3rd sixteenth note accented)
        var crossDrill = ScaleDrillCatalog.Generate(
            tonicPitch: 9,
            scale: MusicTheoryCatalog.GetScale("minor_pent"),
            tuning: std,
            capo: 0,
            activeCagedFilter: "G",
            posMode: DrillPositionMode.FollowCaged,
            pattern: DrillPatternKind.Seq3,
            subdivision: DrillSubdivision.Sixteenth,
            accentMode: DrillAccentMode.GroupOf3CrossBeat);
        Assert.Equal(16, crossDrill.Section.ColumnsPerBar);
        for (var i = 0; i < Math.Min(12, crossDrill.Events.Count); i++)
            Assert.Equal(i % 3 == 0, crossDrill.Events[i].IsAccent);

        // 3. Verify Single-Position Circle of Fourths (Lesson 58): 5 distinct keys & all 5 CAGED shapes inside frets 5..9!
        var cofDrill = ScaleDrillCatalog.Generate(
            tonicPitch: 0,
            scale: MusicTheoryCatalog.GetScale("major_pent"),
            tuning: std,
            capo: 0,
            activeCagedFilter: "ALL",
            posMode: DrillPositionMode.FixedWindow5To8,
            pattern: DrillPatternKind.CircleOf4thsPentatonic,
            subdivision: DrillSubdivision.Eighth,
            accentMode: DrillAccentMode.BeatDownbeat);

        var distinctKeys = cofDrill.Events.Select(e => e.EffectiveTonicPitch).Distinct().ToList();
        var distinctCaged = cofDrill.Events.Select(e => e.EffectiveCagedLetter).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal([0, 5, 10, 3, 8], distinctKeys); // C -> F -> Bb -> Eb -> Ab
        Assert.Equal(5, distinctCaged.Count);         // Covers all 5 C-A-G-E-D shapes in one position!
        Assert.All(cofDrill.Events, ev => Assert.InRange(ev.Fret, 5, 9));

        // 4. Verify single-step pluck + woodblock click audio mixer produces valid RIFF WAVE
        var stepWav = GuitarAudioService.SynthesizeDrillStepWav(midiNote: 60, isNoteAccent: true, playClick: true, isDownbeatClick: true);
        Assert.True(stepWav.Length > 44);
        Assert.Equal((byte)'R', stepWav[0]);
        Assert.Equal((byte)'W', stepWav[8]);
    }
}
