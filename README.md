# 弦笺 · StringNote

**弦笺 (StringNote)** 是一款面向 Windows 的轻量原生吉他记谱桌面应用，专注于**弹唱谱与指弹六线谱的极简制作、排版与高清导出**。

🌐 **项目主页**：[https://li-songbai.github.io/StringNote/](https://li-songbai.github.io/StringNote/)  
📦 **最新版下载**：[GitHub Releases](https://github.com/li-songbai/StringNote/releases)

---

## 核心特性

### 1. 智能导入与 AI 识谱
- **AI 识谱导入**：内置「弹唱谱（歌词 + 和弦）」与「指弹谱（纯六线谱 / 多页）」两套专属 Prompt。一键复制 Prompt 连同曲谱图片发给任意多模态 AI，将返回的 JSON 直接粘贴即可自动解析导入，支持新建曲谱或追加到当前曲谱。
- **LRC / 文本歌词导入**：自动识别 UTF-8 / GBK / GB18030 编码，提取 `[ti:]` 歌名与 `原调 / 选调 / 变调夹` 信息，自动过滤作词/作曲/编曲等制作人员行并清理逐字时间戳。
- **拖拽即开**：支持直接将 `.lrc`、`.txt`、`.xianpu.json` 文件拖入主窗口打开。

### 2. 高效左手和弦记谱与自定义指法
- **点字即填**：单击任意歌词字位，正上方弹出轻量输入框与本调常用和弦快捷按钮，支持键入任意和弦（如 `C`、`Am7`、`Fmaj7`、`G/B`、`D/F#`、`Bm7-5`、`Cadd9` 等）回车确认。
- **纯键盘高速走位**：在谱面使用方向键 `←` `→` `↑` `↓` 移动进唱字位，直接按 `A`–`G` 呼出输入；按 `Tab` / `Shift+Tab` 连续跳字填谱。
- **连点填谱 & 自由拖拽**：开启「连点填谱」可批量放置同一和弦；已放置的和弦可直接拖拽到其他进唱字位，支持字上与字前定位。
- **主谱面撤销 / 重做**：支持 `Ctrl+Z` / `Ctrl+Y`（及工具栏按钮）随时撤销与重做和弦标记、移调、改词、段落编排与六线谱改动。
- **按调自定义和弦与指法图**：支持按调号新增常用和弦，并可视化编辑六弦指法图（品位、指法编号、起始品），自动保存到本地曲库。
- **一键移调**：支持全谱 `♭-1` / `♯+1` 半音升降调，或直接按目标调名批量移调，保留全部换和弦位置。
- **和弦名 / 指法图切换**：支持在谱面上自由切换显示纯和弦名、六弦指法图及顶部和弦一览。

### 3. 可选六线谱（TAB）与演奏技巧
- **独立六线谱编辑器**：可为歌曲添加**前奏、间奏、尾奏**或任意段落后的六线谱区块，支持自定义拍号、小节列数、反复记号、节奏符干与休止符。
- **丰富的演奏技巧标注**：支持扫弦方向（下扫/上扫/切音/琶音）、制音（P.M.）、击弦（H）、勾弦（P）、滑音（S / 滑入 / 滑出）、延音线、倚音、推弦/释放、自然与人工泛音、揉弦、闷音等。
- **联动编辑（Ripple Editing）**：编辑小节和音符时保持节奏对齐，并可在主谱面与导出 PNG 中所见即所得呈现。

### 4. 段落编排与多段叠行排版
- **自动段落识别与编排**：内置段落结构分析，支持标记前奏、主歌、副歌、桥段、尾奏及反复次数，并可随时调整顺序或插入引用段落。
- **多段歌词叠行**：针对主歌 1 / 主歌 2 旋律与和弦相同的曲目，支持叠行显示多段歌词并提供专用对齐编辑器，大幅节省页面空间。
- **单页流式紧凑排版**：默认智能合行紧凑排布，同时提供**自动双栏 / 单栏 / 双栏对照**切换，避免导出长图过长。

### 5. 本地曲库、数据目录设置与自动备份
- **可折叠本地曲库**：支持按歌名、调号、分类或歌词关键词实时搜索，支持 `★ 收藏`、自定义分类、新建/副本/重命名/批量改词/删除。
- **自定义数据路径 & 自动历史快照**：默认存放于 `%LocalAppData%\Xianpu\library.json`，可在「设置与备份」中自由切换存放目录（支持直接指向网盘同步盘实现多设备同步）；每次保存自动在 `backups/` 保留最近 5 份历史快照，并支持**一键导出整库 / 从备份恢复**。
- **高清 PNG 与曲谱分享**：一键导出高清无选中框曲谱图片，也支持导出/导入 `.xianpu.json` 单曲项目与朋友分享。
- **轻量桌面体验**：基于 .NET 10 WPF 原生构建，适配 13 寸小屏与高分屏缩放，配备自定义无边框标题栏、Windows 系统托盘驻留与应用内自动检查更新。

---

## 安装与更新

### 方式一：安装程序（推荐）
前往 [GitHub Releases](https://github.com/li-songbai/StringNote/releases) 下载最新版 `StringNote-Setup-<version>.exe`：
- 采用每用户（Per-User）安装模式，默认安装到 `%LocalAppData%\Programs\StringNote`，**无需管理员 UAC 弹窗**。
- 自动创建开始菜单与桌面快捷方式，并关联 `.xianpu.json` 文件双击打开。
- 支持应用内检查更新，自动下载新版安装包并静默覆盖重启。

### 方式二：免安装便携版
下载 `StringNote-Portable-win-x64-<version>.zip`，完整解压后直接运行 `Xianpu.Desktop.exe` 或 `Start-Xianpu.cmd`（自包含 .NET 10 运行时，无需额外安装运行环境）。

---

## 本地构建（Windows）

需要安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)（Windows x64）：

```powershell
# 直接构建
dotnet build app/Xianpu.Desktop/Xianpu.Desktop.csproj

# 框架依赖打包（生成便携 ZIP）
pwsh -ExecutionPolicy Bypass -File .\Build.ps1

# 自包含单文件打包（如本机已安装 Inno Setup 6，将同时生成安装包）
pwsh -ExecutionPolicy Bypass -File .\Build.ps1 -SelfContained -Version 0.1.0-beta.8
```

也可直接双击根目录的 [启动弦谱.cmd](启动弦谱.cmd) 快速启动本地构建产物。

---

## 自动发布流水线

仓库已配置 GitHub Actions（[`.github/workflows/release.yml`](.github/workflows/release.yml)）：

1. 推送语义化版本标签（`v*`）：
   ```bash
   git tag v0.1.0-beta.8
   git push origin main --tags
   ```
2. Actions 将在 `windows-latest` 上自动完成 .NET 10 `win-x64` 单文件发布、便携包压缩、Inno Setup 6 安装包编译，并自动创建 GitHub Release 与 Release Notes。
3. 亦可在 GitHub 仓库的 **Actions → Release StringNote → Run workflow** 中手动触发。

> 开发者与 AI 协作者请参阅 [AGENTS.md](AGENTS.md) 了解代码架构与开发约定。
