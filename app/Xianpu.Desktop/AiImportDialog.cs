using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Xianpu.Desktop;

internal sealed record AiImportDialogResult(bool Accepted, bool AppendToCurrent, AiImportResult? Parsed);

internal static class AiImportDialog
{
    public static AiImportDialogResult Show(Window? owner, bool hasCurrentSong)
    {
        var workArea = SystemParameters.WorkArea;
        var winWidth = Math.Max(680, Math.Min(880, workArea.Width * 0.86));
        var winHeight = Math.Max(540, Math.Min(700, workArea.Height * 0.88));

        var window = AppDialogs.CreateCardWindow(
            owner,
            "AI 识谱导入",
            "先复制专属 Prompt 连同图片发给任意 AI，再把 AI 返回的 JSON 粘贴到下方即可直接导入",
            winWidth,
            winHeight,
            resizable: true,
            out var root,
            badgeText: "AI");

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 0: Mode & Copy Prompt
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 1: Prompt preview
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 2: Paste header
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Row 3: JSON input
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 4: Status & Footer

        AiImportResult? parsedResult = null;
        var appendChosen = false;

        // ─── Row 0: Mode Selection + Copy Prompt Button ───────────────────────────────
        var step1Bar = new DockPanel { Margin = new Thickness(0, 0, 0, 6), LastChildFill = false };

        var modePanel = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        modePanel.Children.Add(new TextBlock
        {
            Text = "第一步：",
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.TitleBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 8, 2)
        });

        var vocalRadio = new RadioButton
        {
            Content = "类型 1：弹唱谱（歌词 + 和弦）",
            GroupName = "AiImportMode",
            IsChecked = true,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.AccentHoverBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 14, 2),
            Cursor = Cursors.Hand
        };
        var fingerstyleRadio = new RadioButton
        {
            Content = "类型 2：指弹谱（纯六线谱 / 多页）",
            GroupName = "AiImportMode",
            IsChecked = false,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.BodyBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 8, 2),
            Cursor = Cursors.Hand
        };
        modePanel.Children.Add(vocalRadio);
        modePanel.Children.Add(fingerstyleRadio);
        DockPanel.SetDock(modePanel, Dock.Left);
        step1Bar.Children.Add(modePanel);

        var copyPromptBtn = AppDialogs.CreateAccentOutlineButton(owner, "复制 Prompt", minWidth: 112);
        copyPromptBtn.Padding = new Thickness(12, 4, 12, 4);
        DockPanel.SetDock(copyPromptBtn, Dock.Right);
        step1Bar.Children.Add(copyPromptBtn);

        Grid.SetRow(step1Bar, 0);
        root.Children.Add(step1Bar);

        // ─── Row 1: Prompt Preview ────────────────────────────────────────────────────
        var promptBox = new TextBox
        {
            Height = 148,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
            FontSize = 11.5,
            Foreground = AppDialogs.BodyBrush,
            Background = AppDialogs.SubtleSurfaceBrush,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(9, 7, 9, 7),
            Margin = new Thickness(0, 0, 0, 10),
            Text = AiScoreImporter.VocalPrompt
        };
        Grid.SetRow(promptBox, 1);
        root.Children.Add(promptBox);

        // ─── Row 2: Step 2 Paste Header + Actions ─────────────────────────────────────
        var step2Bar = new DockPanel { Margin = new Thickness(0, 0, 0, 6), LastChildFill = false };
        var step2Label = new TextBlock
        {
            Text = "第二步：粘贴 AI 回复内容（支持整段粘贴，自动提取 JSON，也兼容 .xianpu.json）",
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.TitleBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(step2Label, Dock.Left);
        step2Bar.Children.Add(step2Label);

        var pasteActions = new StackPanel { Orientation = Orientation.Horizontal };
        var pasteBtn = AppDialogs.CreateSecondaryButton(owner, "粘贴剪贴板", minWidth: 86);
        pasteBtn.Padding = new Thickness(9, 3, 9, 3);
        pasteBtn.FontSize = 11.5;
        pasteBtn.Margin = new Thickness(0, 0, 6, 0);
        var clearBtn = AppDialogs.CreateSecondaryButton(owner, "清空", minWidth: 56);
        clearBtn.Padding = new Thickness(9, 3, 9, 3);
        clearBtn.FontSize = 11.5;
        pasteActions.Children.Add(pasteBtn);
        pasteActions.Children.Add(clearBtn);
        DockPanel.SetDock(pasteActions, Dock.Right);
        step2Bar.Children.Add(pasteActions);

        Grid.SetRow(step2Bar, 2);
        root.Children.Add(step2Bar);

        // ─── Row 3: JSON Input Box ────────────────────────────────────────────────────
        var inputBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
            FontSize = 12,
            Foreground = AppDialogs.TitleBrush,
            Background = Brushes.White,
            BorderBrush = AppDialogs.InputBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(9, 7, 9, 7),
            ToolTip = "直接粘贴 AI 输出（带不带 ```json ``` 代码块或前后说明文字都可以）"
        };
        Grid.SetRow(inputBox, 3);
        root.Children.Add(inputBox);

        // ─── Row 4: Status Bar + Footer Buttons ───────────────────────────────────────
        var bottomStack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        var statusText = new TextBlock
        {
            Text = hasCurrentSong
                ? "提示：多页长谱可先导入第 1 页为新曲谱，再逐页粘贴后点「追加到当前曲谱」拼接。"
                : "提示：可先测试粘贴 AI 返回的 ```json ... ``` 内容并点击「导入为新曲谱」。",
            FontSize = 11.5,
            Foreground = AppDialogs.MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        bottomStack.Children.Add(statusText);

        var footer = new DockPanel { LastChildFill = false };
        var rightBtns = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(rightBtns, Dock.Right);

        var cancelBtn = AppDialogs.CreateSecondaryButton(owner, "取消", minWidth: 74);
        cancelBtn.IsCancel = true;
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);
        cancelBtn.Click += (_, _) => AppDialogs.CloseWithResult(window, false);

        var appendBtn = AppDialogs.CreateSecondaryButton(owner, "追加到当前曲谱", minWidth: 118);
        appendBtn.IsEnabled = hasCurrentSong;
        appendBtn.Margin = new Thickness(0, 0, 8, 0);
        appendBtn.ToolTip = "保留当前打开的曲谱，将本次导入的歌词段落或指弹小节接在末尾";

        var importNewBtn = AppDialogs.CreatePrimaryButton(owner, "导入为新曲谱", minWidth: 118);

        rightBtns.Children.Add(cancelBtn);
        rightBtns.Children.Add(appendBtn);
        rightBtns.Children.Add(importNewBtn);
        footer.Children.Add(rightBtns);
        bottomStack.Children.Add(footer);

        Grid.SetRow(bottomStack, 4);
        root.Children.Add(bottomStack);

        // ─── Wiring ───────────────────────────────────────────────────────────────────
        void SyncModePrompt()
        {
            var isFingerstyle = fingerstyleRadio.IsChecked == true;
            promptBox.Text = isFingerstyle ? AiScoreImporter.FingerstylePrompt : AiScoreImporter.VocalPrompt;
            vocalRadio.Foreground = isFingerstyle ? AppDialogs.BodyBrush : AppDialogs.AccentHoverBrush;
            fingerstyleRadio.Foreground = isFingerstyle ? AppDialogs.AccentHoverBrush : AppDialogs.BodyBrush;
        }

        vocalRadio.Checked += (_, _) => SyncModePrompt();
        fingerstyleRadio.Checked += (_, _) => SyncModePrompt();

        var copyResetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
        copyResetTimer.Tick += (_, _) =>
        {
            copyResetTimer.Stop();
            copyPromptBtn.Content = "复制 Prompt";
        };

        copyPromptBtn.Click += (_, _) =>
        {
            Clipboard.SetText(promptBox.Text);
            copyPromptBtn.Content = "✓ 已复制到剪贴板";
            copyResetTimer.Stop();
            copyResetTimer.Start();
            var modeName = fingerstyleRadio.IsChecked == true ? "指弹谱" : "弹唱谱";
            statusText.Foreground = AppDialogs.AccentHoverBrush;
            statusText.Text = $"已复制【{modeName}】专属 Prompt，请连同乐谱图片发给任意 AI，再把回复粘贴到下方。";
        };

        pasteBtn.Click += (_, _) =>
        {
            if (Clipboard.ContainsText())
            {
                inputBox.Text = Clipboard.GetText();
                inputBox.CaretIndex = inputBox.Text.Length;
                statusText.Foreground = AppDialogs.MutedBrush;
                statusText.Text = "已从剪贴板填入内容，点击下方按钮即可解析导入。";
            }
            else
            {
                statusText.Foreground = AppDialogs.DangerBrush;
                statusText.Text = "剪贴板中没有文本内容。";
            }
            inputBox.Focus();
        };

        clearBtn.Click += (_, _) =>
        {
            inputBox.Clear();
            statusText.Foreground = AppDialogs.MutedBrush;
            statusText.Text = "已清空输入框。";
            inputBox.Focus();
        };

        void TryImport(bool append)
        {
            try
            {
                parsedResult = AiScoreImporter.Parse(inputBox.Text);
                appendChosen = append;
                AppDialogs.CloseWithResult(window, true);
            }
            catch (InvalidDataException ex)
            {
                statusText.Foreground = AppDialogs.DangerBrush;
                statusText.Text = ex.Message;
                inputBox.Focus();
            }
        }

        appendBtn.Click += (_, _) => TryImport(append: true);
        importNewBtn.Click += (_, _) => TryImport(append: false);

        window.Loaded += (_, _) => inputBox.Focus();

        var ok = window.ShowDialog() == true;
        return ok && parsedResult is not null
            ? new AiImportDialogResult(true, appendChosen, parsedResult)
            : new AiImportDialogResult(false, false, null);
    }
}
