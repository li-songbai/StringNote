using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Xianpu.Desktop;

public static partial class LrcImporter
{
    [GeneratedRegex(@"^(?:\[\d{1,2}:\d{2}(?:[.:]\d{1,3})?\])+", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingTimestampsRegex();

    [GeneratedRegex(@"\[(\d{1,2}):(\d{2})(?:[.:](\d{1,3}))?\]", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"<\d{1,2}:\d{2}(?:[.:]\d{1,3})?>", RegexOptions.CultureInvariant)]
    private static partial Regex InlineWordSyncRegex();

    [GeneratedRegex(@"^\[(ti|ar|al|by|re|ve|offset):\s*(.*?)\s*\]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"^(作词|作曲|编曲|制作人?|词|曲|演唱|原唱|混音|母带|录音|吉他|贝斯|鼓|弦乐|和声|统筹|出品|发行|监制|OP|SP|联合出品)\s*[:：]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CreditLineRegex();

    [GeneratedRegex(@"原调\s*[:：=]?\s*([1-7]?\s*[=＝]?\s*[A-Ga-g][#b♯♭]?m?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OriginalKeyRegex();

    [GeneratedRegex(@"选调\s*[:：=]?\s*([1-7]?\s*[=＝]?\s*[A-Ga-g][#b♯♭]?m?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SelectedKeyRegex();

    [GeneratedRegex(@"(?:变调夹|Capo)\s*[:：=]?\s*([0-9一二三四五六七八九十]+\s*品|不夹|免夹|无)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CapoRegex();

    [GeneratedRegex(@"^(?:主歌|副歌|预副歌|导歌|桥段|尾声|尾奏|前奏|引子|间奏|过渡|段落|重复|Part|Verse|Chorus|Pre-?Chorus|Bridge|Outro|Intro|Hook|Refrain|[A-Za-z]段)(?:[\s\-_#]*[0-9A-Za-z一二三四五六七八九十]+)?(?:\s*[（(].*?[)）])?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SectionNameRegex();

    public static string ReadFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.AsSpan().StartsWith(new byte[] { 255, 254 }))
            return Encoding.Unicode.GetString(bytes);
        if (bytes.AsSpan().StartsWith(new byte[] { 254, 255 }))
            return Encoding.BigEndianUnicode.GetString(bytes);
        if (bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 }))
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding("GB18030").GetString(bytes);
        }
    }

    public static SongModel Import(string content, string fileName) =>
        ImportText(content, Path.GetFileNameWithoutExtension(fileName));

    public static SongModel ImportText(string content, string? defaultTitle = null)
    {
        var timedLines = new List<(double Time, int Order, string Text, string? ExplicitPartTitle)>();
        var plainLines = new List<(string Text, string? ExplicitPartTitle, bool BlankBefore)>();
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var originalKey = "";
        var selectedKey = "";
        var capo = "";
        var order = 0;
        string? pendingPartTitle = null;
        var sawBlankLine = false;

        foreach (var rawLine in (content ?? "").TrimStart('\uFEFF').Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
        {
            var trimmed = rawLine.Trim();
            if (trimmed.Length == 0)
            {
                if (plainLines.Count > 0 || timedLines.Count > 0)
                    sawBlankLine = true;
                continue;
            }

            if (TryParseSectionHeader(trimmed, out var headerTitle))
            {
                pendingPartTitle = headerTitle;
                sawBlankLine = false;
                continue;
            }

            var tag = TagRegex().Match(trimmed);
            if (tag.Success)
            {
                metadata[tag.Groups[1].Value] = tag.Groups[2].Value.Trim();
                continue;
            }

            var leadingMatch = LeadingTimestampsRegex().Match(trimmed);
            if (leadingMatch.Success)
            {
                var remainder = trimmed[leadingMatch.Length..];
                var lyric = CleanInlineTags(remainder);
                if (lyric.Length == 0) continue;

                if (TryParseSectionHeader(lyric, out var inlineHeader))
                {
                    pendingPartTitle = inlineHeader;
                    sawBlankLine = false;
                    continue;
                }

                var extractedMeta = TryExtractMusicalMeta(lyric, ref originalKey, ref selectedKey, ref capo);
                if (CreditLineRegex().IsMatch(lyric) || IsPureMusicalMetaLine(lyric, extractedMeta))
                    continue;

                var matches = TimestampRegex().Matches(leadingMatch.Value);
                for (var matchIndex = 0; matchIndex < matches.Count; matchIndex++)
                {
                    var match = matches[matchIndex];
                    var fraction = match.Groups[3].Success ? match.Groups[3].Value : "0";
                    var fractionalSeconds = double.Parse("0." + fraction, CultureInfo.InvariantCulture);
                    var seconds = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 60
                                  + int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
                                  + fractionalSeconds;
                    var partTitleForMatch = matchIndex == 0 ? pendingPartTitle : null;
                    timedLines.Add((seconds, order++, lyric, partTitleForMatch));
                }
                pendingPartTitle = null;
                sawBlankLine = false;
            }
            else
            {
                if (trimmed.StartsWith('[') && trimmed.EndsWith(']')) continue;
                var lyric = CleanInlineTags(trimmed);
                if (lyric.Length == 0) continue;

                var extractedMeta = TryExtractMusicalMeta(lyric, ref originalKey, ref selectedKey, ref capo);
                if (CreditLineRegex().IsMatch(lyric) || IsPureMusicalMetaLine(lyric, extractedMeta))
                    continue;

                plainLines.Add((lyric, pendingPartTitle, sawBlankLine));
                pendingPartTitle = null;
                sawBlankLine = false;
            }
        }

        if (timedLines.Count == 0 && plainLines.Count == 0)
            throw new InvalidDataException("文件中没有可导入的歌词。请选择含歌词的 LRC 或文本文件。");

        var title = metadata.TryGetValue("ti", out var ti) && !string.IsNullOrWhiteSpace(ti)
            ? ti
            : (!string.IsNullOrWhiteSpace(defaultTitle) ? defaultTitle.Trim() : "未命名歌曲");
        var artist = metadata.TryGetValue("ar", out var ar) && !string.IsNullOrWhiteSpace(ar)
            ? ar.Trim()
            : "";

        var song = new SongModel
        {
            Title = title,
            Artist = artist,
            OriginalKey = originalKey,
            SelectedKey = !string.IsNullOrWhiteSpace(selectedKey) ? selectedKey : originalKey,
            Capo = capo,
            Lines = new()
        };

        if (timedLines.Count > 0)
        {
            var sorted = timedLines.OrderBy(x => x.Time).ThenBy(x => x.Order).ToList();
            var boundaryIndices = new HashSet<int>();

            for (var i = 0; i < sorted.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(sorted[i].ExplicitPartTitle))
                    boundaryIndices.Add(i);
            }

            if (sorted.Count >= 6)
            {
                var positiveGaps = new List<double>();
                for (var i = 1; i < sorted.Count; i++)
                {
                    var dt = sorted[i].Time - sorted[i - 1].Time;
                    if (dt > 0.1) positiveGaps.Add(dt);
                }

                if (positiveGaps.Count >= 3)
                {
                    positiveGaps.Sort();
                    var medianDt = positiveGaps[positiveGaps.Count / 2];
                    var gapThreshold = Math.Max(medianDt * 2.2, 8.0);
                    for (var i = 1; i < sorted.Count; i++)
                    {
                        var dt = sorted[i].Time - sorted[i - 1].Time;
                        if (dt >= gapThreshold)
                            boundaryIndices.Add(i);
                    }
                }
            }

            var hasSegmentation = boundaryIndices.Any(idx => idx > 0) || boundaryIndices.Contains(0);
            var partNumber = 0;
            for (var i = 0; i < sorted.Count; i++)
            {
                var explicitTitle = sorted[i].ExplicitPartTitle;
                var isStart = i == 0 ? hasSegmentation : boundaryIndices.Contains(i);
                var partTitle = "";
                if (isStart)
                {
                    partNumber++;
                    partTitle = !string.IsNullOrWhiteSpace(explicitTitle) ? explicitTitle : $"段落 {partNumber}";
                }
                song.Lines.Add(new LyricLine { Text = sorted[i].Text, PartTitle = partTitle });
            }
        }
        else
        {
            var hasSegmentation = plainLines.Where((_, idx) => idx > 0).Any(x => !string.IsNullOrWhiteSpace(x.ExplicitPartTitle) || x.BlankBefore)
                                  || !string.IsNullOrWhiteSpace(plainLines[0].ExplicitPartTitle);
            var partNumber = 0;
            for (var i = 0; i < plainLines.Count; i++)
            {
                var item = plainLines[i];
                var isStart = i == 0
                    ? hasSegmentation
                    : (!string.IsNullOrWhiteSpace(item.ExplicitPartTitle) || item.BlankBefore);
                var partTitle = "";
                if (isStart)
                {
                    partNumber++;
                    partTitle = !string.IsNullOrWhiteSpace(item.ExplicitPartTitle) ? item.ExplicitPartTitle : $"段落 {partNumber}";
                }
                song.Lines.Add(new LyricLine { Text = item.Text, PartTitle = partTitle });
            }
        }

        return song;
    }

    private static bool TryParseSectionHeader(string trimmed, out string partTitle)
    {
        partTitle = "";
        if (string.IsNullOrWhiteSpace(trimmed)) return false;

        string candidate;
        var allowCustomTitle = false;
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']') && trimmed.Length > 2)
        {
            candidate = trimmed[1..^1].Trim();
        }
        else if (trimmed.StartsWith('【') && trimmed.EndsWith('】') && trimmed.Length > 2)
        {
            candidate = trimmed[1..^1].Trim();
        }
        else if (trimmed.StartsWith("---", StringComparison.Ordinal) && trimmed.EndsWith("---", StringComparison.Ordinal) && trimmed.Length > 6)
        {
            candidate = trimmed.Trim('-', ' ', '\t');
            allowCustomTitle = true;
        }
        else if (trimmed.EndsWith(':') || trimmed.EndsWith('：'))
        {
            candidate = trimmed[..^1].Trim();
        }
        else
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        if (!allowCustomTitle && !SectionNameRegex().IsMatch(candidate))
            return false;

        if (allowCustomTitle && candidate.Length > 32)
            return false;

        partTitle = candidate;
        return true;
    }

    private static string CleanInlineTags(string input)
    {
        var withoutTimestamps = TimestampRegex().Replace(input, string.Empty);
        var withoutWordSync = InlineWordSyncRegex().Replace(withoutTimestamps, string.Empty);
        return withoutWordSync.Trim();
    }

    private static bool TryExtractMusicalMeta(string line, ref string originalKey, ref string selectedKey, ref string capo)
    {
        var found = false;
        var origMatch = OriginalKeyRegex().Match(line);
        if (origMatch.Success)
        {
            var parsed = ExtractKeyToken(origMatch.Groups[1].Value);
            if (!string.IsNullOrEmpty(parsed))
            {
                originalKey = parsed;
                found = true;
            }
        }

        var selMatch = SelectedKeyRegex().Match(line);
        if (selMatch.Success)
        {
            var parsed = ExtractKeyToken(selMatch.Groups[1].Value);
            if (!string.IsNullOrEmpty(parsed))
            {
                selectedKey = parsed;
                found = true;
            }
        }

        var capoMatch = CapoRegex().Match(line);
        if (capoMatch.Success)
        {
            capo = capoMatch.Groups[1].Value.Trim();
            found = true;
        }

        return found;
    }

    private static string ExtractKeyToken(string raw)
    {
        var match = Regex.Match(raw.Replace('♯', '#').Replace('♭', 'b'), @"([A-Ga-g][#b]?m?)$");
        if (!match.Success) return "";
        var token = match.Groups[1].Value;
        return char.ToUpperInvariant(token[0]) + token[1..];
    }

    private static bool IsPureMusicalMetaLine(string line, bool extractedMeta)
    {
        if (!extractedMeta) return false;
        var stripped = OriginalKeyRegex().Replace(line, "");
        stripped = SelectedKeyRegex().Replace(stripped, "");
        stripped = CapoRegex().Replace(stripped, "");
        stripped = Regex.Replace(stripped, @"[\s,，;；|｜/·\-—()（）:：]+", "");
        return stripped.Length == 0;
    }
}
