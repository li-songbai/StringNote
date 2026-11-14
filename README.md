# 弦笺 · StringNote：吉他歌词与左手和弦记谱工具

**弦笺 (StringNote)** 是一个面向 Windows 的轻量原生桌面制谱工具，专注于**导入 LRC 歌词 → 在进唱字位上方标记左手和弦 → 导出紧凑高清曲谱图片 (PNG)** 的极简工作流。

## 核心特点

- **可折叠本地曲库管理**：左上角点击 `曲库 (N)` 按钮可随时展开或收起左侧曲库抽屉（收起后谱面自动占满全宽）。支持**按歌名、调号、分类或歌词关键词实时搜索**、**全部 / ★ 收藏筛选**、**自定义分类管理（`+ 分类`）**，以及一键**新建、保存、创建副本、重命名、批量改词、删除**曲谱，所有修改自动持久化到本地曲库。
- **单页流式排版**：默认采用**合行紧凑**排版，从头到尾顺序排布并自动将 2~3 句短歌词放在同一行内（也提供**逐句单行**与**双栏对照**选项），避免导出长图过长或分太多页。
- **简洁谱头信息**：仅保留最核心的四项信息——**歌曲名称、原调、选调、变调夹**，修改后实时同步到谱面。
- **智能 LRC 歌词导入**：自动识别 UTF-8 / GBK / GB18030 编码，提取 `[ti:]` 歌名与 `原调/选调/变调夹` 信息，自动过滤作词/作曲/编曲等制作人员行，并清理句中逐字时间戳。支持直接把 `.lrc` / `.txt` / `.xianpu.json` 拖入窗口打开。
- **高效左手和弦标记（放在歌词上方进唱的地方）**：
  - **点击字位直接输入或点选**：单击任意歌词字位，字位正上方即弹出轻量输入框与本调常用和弦按钮，可直接键入任意和弦（如 `C`、`Am7`、`Fmaj7`、`G/B`、`D/F#`、`Bm7-5`、`Cadd9` 等）按 `Enter` 确认，或单击常用和弦一键填入。
  - **纯键盘快速走位填谱**：在谱面上使用方向键 `←` `→` `↑` `↓` 移动进唱字位，直接按字母键 `A`–`G` 即可呼出和弦输入；在输入框中按 `Tab` / `Shift+Tab` 可边填边跳到下一字位。
  - **连点填谱模式**：开启工具栏“✎ 连点填谱”后，点选一个常用和弦即可在歌词不同位置连续点击批量放置。
  - **自由拖动与移调**：已放置的和弦可直接鼠标拖拽到其他进唱字位（自动覆盖目标字位），支持全谱 `♭-1` / `♯+1` 半音升降调与指定调名批量移调。

## 安装与自动更新

### 1. 安装包安装（推荐）
前往项目的 **GitHub Releases** 下载最新版安装程序 `StringNote-Setup-<version>.exe`：
- 安装包采用每用户（Per-User）静默权限模式，默认安装到 `%LocalAppData%\Programs\StringNote`，**无需管理员 UAC 弹窗**。
- 自动创建开始菜单与桌面快捷方式 `弦笺 (StringNote)`，并关联 `.xianpu.json` 曲谱文件双击打开。
- 支持通过应用内 GitHub Releases 检查更新，自动下载安装包并静默覆盖更新后重启应用。

### 2. 免安装便携版
下载 `StringNote-Portable-win-x64-<version>.zip`，完整解压后直接运行 `Xianpu.Desktop.exe` 或 `Start-Xianpu.cmd` 即可使用（自包含 .NET 运行时，无需额外安装环境）。

## 启动与本地构建

直接双击根目录的 `启动弦谱.cmd` 或编译产物中的 `Xianpu.Desktop.exe` 即可运行。

在源码目录构建：

```powershell
dotnet build app/Xianpu.Desktop/Xianpu.Desktop.csproj
```

使用打包脚本生成便携压缩包（若本机已安装 Inno Setup 6，将同时自动生成 `dist\StringNote-Setup-<Version>.exe`）：

```powershell
# 框架依赖发布
pwsh -ExecutionPolicy Bypass -File .\Build.ps1

# 自包含单文件发布并指定版本号
pwsh -ExecutionPolicy Bypass -File .\Build.ps1 -SelfContained -Version 1.0.0
```

## 开发者发布流程

仓库已配置 GitHub Actions 自动发布流水线（[`.github/workflows/release.yml`](.github/workflows/release.yml)）：

1. 推送语义化版本标签（`v*.*.*`）：
   ```bash
   git tag v1.0.0
   git push --tags
   ```
2. GitHub Actions 将在 `windows-latest` 上自动执行：
   - 使用 .NET 10 发布 `win-x64` 自包含单文件版本
   - 生成便携压缩包 `dist/StringNote-Portable-win-x64-<version>.zip`
   - 使用 Inno Setup 6 编译安装程序 `dist/StringNote-Setup-<version>.exe`
   - 自动创建 GitHub Release 并上传安装包与便携压缩包（支持自动生成 Release Notes）
3. 也可以在 GitHub 仓库的 **Actions → Release StringNote → Run workflow** 中手动触发构建。
