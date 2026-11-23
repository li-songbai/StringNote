using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Xianpu.Desktop;

public sealed record PartBlock(
    int PartIndex,
    int StartLine,
    int LineCount,
    string Title,
    int RepeatCount,
    bool HasStacked,
    bool IsReference,
    int ChordCount,
    int? DuplicateOfPartIndex,
    int? SimilarToPartIndex);

public static class PartAnalyzer
{
    public static List<PartBlock> BuildParts(IList<LyricLine> lines)
    {
        if (lines.Count == 0) return [];

        var starts = new List<int> { 0 };
        for (var i = 1; i < lines.Count; i++)
        {
            if (lines[i].IsReferenceLine ||
                lines[i - 1].IsReferenceLine ||
                !string.IsNullOrWhiteSpace(lines[i].PartTitle))
            {
                starts.Add(i);
            }
        }

        var rawBlocks = new List<(int Start, int Count, string Title, int RepeatCount, bool HasStacked, bool IsReference, int ChordCount, List<string> NormLines)>(starts.Count);
        for (var p = 0; p < starts.Count; p++)
        {
            var start = starts[p];
            var end = p + 1 < starts.Count ? starts[p + 1] : lines.Count;
            var count = end - start;
            var first = lines[start];
            var isRef = first.IsReferenceLine;
            var hasStacked = false;
            var chordCount = 0;
            var normLines = new List<string>(count);

            for (var i = start; i < end; i++)
            {
                var line = lines[i];
                if (line.HasStackedLyrics) hasStacked = true;
                chordCount += line.Chords?.Count ?? 0;
                normLines.Add(NormalizeComparable(line.Text));
            }

            var title = isRef
                ? "反复引用"
                : (!string.IsNullOrWhiteSpace(first.PartTitle)
                    ? first.PartTitle.Trim()
                    : (starts.Count > 1 ? $"段落 {p + 1}" : "全曲"));

            rawBlocks.Add((start, count, title, Math.Max(1, first.RepeatCount), hasStacked, isRef, chordCount, normLines));
        }

        var result = new List<PartBlock>(rawBlocks.Count);
        for (var i = 0; i < rawBlocks.Count; i++)
        {
            var cur = rawBlocks[i];
            int? dupOf = null;
            int? simOf = null;

            if (!cur.IsReference)
            {
                for (var prev = 0; prev < i; prev++)
                {
                    var earlier = rawBlocks[prev];
                    if (earlier.IsReference || earlier.Count != cur.Count) continue;

                    if (AreExactDuplicates(earlier.NormLines, cur.NormLines))
                    {
                        dupOf = prev;
                        break;
                    }
                }

                if (dupOf is null && cur.Count >= 2)
                {
                    for (var prev = 0; prev < i; prev++)
                    {
                        var earlier = rawBlocks[prev];
                        if (earlier.IsReference || earlier.Count != cur.Count) continue;
                        if (AreStackableSimilar(earlier.NormLines, cur.NormLines))
                        {
                            simOf = prev;
                            break;
                        }
                    }
                }
            }

            result.Add(new PartBlock(
                PartIndex: i,
                StartLine: cur.Start,
                LineCount: cur.Count,
                Title: cur.Title,
                RepeatCount: cur.RepeatCount,
                HasStacked: cur.HasStacked,
                IsReference: cur.IsReference,
                ChordCount: cur.ChordCount,
                DuplicateOfPartIndex: dupOf,
                SimilarToPartIndex: simOf));
        }

        return result;
    }

    public static void AutoSegment(IList<LyricLine> lines)
    {
        if (lines.Count < 4) return;

        var norm = lines.Select(l => l.IsReferenceLine ? "" : NormalizeComparable(l.Text)).ToList();
        var boundaries = new HashSet<int> { 0 };
        var suggestedTitles = new Dictionary<int, string>();

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].IsReferenceLine ||
                (i > 0 && lines[i - 1].IsReferenceLine) ||
                !string.IsNullOrWhiteSpace(lines[i].PartTitle))
            {
                boundaries.Add(i);
            }
        }

        // Step A: Find exact repeated blocks (length >= 2, longest match first).
        var matched = new bool[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].IsReferenceLine || string.IsNullOrEmpty(norm[i]))
                matched[i] = true;
        }

        var maxLen = Math.Min(16, lines.Count / 2);
        for (var len = maxLen; len >= 2; len--)
        {
            for (var i = 0; i + len <= lines.Count; i++)
            {
                if (RangeHasFlag(matched, i, len)) continue;
                var occ = new List<int> { i };
                for (var j = i + len; j + len <= lines.Count; j++)
                {
                    if (RangeHasFlag(matched, j, len)) continue;
                    var same = true;
                    for (var k = 0; k < len; k++)
                    {
                        if (!string.Equals(norm[i + k], norm[j + k], StringComparison.Ordinal))
                        {
                            same = false;
                            break;
                        }
                    }
                    if (same)
                    {
                        occ.Add(j);
                        j += len - 1;
                    }
                }

                if (occ.Count >= 2)
                {
                    foreach (var start in occ)
                    {
                        boundaries.Add(start);
                        if (start + len < lines.Count)
                            boundaries.Add(start + len);

                        if (!suggestedTitles.ContainsKey(start))
                            suggestedTitles[start] = "副歌";

                        for (var k = 0; k < len; k++)
                            matched[start + k] = true;
                    }
                }
            }
        }

        // Step B: Split remaining long unsplit spans by stanza line-count period (4, 6, 8, 2).
        var sortedBoundaries = boundaries.OrderBy(x => x).ToList();
        var spanBounds = new List<(int Start, int End)>();
        for (var b = 0; b < sortedBoundaries.Count; b++)
        {
            var start = sortedBoundaries[b];
            var end = b + 1 < sortedBoundaries.Count ? sortedBoundaries[b + 1] : lines.Count;
            if (!lines[start].IsReferenceLine && end - start >= 8)
                spanBounds.Add((start, end));
        }

        int[] candidatePeriods = [4, 6, 8, 2];
        foreach (var (spanStart, spanEnd) in spanBounds)
        {
            var spanLen = spanEnd - spanStart;
            foreach (var period in candidatePeriods)
            {
                var chunkCount = spanLen / period;
                if (chunkCount < 2 || spanLen % period != 0) continue;

                var chunks = new List<List<string>>(chunkCount);
                for (var c = 0; c < chunkCount; c++)
                    chunks.Add(norm.GetRange(spanStart + c * period, period));

                var allSimilar = true;
                for (var c = 1; c < chunks.Count; c++)
                {
                    if (!AreLengthProfilesSimilar(chunks[0], chunks[c], maxAvgDiff: 3.0))
                    {
                        allSimilar = false;
                        break;
                    }
                }

                if (allSimilar)
                {
                    for (var c = 0; c < chunkCount; c++)
                        boundaries.Add(spanStart + c * period);
                    break;
                }
            }
        }

        var finalStarts = boundaries.OrderBy(x => x).ToList();
        if (finalStarts.Count <= 1) return;

        var partCounter = 0;
        foreach (var start in finalStarts)
        {
            if (lines[start].IsReferenceLine) continue;
            partCounter++;
            if (string.IsNullOrWhiteSpace(lines[start].PartTitle))
            {
                lines[start].PartTitle = suggestedTitles.TryGetValue(start, out var title)
                    ? title
                    : $"段落 {partCounter}";
            }
        }
    }

    public static (int PartCount, int DuplicateGroups, int StackableGroups) SummarizeCandidates(SongModel song)
    {
        if (song.Lines.Count == 0) return (0, 0, 0);

        var working = song.Lines.Select(l => l.Clone()).ToList();
        var hasExplicitParts = working.Any(l => !string.IsNullOrWhiteSpace(l.PartTitle) || l.IsReferenceLine);
        if (!hasExplicitParts)
            AutoSegment(working);

        var parts = BuildParts(working);
        var dupCount = parts.Count(p => p.DuplicateOfPartIndex is not null);
        var simCount = parts.Count(p => p.SimilarToPartIndex is not null);
        return (parts.Count, dupCount, simCount);
    }

    public static int GetHalfCellUnits(string textElement)
    {
        if (string.IsNullOrEmpty(textElement)) return 0;
        var rune = Rune.GetRuneAt(textElement, 0);
        return rune.Value > 0x024F ? 2 : 1;
    }

    public static int MeasureHalfCells(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var total = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
            total += GetHalfCellUnits(enumerator.GetTextElement());
        return total;
    }

    public static string AlignAltLyricToPrimary(string primary, string alt)
    {
        var safeAlt = (alt ?? "").Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(primary) || string.IsNullOrWhiteSpace(safeAlt))
            return safeAlt.TrimStart();

        var primarySegs = Regex.Matches(primary, @"\S+");
        var altSegs = Regex.Matches(safeAlt, @"\S+");
        if (primarySegs.Count >= 2 && primarySegs.Count == altSegs.Count)
        {
            var sb = new StringBuilder();
            var currentCells = 0;
            for (var i = 0; i < primarySegs.Count; i++)
            {
                var targetCells = MeasureHalfCells(primary[..primarySegs[i].Index]);
                var needed = targetCells - currentCells;
                if (i > 0 && needed < 1)
                    needed = 1;
                if (needed > 0)
                {
                    sb.Append(' ', needed);
                    currentCells += needed;
                }
                var segText = altSegs[i].Value;
                sb.Append(segText);
                currentCells += MeasureHalfCells(segText);
            }
            return sb.ToString();
        }

        return safeAlt.TrimStart();
    }

    public static void FoldPartsAsStacked(IList<LyricLine> lines, IReadOnlyList<int> selectedPartIndices)
    {
        var parts = BuildParts(lines);
        var ordered = selectedPartIndices
            .Where(i => i >= 0 && i < parts.Count && !parts[i].IsReference)
            .Distinct()
            .OrderBy(i => i)
            .ToList();

        if (ordered.Count < 2) return;

        var targetPart = parts[ordered[0]];
        var targetLastLine = lines[targetPart.StartLine + targetPart.LineCount - 1];
        var linesToRemove = new HashSet<LyricLine>(ReferenceEqualityComparer.Instance);
        var extraLines = new List<LyricLine>();

        for (var pIdx = 1; pIdx < ordered.Count; pIdx++)
        {
            var srcPart = parts[ordered[pIdx]];
            var common = Math.Min(targetPart.LineCount, srcPart.LineCount);
            for (var row = 0; row < common; row++)
            {
                var targetLine = lines[targetPart.StartLine + row];
                var srcLine = lines[srcPart.StartLine + row];
                targetLine.AltLyrics ??= [];
                targetLine.AltLyrics.Add(AlignAltLyricToPrimary(targetLine.Text, srcLine.Text));
                if (srcLine.AltLyrics is { Count: > 0 })
                {
                    foreach (var nested in srcLine.AltLyrics)
                        targetLine.AltLyrics.Add(AlignAltLyricToPrimary(targetLine.Text, nested));
                }
            }

            for (var row = targetPart.LineCount; row < srcPart.LineCount; row++)
            {
                var extra = lines[srcPart.StartLine + row].Clone();
                extra.PartTitle = "";
                extra.RepeatCount = 1;
                extraLines.Add(extra);
            }

            for (var row = 0; row < srcPart.LineCount; row++)
                linesToRemove.Add(lines[srcPart.StartLine + row]);
        }

        var targetFirstLine = lines[targetPart.StartLine];
        if (string.IsNullOrWhiteSpace(targetFirstLine.PartTitle))
            targetFirstLine.PartTitle = "主歌";

        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (linesToRemove.Contains(lines[i]))
                lines.RemoveAt(i);
        }

        if (extraLines.Count > 0)
        {
            var insertAfterIdx = -1;
            for (var i = 0; i < lines.Count; i++)
            {
                if (ReferenceEquals(lines[i], targetLastLine))
                {
                    insertAfterIdx = i;
                    break;
                }
            }
            var insertAt = insertAfterIdx >= 0 ? insertAfterIdx + 1 : lines.Count;
            for (var i = 0; i < extraLines.Count; i++)
                lines.Insert(insertAt + i, extraLines[i]);
        }
    }

    public static void ExpandStackedPart(IList<LyricLine> lines, int partIndex, bool copyChords)
    {
        var parts = BuildParts(lines);
        if (partIndex < 0 || partIndex >= parts.Count) return;

        var part = parts[partIndex];
        var sourceLines = new List<LyricLine>(part.LineCount);
        var maxAltCount = 0;
        for (var i = 0; i < part.LineCount; i++)
        {
            var line = lines[part.StartLine + i];
            sourceLines.Add(line);
            maxAltCount = Math.Max(maxAltCount, line.AltLyrics?.Count ?? 0);
        }

        if (maxAltCount == 0) return;

        var baseTitle = string.IsNullOrWhiteSpace(part.Title) ? "段落" : part.Title.Trim();
        var expandedBlocks = new List<LyricLine>();

        for (var verse = 1; verse <= maxAltCount; verse++)
        {
            var firstInVerse = true;
            foreach (var srcLine in sourceLines)
            {
                if (srcLine.AltLyrics is null || verse - 1 >= srcLine.AltLyrics.Count)
                    continue;

                var cleanedText = Regex.Replace(srcLine.AltLyrics[verse - 1].Trim(), @" {2,}", " ");
                if (string.IsNullOrWhiteSpace(cleanedText))
                    continue;

                var expandedLine = new LyricLine
                {
                    Text = cleanedText,
                    PartTitle = firstInVerse ? $"{baseTitle} · 第{verse + 1}遍" : "",
                    RepeatCount = 1
                };
                if (copyChords)
                    CopyLineChordsProportionally(srcLine, expandedLine);

                expandedBlocks.Add(expandedLine);
                firstInVerse = false;
            }
        }

        foreach (var srcLine in sourceLines)
            srcLine.AltLyrics.Clear();

        var insertAt = part.StartLine + part.LineCount;
        for (var i = 0; i < expandedBlocks.Count; i++)
            lines.Insert(insertAt + i, expandedBlocks[i]);
    }

    public static void FoldDuplicateAsReference(IList<LyricLine> lines, int duplicatePartIndex, int sourcePartIndex)
    {
        var parts = BuildParts(lines);
        if (duplicatePartIndex < 0 || duplicatePartIndex >= parts.Count) return;
        if (sourcePartIndex < 0 || sourcePartIndex >= parts.Count || duplicatePartIndex == sourcePartIndex) return;

        var dupPart = parts[duplicatePartIndex];
        var srcPart = parts[sourcePartIndex];
        var srcTitle = string.IsNullOrWhiteSpace(srcPart.Title) ? $"段落 {sourcePartIndex + 1}" : srcPart.Title;

        var refLine = new LyricLine
        {
            Text = $"→ 反复 [{srcTitle}]",
            PartTitle = "",
            RepeatCount = 1,
            IsReferenceLine = true
        };

        for (var i = dupPart.LineCount - 1; i >= 0; i--)
            lines.RemoveAt(dupPart.StartLine + i);

        lines.Insert(dupPart.StartLine, refLine);
    }

    public static void CopyPartChords(IList<LyricLine> lines, int sourcePartIndex, IReadOnlyList<int> targetPartIndices)
    {
        var parts = BuildParts(lines);
        if (sourcePartIndex < 0 || sourcePartIndex >= parts.Count) return;
        var srcPart = parts[sourcePartIndex];
        if (srcPart.IsReference) return;

        foreach (var targetIdx in targetPartIndices.Distinct())
        {
            if (targetIdx == sourcePartIndex || targetIdx < 0 || targetIdx >= parts.Count) continue;
            var tgtPart = parts[targetIdx];
            if (tgtPart.IsReference) continue;

            var count = Math.Min(srcPart.LineCount, tgtPart.LineCount);
            for (var i = 0; i < count; i++)
                CopyLineChordsProportionally(lines[srcPart.StartLine + i], lines[tgtPart.StartLine + i]);
        }
    }

    public static void CopyLineChordsProportionally(LyricLine source, LyricLine target)
    {
        target.Chords ??= [];
        target.Chords.Clear();
        if (source.Chords is null || source.Chords.Count == 0) return;

        var srcPositions = GetElementPositions(source.Text);
        var tgtPositions = GetElementPositions(target.Text);
        var srcMaxIdx = Math.Max(1, srcPositions.Count);
        var tgtMaxIdx = Math.Max(0, tgtPositions.Count - 1);

        foreach (var chord in source.Chords)
        {
            var srcElemIdx = FindClosestElementIndex(srcPositions, chord.Position);
            int tgtPos;
            if (string.IsNullOrEmpty(target.Text))
            {
                tgtPos = 0;
            }
            else if (chord.Position >= source.Text.Length)
            {
                tgtPos = target.Text.Length;
            }
            else
            {
                var mappedElemIdx = Math.Clamp((int)Math.Round(srcElemIdx * (double)tgtPositions.Count / srcMaxIdx), 0, tgtMaxIdx);
                tgtPos = tgtPositions[mappedElemIdx];
            }

            target.Chords.RemoveAll(c => c.Position == tgtPos && c.BeforeChar == chord.BeforeChar);
            target.Chords.Add(new ChordPlacement
            {
                Position = tgtPos,
                BeforeChar = chord.BeforeChar,
                Name = chord.Name,
                ShapeId = chord.ShapeId
            });
        }

        target.Chords = target.Chords.OrderBy(x => x.Position).ThenBy(x => x.BeforeChar ? 0 : 1).ToList();
    }

    public static void InsertNewPartAfterLine(
        IList<LyricLine> lines,
        int afterLineIndex,
        string partTitle,
        IReadOnlyList<string> lyricTexts)
    {
        var cleaned = lyricTexts
            .Select(x => (x ?? "").Trim())
            .Where(x => x.Length > 0)
            .ToList();
        if (cleaned.Count == 0) return;

        var parts = BuildParts(lines);
        var insertAt = lines.Count == 0 ? 0 : Math.Clamp(afterLineIndex + 1, 0, lines.Count);
        EnsureTailPartSeparated(lines, parts, insertAt);

        var title = string.IsNullOrWhiteSpace(partTitle) ? $"段落 {parts.Count + 1}" : partTitle.Trim();
        for (var i = 0; i < cleaned.Count; i++)
        {
            lines.Insert(insertAt + i, new LyricLine
            {
                Text = cleaned[i],
                PartTitle = i == 0 ? title : "",
                RepeatCount = 1
            });
        }
    }

    public static void InsertClonedPartAfterLine(
        IList<LyricLine> lines,
        int afterLineIndex,
        int sourcePartIndex,
        bool includeChords,
        bool asReferenceLine,
        string? customTitle = null)
    {
        var parts = BuildParts(lines);
        if (sourcePartIndex < 0 || sourcePartIndex >= parts.Count) return;
        var srcPart = parts[sourcePartIndex];
        if (srcPart.IsReference) return;

        var insertAt = lines.Count == 0 ? 0 : Math.Clamp(afterLineIndex + 1, 0, lines.Count);
        EnsureTailPartSeparated(lines, parts, insertAt);

        if (asReferenceLine)
        {
            var refTitle = string.IsNullOrWhiteSpace(srcPart.Title) ? $"段落 {sourcePartIndex + 1}" : srcPart.Title;
            lines.Insert(insertAt, new LyricLine
            {
                Text = $"→ 反复 [{refTitle}]",
                PartTitle = "",
                RepeatCount = 1,
                IsReferenceLine = true
            });
            return;
        }

        var clones = new List<LyricLine>(srcPart.LineCount);
        for (var i = 0; i < srcPart.LineCount; i++)
        {
            var copy = lines[srcPart.StartLine + i].Clone();
            if (!includeChords)
                copy.Chords.Clear();
            clones.Add(copy);
        }
        if (clones.Count == 0) return;

        var resolvedTitle = !string.IsNullOrWhiteSpace(customTitle)
            ? customTitle.Trim()
            : (string.IsNullOrWhiteSpace(srcPart.Title) || srcPart.Title == "全曲"
                ? $"段落 {parts.Count + 1}"
                : srcPart.Title);
        clones[0].PartTitle = resolvedTitle;
        clones[0].RepeatCount = Math.Max(1, srcPart.RepeatCount);

        for (var i = 0; i < clones.Count; i++)
            lines.Insert(insertAt + i, clones[i]);
    }

    public static void DuplicatePart(IList<LyricLine> lines, int partIndex, bool includeChords = true)
    {
        var parts = BuildParts(lines);
        if (partIndex < 0 || partIndex >= parts.Count) return;
        var part = parts[partIndex];
        if (part.IsReference) return;
        InsertClonedPartAfterLine(lines, part.StartLine + part.LineCount - 1, partIndex, includeChords, asReferenceLine: false);
    }

    public static void MovePart(IList<LyricLine> lines, int partIndex, int delta)
    {
        if (delta is not (-1 or 1)) return;
        var parts = BuildParts(lines);
        var targetIndex = partIndex + delta;
        if (partIndex < 0 || partIndex >= parts.Count || targetIndex < 0 || targetIndex >= parts.Count)
            return;

        var firstIdx = Math.Min(partIndex, targetIndex);
        var secondIdx = Math.Max(partIndex, targetIndex);
        var partA = parts[firstIdx];
        var partB = parts[secondIdx];

        if (!partA.IsReference && string.IsNullOrWhiteSpace(lines[partA.StartLine].PartTitle))
            lines[partA.StartLine].PartTitle = partA.Title == "全曲" ? "段落 1" : partA.Title;
        if (!partB.IsReference && string.IsNullOrWhiteSpace(lines[partB.StartLine].PartTitle))
            lines[partB.StartLine].PartTitle = partB.Title == "全曲" ? "段落 2" : partB.Title;

        var blockA = new List<LyricLine>(partA.LineCount);
        for (var i = 0; i < partA.LineCount; i++)
            blockA.Add(lines[partA.StartLine + i]);

        var blockB = new List<LyricLine>(partB.LineCount);
        for (var i = 0; i < partB.LineCount; i++)
            blockB.Add(lines[partB.StartLine + i]);

        var totalRemove = partA.LineCount + partB.LineCount;
        for (var i = 0; i < totalRemove; i++)
            lines.RemoveAt(partA.StartLine);

        var insertPos = partA.StartLine;
        foreach (var line in blockB)
            lines.Insert(insertPos++, line);
        foreach (var line in blockA)
            lines.Insert(insertPos++, line);
    }

    private static void EnsureTailPartSeparated(IList<LyricLine> lines, IReadOnlyList<PartBlock> parts, int insertAt)
    {
        if (insertAt <= 0 || insertAt >= lines.Count) return;
        var tailLine = lines[insertAt];
        if (tailLine.IsReferenceLine || lines[insertAt - 1].IsReferenceLine) return;
        if (!string.IsNullOrWhiteSpace(tailLine.PartTitle)) return;

        var enclosing = parts.FirstOrDefault(p => insertAt >= p.StartLine && insertAt < p.StartLine + p.LineCount);
        var baseName = enclosing is null || string.IsNullOrWhiteSpace(enclosing.Title) || enclosing.Title == "全曲"
            ? "段落"
            : enclosing.Title;
        tailLine.PartTitle = $"{baseName} (续)";
    }

    public static void RemapTabSections(SongModel song, IReadOnlyList<LyricLine> oldLines, IReadOnlyList<LyricLine> newLines)
    {
        if (song.TabSections is null || song.TabSections.Count == 0) return;

        var newIndexMap = new Dictionary<LyricLine, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < newLines.Count; i++)
            newIndexMap[newLines[i]] = i;

        foreach (var section in song.TabSections)
        {
            if (section.Kind != TabSectionKind.Interlude) continue;
            var oldIdx = section.InsertAfterLineIndex;
            if (oldIdx < 0) continue;

            if (oldLines.Count == 0 || newLines.Count == 0)
            {
                section.InsertAfterLineIndex = -1;
                continue;
            }

            var mapped = -1;
            for (var k = Math.Min(oldIdx, oldLines.Count - 1); k >= 0; k--)
            {
                if (newIndexMap.TryGetValue(oldLines[k], out var newIdx))
                {
                    mapped = newIdx;
                    break;
                }
            }

            section.InsertAfterLineIndex = mapped >= 0
                ? mapped
                : Math.Clamp(oldIdx, 0, newLines.Count - 1);
        }
    }

    private static bool AreExactDuplicates(List<string> a, List<string> b)
    {
        if (a.Count != b.Count || a.Count == 0) return false;
        if (a.Count == 1 && a[0].Length < 4) return false;

        var hasContent = false;
        for (var i = 0; i < a.Count; i++)
        {
            if (!string.IsNullOrEmpty(a[i])) hasContent = true;
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                return false;
        }
        return hasContent;
    }

    private static bool AreStackableSimilar(List<string> a, List<string> b) =>
        AreLengthProfilesSimilar(a, b, maxAvgDiff: 3.5, allowRatioFallback: true);

    private static bool AreLengthProfilesSimilar(List<string> a, List<string> b, double maxAvgDiff, bool allowRatioFallback = false)
    {
        if (a.Count != b.Count || a.Count == 0) return false;

        var diffSum = 0;
        var maxDiff = 0;
        var sumA = 0;
        var sumB = 0;
        for (var i = 0; i < a.Count; i++)
        {
            var la = a[i].Length;
            var lb = b[i].Length;
            var diff = Math.Abs(la - lb);
            diffSum += diff;
            if (diff > maxDiff) maxDiff = diff;
            sumA += la;
            sumB += lb;
        }

        if (sumA == 0 || sumB == 0) return false;
        var avgDiff = diffSum / (double)a.Count;
        if (avgDiff <= maxAvgDiff) return true;

        if (allowRatioFallback)
        {
            var ratio = sumB / (double)sumA;
            return ratio is >= 0.65 and <= 1.55 && maxDiff <= 8;
        }

        return false;
    }

    private static bool RangeHasFlag(bool[] flags, int start, int len)
    {
        for (var i = 0; i < len; i++)
            if (flags[start + i]) return true;
        return false;
    }

    private static string NormalizeComparable(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch))
                continue;
            sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    private static List<int> GetElementPositions(string text)
    {
        var result = new List<int>();
        if (string.IsNullOrEmpty(text))
        {
            result.Add(0);
            return result;
        }
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
            result.Add(enumerator.ElementIndex);
        if (result.Count == 0) result.Add(0);
        return result;
    }

    private static int FindClosestElementIndex(List<int> positions, int charPos)
    {
        var idx = 0;
        for (var i = 0; i < positions.Count; i++)
        {
            if (positions[i] <= charPos) idx = i;
            else break;
        }
        return idx;
    }
}
