using Microsoft.Win32;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Xianpu.Desktop;

internal static class SettingsDialog
{
    private enum TargetLibraryConflictChoice
    {
        Cancel,
        LoadExisting,
        OverwriteWithCurrent
    }

    public static void Show(
        Window owner,
        LibraryStore store,
        Func<LibraryData> getCurrentLibrary,
        Action<LibraryData, string> applyLoadedLibrary)
    {
        var workArea = SystemParameters.WorkArea;
        var winWidth = Math.Max(580, Math.Min(700, workArea.Width * 0.84));
        var winHeight = Math.Max(480, Math.Min(620, workArea.Height * 0.86));

        var window = AppDialogs.CreateCardWindow(
            owner,
            "设置与曲库备份",
            "自定义曲库数据存放路径（支持网盘同步目录），管理自动历史快照与整库备份恢复",
            winWidth,
            winHeight,
            resizable: true,
            out var root,
            badgeText: "设");

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 0: Data directory section
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Row 1: Backup section
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 2: Footer

        // ─── Section 1: Data Directory ───────────────────────────────────────────────
        var pathSection = new Border
        {
            Background = AppDialogs.SubtleSurfaceBrush,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var pathStack = new StackPanel();
        pathStack.Children.Add(new TextBlock
        {
            Text = "曲库数据存放路径",
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.TitleBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });

        var pathHint = new TextBlock
        {
            FontSize = 11.5,
            Foreground = AppDialogs.MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        pathStack.Children.Add(pathHint);

        var pathBox = new TextBox
        {
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
            FontSize = 12,
            Foreground = AppDialogs.BodyBrush,
            Background = Brushes.White,
            BorderBrush = AppDialogs.InputBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 10)
        };
        pathStack.Children.Add(pathBox);

        var pathBtnRow = new WrapPanel();
        var changeDirBtn = AppDialogs.CreateAccentOutlineButton(owner, "更改目录...", minWidth: 102);
        changeDirBtn.Margin = new Thickness(0, 0, 8, 4);
        var resetDirBtn = AppDialogs.CreateSecondaryButton(owner, "恢复默认", minWidth: 88);
        resetDirBtn.Margin = new Thickness(0, 0, 8, 4);
        var openDirBtn = AppDialogs.CreateSecondaryButton(owner, "打开目录", minWidth: 88);
        openDirBtn.Margin = new Thickness(0, 0, 0, 4);

        pathBtnRow.Children.Add(changeDirBtn);
        pathBtnRow.Children.Add(resetDirBtn);
        pathBtnRow.Children.Add(openDirBtn);
        pathStack.Children.Add(pathBtnRow);

        pathSection.Child = pathStack;
        Grid.SetRow(pathSection, 0);
        root.Children.Add(pathSection);

        // ─── Section 2: Full Library Backup & Rolling Snapshots ──────────────────────
        var backupSection = new Border
        {
            Background = AppDialogs.SubtleSurfaceBrush,
            BorderBrush = AppDialogs.SubtleBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var backupGrid = new Grid();
        backupGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        backupGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        backupGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        backupGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var backupTitle = new TextBlock
        {
            Text = "整库备份与自动历史快照",
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialogs.TitleBrush,
            Margin = new Thickness(0, 0, 0, 4)
        };
        Grid.SetRow(backupTitle, 0);
        backupGrid.Children.Add(backupTitle);

        var backupDesc = new TextBlock
        {
            Text = $"每次保存曲库时自动在 backups 目录保留最近 {store.MaxRollingBackups} 份历史快照，也支持手动导出整库文件（含全部曲谱、分类与自定义和弦）。",
            FontSize = 11.5,
            Foreground = AppDialogs.MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(backupDesc, 1);
        backupGrid.Children.Add(backupDesc);

        var backupBtnRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        var exportAllBtn = AppDialogs.CreatePrimaryButton(owner, "导出整库备份...", minWidth: 118);
        exportAllBtn.Margin = new Thickness(0, 0, 8, 4);
        var importAllBtn = AppDialogs.CreateAccentOutlineButton(owner, "从文件恢复整库...", minWidth: 126);
        importAllBtn.Margin = new Thickness(0, 0, 8, 4);
        var snapshotNowBtn = AppDialogs.CreateSecondaryButton(owner, "立即创建快照", minWidth: 102);
        snapshotNowBtn.Margin = new Thickness(0, 0, 8, 4);
        var restoreSnapshotBtn = AppDialogs.CreateSecondaryButton(owner, "恢复选中快照", minWidth: 102);
        restoreSnapshotBtn.Margin = new Thickness(0, 0, 0, 4);
        restoreSnapshotBtn.IsEnabled = false;

        backupBtnRow.Children.Add(exportAllBtn);
        backupBtnRow.Children.Add(importAllBtn);
        backupBtnRow.Children.Add(snapshotNowBtn);
        backupBtnRow.Children.Add(restoreSnapshotBtn);
        Grid.SetRow(backupBtnRow, 2);
        backupGrid.Children.Add(backupBtnRow);

        var backupsList = new ListBox
        {
            Background = Brushes.White,
            BorderBrush = AppDialogs.InputBorderBrush,
            BorderThickness = new Thickness(1),
            FontSize = 12,
            Padding = new Thickness(4)
        };
        Grid.SetRow(backupsList, 3);
        backupGrid.Children.Add(backupsList);

        backupSection.Child = backupGrid;
        Grid.SetRow(backupSection, 1);
        root.Children.Add(backupSection);

        // ─── Footer ──────────────────────────────────────────────────────────────────
        var footer = new DockPanel { LastChildFill = false };
        var statusText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = AppDialogs.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 420
        };
        DockPanel.SetDock(statusText, Dock.Left);
        footer.Children.Add(statusText);

        var closeBtn = AppDialogs.CreateSecondaryButton(owner, "关闭", minWidth: 82);
        closeBtn.IsCancel = true;
        closeBtn.Click += (_, _) => window.Close();
        DockPanel.SetDock(closeBtn, Dock.Right);
        footer.Children.Add(closeBtn);

        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        // ─── Helpers ─────────────────────────────────────────────────────────────────
        IReadOnlyList<LibraryBackupInfo> currentBackups = [];

        void RefreshPathUi()
        {
            pathBox.Text = store.DataDirectory;
            if (LibraryStore.IsDataDirOverriddenByEnv)
            {
                pathHint.Text = "当前正由环境变量 XIANPU_DATA_DIR 指定数据目录，界面修改已锁定。";
                changeDirBtn.IsEnabled = false;
                resetDirBtn.IsEnabled = false;
            }
            else if (store.IsUsingCustomDataDirectory)
            {
                pathHint.Text = "当前使用自定义数据目录（配置记录在默认目录的 settings.json 中，升级或重装软件不会丢失）。";
                changeDirBtn.IsEnabled = true;
                resetDirBtn.IsEnabled = true;
            }
            else
            {
                pathHint.Text = "当前使用默认数据目录（与安装程序分离，卸载重装或自动更新均不会覆盖曲谱）。可切换至网盘同步盘实现多设备同步。";
                changeDirBtn.IsEnabled = true;
                resetDirBtn.IsEnabled = false;
            }
        }

        void RefreshBackupsUi()
        {
            currentBackups = store.ListRollingBackups();
            backupsList.Items.Clear();

            if (currentBackups.Count == 0)
            {
                backupsList.Items.Add(new ListBoxItem
                {
                    Content = "暂无自动历史快照（下次修改并保存曲谱后将自动生成）",
                    Foreground = AppDialogs.MutedBrush,
                    IsEnabled = false,
                    Padding = new Thickness(6, 8, 6, 8)
                });
                restoreSnapshotBtn.IsEnabled = false;
                return;
            }

            foreach (var backup in currentBackups)
            {
                var songText = backup.SongCount.HasValue ? $"{backup.SongCount.Value} 首曲谱" : "未知曲数";
                var kb = Math.Max(1, (int)Math.Round(backup.SizeBytes / 1024.0));
                var label = $"{backup.CreatedAt:yyyy-MM-dd HH:mm:ss}   ·   {songText}   ·   {kb} KB   ({backup.FileName})";
                backupsList.Items.Add(new ListBoxItem
                {
                    Content = label,
                    Tag = backup,
                    Foreground = AppDialogs.BodyBrush,
                    Padding = new Thickness(6, 6, 6, 6)
                });
            }
            restoreSnapshotBtn.IsEnabled = false;
        }

        void SwitchDataDirectory(string targetDir)
        {
            var normalizedTarget = Path.GetFullPath(targetDir.Trim())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedCurrent = Path.GetFullPath(store.DataDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(normalizedTarget, normalizedCurrent, StringComparison.OrdinalIgnoreCase))
            {
                statusText.Text = "所选目录与当前数据目录相同，无需切换。";
                return;
            }

            try
            {
                Directory.CreateDirectory(normalizedTarget);
            }
            catch (Exception ex)
            {
                AppDialogs.ShowError(window, "目录不可用", $"无法访问或创建目标目录：\n{ex.Message}");
                return;
            }

            var targetLibraryPath = Path.Combine(normalizedTarget, "library.json");
            var existingCount = LibraryStore.PeekLibrarySongCount(targetLibraryPath);

            if (existingCount.HasValue)
            {
                var choice = ShowTargetConflictDialog(window, normalizedTarget, existingCount.Value, getCurrentLibrary().Songs.Count);
                if (choice == TargetLibraryConflictChoice.Cancel)
                    return;

                try
                {
                    if (choice == TargetLibraryConflictChoice.LoadExisting)
                    {
                        var loaded = LibraryStore.LoadLibraryBackup(targetLibraryPath);
                        store.SetCustomDataDirectory(normalizedTarget);
                        applyLoadedLibrary(loaded, $"已切换数据目录并加载目标曲库（共 {loaded.Songs.Count} 首）。");
                    }
                    else
                    {
                        store.CreateManualSnapshot("before-switch");
                        var currentLib = getCurrentLibrary();
                        store.SetCustomDataDirectory(normalizedTarget);
                        store.CreateManualSnapshot("before-overwrite");
                        store.Save(currentLib);
                        applyLoadedLibrary(currentLib, $"已切换数据目录并保存当前曲库（共 {currentLib.Songs.Count} 首）。");
                    }
                    RefreshPathUi();
                    RefreshBackupsUi();
                    statusText.Text = $"已切换数据目录至：{store.DataDirectory}";
                }
                catch (Exception ex)
                {
                    AppDialogs.ShowError(window, "切换目录失败", ex.Message);
                }
            }
            else
            {
                var currentCount = getCurrentLibrary().Songs.Count;
                if (!AppDialogs.Confirm(
                        window,
                        "切换数据存放目录",
                        $"将把当前曲库（共 {currentCount} 首）复制到新目录并立即切换：\n{normalizedTarget}\n\n原目录文件会保留作为兜底备份，不会删除。",
                        "确定切换"))
                {
                    return;
                }

                try
                {
                    var currentLib = getCurrentLibrary();
                    store.SetCustomDataDirectory(normalizedTarget);
                    store.Save(currentLib);
                    applyLoadedLibrary(currentLib, $"已切换数据目录至 {store.DataDirectory}（共 {currentLib.Songs.Count} 首）。");
                    RefreshPathUi();
                    RefreshBackupsUi();
                    statusText.Text = $"已切换数据目录至：{store.DataDirectory}";
                }
                catch (Exception ex)
                {
                    AppDialogs.ShowError(window, "切换目录失败", ex.Message);
                }
            }
        }

        void RestoreFromBackupFile(string filePath, string sourceLabel)
        {
            try
            {
                var backupLib = LibraryStore.LoadLibraryBackup(filePath);
                var currentCount = getCurrentLibrary().Songs.Count;
                if (!AppDialogs.Confirm(
                        window,
                        "恢复整库备份",
                        $"确定从{sourceLabel}恢复完整曲库吗？\n\n备份包含：{backupLib.Songs.Count} 首曲谱、{backupLib.Categories.Count} 个分类\n当前曲库：{currentCount} 首曲谱\n\n恢复前会自动为当前曲库创建一份安全快照。",
                        "确定恢复",
                        isDanger: true))
                {
                    return;
                }

                store.CreateManualSnapshot("before-restore");
                store.Save(backupLib);
                applyLoadedLibrary(backupLib, $"已从备份恢复整库（共 {backupLib.Songs.Count} 首曲谱）。");
                RefreshBackupsUi();
                statusText.Text = $"已恢复整库（共 {backupLib.Songs.Count} 首），旧曲库已自动留存快照。";
            }
            catch (Exception ex)
            {
                AppDialogs.ShowError(window, "恢复备份失败", ex.Message);
            }
        }

        // ─── Events ──────────────────────────────────────────────────────────────────
        backupsList.SelectionChanged += (_, _) =>
        {
            restoreSnapshotBtn.IsEnabled = backupsList.SelectedItem is ListBoxItem { Tag: LibraryBackupInfo };
        };

        changeDirBtn.Click += (_, _) =>
        {
            var dialog = new OpenFolderDialog
            {
                Title = "选择曲库数据存放文件夹",
                InitialDirectory = Directory.Exists(store.DataDirectory) ? store.DataDirectory : LibraryStore.DefaultDataDirectory
            };
            if (dialog.ShowDialog(window) == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                SwitchDataDirectory(dialog.FolderName);
            }
        };

        resetDirBtn.Click += (_, _) =>
        {
            SwitchDataDirectory(LibraryStore.DefaultDataDirectory);
        };

        openDirBtn.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(store.DataDirectory);
                Process.Start(new ProcessStartInfo
                {
                    FileName = store.DataDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppDialogs.ShowError(window, "打开目录失败", ex.Message);
            }
        };

        exportAllBtn.Click += (_, _) =>
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
            var dialog = new SaveFileDialog
            {
                Title = "导出完整曲库备份",
                Filter = "弦笺曲库备份 (*.xianpu-library.json)|*.xianpu-library.json|JSON 文件 (*.json)|*.json",
                FileName = $"StringNote-Library-{stamp}.xianpu-library.json"
            };
            if (dialog.ShowDialog(window) != true) return;

            try
            {
                var lib = getCurrentLibrary();
                LibraryStore.ExportLibraryBackup(lib, dialog.FileName);
                statusText.Text = $"已导出整库备份（共 {lib.Songs.Count} 首）：{Path.GetFileName(dialog.FileName)}";
                AppDialogs.ShowInfo(window, "整库导出成功", $"已导出完整曲库（共 {lib.Songs.Count} 首曲谱、{lib.Categories.Count} 个分类）至：\n{dialog.FileName}");
            }
            catch (Exception ex)
            {
                AppDialogs.ShowError(window, "导出失败", ex.Message);
            }
        };

        importAllBtn.Click += (_, _) =>
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择整库备份文件",
                Filter = "弦笺曲库备份 (*.xianpu-library.json;*.json)|*.xianpu-library.json;*.json|所有文件 (*.*)|*.*"
            };
            if (dialog.ShowDialog(window) != true) return;
            RestoreFromBackupFile(dialog.FileName, $"文件 {Path.GetFileName(dialog.FileName)}");
        };

        snapshotNowBtn.Click += (_, _) =>
        {
            var created = store.CreateManualSnapshot("manual");
            RefreshBackupsUi();
            statusText.Text = created is not null
                ? $"已创建手动快照：{Path.GetFileName(created)}"
                : "当前尚未生成 library.json，请先保存曲谱后再创建快照。";
        };

        restoreSnapshotBtn.Click += (_, _) =>
        {
            if (backupsList.SelectedItem is ListBoxItem { Tag: LibraryBackupInfo backup })
            {
                RestoreFromBackupFile(backup.FilePath, $"快照 {backup.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            }
        };

        RefreshPathUi();
        RefreshBackupsUi();
        window.ShowDialog();
    }

    private static TargetLibraryConflictChoice ShowTargetConflictDialog(
        Window owner,
        string targetDir,
        int targetSongCount,
        int currentSongCount)
    {
        var choice = TargetLibraryConflictChoice.Cancel;
        var dialog = AppDialogs.CreateCardWindow(
            owner,
            "目标目录已有曲库",
            "请选择如何处理目标目录中已存在的 library.json",
            480,
            null,
            resizable: false,
            out var body,
            badgeText: "库");

        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var message = new TextBlock
        {
            Text = $"目标目录：\n{targetDir}\n\n检测到该目录已存在曲库（共 {targetSongCount} 首曲谱）；当前正在使用的曲库共有 {currentSongCount} 首曲谱。\n\n• 推荐：若这是你之前在其他电脑同步的旧曲库，请选择「加载目标曲库」。\n• 若希望用当前曲库替换它，请选择「用当前曲库覆盖」（覆盖前会自动备份目标旧库）。",
            FontSize = 12.5,
            Foreground = AppDialogs.BodyBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        };
        Grid.SetRow(message, 0);
        body.Children.Add(message);

        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelBtn = AppDialogs.CreateSecondaryButton(owner, "取消", minWidth: 74);
        cancelBtn.IsCancel = true;
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);
        cancelBtn.Click += (_, _) =>
        {
            choice = TargetLibraryConflictChoice.Cancel;
            dialog.Close();
        };

        var overwriteBtn = AppDialogs.CreateDangerButton(owner, "用当前曲库覆盖", minWidth: 118);
        overwriteBtn.Margin = new Thickness(0, 0, 8, 0);
        overwriteBtn.Click += (_, _) =>
        {
            choice = TargetLibraryConflictChoice.OverwriteWithCurrent;
            dialog.Close();
        };

        var loadBtn = AppDialogs.CreatePrimaryButton(owner, "加载目标曲库", minWidth: 112);
        loadBtn.IsDefault = true;
        loadBtn.Click += (_, _) =>
        {
            choice = TargetLibraryConflictChoice.LoadExisting;
            dialog.Close();
        };

        btnRow.Children.Add(cancelBtn);
        btnRow.Children.Add(overwriteBtn);
        btnRow.Children.Add(loadBtn);
        Grid.SetRow(btnRow, 1);
        body.Children.Add(btnRow);

        dialog.ShowDialog();
        return choice;
    }
}
