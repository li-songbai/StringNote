using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Xianpu.Desktop;

public sealed record CustomChordDialogResult(
    bool Saved,
    string TargetKey,
    string ChordName,
    CustomChordDefinition? CustomShape);

internal static class CustomChordDialog
{
    public static CustomChordDialogResult Show(
        Window? owner,
        string defaultKey,
        IReadOnlyList<string> keyOptions,
        string? initialChordName = null,
        bool lockChordName = false)
    {
        var normalizedKey = ChordCatalog.NormalizeKeyBucket(defaultKey);
        var normalizedInitial = ChordCatalog.NormalizeChordName(initialChordName);

        var workArea = SystemParameters.WorkArea;
        var winWidth = Math.Min(600, Math.Max(480, workArea.Width * 0.85));
        var winHeight = Math.Min(490, Math.Max(420, workArea.Height * 0.85));

        var window = AppDialogs.CreateCardWindow(
            owner,
            lockChordName ? $"自定义左手按法 · {normalizedInitial}" : "在对应调下新增左手和弦",
            "添加后将出现在对应调的常用和弦栏与填谱弹窗中，并自动保存",
            winWidth,
            winHeight,
            resizable: false,
            out var root,
            badgeText: "弦");

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ─── Row 0: Key + Chord Name ──────────────────────────────────────────────────
        var topRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 12), VerticalAlignment = VerticalAlignment.Center };

        topRow.Children.Add(MakeCaption("所属调性"));
        var keyBox = new ComboBox
        {
            Width = 92,
            IsEditable = true,
            ItemsSource = keyOptions,
            Text = normalizedKey,
            IsEnabled = !lockChordName,
            Margin = new Thickness(0, 2, 16, 2),
            FontSize = 12.5
        };
        topRow.Children.Add(keyBox);

        topRow.Children.Add(MakeCaption("和弦名称"));
        var nameBox = new TextBox
        {
            Width = 148,
            Text = normalizedInitial,
            IsReadOnly = lockChordName,
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 2, 10, 2),
            ToolTip = "输入任意左手和弦名称，例如 E7、Bm7-5、Gadd9、D/F#、C#m7"
        };
        topRow.Children.Add(nameBox);

        var statusPill = new TextBlock
        {
            Text = "可直接使用内置指法，或在下方点选自定义按法",
            FontSize = 11.5,
            Foreground = AppDialogs.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2)
        };
        topRow.Children.Add(statusPill);

        Grid.SetRow(topRow, 0);
        root.Children.Add(topRow);

        // ─── Row 1: Left Interactive Fretboard + Right Live Preview ───────────────────
        var editorGrid = new Grid();
        editorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        editorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(148) });

        var fretboard = new FretboardEditorCanvas();
        var leftPanel = new StackPanel();

        var controlBar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8), VerticalAlignment = VerticalAlignment.Center };
        controlBar.Children.Add(MakeCaption("起始品"));
        var baseFretBox = new ComboBox { Width = 64, Margin = new Thickness(0, 2, 12, 2), FontSize = 12 };
        for (var f = 1; f <= 12; f++) baseFretBox.Items.Add(f);
        baseFretBox.SelectedItem = 1;
        controlBar.Children.Add(baseFretBox);

        controlBar.Children.Add(MakeCaption("指法编号"));
        var fingerBox = new ComboBox { Width = 84, Margin = new Thickness(0, 2, 10, 2), FontSize = 12 };
        fingerBox.Items.Add(new ComboBoxItem { Content = "不标", Tag = 0 });
        fingerBox.Items.Add(new ComboBoxItem { Content = "1 食指", Tag = 1 });
        fingerBox.Items.Add(new ComboBoxItem { Content = "2 中指", Tag = 2 });
        fingerBox.Items.Add(new ComboBoxItem { Content = "3 无名指", Tag = 3 });
        fingerBox.Items.Add(new ComboBoxItem { Content = "4 小指", Tag = 4 });
        fingerBox.SelectedIndex = 1;
        controlBar.Children.Add(fingerBox);

        var clearFretBtn = new Button
        {
            Content = "清空按法",
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 2, 6, 2),
            FontSize = 11.5,
            Cursor = Cursors.Hand
        };
        controlBar.Children.Add(clearFretBtn);

        var cycleVoicingBtn = AppDialogs.CreateAccentOutlineButton(owner, "↻ 换把位", minWidth: 88);
        cycleVoicingBtn.Padding = new Thickness(8, 3, 8, 3);
        cycleVoicingBtn.Margin = new Thickness(0, 2, 0, 2);
        cycleVoicingBtn.FontSize = 11.5;
        cycleVoicingBtn.ToolTip = "根据乐理自动推导并在多个可用吉他把位间循环切换";
        cycleVoicingBtn.IsEnabled = false;
        controlBar.Children.Add(cycleVoicingBtn);

        leftPanel.Children.Add(controlBar);

        var fretboardHost = new Border
        {
            Background = AppDialogs.SubtleSurfaceBrush,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = fretboard
        };
        leftPanel.Children.Add(fretboardHost);
        leftPanel.Children.Add(new TextBlock
        {
            Text = "提示：点击弦顶部切换空弦 ○ / 闷音 ×；点击品格放置或取消按弦位置。",
            FontSize = 11,
            Foreground = AppDialogs.MutedBrush,
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        Grid.SetColumn(leftPanel, 0);
        editorGrid.Children.Add(leftPanel);

        // Right Preview Card
        var previewView = new ChordDiagramView { Width = 116, Height = 144 };
        var previewCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = "指法预览",
                        FontSize = 11.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = AppDialogs.MutedBrush,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 6)
                    },
                    previewView
                }
            }
        };
        Grid.SetColumn(previewCard, 1);
        editorGrid.Children.Add(previewCard);

        Grid.SetRow(editorGrid, 1);
        root.Children.Add(editorGrid);

        // ─── Row 2: Footer ────────────────────────────────────────────────────────────
        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var cancelBtn = AppDialogs.CreateSecondaryButton(owner, "取消", minWidth: 74);
        cancelBtn.IsCancel = true;
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);
        cancelBtn.Click += (_, _) => AppDialogs.CloseWithResult(window, false);

        var saveBtn = AppDialogs.CreatePrimaryButton(owner, lockChordName ? "保存按法" : "添加到该调", minWidth: 106);
        saveBtn.IsDefault = true;
        footer.Children.Add(cancelBtn);
        footer.Children.Add(saveBtn);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        // ─── Wiring ───────────────────────────────────────────────────────────────────
        var hasEditedFretboard = false;
        var syncing = false;
        IReadOnlyList<ChordShape> currentCandidates = [];
        var candidateIndex = 0;

        void UpdateCycleButtonVisual()
        {
            if (currentCandidates.Count > 1)
            {
                cycleVoicingBtn.Content = $"↻ 换把位 ({candidateIndex + 1}/{currentCandidates.Count})";
                cycleVoicingBtn.IsEnabled = true;
            }
            else if (currentCandidates.Count == 1)
            {
                cycleVoicingBtn.Content = "↻ 自动推导";
                cycleVoicingBtn.IsEnabled = true;
            }
            else
            {
                cycleVoicingBtn.Content = "↻ 换把位";
                cycleVoicingBtn.IsEnabled = false;
            }
        }

        void RefreshPreview()
        {
            var rawName = ChordCatalog.NormalizeChordName(nameBox.Text);
            var displayName = string.IsNullOrWhiteSpace(rawName) ? "和弦" : rawName;
            var activeCandidate = candidateIndex >= 0 && candidateIndex < currentCandidates.Count
                ? currentCandidates[candidateIndex]
                : ChordCatalog.ByName(rawName);

            if (!hasEditedFretboard && activeCandidate is not null)
            {
                previewView.Shape = new ChordShape
                {
                    Id = activeCandidate.Id,
                    Name = displayName,
                    Type = activeCandidate.Type,
                    Frets = activeCandidate.Frets,
                    Fingers = activeCandidate.Fingers,
                    BaseFret = activeCandidate.BaseFret,
                    Barre = activeCandidate.Barre,
                    Label = activeCandidate.Label
                };

                if (ChordCatalog.HasCustomShape(rawName) && candidateIndex == 0)
                    statusPill.Text = "已加载自定义按法（可点「换把位」或直接微调）";
                else if (ChordCatalog.HasCuratedBuiltIn(rawName) && candidateIndex == 0)
                    statusPill.Text = currentCandidates.Count > 1
                        ? $"已匹配经典指法（共 {currentCandidates.Count} 个把位可切换）"
                        : "已匹配经典指法（也可在下方微调）";
                else
                    statusPill.Text = currentCandidates.Count > 1
                        ? $"✨ 已按乐理自动推导指法（共 {currentCandidates.Count} 个把位可切换）"
                        : "✨ 已按乐理自动推导指法（也可在下方微调）";
                statusPill.Foreground = AppDialogs.AccentBrush;
            }
            else if (fretboard.HasAnyNote)
            {
                previewView.Shape = fretboard.BuildShape(displayName);
                statusPill.Text = "将保存下方自定义的左手按法";
                statusPill.Foreground = AppDialogs.AccentBrush;
            }
            else
            {
                previewView.Shape = new ChordShape
                {
                    Id = "preview-empty",
                    Name = displayName,
                    Frets = [null, null, null, null, null, null],
                    Fingers = new int[6],
                    BaseFret = 1,
                    Label = "未设置指法"
                };
                statusPill.Text = string.IsNullOrWhiteSpace(rawName)
                    ? "请输入和弦名称（输入后自动按乐理推导指法）"
                    : "暂未识别该和弦记号，可在下方手动点选按法";
                statusPill.Foreground = AppDialogs.MutedBrush;
            }

            UpdateCycleButtonVisual();
        }

        void LoadShapeToBoard(ChordShape shape)
        {
            syncing = true;
            try
            {
                baseFretBox.SelectedItem = Math.Clamp(shape.BaseFret, 1, 12);
                fretboard.LoadFromShape(shape);
            }
            finally
            {
                syncing = false;
            }
        }

        void RefreshCandidatesForName(bool reloadBoard)
        {
            var normalized = ChordCatalog.NormalizeChordName(nameBox.Text);
            currentCandidates = ChordCatalog.GetCandidates(normalized);
            candidateIndex = 0;
            if (reloadBoard && currentCandidates.Count > 0)
                LoadShapeToBoard(currentCandidates[0]);
            UpdateCycleButtonVisual();
        }

        RefreshCandidatesForName(reloadBoard: !string.IsNullOrWhiteSpace(normalizedInitial));

        nameBox.TextChanged += (_, _) =>
        {
            RefreshCandidatesForName(reloadBoard: !hasEditedFretboard);
            RefreshPreview();
        };

        cycleVoicingBtn.Click += (_, _) =>
        {
            if (currentCandidates.Count == 0)
                RefreshCandidatesForName(reloadBoard: false);
            if (currentCandidates.Count == 0)
                return;

            if (hasEditedFretboard && currentCandidates.Count == 1)
            {
                candidateIndex = 0;
                hasEditedFretboard = false;
                LoadShapeToBoard(currentCandidates[0]);
            }
            else
            {
                candidateIndex = (candidateIndex + 1) % currentCandidates.Count;
                LoadShapeToBoard(currentCandidates[candidateIndex]);
                // Mark as customized when choosing a non-default candidate so saving persists the chosen voicing
                hasEditedFretboard = candidateIndex != 0;
            }
            RefreshPreview();
        };

        baseFretBox.SelectionChanged += (_, _) =>
        {
            if (syncing || baseFretBox.SelectedItem is not int bf) return;
            hasEditedFretboard = true;
            fretboard.SetBaseFret(bf);
            RefreshPreview();
        };

        fingerBox.SelectionChanged += (_, _) =>
        {
            if (fingerBox.SelectedItem is ComboBoxItem { Tag: int f })
                fretboard.ActiveFinger = f;
        };

        fretboard.BoardEdited += () =>
        {
            hasEditedFretboard = true;
            RefreshPreview();
        };

        clearFretBtn.Click += (_, _) =>
        {
            hasEditedFretboard = true;
            fretboard.Clear();
            RefreshPreview();
        };

        saveBtn.Click += (_, _) =>
        {
            var chordName = ChordCatalog.NormalizeChordName(nameBox.Text);
            if (string.IsNullOrWhiteSpace(chordName))
            {
                AppDialogs.ShowInfo(window, "请输入和弦名称", "请先输入要添加的左手和弦名称（例如 E7、Bm7-5、Gadd9）。");
                nameBox.Focus();
                return;
            }
            AppDialogs.CloseWithResult(window, true);
        };

        window.Loaded += (_, _) =>
        {
            RefreshPreview();
            if (!lockChordName)
            {
                nameBox.Focus();
                nameBox.SelectAll();
            }
        };

        if (window.ShowDialog() != true)
            return new CustomChordDialogResult(false, normalizedKey, "", null);

        var finalKey = ChordCatalog.NormalizeKeyBucket(keyBox.Text);
        var finalName = ChordCatalog.NormalizeChordName(nameBox.Text);

        CustomChordDefinition? customDef = null;
        if (hasEditedFretboard && fretboard.HasAnyNote)
        {
            customDef = fretboard.BuildDefinition(finalName);
        }
        else if (!hasEditedFretboard && ChordCatalog.ByName(finalName) is null && fretboard.HasAnyNote)
        {
            customDef = fretboard.BuildDefinition(finalName);
        }

        return new CustomChordDialogResult(true, finalKey, finalName, customDef);
    }

    private static TextBlock MakeCaption(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = AppDialogs.MutedBrush,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 2, 6, 2)
    };

    private sealed class FretboardEditorCanvas : FrameworkElement
    {
        private const double LeftPad = 36;
        private const double TopPad = 24;
        private const double StringSpacing = 34;
        private const double FretSpacing = 32;
        private const int FretCount = 5;

        private static readonly string[] StringLabels = ["6E", "5A", "4D", "3G", "2B", "1e"];
        private static readonly Typeface LabelFont = new(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        private static readonly Typeface DotFont = new(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        private static readonly Brush InkBrush = Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)));
        private static readonly Brush MutedBrush = Freeze(new SolidColorBrush(Color.FromRgb(100, 116, 139)));
        private static readonly Brush GreenBrush = Freeze(new SolidColorBrush(Color.FromRgb(47, 107, 72)));
        private static readonly Brush GridBrush = Freeze(new SolidColorBrush(Color.FromRgb(186, 196, 208)));
        private static readonly Brush HoverBrush = Freeze(new SolidColorBrush(Color.FromArgb(45, 47, 107, 72)));

        private readonly int?[] _frets = new int?[6];
        private readonly int[] _fingers = new int[6];
        private int _baseFret = 1;
        private (int StringIdx, int FretOffset)? _hover;
        private double _pixelsPerDip = 1;

        public int ActiveFinger { get; set; } = 1;
        public bool HasAnyNote => _frets.Any(f => f is not null);
        public event Action? BoardEdited;

        public FretboardEditorCanvas()
        {
            Width = LeftPad + StringSpacing * 5 + 24;
            Height = TopPad + FretSpacing * FretCount + 22;
            SnapsToDevicePixels = true;
            Cursor = Cursors.Hand;
            Loaded += (_, _) => _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        }

        private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

        public void SetBaseFret(int baseFret)
        {
            var next = Math.Clamp(baseFret, 1, 12);
            if (_baseFret == next) return;
            var delta = next - _baseFret;
            for (var i = 0; i < 6; i++)
            {
                if (_frets[i] is > 0)
                    _frets[i] = Math.Max(1, _frets[i]!.Value + delta);
            }
            _baseFret = next;
            InvalidateVisual();
        }

        public void Clear()
        {
            Array.Fill(_frets, null);
            Array.Fill(_fingers, 0);
            InvalidateVisual();
        }

        public void LoadFromShape(ChordShape shape)
        {
            _baseFret = Math.Clamp(shape.BaseFret, 1, 12);
            for (var i = 0; i < 6; i++)
            {
                _frets[i] = i < shape.Frets.Length ? shape.Frets[i] : null;
                _fingers[i] = i < shape.Fingers.Length ? shape.Fingers[i] : 0;
            }
            InvalidateVisual();
        }

        public ChordShape BuildShape(string name)
        {
            var fretsCopy = (int?[])_frets.Clone();
            var fingersCopy = (int[])_fingers.Clone();
            return new ChordShape
            {
                Id = $"custom-{name}",
                Name = name,
                Type = "custom",
                Frets = fretsCopy,
                Fingers = fingersCopy,
                BaseFret = _baseFret,
                Barre = ChordVoicingEngine.InferBarre(fretsCopy, fingersCopy),
                Label = "自定义和弦"
            };
        }

        public CustomChordDefinition BuildDefinition(string name) => new()
        {
            Name = name,
            BaseFret = _baseFret,
            Frets = (int?[])_frets.Clone(),
            Fingers = (int[])_fingers.Clone(),
            Label = "自定义和弦"
        };

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, Width, Height));

            var gridPen = new Pen(GridBrush, 1.1);
            var nutPen = new Pen(InkBrush, _baseFret == 1 ? 3.0 : 1.3);

            // Base fret label
            if (_baseFret > 1)
            {
                var bf = CreateText($"{_baseFret}品", 11, LabelFont, MutedBrush, _pixelsPerDip);
                dc.DrawText(bf, new Point(4, TopPad + FretSpacing * 0.5 - bf.Height / 2));
            }

            // Horizontal frets
            for (var f = 0; f <= FretCount; f++)
            {
                var y = TopPad + f * FretSpacing;
                dc.DrawLine(f == 0 ? nutPen : gridPen, new Point(LeftPad, y), new Point(LeftPad + StringSpacing * 5, y));
            }

            // Vertical strings + top O/X markers + bottom string names
            for (var s = 0; s < 6; s++)
            {
                var x = LeftPad + s * StringSpacing;
                dc.DrawLine(gridPen, new Point(x, TopPad), new Point(x, TopPad + FretSpacing * FretCount));

                var topMark = _frets[s] switch { null => "×", 0 => "○", _ => "" };
                if (topMark.Length > 0)
                {
                    var markFt = CreateText(topMark, 12, DotFont, _frets[s] == 0 ? GreenBrush : MutedBrush, _pixelsPerDip);
                    dc.DrawText(markFt, new Point(x - markFt.Width / 2, TopPad - 20));
                }

                var labelFt = CreateText(StringLabels[s], 10, LabelFont, MutedBrush, _pixelsPerDip);
                dc.DrawText(labelFt, new Point(x - labelFt.Width / 2, TopPad + FretSpacing * FretCount + 5));
            }

            // Hover indicator
            if (_hover is { } hv)
            {
                var hx = LeftPad + hv.StringIdx * StringSpacing;
                var hy = hv.FretOffset == 0
                    ? TopPad - 12
                    : TopPad + (hv.FretOffset - 0.5) * FretSpacing;
                dc.DrawEllipse(HoverBrush, null, new Point(hx, hy), 10, 10);
            }

            // Active finger dots
            for (var s = 0; s < 6; s++)
            {
                var fret = _frets[s];
                if (fret is null or <= 0) continue;
                var offset = fret.Value - _baseFret + 1;
                if (offset is < 1 or > FretCount) continue;

                var cx = LeftPad + s * StringSpacing;
                var cy = TopPad + (offset - 0.5) * FretSpacing;
                dc.DrawEllipse(GreenBrush, null, new Point(cx, cy), 10.5, 10.5);
                if (_fingers[s] > 0)
                {
                    var fingerFt = CreateText(_fingers[s].ToString(CultureInfo.InvariantCulture), 10.5, DotFont, Brushes.White, _pixelsPerDip);
                    dc.DrawText(fingerFt, new Point(cx - fingerFt.Width / 2, cy - fingerFt.Height / 2));
                }
            }
        }

        private (int StringIdx, int FretOffset)? HitTest(Point pt)
        {
            var s = (int)Math.Round((pt.X - LeftPad) / StringSpacing);
            if (s is < 0 or > 5) return null;
            if (Math.Abs(pt.X - (LeftPad + s * StringSpacing)) > StringSpacing * 0.45) return null;

            if (pt.Y >= 0 && pt.Y < TopPad)
                return (s, 0);

            var offset = (int)Math.Ceiling((pt.Y - TopPad) / FretSpacing);
            if (offset is >= 1 and <= FretCount)
                return (s, offset);

            return null;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (HitTest(e.GetPosition(this)) is not { } hit) return;

            if (hit.FretOffset == 0)
            {
                _frets[hit.StringIdx] = _frets[hit.StringIdx] == 0 ? null : 0;
                _fingers[hit.StringIdx] = 0;
            }
            else
            {
                var targetFret = _baseFret + hit.FretOffset - 1;
                if (_frets[hit.StringIdx] == targetFret)
                {
                    _frets[hit.StringIdx] = 0;
                    _fingers[hit.StringIdx] = 0;
                }
                else
                {
                    _frets[hit.StringIdx] = targetFret;
                    _fingers[hit.StringIdx] = ActiveFinger;
                }
            }

            InvalidateVisual();
            BoardEdited?.Invoke();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var hit = HitTest(e.GetPosition(this));
            if (hit != _hover)
            {
                _hover = hit;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover is not null)
            {
                _hover = null;
                InvalidateVisual();
            }
        }

        private static FormattedText CreateText(string text, double size, Typeface typeface, Brush brush, double pixelsPerDip) =>
            new(text ?? string.Empty, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, typeface, size, brush, pixelsPerDip);
    }
}
