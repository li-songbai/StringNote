using System.Globalization;

namespace Xianpu.Desktop;

internal static class LyricLineEditHelper
{
    public static void ApplyMainLyricEdit(LyricLine line, string rawNewText)
    {
        var newText = SanitizeSingleLine(rawNewText);
        var oldText = line.Text ?? "";
        if (string.Equals(oldText, newText, StringComparison.Ordinal))
            return;

        if (line.Chords.Count > 0)
            RemapChordsForEditedText(oldText, newText, line.Chords);

        line.Text = newText;
    }

    public static void RemapChordsForEditedText(string oldText, string newText, List<ChordPlacement> chords)
    {
        if (chords.Count == 0)
            return;

        if (newText.Length == 0)
        {
            foreach (var c in chords)
                c.Position = 0;
            DeduplicateAnchors(chords);
            return;
        }

        if (oldText.Length == 0)
        {
            foreach (var c in chords)
                c.Position = Math.Clamp(c.Position, 0, newText.Length);
            DeduplicateAnchors(chords);
            return;
        }

        var anchors = ComputeMatchedPairs(oldText, newText);

        foreach (var chord in chords)
        {
            var oldPos = Math.Clamp(chord.Position, 0, oldText.Length);
            if (oldPos >= oldText.Length)
            {
                chord.Position = newText.Length;
                continue;
            }

            chord.Position = MapIndexThroughAnchors(oldPos, chord.BeforeChar, oldText.Length, newText.Length, anchors);
        }

        chords.Sort((a, b) =>
        {
            var cmp = a.Position.CompareTo(b.Position);
            return cmp != 0 ? cmp : (a.BeforeChar == b.BeforeChar ? 0 : (a.BeforeChar ? -1 : 1));
        });
        DeduplicateAnchors(chords);
    }

    public static int SplitLineAt(SongModel song, int lineIndex, string editedMainText, int caretIndex)
    {
        if (lineIndex < 0 || lineIndex >= song.Lines.Count)
            return lineIndex;

        var line = song.Lines[lineIndex];
        ApplyMainLyricEdit(line, editedMainText);

        var fullText = line.Text;
        var splitPos = Math.Clamp(caretIndex, 0, fullText.Length);

        var rawLeft = fullText[..splitPos];
        var rawRight = fullText[splitPos..];
        var leftText = rawLeft.TrimEnd();
        var rightText = rawRight.TrimStart();
        var rightTrimmedLeading = rawRight.Length - rightText.Length;

        var stayChords = new List<ChordPlacement>();
        var movedChords = new List<ChordPlacement>();

        foreach (var chord in line.Chords)
        {
            if (chord.Position < splitPos)
            {
                chord.Position = Math.Clamp(chord.Position, 0, leftText.Length);
                stayChords.Add(chord);
            }
            else
            {
                chord.Position = Math.Clamp(chord.Position - splitPos - rightTrimmedLeading, 0, rightText.Length);
                movedChords.Add(chord);
            }
        }

        DeduplicateAnchors(stayChords);
        DeduplicateAnchors(movedChords);

        var nextAltLyrics = new List<string>();
        if (line.AltLyrics.Count > 0)
        {
            var splitHalfCells = PartAnalyzer.MeasureHalfCells(rawLeft);
            for (var i = 0; i < line.AltLyrics.Count; i++)
            {
                var (altLeft, altRight) = SplitByHalfCells(line.AltLyrics[i], splitHalfCells);
                line.AltLyrics[i] = altLeft.TrimEnd();
                nextAltLyrics.Add(altRight);
            }
        }

        line.Text = leftText;
        line.Chords = stayChords;

        var nextLine = new LyricLine
        {
            Text = rightText,
            Chords = movedChords,
            AltLyrics = nextAltLyrics
        };

        var insertedIndex = lineIndex + 1;
        song.Lines.Insert(insertedIndex, nextLine);

        foreach (var tab in song.TabSections)
        {
            if (tab.Kind == TabSectionKind.Interlude && tab.InsertAfterLineIndex >= lineIndex)
                tab.InsertAfterLineIndex++;
        }

        LibraryStore.NormalizeSong(song);
        return insertedIndex;
    }

    public static void RemoveVerseOrLine(SongModel song, int lineIndex, int verseIndex)
    {
        if (lineIndex < 0 || lineIndex >= song.Lines.Count)
            return;

        var line = song.Lines[lineIndex];
        if (verseIndex > 0)
        {
            var altIdx = verseIndex - 1;
            if (altIdx < line.AltLyrics.Count)
                line.AltLyrics.RemoveAt(altIdx);
            LibraryStore.NormalizeSong(song);
            return;
        }

        if (line.AltLyrics.Count > 0)
        {
            var promoted = line.AltLyrics[0];
            line.AltLyrics.RemoveAt(0);
            ApplyMainLyricEdit(line, promoted);
            LibraryStore.NormalizeSong(song);
            return;
        }

        var carryPartTitle = line.PartTitle;
        var carryRepeat = line.RepeatCount;
        song.Lines.RemoveAt(lineIndex);

        if (lineIndex < song.Lines.Count &&
            string.IsNullOrWhiteSpace(song.Lines[lineIndex].PartTitle) &&
            !string.IsNullOrWhiteSpace(carryPartTitle))
        {
            song.Lines[lineIndex].PartTitle = carryPartTitle;
            song.Lines[lineIndex].RepeatCount = carryRepeat;
        }

        foreach (var tab in song.TabSections)
        {
            if (tab.Kind == TabSectionKind.Interlude && tab.InsertAfterLineIndex >= lineIndex)
                tab.InsertAfterLineIndex = Math.Max(-1, tab.InsertAfterLineIndex - 1);
        }

        LibraryStore.NormalizeSong(song);
    }

    private static string SanitizeSingleLine(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        return text
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .TrimEnd();
    }

    private static List<(int OldIdx, int NewIdx)> ComputeMatchedPairs(string oldText, string newText)
    {
        var m = oldText.Length;
        var n = newText.Length;

        var prefix = 0;
        var minLen = Math.Min(m, n);
        while (prefix < minLen && oldText[prefix] == newText[prefix])
            prefix++;

        var suffix = 0;
        while (suffix < (minLen - prefix) && oldText[m - 1 - suffix] == newText[n - 1 - suffix])
            suffix++;

        var pairs = new List<(int OldIdx, int NewIdx)>(minLen);
        for (var i = 0; i < prefix; i++)
            pairs.Add((i, i));

        var midOldLen = m - prefix - suffix;
        var midNewLen = n - prefix - suffix;

        if (midOldLen > 0 && midNewLen > 0 && midOldLen * midNewLen <= 65536)
        {
            var dp = new int[midOldLen + 1, midNewLen + 1];
            for (var i = midOldLen - 1; i >= 0; i--)
            {
                for (var j = midNewLen - 1; j >= 0; j--)
                {
                    dp[i, j] = oldText[prefix + i] == newText[prefix + j]
                        ? dp[i + 1, j + 1] + 1
                        : Math.Max(dp[i + 1, j], dp[i, j + 1]);
                }
            }

            var oi = 0;
            var nj = 0;
            while (oi < midOldLen && nj < midNewLen)
            {
                if (oldText[prefix + oi] == newText[prefix + nj])
                {
                    pairs.Add((prefix + oi, prefix + nj));
                    oi++;
                    nj++;
                }
                else if (dp[oi + 1, nj] >= dp[oi, nj + 1])
                {
                    oi++;
                }
                else
                {
                    nj++;
                }
            }
        }

        for (var i = 0; i < suffix; i++)
            pairs.Add((m - suffix + i, n - suffix + i));

        return pairs;
    }

    private static int MapIndexThroughAnchors(
        int oldPos,
        bool beforeChar,
        int oldLen,
        int newLen,
        List<(int OldIdx, int NewIdx)> anchors)
    {
        // Sentinel boundaries: (-1, -1) to (oldLen, newLen)
        var prevOld = -1;
        var prevNew = -1;

        for (var k = 0; k <= anchors.Count; k++)
        {
            var currOld = k < anchors.Count ? anchors[k].OldIdx : oldLen;
            var currNew = k < anchors.Count ? anchors[k].NewIdx : newLen;

            if (oldPos == currOld && k < anchors.Count)
            {
                // If pure insertion happened right at line start before a BeforeChar pickup chord ([^Em]给 -> [^Em]噢 给),
                // keep BeforeChar at 0 only if it was an opening empty-beat chord at index 0; otherwise stay glued to the matched character.
                if (beforeChar && oldPos == 0 && prevOld == -1 && currNew > 0)
                    return 0;
                return currNew;
            }

            if (oldPos < currOld)
            {
                // oldPos falls inside unmatched span (prevOld + 1 .. currOld - 1) -> mapped to (prevNew + 1 .. currNew - 1)
                var oldSpanStart = prevOld + 1;
                var oldSpanLen = currOld - oldSpanStart;
                var newSpanStart = prevNew + 1;
                var newSpanLen = currNew - newSpanStart;

                if (newSpanLen <= 0)
                    return Math.Clamp(newSpanStart, 0, newLen);
                if (oldSpanLen <= 0)
                    return Math.Clamp(newSpanStart, 0, newLen);

                var ratio = (double)(oldPos - oldSpanStart) / oldSpanLen;
                var mapped = newSpanStart + (int)Math.Round(ratio * (newSpanLen - 1), MidpointRounding.AwayFromZero);
                return Math.Clamp(mapped, 0, newLen);
            }

            prevOld = currOld;
            prevNew = currNew;
        }

        return Math.Clamp(oldPos, 0, newLen);
    }

    private static void DeduplicateAnchors(List<ChordPlacement> chords)
    {
        for (var i = chords.Count - 1; i > 0; i--)
        {
            var curr = chords[i];
            var prev = chords[i - 1];
            if (curr.Position == prev.Position && curr.BeforeChar == prev.BeforeChar)
                chords.RemoveAt(i);
        }
    }

    private static (string Left, string Right) SplitByHalfCells(string text, int targetHalfCells)
    {
        if (string.IsNullOrEmpty(text) || targetHalfCells <= 0)
            return ("", text ?? "");

        var acc = 0;
        var splitCharIdx = text.Length;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            if (acc >= targetHalfCells)
            {
                splitCharIdx = enumerator.ElementIndex;
                break;
            }
            acc += PartAnalyzer.GetHalfCellUnits(enumerator.GetTextElement());
        }

        return (text[..splitCharIdx], text[splitCharIdx..]);
    }
}
