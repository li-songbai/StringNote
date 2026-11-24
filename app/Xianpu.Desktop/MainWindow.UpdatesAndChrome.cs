using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Xianpu.Desktop;

public partial class MainWindow
{
    private async Task CheckForUpdatesSilentlyAsync()
    {
        if (!UpdateService.IsRepositoryConfigured && !UpdateService.HasCustomUpdateUrl)
            return;

        try
        {
            var result = await UpdateService.CheckForUpdatesAsync();
            if (result is { HasUpdate: true })
            {
                CheckUpdateButton.Content = $"发现新版 v{result.LatestVersion}";
                CheckUpdateButton.Style = (Style)FindResource("AccentOutlineButton");
                CheckUpdateButton.ToolTip = $"发现新版本 v{result.LatestVersion}（当前 v{UpdateService.CurrentVersion}），点击查看详情";
                SetStatus($"发现弦笺新版本 v{result.LatestVersion}，可点击右上角「发现新版」更新。");
            }
        }
        catch
        {
            // Background update checks must never interrupt editing.
        }
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (!UpdateService.IsRepositoryConfigured && !UpdateService.HasCustomUpdateUrl)
        {
            ShowUnconfiguredUpdateDialog();
            return;
        }

        CheckUpdateButton.IsEnabled = false;
        var previousContent = CheckUpdateButton.Content;
        CheckUpdateButton.Content = "检查中...";
        try
        {
            var result = await UpdateService.CheckForUpdatesAsync();
            if (result is { HasUpdate: true })
            {
                CheckUpdateButton.Content = $"发现新版 v{result.LatestVersion}";
                CheckUpdateButton.Style = (Style)FindResource("AccentOutlineButton");
            }
            else
            {
                CheckUpdateButton.Content = previousContent;
            }

            ShowUpdateDialog(result);
        }
        catch (Exception ex)
        {
            CheckUpdateButton.Content = previousContent;
            ShowUpdateDialog(null, ex.Message);
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void ShowUnconfiguredUpdateDialog()
    {
        var dialog = AppDialogs.CreateCardWindow(
            this,
            "检查更新",
            "弦笺 StringNote · 版本信息",
            450,
            null,
            false,
            out var layout);

        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var versionPill = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = AppDialogs.AccentLightBrush,
            Padding = new Thickness(10, 5, 10, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 12),
            Child = new TextBlock
            {
                Text = $"当前版本：v{UpdateService.CurrentVersion}",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = AppDialogs.AccentBrush
            }
        };
        Grid.SetRow(versionPill, 0);
        layout.Children.Add(versionPill);

        var bodyBox = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = AppDialogs.SubtleSurfaceBrush,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 10, 12, 10),
            Child = new TextBlock
            {
                Text = "GitHub 自动更新仓库尚未绑定。\n在公开仓库创建后，只需将 UpdateService.cs 中的 DefaultOwner / DefaultRepo 配置为对应的 GitHub 用户名与仓库名，即可启用一键检查更新与自动安装。",
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = AppDialogs.BodyBrush,
                LineHeight = 20
            }
        };
        Grid.SetRow(bodyBox, 1);
        layout.Children.Add(bodyBox);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var ok = AppDialogs.CreatePrimaryButton(this, "知道了", minWidth: 88);
        ok.IsDefault = true;
        ok.IsCancel = true;
        ok.Click += (_, _) => AppDialogs.CloseWithResult(dialog, true);
        footer.Children.Add(ok);
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);

        dialog.ShowDialog();
    }

    private void ShowUpdateDialog(UpdateCheckResult? result, string? errorMessage = null)
    {
        var cts = new CancellationTokenSource();
        var titleText = result switch
        {
            { HasUpdate: true } => $"发现新版本 v{result.LatestVersion}",
            { HasUpdate: false } => "当前已是最新版本",
            _ => "暂时无法获取更新信息"
        };
        var dialog = AppDialogs.CreateCardWindow(
            this,
            titleText,
            "软件更新 · 弦笺 StringNote",
            510,
            420,
            false,
            out var layout);
        dialog.Closed += (_, _) => cts.Cancel();

        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var versionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 10)
        };
        versionRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
            Padding = new Thickness(9, 4, 9, 4),
            Child = new TextBlock
            {
                Text = $"当前 v{UpdateService.CurrentVersion}",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = AppDialogs.BodyBrush
            }
        });

        if (result is not null)
        {
            versionRow.Children.Add(new TextBlock
            {
                Text = "→",
                FontSize = 12,
                Foreground = AppDialogs.MutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0)
            });
            versionRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = AppDialogs.AccentLightBrush,
                Padding = new Thickness(9, 4, 9, 4),
                Child = new TextBlock
                {
                    Text = $"最新 v{result.LatestVersion}",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = AppDialogs.AccentBrush
                }
            });
        }
        else if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            versionRow.Children.Add(new TextBlock
            {
                Text = errorMessage,
                FontSize = 12,
                Foreground = AppDialogs.MutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        Grid.SetRow(versionRow, 0);
        layout.Children.Add(versionRow);

        var notesText = result switch
        {
            not null when !string.IsNullOrWhiteSpace(result.ReleaseNotes) => result.ReleaseNotes,
            { HasUpdate: true } => "本次更新包含功能优化与稳定性改进。",
            { HasUpdate: false } => "您正在使用最新发布的弦笺版本。",
            _ => "请检查网络连接，或点击下方「问题反馈 (GitHub)」前往项目页面查看。"
        };
        var notesContainer = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = AppDialogs.SubtleSurfaceBrush,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            Margin = new Thickness(0, 0, 0, 12),
            Child = new TextBox
            {
                Text = notesText,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalContentAlignment = VerticalAlignment.Top,
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 12.5,
                Foreground = AppDialogs.BodyBrush,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            }
        };
        Grid.SetRow(notesContainer, 1);
        layout.Children.Add(notesContainer);

        var progressPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12), Visibility = Visibility.Collapsed };
        var progressStatus = new TextBlock
        {
            Text = "准备下载...",
            FontSize = 12,
            Foreground = AppDialogs.BodyBrush,
            Margin = new Thickness(0, 0, 0, 6)
        };
        var progressBar = new ProgressBar
        {
            Height = 8,
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Foreground = AppDialogs.AccentBrush,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        var progressTrack = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = AppDialogs.SubtleBorderBrush,
            ClipToBounds = true,
            Child = progressBar
        };
        progressPanel.Children.Add(progressStatus);
        progressPanel.Children.Add(progressTrack);
        Grid.SetRow(progressPanel, 2);
        layout.Children.Add(progressPanel);

        var footer = new DockPanel { LastChildFill = false };
        var feedbackBtn = AppDialogs.CreateSecondaryButton(this, "问题反馈 (GitHub)", minWidth: 120);
        feedbackBtn.Click += (_, _) => OpenExternalUrl(UpdateService.IssuesPageUrl);
        DockPanel.SetDock(feedbackBtn, Dock.Left);
        footer.Children.Add(feedbackBtn);

        var rightButtons = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(rightButtons, Dock.Right);

        var closeBtn = AppDialogs.CreateSecondaryButton(this, "关闭", minWidth: 74);
        closeBtn.IsCancel = true;
        closeBtn.Click += (_, _) => AppDialogs.CloseWithResult(dialog, false);

        if (result is { HasUpdate: true })
        {
            var installBtn = AppDialogs.CreatePrimaryButton(this, "下载并安装更新", minWidth: 128);
            installBtn.Margin = new Thickness(0, 0, 8, 0);
            installBtn.Click += async (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(result.DownloadUrl))
                {
                    OpenExternalUrl(string.IsNullOrWhiteSpace(result.HtmlUrl) ? UpdateService.ReleasesPageUrl : result.HtmlUrl);
                    return;
                }

                installBtn.IsEnabled = false;
                progressPanel.Visibility = Visibility.Visible;
                progressBar.Value = 0;
                progressStatus.Text = $"正在下载 {result.AssetName}...";

                try
                {
                    var progress = new Progress<double>(p =>
                    {
                        progressBar.Value = p;
                        progressStatus.Text = $"正在下载 {result.AssetName}... {p * 100:0}%";
                    });
                    var filePath = await UpdateService.DownloadAssetAsync(result.DownloadUrl, result.AssetName, progress, cts.Token);
                    progressStatus.Text = "下载完成，正在启动安装程序...";
                    UpdateService.LaunchInstallerAndExit(filePath);
                    dialog.Close();
                }
                catch (OperationCanceledException)
                {
                    // Dialog closed by user.
                }
                catch (Exception ex)
                {
                    progressStatus.Text = $"下载失败：{ex.Message}";
                    installBtn.IsEnabled = true;
                }
            };
            rightButtons.Children.Add(installBtn);
        }

        rightButtons.Children.Add(closeBtn);
        footer.Children.Add(rightButtons);
        Grid.SetRow(footer, 3);
        layout.Children.Add(footer);

        dialog.ShowDialog();
    }

    private void LoadWindowIcon()
    {
        try
        {
            using var iconStream = typeof(MainWindow).Assembly.GetManifestResourceStream("Xianpu.Desktop.Assets.StringNote.ico");
            if (iconStream is not null)
            {
                Icon = BitmapFrame.Create(iconStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            }
        }
        catch
        {
            // Ignore icon load failures; Win32 ApplicationIcon remains active.
        }
    }

    private void RestoreFromTray()
    {
        if (!IsVisible)
            Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void ToggleMaximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void ExitWindow_Click(object sender, RoutedEventArgs e) =>
        ExitApplication();

    private void UpdateMaximizeVisual()
    {
        var isMaximized = WindowState == WindowState.Maximized;
        if (MaximizeIconPath is not null)
        {
            MaximizeIconPath.Data = Geometry.Parse(isMaximized
                ? "M 0,3 L 7,3 L 7,10 L 0,10 Z M 3,3 L 3,0 L 10,0 L 10,7 L 7,7"
                : "M 0,0 L 10,0 L 10,10 L 0,10 Z");
        }
        if (MaximizeButton is not null)
            MaximizeButton.ToolTip = isMaximized ? "还原窗口" : "全屏 / 最大化";
        if (WindowOuterBorder is not null)
            WindowOuterBorder.BorderThickness = isMaximized ? new Thickness(0) : new Thickness(1);
        UpdateViewportScale();
    }

    private bool _closingCommitted;

    private void CommitPendingEditsBeforeExit()
    {
        if (_closingCommitted) return;
        _closingCommitted = true;
        CommitInlineLyricEdit(refocusSurface: false);
        CommitFields();
        FlushPendingHeaderSave();
    }

    private void ExitApplication()
    {
        CommitPendingEditsBeforeExit();
        StopAutoScroll(clearSession: true);
        _metronome.Dispose();
        _trayService?.Dispose();
        _trayService = null;
        Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        CommitPendingEditsBeforeExit();
        StopAutoScroll(clearSession: true);
        _metronome.Dispose();
        _trayService?.Dispose();
        _trayService = null;
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        StopAutoScroll(clearSession: true);
        _metronome.Dispose();
        _trayService?.Dispose();
        _trayService = null;
        base.OnClosed(e);
    }

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(monitor, ref monitorInfo))
            {
                var rcWorkArea = monitorInfo.rcWork;
                var rcMonitorArea = monitorInfo.rcMonitor;
                mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
            }
        }
        Marshal.StructureToPtr(mmi, lParam, true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left; public int top; public int right; public int bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private static void OpenExternalUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Ignore browser launch failures.
        }
    }
}
