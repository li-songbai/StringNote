using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Xianpu.Desktop;

public partial class MainWindow
{
    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && File.Exists(files[0]))
        {
            LoadFromFilePath(files[0]);
            e.Handled = true;
        }
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        CommitFields();
        var dialog = new SaveFileDialog
        {
            Filter = "可编辑弦谱文件 (*.xianpu.json)|*.xianpu.json|JSON 文件 (*.json)|*.json",
            FileName = SafeFileName(_selectedSong.Title) + ".xianpu.json",
            DefaultExt = ".json",
            AddExtension = false,
            Title = "导出可编辑曲谱文件"
        };
        if (dialog.ShowDialog(this) != true) return;
        var targetPath = dialog.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? dialog.FileName
            : dialog.FileName + ".xianpu.json";
        try
        {
            LibraryStore.SaveSongProject(_selectedSong, targetPath);
            SetStatus($"可编辑曲谱已导出：{Path.GetFileName(targetPath)}（发给好友可导入继续编辑）");
            AppDialogs.ShowInfo(
                this,
                "导出完成",
                $"可编辑曲谱已导出：\n{targetPath}\n\n将此文件发给使用同款软件的好友，对方点「导入谱子」或直接拖入窗口即可继续编辑。",
                "打开所在文件夹",
                () => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{targetPath}\"") { UseShellExecute = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppDialogs.ShowError(this, "导出失败", ex.Message);
        }
    }

    private void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        CommitFields();
        var dialog = new SaveFileDialog
        {
            Filter = "PNG 图片 (*.png)|*.png",
            FileName = SafeFileName(_selectedSong.Title) + ".png",
            DefaultExt = ".png",
            AddExtension = true,
            Title = "导出曲谱图片"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var bitmap = ScoreSurface.RenderPng();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
            SetStatus($"PNG 图片已导出（{bitmap.PixelWidth} × {bitmap.PixelHeight}）：{Path.GetFileName(dialog.FileName)}");
            AppDialogs.ShowInfo(
                this,
                "导出完成",
                $"曲谱图片已导出：\n{dialog.FileName}\n尺寸：{bitmap.PixelWidth} × {bitmap.PixelHeight} px",
                "打开所在文件夹",
                () => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{dialog.FileName}\"") { UseShellExecute = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            AppDialogs.ShowError(this, "导出失败", ex.Message);
        }
    }

    private void TransposeDown_Click(object sender, RoutedEventArgs e) => TransposeBySemitones(-1);

    private void TransposeUp_Click(object sender, RoutedEventArgs e) => TransposeBySemitones(1);

    private void TransposeBySemitones(int semitones)
    {
        if (_selectedSong is null) return;
        CommitFields();
        ChordTranspose.TransposeSongInPlace(_selectedSong, semitones, updateSelectedKey: true);
        _initializing = true;
        SelectedKeyBox.Text = _selectedSong.SelectedKey;
        _initializing = false;
        RefreshQuickChordBar();
        ScoreSurface.Rebuild();
        SaveLibraryQuietly();
        SetStatus($"全谱和弦已{(semitones > 0 ? "升" : "降")}半音，当前选调：{_selectedSong.SelectedKey}。");
    }

    private void TransposeDialog_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is not { } song) return;
        CommitFields();

        var dialog = AppDialogs.CreateCardWindow(
            this,
            "批量移调",
            "指定原和弦调与目标选调，一键转换全谱和弦",
            390,
            null,
            false,
            out var root);

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var row1 = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        row1.Children.Add(new TextBlock
        {
            Text = "当前谱面和弦调：",
            Width = 122,
            FontSize = 12.5,
            Foreground = AppDialogs.BodyBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        var fromBox = new ComboBox
        {
            ItemsSource = KeyNames,
            IsEditable = true,
            Text = string.IsNullOrWhiteSpace(song.SelectedKey) ? "C" : song.SelectedKey
        };
        row1.Children.Add(fromBox);
        Grid.SetRow(row1, 0);

        var row2 = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        row2.Children.Add(new TextBlock
        {
            Text = "转换到目标选调：",
            Width = 122,
            FontSize = 12.5,
            Foreground = AppDialogs.BodyBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        var toBox = new ComboBox
        {
            ItemsSource = KeyNames,
            IsEditable = true,
            Text = string.IsNullOrWhiteSpace(song.SelectedKey) ? "G" : song.SelectedKey
        };
        row2.Children.Add(toBox);
        Grid.SetRow(row2, 1);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancelBtn = AppDialogs.CreateSecondaryButton(this, "取消", minWidth: 74);
        cancelBtn.IsCancel = true;
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);
        cancelBtn.Click += (_, _) => AppDialogs.CloseWithResult(dialog, false);

        var okBtn = AppDialogs.CreatePrimaryButton(this, "确定移调", minWidth: 88);
        okBtn.IsDefault = true;
        okBtn.Click += (_, _) => AppDialogs.CloseWithResult(dialog, true);

        buttons.Children.Add(cancelBtn);
        buttons.Children.Add(okBtn);
        Grid.SetRow(buttons, 2);

        root.Children.Add(row1);
        root.Children.Add(row2);
        root.Children.Add(buttons);

        if (dialog.ShowDialog() != true) return;
        var fromKey = fromBox.Text.Trim();
        var toKey = toBox.Text.Trim();
        if (!ChordTranspose.TransposeSongKeys(song, fromKey, toKey))
        {
            AppDialogs.ShowInfo(this, "移调提示", "请输入有效的调名（例如 C → G 或 Am → Em）。");
            return;
        }

        _initializing = true;
        SelectedKeyBox.Text = song.SelectedKey;
        _initializing = false;
        RefreshQuickChordBar();
        ScoreSurface.Rebuild();
        SaveLibraryQuietly();
        SetStatus($"已将全谱和弦从 {fromKey} 调移至 {song.SelectedKey} 调。");
    }

    private void EditAllLyrics_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        var currentText = FormatLyricsForBatchEdit(_selectedSong);
        var updatedText = PromptLyrics("编辑或粘贴全文歌词", "每行对应一句歌词（支持空行、[主歌]/[副歌] 分段及带 [mm:ss] 时间戳的 LRC 内容；未改动的歌词行会自动保留已填和弦和叠行）：", currentText, height: 480);
        if (updatedText is null) return;

        ApplyFullLyricsEdit(_selectedSong, updatedText);
        ScoreSurface.SetSong(_selectedSong);
        UpdateViewportScale();
        SaveLibraryQuietly();
        UpdatePartArrangeButtonCue();

        var summary = PartAnalyzer.SummarizeCandidates(_selectedSong);
        if (summary.DuplicateGroups > 0 || summary.StackableGroups > 0)
            SetStatus($"已更新歌词（共 {_selectedSong.Lines.Count} 行，检测到 {summary.DuplicateGroups} 组同词、{summary.StackableGroups} 组可叠行段落，可点「段落编排」整理）。");
        else
            SetStatus($"已更新歌词（共 {_selectedSong.Lines.Count} 行）。");
    }

    private void PartArrange_Click(object sender, RoutedEventArgs e) => OpenPartArrangeDialog();

    private void OpenPartArrangeDialog()
    {
        if (_selectedSong is null) return;
        var updatedLines = PartArrangeDialog.Show(this, _selectedSong);
        if (updatedLines is null) return;

        var oldLines = _selectedSong.Lines.ToList();
        _selectedSong.Lines.Clear();
        foreach (var line in updatedLines)
            _selectedSong.Lines.Add(line);

        PartAnalyzer.RemapTabSections(_selectedSong, oldLines, _selectedSong.Lines);
        LibraryStore.NormalizeSong(_selectedSong);
        ScoreSurface.SetSong(_selectedSong);
        UpdateViewportScale();
        SaveLibraryQuietly();
        UpdatePartArrangeButtonCue();

        var stackedCount = _selectedSong.Lines.Count(l => l.HasStackedLyrics);
        var refCount = _selectedSong.Lines.Count(l => l.IsReferenceLine);
        SetStatus($"已应用段落编排（当前 {_selectedSong.Lines.Count} 行，含 {stackedCount} 句多段叠行、{refCount} 处反复引用）。");
    }

    private void OpenInsertPartDialog(int afterLineIndex)
    {
        if (_selectedSong is null) return;
        var result = InsertPartDialog.Show(this, _selectedSong, afterLineIndex);
        if (result is null) return;

        var oldLines = _selectedSong.Lines.ToList();
        switch (result.Mode)
        {
            case InsertPartMode.CloneExisting:
                PartAnalyzer.InsertClonedPartAfterLine(
                    _selectedSong.Lines,
                    afterLineIndex,
                    result.SourcePartIndex,
                    result.IncludeChords,
                    asReferenceLine: false,
                    customTitle: result.PartTitle);
                break;
            case InsertPartMode.ReferenceExisting:
                PartAnalyzer.InsertClonedPartAfterLine(
                    _selectedSong.Lines,
                    afterLineIndex,
                    result.SourcePartIndex,
                    includeChords: false,
                    asReferenceLine: true);
                break;
            case InsertPartMode.NewBlank:
                PartAnalyzer.InsertNewPartAfterLine(
                    _selectedSong.Lines,
                    afterLineIndex,
                    result.PartTitle,
                    result.NewLyrics);
                break;
        }

        PartAnalyzer.RemapTabSections(_selectedSong, oldLines, _selectedSong.Lines);
        LibraryStore.NormalizeSong(_selectedSong);
        ScoreSurface.SetSong(_selectedSong);
        UpdateViewportScale();
        SaveLibraryQuietly();
        UpdatePartArrangeButtonCue();

        var modeDesc = result.Mode switch
        {
            InsertPartMode.CloneExisting => result.IncludeChords ? "复用段落（含和弦）" : "复用段落歌词",
            InsertPartMode.ReferenceExisting => "反复引用行",
            _ => "新段落"
        };
        SetStatus($"已在第 {Math.Max(1, afterLineIndex + 1)} 句后插入{modeDesc}。");
    }

    private void OpenStackedLyricEditor(int index, bool addNewRow)
    {
        if (_selectedSong is null || index < 0 || index >= _selectedSong.Lines.Count) return;
        var line = _selectedSong.Lines[index];
        if (line.IsReferenceLine)
        {
            OpenPartArrangeDialog();
            return;
        }

        var result = StackedLyricDialog.Show(this, line, addNewRow);
        if (result is null) return;

        line.Text = result.PrimaryText;
        line.AltLyrics = result.AltLyrics;
        foreach (var chord in line.Chords)
            chord.Position = Math.Clamp(chord.Position, 0, line.Text.Length);

        LibraryStore.NormalizeSong(_selectedSong);
        ScoreSurface.SetSong(_selectedSong);
        UpdateViewportScale();
        SaveLibraryQuietly();
        UpdatePartArrangeButtonCue();
        SetStatus(line.HasStackedLyrics
            ? $"已保存多段叠行歌词（共 {1 + line.AltLyrics.Count} 遍）。"
            : "已更新该行歌词。");
    }

    private void UpdatePartArrangeButtonCue()
    {
        if (PartArrangeButton is null || _selectedSong is null) return;

        var hasStructuredParts = _selectedSong.Lines.Any(l =>
            !string.IsNullOrWhiteSpace(l.PartTitle) || l.HasStackedLyrics || l.IsReferenceLine);

        var highlight = false;
        if (!hasStructuredParts && _selectedSong.Lines.Count >= 4)
        {
            var summary = PartAnalyzer.SummarizeCandidates(_selectedSong);
            highlight = summary.DuplicateGroups > 0 || summary.StackableGroups > 0;
        }

        PartArrangeButton.Style = highlight
            ? TryFindResource("AccentOutlineButton") as Style
            : null;
    }

    private static string FormatLyricsForBatchEdit(SongModel song)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < song.Lines.Count; i++)
        {
            var line = song.Lines[i];
            if (!string.IsNullOrWhiteSpace(line.PartTitle))
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine($"--- {line.PartTitle} ---");
            }
            sb.AppendLine(line.Text);
        }
        return sb.ToString().TrimEnd();
    }

    private static void ApplyFullLyricsEdit(SongModel song, string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            song.Lines.Clear();
            return;
        }

        try
        {
            var imported = LrcImporter.ImportText(rawText, song.Title);
            if (imported.Lines.Count > 0)
            {
                MergeLyricLinesPreservingChords(song, imported.Lines.ToList());
                if (!string.IsNullOrWhiteSpace(imported.OriginalKey) && string.IsNullOrWhiteSpace(song.OriginalKey))
                    song.OriginalKey = imported.OriginalKey;
                if (!string.IsNullOrWhiteSpace(imported.SelectedKey) && string.IsNullOrWhiteSpace(song.SelectedKey))
                    song.SelectedKey = imported.SelectedKey;
                if (!string.IsNullOrWhiteSpace(imported.Capo) && string.IsNullOrWhiteSpace(song.Capo))
                    song.Capo = imported.Capo;
                return;
            }
        }
        catch (InvalidDataException)
        {
            // Fall back to line splitting if the text only contained filtered headers/tags
        }

        var fallbackLines = rawText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Select(x => new LyricLine { Text = x })
            .ToList();

        MergeLyricLinesPreservingChords(song, fallbackLines);
    }

    private static void MergeLyricLinesPreservingChords(SongModel song, List<LyricLine> incomingLines)
    {
        var oldLines = song.Lines.ToList();
        var usedOldIndices = new HashSet<int>();
        var result = new List<LyricLine>(incomingLines.Count);
        var hasIncomingPartTitles = incomingLines.Any(x => !string.IsNullOrWhiteSpace(x.PartTitle));

        for (var i = 0; i < incomingLines.Count; i++)
        {
            var incoming = incomingLines[i];
            var text = incoming.Text;
            LyricLine? matched = null;

            if (i < oldLines.Count && !usedOldIndices.Contains(i) && oldLines[i].Text == text)
            {
                matched = oldLines[i];
                usedOldIndices.Add(i);
            }
            else
            {
                var foundIdx = oldLines.FindIndex(x => x.Text == text);
                if (foundIdx >= 0 && !usedOldIndices.Contains(foundIdx))
                {
                    matched = oldLines[foundIdx];
                    usedOldIndices.Add(foundIdx);
                }
                else if (i < oldLines.Count && !usedOldIndices.Contains(i) && oldLines.Count == incomingLines.Count)
                {
                    matched = oldLines[i];
                    matched.Text = text;
                    foreach (var c in matched.Chords)
                        c.Position = Math.Clamp(c.Position, 0, text.Length);
                    usedOldIndices.Add(i);
                }
            }

            if (matched is not null)
            {
                if (hasIncomingPartTitles)
                    matched.PartTitle = incoming.PartTitle;
                result.Add(matched);
            }
            else
            {
                result.Add(incoming);
            }
        }

        song.Lines.Clear();
        foreach (var line in result)
            song.Lines.Add(line);
        LibraryStore.NormalizeSong(song);
    }

    private void EditLyricLine(int index)
    {
        if (_selectedSong is null || index < 0 || index >= _selectedSong.Lines.Count) return;
        var oldLine = _selectedSong.Lines[index];
        if (oldLine.IsReferenceLine)
        {
            OpenPartArrangeDialog();
            return;
        }
        if (oldLine.HasStackedLyrics)
        {
            OpenStackedLyricEditor(index, addNewRow: false);
            return;
        }

        var text = PromptLyrics("编辑单行歌词", "修改当前行歌词（留空确定可删除该行；输入多行可拆分为多句）：", oldLine.Text, height: 260);
        if (text is null) return;

        var newLines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        if (newLines.Count == 0)
        {
            var carryPartTitle = oldLine.PartTitle;
            var carryRepeat = oldLine.RepeatCount;
            _selectedSong.Lines.RemoveAt(index);
            if (index < _selectedSong.Lines.Count &&
                string.IsNullOrWhiteSpace(_selectedSong.Lines[index].PartTitle) &&
                !string.IsNullOrWhiteSpace(carryPartTitle))
            {
                _selectedSong.Lines[index].PartTitle = carryPartTitle;
                _selectedSong.Lines[index].RepeatCount = carryRepeat;
            }
        }
        else
        {
            oldLine.Text = newLines[0];
            foreach (var chord in oldLine.Chords)
                chord.Position = Math.Clamp(chord.Position, 0, oldLine.Text.Length);

            for (var i = 1; i < newLines.Count; i++)
                _selectedSong.Lines.Insert(index + i, new LyricLine { Text = newLines[i] });
        }

        LibraryStore.NormalizeSong(_selectedSong);
        ScoreSurface.SetSong(_selectedSong);
        UpdateViewportScale();
        SaveLibraryQuietly();
    }

    private void CopyLineChords_Click(object sender, RoutedEventArgs e)
    {
        if (ScoreSurface.ActiveLineIndex is { } lineIdx)
            ScoreSurface.CopyLineChords(lineIdx);
        ScoreSurface.Focus();
    }

    private void PasteLineChords_Click(object sender, RoutedEventArgs e)
    {
        if (ScoreSurface.ActiveLineIndex is { } lineIdx)
            ScoreSurface.PasteLineChords(lineIdx);
        ScoreSurface.Focus();
    }

    private void RemoveChord_Click(object sender, RoutedEventArgs e)
    {
        ScoreSurface.RemoveSelectedChord();
        SetStatus("已删除选中的和弦。");
    }

    private void ClearAllChords_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        var total = _selectedSong.Lines.Sum(x => x.Chords.Count);
        if (total == 0) return;
        if (!AppDialogs.Confirm(this, "清空和弦", $"确定清除当前曲谱上的全部 {total} 个和弦标记吗？（歌词内容会保留，前奏/间奏六线谱不受影响）", "确定清空", isDanger: true))
            return;

        foreach (var line in _selectedSong.Lines)
            line.Chords.Clear();

        ScoreSurface.Rebuild();
        UpdateActionStates();
        SaveLibraryQuietly();
        SetStatus("已清空整首歌词上的所有和弦标记（六线谱段落已保留）。");
    }

    private void UpdateActionStates()
    {
        UpdateUndoRedoButtons();

        if (CopyLineChordsButton is not null)
            CopyLineChordsButton.IsEnabled = ScoreSurface.SelectedLineHasChords;

        if (PasteLineChordsButton is not null)
        {
            var canPaste = ScoreSurface.HasCopiedLineChords &&
                           ScoreSurface.ActiveLineIndex is { } lineIdx &&
                           _selectedSong is not null &&
                           lineIdx >= 0 &&
                           lineIdx < _selectedSong.Lines.Count &&
                           !_selectedSong.Lines[lineIdx].IsReferenceLine;
            PasteLineChordsButton.IsEnabled = canPaste;
            PasteLineChordsButton.ToolTip = ScoreSurface.HasCopiedLineChords
                ? $"将已复制的和弦走向（{ScoreSurface.CopiedLineChordsSummary}）粘贴到当前选中句（Ctrl+V）"
                : "将已复制的和弦走向粘贴到当前选中句（Ctrl+V）";
        }

        if (RemoveChordButton is not null)
            RemoveChordButton.IsEnabled = ScoreSurface.HasSelectedChord;

        if (TabSectionButton is not null && _selectedSong is not null)
        {
            var count = _selectedSong.TabSections.Count;
            if (count == 0)
                TabSectionButton.Content = "+ 六线谱 (可选)";
            else if (!_selectedSong.ShowTabSections)
                TabSectionButton.Content = $"六线谱 ({count} · 已隐藏)";
            else
                TabSectionButton.Content = $"六线谱 ({count})";
        }
    }

    private void TabSectionMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null || TabSectionButton is null) return;

        var menu = new ContextMenu { PlacementTarget = TabSectionButton, Placement = PlacementMode.Bottom };

        var addIntro = new MenuItem { Header = "+ 添加前奏六线谱（歌曲开头）..." };
        addIntro.Click += (_, _) => InsertNewTabSection(TabSectionKind.Intro, -1);
        menu.Items.Add(addIntro);

        var defaultLine = ScoreSurface.SelectedLineIndex is { } sel && sel >= 0 && sel < _selectedSong.Lines.Count
            ? sel
            : Math.Max(0, _selectedSong.Lines.Count / 2 - 1);
        var addInterlude = new MenuItem { Header = "+ 添加间奏六线谱（歌词句间）..." };
        addInterlude.Click += (_, _) => InsertNewTabSection(TabSectionKind.Interlude, defaultLine);
        menu.Items.Add(addInterlude);

        var addOutro = new MenuItem { Header = "+ 添加尾奏六线谱（歌曲结尾）..." };
        addOutro.Click += (_, _) => InsertNewTabSection(TabSectionKind.Outro, Math.Max(0, _selectedSong.Lines.Count - 1));
        menu.Items.Add(addOutro);

        if (_selectedSong.TabSections.Count > 0)
        {
            menu.Items.Add(new Separator());

            var toggleVisible = new MenuItem
            {
                Header = "在谱面上显示六线谱组件",
                IsCheckable = true,
                IsChecked = _selectedSong.ShowTabSections
            };
            toggleVisible.Click += (_, _) =>
            {
                _selectedSong.ShowTabSections = !_selectedSong.ShowTabSections;
                ScoreSurface.Rebuild();
                UpdateViewportScale();
                UpdateActionStates();
                SaveLibraryQuietly();
            };
            menu.Items.Add(toggleVisible);
            menu.Items.Add(new Separator());

            foreach (var section in _selectedSong.TabSections.ToList())
            {
                var posDesc = section.Kind switch
                {
                    TabSectionKind.Intro => "开头",
                    TabSectionKind.Outro => "结尾",
                    _ => section.InsertAfterLineIndex >= 0 ? $"第 {section.InsertAfterLineIndex + 1} 句后" : "句间"
                };
                var item = new MenuItem { Header = $"✎ 编辑「{section.Title}」（{posDesc} · {section.Bars.Count} 小节）" };
                item.Click += (_, _) => EditTabSection(section);
                menu.Items.Add(item);
            }

            menu.Items.Add(new Separator());
            var clearTabs = new MenuItem { Header = "删除全部六线谱段落..." };
            clearTabs.Click += (_, _) =>
            {
                if (!AppDialogs.Confirm(this, "删除六线谱", $"确定移除当前歌曲的全部 {_selectedSong.TabSections.Count} 个六线谱段落吗？", "确定删除", isDanger: true))
                    return;
                _selectedSong.TabSections.Clear();
                ScoreSurface.Rebuild();
                UpdateViewportScale();
                UpdateActionStates();
                SaveLibraryQuietly();
                SetStatus("已移除所有六线谱段落。");
            };
            menu.Items.Add(clearTabs);
        }

        menu.IsOpen = true;
    }

    private void InsertNewTabSection(TabSectionKind kind, int insertAfterLineIndex)
    {
        if (_selectedSong is null) return;
        var result = TabEditorWindow.Show(this, null, _selectedSong.Lines, kind, insertAfterLineIndex);
        if (!result.Saved || result.Section is null) return;

        _selectedSong.ShowTabSections = true;
        _selectedSong.TabSections.Add(result.Section);
        LibraryStore.NormalizeSong(_selectedSong);
        ScoreSurface.Rebuild();
        UpdateViewportScale();
        UpdateActionStates();
        SaveLibraryQuietly();
        SetStatus($"已添加六线谱段落「{result.Section.Title}」（不受全谱变调影响）。");
    }

    private void EditTabSection(TabSection section)
    {
        if (_selectedSong is null) return;
        var idx = _selectedSong.TabSections.IndexOf(section);
        if (idx < 0) return;

        var result = TabEditorWindow.Show(this, section, _selectedSong.Lines);
        if (result.Deleted)
        {
            _selectedSong.TabSections.RemoveAt(idx);
            ScoreSurface.Rebuild();
            UpdateViewportScale();
            UpdateActionStates();
            SaveLibraryQuietly();
            SetStatus($"已删除六线谱段落「{section.Title}」。");
            return;
        }
        if (!result.Saved || result.Section is null) return;

        result.Section.Id = section.Id;
        _selectedSong.TabSections[idx] = result.Section;
        LibraryStore.NormalizeSong(_selectedSong);
        ScoreSurface.Rebuild();
        UpdateViewportScale();
        UpdateActionStates();
        SaveLibraryQuietly();
        SetStatus($"已更新六线谱段落「{result.Section.Title}」（不受全谱变调影响）。");
    }

    private void DuplicateTabSection(TabSection section)
    {
        if (_selectedSong is null) return;
        var copy = section.Clone();
        copy.Title = string.IsNullOrWhiteSpace(section.Title) ? "六线谱 (副本)" : section.Title + " (副本)";
        _selectedSong.TabSections.Add(copy);
        LibraryStore.NormalizeSong(_selectedSong);
        ScoreSurface.Rebuild();
        UpdateViewportScale();
        UpdateActionStates();
        SaveLibraryQuietly();
        SetStatus($"已复制六线谱段落「{copy.Title}」。");
    }

    private void DeleteTabSection(TabSection section)
    {
        if (_selectedSong is null) return;
        if (!AppDialogs.Confirm(this, "删除六线谱", $"确定删除六线谱段落「{section.Title}」吗？", "确定删除", isDanger: true))
            return;
        _selectedSong.TabSections.Remove(section);
        ScoreSurface.Rebuild();
        UpdateViewportScale();
        UpdateActionStates();
        SaveLibraryQuietly();
        SetStatus($"已删除六线谱段落「{section.Title}」。");
    }

    private void SongField_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        if (ReferenceEquals(sender, TitleBox))
            _selectedSong.Title = TitleBox.Text;
    }

    private void OriginalKeyBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        if (OriginalKeyBox.SelectedItem is string val)
            _selectedSong.OriginalKey = val.Trim();
    }

    private void OriginalKeyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        _selectedSong.OriginalKey = OriginalKeyBox.Text.Trim();
    }

    private void SelectedKeyBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        if (SelectedKeyBox.SelectedItem is string val)
            _selectedSong.SelectedKey = val.Trim();
    }

    private void SelectedKeyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        _selectedSong.SelectedKey = SelectedKeyBox.Text.Trim();
    }

    private void CapoBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        if (CapoBox.SelectedItem is string val)
            _selectedSong.Capo = val == "不夹" ? "" : val.Trim();
    }

    private void CapoBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        var text = CapoBox.Text.Trim();
        _selectedSong.Capo = text == "不夹" ? "" : text;
    }

    private void KeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        CommitFields();
    }

    private void CommitFields()
    {
        if (_selectedSong is null) return;
        _headerSaveDebounceTimer.Stop();
        _selectedSong.Title = TitleBox.Text.Trim();
        _selectedSong.OriginalKey = OriginalKeyBox.Text.Trim();
        _selectedSong.SelectedKey = SelectedKeyBox.Text.Trim();
        var capo = CapoBox.Text.Trim();
        _selectedSong.Capo = capo == "不夹" ? "" : capo;
        if (BpmBox is not null)
        {
            var bpmText = BpmBox.Text.Trim();
            if (string.IsNullOrEmpty(bpmText))
                _selectedSong.Bpm = 0;
            else if (int.TryParse(bpmText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedBpm))
                _selectedSong.Bpm = Math.Clamp(parsedBpm, 30, 240);
        }
        _selectedSong.UpdatedAt = DateTime.Now;
        SaveLibraryQuietly();
        RefreshLibraryList();
    }

    private void ColumnMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null || ColumnModeBox.SelectedValue is not string raw) return;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mode))
        {
            _selectedSong.ColumnMode = mode;
            ScoreSurface.Rebuild();
            UpdateViewportScale();
            SaveLibraryQuietly();
        }
    }

    private void ChordDisplay_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null || ChordDisplayBox.SelectedValue is not string mode) return;
        _selectedSong.ChordDisplayMode = mode;
        ScoreSurface.Rebuild();
        UpdateViewportScale();
        SaveLibraryQuietly();
    }
}
