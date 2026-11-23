using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Xianpu.Desktop;

internal sealed record AiImportResult(
    SongModel Song,
    string DetectedKind,
    int LineCount,
    int ChordCount,
    int TabSectionCount,
    int TabBarCount);

internal static class AiScoreImporter
{
    private static readonly JsonSerializerOptions NativeJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Regex FencedBlockRegex = new(
        @"```(?:json)?\s*\r?\n([\s\S]*?)\r?\n?```",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InlineChordRegex = new(
        @"\[(\^?)([^\[\]\r\n]+)\]",
        RegexOptions.Compiled);

    private static readonly Regex GraceFretRegex = new(
        @"^(\d{1,2})\s*(s|/|\\|h|p)\s*(\d{1,2}|[xX])$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public const string VocalPrompt = """
请仔细识别我上传的吉他【弹唱谱】图片，并仅输出一个合法的 JSON 代码块（```json ... ```），不要额外解释，以便我直接导入到《弦笺》软件中。

请严格使用以下结构与格式：
```json
{
  "mode": "vocal",
  "title": "歌名 - 歌手",
  "originalKey": "B",
  "selectedKey": "G",
  "capo": "4品",
  "parts": [
    {
      "title": "主歌",
      "repeat": 1,
      "lines": [
        {
          "main": "oh [G]Honey 我脑海里全[D]都是你 [Em]",
          "alt": ["   Honey 你目光里充满忧郁"]
        }
      ]
    }
  ],
  "tabs": []
}
```

关键识别规则：
1. 和弦必须直接以内联标记写在对应的歌词字/词前面，不要输出字符下标：
   - `[C]爱` 表示和弦落在这个字上方；
   - `[^Em]我` 表示和弦进在当前字之前（空拍进唱）；
   - `都是你 [Em]` 表示和弦落在该句结尾延音拍。
2. 如遇多段歌词上下对齐（主歌第1遍/第2遍叠行），第一遍写 `main`，其余各遍按顺序写进 `alt` 数组，可保留少量前置空格以维持对齐。
3. 标题、原调（如 `1=B` -> `"B"`）、选调指法（如 `用G调指法` -> `"G"`）和变调夹（如 `"4品"`）请如实填写；无需输出六线谱扫弦伴奏小节，`tabs` 留空即可（只有明显属于纯器乐前奏/间奏的独奏六线谱小节时才可放入 `tabs`）。
4. 如果图片有多页长谱，每次可只识别我发的这几页，我会在软件里逐页合并。
""";

    public const string FingerstylePrompt = """
请仔细识别我上传的吉他【指弹 / 独奏六线谱】图片，并仅输出一个合法的 JSON 代码块（```json ... ```），不要额外解释，以便我直接导入到《弦笺》软件中。

请严格使用以下紧凑 JSON 结构（只记录有音符/记号的拍位，节省长度避免截断）：
```json
{
  "mode": "fingerstyle",
  "title": "曲名",
  "capo": "4品",
  "sections": [
    {
      "title": "A段 (1-4小节)",
      "time": "4/4",
      "cols": 8,
      "bars": [
        {
          "repeatStart": false,
          "repeatEnd": false,
          "chords": [[0, "C"]],
          "notes": [
            [0, 5, "3"],
            [1, 3, "2"],
            [2, 2, "0", "nh"],
            [3, 1, "5s7", "b0.5r"]
          ],
          "metas": [
            { "col": 0, "dur": "8", "dot": false, "rest": false, "strum": "arpeggio", "pm": false }
          ]
        }
      ]
    }
  ]
}
```

关键识别规则：
1. 【弦号方向绝不能反】：`notes` 每项为 `[col, str, fret, tech?, link?]`：
   - `col` 为当前小节内从 `0` 开始的拍位列号（`0 .. cols-1`，通常每小节 8 或 16 格，按最细时值对齐）；
   - `str` 为弦号 `1`~`6`：**`1` = 最上面最细的 1弦(e)，`6` = 最下面最粗的 6弦(E)**！
   - `fret` 为品格数字（或 `"x"` 闷音）；不占拍子的前置倚音直接写成 `"5s7"` / `"5h7"` / `"7p5"`。
2. 可选第 4 项 `tech` 技巧码（无技巧可省略）：
   - `"b"` 全音推弦，`"b0.5"` 半音推弦，`"br"` 全音推弦回放，`"b0.5r"` 半音推弦回放；
   - `"nh"` 自然泛音，`"ah"` 人工泛音，`"~"` 揉弦，`"()"` 轻音括号，`"/"` 无起点滑入，`"\\"` 无终点滑出。
3. 可选第 5 项 `link`（连到同弦下一音）：`"h"` 击弦、`"p"` 勾弦、`"s"` 滑音、`"tie"` 延音线。
4. `metas`（仅当该拍需要指定时值/扫弦/闷音/休止时填写）：`dur` 可填 `"1"`/`"2"`/`"4"`/`"8"`/`"16"`，`strum` 可填 `"down"`/`"up"`/`"mute"`/`"arpeggio"`，`pm: true` 表示右手闷音 P.M.。
5. 如果整首曲谱有很多页，请先完整识别当前页的所有小节，我会在软件内点「追加到当前曲谱」逐页拼接。
""";

    public static AiImportResult Parse(string rawText)
    {
        var json = ExtractJsonPayload(rawText);
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON 格式有误（第 {ex.LineNumber + 1} 行附近）：可能是 AI 输出被截断或缺少括号/逗号。", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("根节点必须是 JSON 对象 { ... }。");

            SongModel song;
            string kindLabel;

            if (HasPropertyCI(root, "mode") || HasPropertyCI(root, "parts") || HasPropertyCI(root, "sections") || HasPropertyCI(root, "tabs"))
            {
                (song, kindLabel) = ParseCompactSong(root);
            }
            else if (HasPropertyCI(root, "lines") || HasPropertyCI(root, "tabSections"))
            {
                try
                {
                    song = JsonSerializer.Deserialize<SongModel>(json, NativeJsonOptions)
                           ?? throw new InvalidDataException("曲谱 JSON 内容为空或格式不正确。");
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException($"原生曲谱 JSON 解析失败（第 {ex.LineNumber + 1} 行附近）：{ex.Message}", ex);
                }
                song.Id = Guid.NewGuid().ToString("N");
                LibraryStore.NormalizeSong(song);
                kindLabel = song.Lines.Count > 0 ? "原生弦谱工程" : "原生指弹工程";
            }
            else if (HasPropertyCI(root, "bars"))
            {
                TabSection section;
                try
                {
                    section = JsonSerializer.Deserialize<TabSection>(json, NativeJsonOptions)
                              ?? throw new InvalidDataException("六线谱片段 JSON 内容为空或格式不正确。");
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException($"六线谱片段 JSON 解析失败（第 {ex.LineNumber + 1} 行附近）：{ex.Message}", ex);
                }
                section.Id = Guid.NewGuid().ToString("N");
                song = new SongModel
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = string.IsNullOrWhiteSpace(section.Title) ? "导入指弹片段" : section.Title.Trim(),
                    OriginalKey = "C",
                    SelectedKey = "C",
                    ShowTabSections = true,
                    TabSections = [section]
                };
                LibraryStore.NormalizeSong(song);
                kindLabel = "六线谱片段";
            }
            else
            {
                throw new InvalidDataException("未识别的曲谱结构，请使用本窗口提供的弹唱谱/指弹谱 Prompt 生成 JSON，或粘贴 .xianpu.json 内容。");
            }

            if (song.Lines.Count == 0 && song.TabSections.Count == 0)
                throw new InvalidDataException("未从 JSON 中解析出任何歌词行或六线谱小节，请检查 AI 返回内容是否完整。");

            var chordCount = song.Lines.Sum(l => l.Chords?.Count ?? 0);
            var barCount = song.TabSections.Sum(t => t.Bars?.Count ?? 0);
            return new AiImportResult(song, kindLabel, song.Lines.Count, chordCount, song.TabSections.Count, barCount);
        }
    }

    public static void MergeIntoSong(SongModel target, SongModel incoming)
    {
        var isTargetBlank = target.Lines.Count == 0 && target.TabSections.Count == 0;
        if (isTargetBlank)
        {
            if (string.IsNullOrWhiteSpace(target.Title) || target.Title == "未命名歌曲")
                target.Title = incoming.Title;
            if (!string.IsNullOrWhiteSpace(incoming.OriginalKey))
                target.OriginalKey = incoming.OriginalKey;
            if (!string.IsNullOrWhiteSpace(incoming.SelectedKey))
                target.SelectedKey = incoming.SelectedKey;
            if (string.IsNullOrWhiteSpace(target.Capo) && !string.IsNullOrWhiteSpace(incoming.Capo))
                target.Capo = incoming.Capo;
        }

        var lineOffset = target.Lines.Count;
        foreach (var line in incoming.Lines)
            target.Lines.Add(line.Clone());

        foreach (var sec in incoming.TabSections)
        {
            var cloned = sec.Clone();
            if (target.Lines.Count == 0)
            {
                cloned.Kind = TabSectionKind.Intro;
                cloned.InsertAfterLineIndex = -1;
            }
            else if (cloned.InsertAfterLineIndex >= 0)
            {
                cloned.InsertAfterLineIndex += lineOffset;
            }
            target.TabSections.Add(cloned);
        }

        target.ShowTabSections = target.TabSections.Count > 0;
        LibraryStore.NormalizeSong(target);
    }

    private static string ExtractJsonPayload(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidDataException("请先粘贴 AI 返回的 JSON 内容。");

        var text = raw.Trim().TrimStart('\uFEFF');
        string? bestCandidate = null;
        foreach (Match match in FencedBlockRegex.Matches(text))
        {
            var candidate = match.Groups[1].Value.Trim();
            if (candidate.StartsWith('{') && candidate.EndsWith('}') &&
                (bestCandidate is null || candidate.Length > bestCandidate.Length))
            {
                bestCandidate = candidate;
            }
        }

        if (bestCandidate is not null)
            return bestCandidate;

        var firstBrace = text.IndexOf('{');
        var lastBrace = text.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return text[firstBrace..(lastBrace + 1)];

        throw new InvalidDataException("未识别到有效的 JSON 内容，请确认已完整复制包含 { ... } 的 AI 回复。");
    }

    private static (SongModel Song, string KindLabel) ParseCompactSong(JsonElement root)
    {
        var mode = GetStringCI(root, "mode") ?? "";
        var title = GetStringCI(root, "title") ?? "未命名歌曲";
        var originalKey = GetStringCI(root, "originalKey") ?? "C";
        var selectedKey = GetStringCI(root, "selectedKey");
        if (string.IsNullOrWhiteSpace(selectedKey))
            selectedKey = originalKey;
        var capo = GetStringCI(root, "capo") ?? "";

        var song = new SongModel
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = string.IsNullOrWhiteSpace(title) ? "未命名歌曲" : title.Trim(),
            OriginalKey = string.IsNullOrWhiteSpace(originalKey) ? "C" : originalKey.Trim(),
            SelectedKey = string.IsNullOrWhiteSpace(selectedKey) ? "C" : selectedKey.Trim(),
            Capo = capo.Trim(),
            ColumnMode = 0,
            ChordDisplayMode = "name"
        };

        if (TryGetPropCI(root, "parts", out var partsEl) && partsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var partEl in partsEl.EnumerateArray())
            {
                if (partEl.ValueKind != JsonValueKind.Object) continue;
                var partTitle = GetStringCI(partEl, "title") ?? "";
                var repeat = GetIntCI(partEl, "repeat") ?? 1;
                if (!TryGetPropCI(partEl, "lines", out var partLinesEl) || partLinesEl.ValueKind != JsonValueKind.Array)
                    continue;

                var firstInPart = true;
                foreach (var lineEl in partLinesEl.EnumerateArray())
                {
                    var parsedLine = ParseCompactLyricLine(lineEl);
                    if (parsedLine is null) continue;
                    if (firstInPart)
                    {
                        parsedLine.PartTitle = partTitle.Trim();
                        parsedLine.RepeatCount = Math.Clamp(repeat, 1, 16);
                        firstInPart = false;
                    }
                    song.Lines.Add(parsedLine);
                }
            }
        }
        else if (TryGetPropCI(root, "lines", out var linesEl) && linesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var lineEl in linesEl.EnumerateArray())
            {
                var parsedLine = ParseCompactLyricLine(lineEl);
                if (parsedLine is not null)
                    song.Lines.Add(parsedLine);
            }
        }

        var isFingerstyle = mode.Equals("fingerstyle", StringComparison.OrdinalIgnoreCase) ||
                            (song.Lines.Count == 0 && TryGetPropCI(root, "sections", out _));

        if (TryGetPropCI(root, "sections", out var sectionsEl) && sectionsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var secEl in sectionsEl.EnumerateArray())
            {
                var tabSec = ParseCompactTabSection(secEl, song.Lines.Count, isFingerstyle);
                if (tabSec is not null)
                    song.TabSections.Add(tabSec);
            }
        }

        if (TryGetPropCI(root, "tabs", out var tabsEl) && tabsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var secEl in tabsEl.EnumerateArray())
            {
                var tabSec = ParseCompactTabSection(secEl, song.Lines.Count, isFingerstyle);
                if (tabSec is not null)
                    song.TabSections.Add(tabSec);
            }
        }

        song.ShowTabSections = song.TabSections.Count > 0;
        LibraryStore.NormalizeSong(song);

        var kindLabel = song.Lines.Count > 0 && song.TabSections.Count > 0
            ? "弹唱谱（含六线谱）"
            : isFingerstyle || song.Lines.Count == 0
                ? "指弹谱"
                : "弹唱谱";

        return (song, kindLabel);
    }

    private static LyricLine? ParseCompactLyricLine(JsonElement lineEl)
    {
        if (lineEl.ValueKind == JsonValueKind.String)
        {
            var raw = lineEl.GetString() ?? "";
            ParseInlineChordLine(raw, out var text, out var chords);
            return new LyricLine { Text = text, Chords = chords };
        }

        if (lineEl.ValueKind != JsonValueKind.Object)
            return null;

        var rawMain = GetStringCI(lineEl, "main")
                      ?? GetStringCI(lineEl, "text")
                      ?? GetStringCI(lineEl, "lyric")
                      ?? "";
        ParseInlineChordLine(rawMain, out var mainText, out var mainChords);

        if (mainChords.Count == 0 && TryGetPropCI(lineEl, "chords", out var chordsEl) && chordsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var chEl in chordsEl.EnumerateArray())
            {
                if (chEl.ValueKind != JsonValueKind.Object) continue;
                var chName = GetStringCI(chEl, "name");
                var pos = GetIntCI(chEl, "position") ?? GetIntCI(chEl, "pos") ?? 0;
                var before = GetBoolCI(chEl, "beforeChar") ?? GetBoolCI(chEl, "before") ?? false;
                if (string.IsNullOrWhiteSpace(chName)) continue;
                var normalized = ChordCatalog.NormalizeChordName(chName);
                mainChords.Add(new ChordPlacement
                {
                    Position = Math.Clamp(pos, 0, mainText.Length),
                    BeforeChar = before,
                    Name = normalized,
                    ShapeId = ChordCatalog.ByName(normalized)?.Id ?? ""
                });
            }
        }

        var altLyrics = new List<string>();
        if (TryGetPropCI(lineEl, "alt", out var altEl) || TryGetPropCI(lineEl, "altLyrics", out altEl))
        {
            if (altEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in altEl.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String) continue;
                    ParseInlineChordLine(item.GetString() ?? "", out var cleanAlt, out _);
                    altLyrics.Add(cleanAlt);
                }
            }
            else if (altEl.ValueKind == JsonValueKind.String)
            {
                ParseInlineChordLine(altEl.GetString() ?? "", out var cleanAlt, out _);
                altLyrics.Add(cleanAlt);
            }
        }

        var partTitle = GetStringCI(lineEl, "partTitle") ?? "";
        var repeat = GetIntCI(lineEl, "repeat") ?? 1;

        return new LyricLine
        {
            Text = mainText,
            Chords = mainChords,
            AltLyrics = altLyrics,
            PartTitle = partTitle.Trim(),
            RepeatCount = Math.Clamp(repeat, 1, 16)
        };
    }

    private static void ParseInlineChordLine(string rawLine, out string plainText, out List<ChordPlacement> chords)
    {
        chords = [];
        if (string.IsNullOrEmpty(rawLine))
        {
            plainText = "";
            return;
        }

        var normalizedInput = rawLine.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);
        var sb = new StringBuilder(normalizedInput.Length);
        var cursor = 0;

        foreach (Match match in InlineChordRegex.Matches(normalizedInput))
        {
            if (match.Index > cursor)
                sb.Append(normalizedInput, cursor, match.Index - cursor);

            var hasCaret = match.Groups[1].Value == "^";
            var rawChord = match.Groups[2].Value.Trim();
            if (!string.IsNullOrEmpty(rawChord) && IsChordTokenCandidate(rawChord))
            {
                var chordName = ChordCatalog.NormalizeChordName(rawChord);
                if (!string.IsNullOrWhiteSpace(chordName))
                {
                    chords.Add(new ChordPlacement
                    {
                        Position = sb.Length,
                        BeforeChar = hasCaret,
                        Name = chordName,
                        ShapeId = ChordCatalog.ByName(chordName)?.Id ?? ""
                    });
                    cursor = match.Index + match.Length;
                    continue;
                }
            }

            // Literal non-chord bracketed text stays untouched
            sb.Append(match.Value);
            cursor = match.Index + match.Length;
        }

        if (cursor < normalizedInput.Length)
            sb.Append(normalizedInput, cursor, normalizedInput.Length - cursor);

        // Trim only trailing whitespace so leading spaces on stacked alt lines remain aligned
        plainText = sb.ToString().TrimEnd();
        foreach (var chord in chords)
            chord.Position = Math.Clamp(chord.Position, 0, plainText.Length);

        chords = chords
            .OrderBy(c => c.Position)
            .ThenBy(c => c.BeforeChar ? 0 : 1)
            .ToList();
    }

    private static bool IsChordTokenCandidate(string token) =>
        token.Length > 0 && token[0] is >= 'A' and <= 'G' or >= 'a' and <= 'g';

    private static TabSection? ParseCompactTabSection(JsonElement secEl, int songLineCount, bool isFingerstyle)
    {
        if (secEl.ValueKind != JsonValueKind.Object) return null;

        var kindRaw = GetStringCI(secEl, "kind") ?? "";
        var insertAfter = GetIntCI(secEl, "insertAfterLine") ?? GetIntCI(secEl, "afterLine");
        var kind = kindRaw.Trim().ToLowerInvariant() switch
        {
            "interlude" or "间奏" => TabSectionKind.Interlude,
            "outro" or "尾奏" => TabSectionKind.Outro,
            "intro" or "前奏" => TabSectionKind.Intro,
            _ => songLineCount > 0 && insertAfter is >= 0 ? TabSectionKind.Interlude : TabSectionKind.Intro
        };

        var defaultTitle = isFingerstyle
            ? "指弹段"
            : kind switch
            {
                TabSectionKind.Interlude => "间奏",
                TabSectionKind.Outro => "尾奏",
                _ => "前奏"
            };

        var title = GetStringCI(secEl, "title");
        var timeSig = GetStringCI(secEl, "time") ?? GetStringCI(secEl, "timeSignature") ?? "4/4";
        var cols = Math.Clamp(GetIntCI(secEl, "cols") ?? GetIntCI(secEl, "columnsPerBar") ?? 8, 4, 16);
        var showRhythm = GetBoolCI(secEl, "showRhythm") ?? GetBoolCI(secEl, "showRhythmStems") ?? true;

        var bars = new List<TabBar>();
        if (TryGetPropCI(secEl, "bars", out var barsEl) && barsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var barEl in barsEl.EnumerateArray())
            {
                var bar = ParseCompactTabBar(barEl, cols);
                if (bar is not null)
                    bars.Add(bar);
            }
        }

        if (bars.Count == 0)
            return null;

        return new TabSection
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = kind,
            Title = string.IsNullOrWhiteSpace(title) ? defaultTitle : title.Trim(),
            InsertAfterLineIndex = kind == TabSectionKind.Intro
                ? -1
                : kind == TabSectionKind.Outro
                    ? Math.Max(-1, songLineCount - 1)
                    : (insertAfter ?? 0),
            TimeSignature = string.IsNullOrWhiteSpace(timeSig) ? "4/4" : timeSig.Trim(),
            ColumnsPerBar = cols,
            ShowRhythmStems = showRhythm,
            Bars = bars
        };
    }

    private static TabBar? ParseCompactTabBar(JsonElement barEl, int defaultCols)
    {
        if (barEl.ValueKind != JsonValueKind.Object) return null;

        var barCols = Math.Clamp(GetIntCI(barEl, "cols") ?? GetIntCI(barEl, "columnCount") ?? defaultCols, 1, 16);
        var bar = new TabBar
        {
            ColumnCount = barCols,
            RepeatStart = GetBoolCI(barEl, "repeatStart") ?? false,
            RepeatEnd = GetBoolCI(barEl, "repeatEnd") ?? false
        };

        if (TryGetPropCI(barEl, "chords", out var chordsEl) && chordsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var chEl in chordsEl.EnumerateArray())
            {
                if (chEl.ValueKind == JsonValueKind.Array && chEl.GetArrayLength() >= 2)
                {
                    var col = ReadJsonInt(chEl[0]) ?? 0;
                    var text = ReadJsonString(chEl[1]);
                    if (!string.IsNullOrWhiteSpace(text))
                        bar.ChordMarks.Add(new TabChordMark { Column = Math.Clamp(col, 0, barCols - 1), Text = text.Trim() });
                }
                else if (chEl.ValueKind == JsonValueKind.Object)
                {
                    var col = GetIntCI(chEl, "col") ?? GetIntCI(chEl, "column") ?? 0;
                    var text = GetStringCI(chEl, "text") ?? GetStringCI(chEl, "name");
                    if (!string.IsNullOrWhiteSpace(text))
                        bar.ChordMarks.Add(new TabChordMark { Column = Math.Clamp(col, 0, barCols - 1), Text = text.Trim() });
                }
            }
        }

        if (TryGetPropCI(barEl, "notes", out var notesEl) && notesEl.ValueKind == JsonValueKind.Array)
        {
            var rawNotes = new List<(int Col, int RawStr, string RawFret, string? Tech, string? Link, string? Grace, string? GraceLink)>();
            foreach (var nEl in notesEl.EnumerateArray())
            {
                if (nEl.ValueKind == JsonValueKind.Array && nEl.GetArrayLength() >= 3)
                {
                    var col = ReadJsonInt(nEl[0]);
                    var rawStr = ReadJsonInt(nEl[1]);
                    var rawFret = ReadJsonString(nEl[2]);
                    if (col is null || rawStr is null || string.IsNullOrWhiteSpace(rawFret)) continue;
                    var tech = nEl.GetArrayLength() >= 4 ? ReadJsonString(nEl[3]) : null;
                    var link = nEl.GetArrayLength() >= 5 ? ReadJsonString(nEl[4]) : null;
                    rawNotes.Add((col.Value, rawStr.Value, rawFret.Trim(), tech, link, null, null));
                }
                else if (nEl.ValueKind == JsonValueKind.Object)
                {
                    var col = GetIntCI(nEl, "col") ?? GetIntCI(nEl, "column");
                    var rawStr = GetIntCI(nEl, "str") ?? GetIntCI(nEl, "string") ?? GetIntCI(nEl, "stringIndex");
                    var rawFret = GetStringCI(nEl, "fret");
                    if (col is null || rawStr is null || string.IsNullOrWhiteSpace(rawFret)) continue;
                    var tech = GetStringCI(nEl, "tech") ?? GetStringCI(nEl, "technique");
                    var link = GetStringCI(nEl, "link") ?? GetStringCI(nEl, "linkToNext");
                    var grace = GetStringCI(nEl, "grace") ?? GetStringCI(nEl, "graceFret");
                    var graceLink = GetStringCI(nEl, "graceLink");
                    rawNotes.Add((col.Value, rawStr.Value, rawFret.Trim(), tech, link, grace, graceLink));
                }
            }

            if (rawNotes.Count > 0)
            {
                var useZeroBased = rawNotes.Any(r => r.RawStr == 0) && rawNotes.All(r => r.RawStr is >= 0 and <= 5);
                foreach (var item in rawNotes)
                {
                    var strIdx = Math.Clamp(useZeroBased ? item.RawStr : item.RawStr - 1, 0, 5);
                    var note = new TabNote
                    {
                        Column = Math.Clamp(item.Col, 0, barCols - 1),
                        StringIndex = strIdx
                    };
                    ParseCompactNoteFret(item.RawFret, note);

                    if (!string.IsNullOrWhiteSpace(item.Grace))
                    {
                        note.GraceFret = item.Grace.Trim();
                        note.GraceLink = ParseLinkToken(item.GraceLink) is var gl && gl != TabLinkKind.None
                            ? gl
                            : TabLinkKind.Slide;
                    }

                    var parsedTech = ParseTechniqueToken(item.Tech);
                    if (parsedTech != TabNoteTechnique.None)
                        note.Technique = parsedTech;

                    note.LinkToNext = ParseLinkToken(item.Link);
                    if (!string.IsNullOrWhiteSpace(note.Fret))
                        bar.Notes.Add(note);
                }
            }
        }

        if (TryGetPropCI(barEl, "metas", out var metasEl) && metasEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var mEl in metasEl.EnumerateArray())
            {
                if (mEl.ValueKind != JsonValueKind.Object) continue;
                var col = GetIntCI(mEl, "col") ?? GetIntCI(mEl, "column");
                if (col is null) continue;
                var meta = new TabColumnMeta
                {
                    Column = Math.Clamp(col.Value, 0, barCols - 1),
                    Duration = ParseDurationToken(GetStringCI(mEl, "dur") ?? GetStringCI(mEl, "duration")),
                    Dotted = GetBoolCI(mEl, "dot") ?? GetBoolCI(mEl, "dotted") ?? false,
                    IsRest = GetBoolCI(mEl, "rest") ?? GetBoolCI(mEl, "isRest") ?? false,
                    Strum = ParseStrumToken(GetStringCI(mEl, "strum")),
                    PalmMute = GetBoolCI(mEl, "pm") ?? GetBoolCI(mEl, "palmMute") ?? false
                };
                if (meta.Duration != TabDuration.Auto || meta.Dotted || meta.IsRest || meta.Strum != TabStrumKind.None || meta.PalmMute)
                    bar.ColumnMetas.Add(meta);
            }
        }

        return bar;
    }

    private static void ParseCompactNoteFret(string rawFret, TabNote note)
    {
        var tok = rawFret.Trim();
        var graceMatch = GraceFretRegex.Match(tok);
        if (graceMatch.Success)
        {
            note.GraceFret = graceMatch.Groups[1].Value;
            note.GraceLink = graceMatch.Groups[2].Value.ToLowerInvariant() switch
            {
                "h" => TabLinkKind.HammerOn,
                "p" => TabLinkKind.PullOff,
                _ => TabLinkKind.Slide
            };
            note.Fret = graceMatch.Groups[3].Value.ToLowerInvariant();
            return;
        }

        if (tok.StartsWith('(') && tok.EndsWith(')') && tok.Length >= 3)
        {
            note.Fret = tok[1..^1].Trim();
            note.Technique = TabNoteTechnique.GhostNote;
            return;
        }
        if (tok.StartsWith('<') && tok.EndsWith('>') && tok.Length >= 3)
        {
            note.Fret = tok[1..^1].Trim();
            note.Technique = TabNoteTechnique.NaturalHarmonic;
            return;
        }
        if (tok.StartsWith('/') && tok.Length >= 2)
        {
            note.Fret = tok[1..].Trim();
            note.Technique = TabNoteTechnique.SlideIn;
            return;
        }
        if (tok.EndsWith('\\') && tok.Length >= 2)
        {
            note.Fret = tok[..^1].Trim();
            note.Technique = TabNoteTechnique.SlideOut;
            return;
        }

        note.Fret = tok;
    }

    private static TabNoteTechnique ParseTechniqueToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return TabNoteTechnique.None;
        return raw.Trim().ToLowerInvariant() switch
        {
            "b" or "bend" or "full" or "bendfull" => TabNoteTechnique.BendFull,
            "b0.5" or "half" or "1/2" or "½" or "bendhalf" => TabNoteTechnique.BendHalf,
            "br" or "bendrelease" or "release" => TabNoteTechnique.BendRelease,
            "b0.5r" or "halfrelease" or "bendhalfrelease" => TabNoteTechnique.BendHalfRelease,
            "nh" or "naturalharmonic" => TabNoteTechnique.NaturalHarmonic,
            "ah" or "artificialharmonic" => TabNoteTechnique.ArtificialHarmonic,
            "~" or "v" or "vib" or "vibrato" => TabNoteTechnique.Vibrato,
            "()" or "g" or "ghost" or "ghostnote" => TabNoteTechnique.GhostNote,
            "/" or "slidein" => TabNoteTechnique.SlideIn,
            "\\" or "slideout" => TabNoteTechnique.SlideOut,
            _ => TabNoteTechnique.None
        };
    }

    private static TabLinkKind ParseLinkToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return TabLinkKind.None;
        return raw.Trim().ToLowerInvariant() switch
        {
            "h" or "hammer" or "hammeron" => TabLinkKind.HammerOn,
            "p" or "pull" or "pulloff" => TabLinkKind.PullOff,
            "s" or "/" or "\\" or "slide" => TabLinkKind.Slide,
            "tie" or "t" or "~" => TabLinkKind.Tie,
            _ => TabLinkKind.None
        };
    }

    private static TabDuration ParseDurationToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return TabDuration.Auto;
        return raw.Trim().ToLowerInvariant() switch
        {
            "1" or "w" or "whole" => TabDuration.Whole,
            "2" or "h" or "half" => TabDuration.Half,
            "4" or "q" or "quarter" => TabDuration.Quarter,
            "8" or "e" or "eighth" => TabDuration.Eighth,
            "16" or "s" or "sixteenth" => TabDuration.Sixteenth,
            _ => TabDuration.Auto
        };
    }

    private static TabStrumKind ParseStrumToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return TabStrumKind.None;
        return raw.Trim().ToLowerInvariant() switch
        {
            "down" or "d" => TabStrumKind.Down,
            "up" or "u" => TabStrumKind.Up,
            "mute" or "x" or "m" => TabStrumKind.Mute,
            "arpeggio" or "arp" => TabStrumKind.Arpeggio,
            _ => TabStrumKind.None
        };
    }

    private static bool HasPropertyCI(JsonElement obj, string name) => TryGetPropCI(obj, name, out _);

    private static bool TryGetPropCI(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.ValueKind == JsonValueKind.Object)
        {
            if (obj.TryGetProperty(name, out value))
                return true;
            foreach (var prop in obj.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static string? GetStringCI(JsonElement obj, string name) =>
        TryGetPropCI(obj, name, out var el) ? ReadJsonString(el) : null;

    private static int? GetIntCI(JsonElement obj, string name) =>
        TryGetPropCI(obj, name, out var el) ? ReadJsonInt(el) : null;

    private static bool? GetBoolCI(JsonElement obj, string name)
    {
        if (!TryGetPropCI(obj, name, out var el)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(el.GetString(), out var b) => b,
            _ => null
        };
    }

    private static string? ReadJsonString(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };

    private static int? ReadJsonInt(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var val))
            return val;
        if (el.ValueKind == JsonValueKind.String &&
            int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }
}
