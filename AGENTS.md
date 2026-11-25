# AGENTS.md — 弦笺 (StringNote) 开发协作指南

本文档为 AI 编程助手与协作者提供 **弦笺 (StringNote)** 仓库的结构说明、架构约定、构建与发布流程，确保后续迭代保持风格一致与质量稳定。

---

## 1. 项目概览

- **项目名称**：弦笺 · StringNote（`Xianpu.Desktop`）
- **定位**：面向 Windows 的轻量原生吉他弹唱与指弹记谱工具（WPF / .NET 10）
- **核心工作流**：LRC / 文本 / AI 识谱导入 → 谱面可视化标记左手和弦与六线谱（TAB）→ 段落编排与多段叠行排版 → 导出高清 PNG 曲谱 / `.xianpu.json` 分享
- **目标平台**：`net10.0-windows`（WPF + WinForms 托盘集成，仅引入微软官方 `CommunityToolkit.Mvvm` 支撑渐进式 MVVM）

---

## 2. 目录与模块结构

```text
StringNote/
├── app/Xianpu.Desktop/          # 核心 WPF 桌面应用（.NET 10）
│   ├── ViewModels/              # MVVM 视图模型（CommunityToolkit.Mvvm）：MainViewModel、PracticeViewModel
│   ├── Services/                # 纯逻辑应用服务：SongUndoManager（撤销/重做）、IDialogService / WpfDialogService（弹窗抽象）
│   ├── Models.cs                # 数据模型：LibraryData、SongModel、LyricLine、ChordPlacement、TabSection/TabBar/TabNote 等
│   ├── LibraryStore.cs          # 本地曲库持久化（%LocalAppData%\Xianpu\library.json，支持 XIANPU_DATA_DIR 覆盖）与 .xianpu.json 导入导出
│   ├── MainWindow.xaml(.cs)     # 主窗口生命周期、选歌协调与视口缩放（按职责拆分为 MainWindow.ChordPopover.cs / MainWindow.InlineLyric.cs / MainWindow.EditingAndIO.cs / MainWindow.Library.cs / MainWindow.UndoAndSettings.cs / MainWindow.Practice.cs / MainWindow.UpdatesAndChrome.cs）
│   ├── ScoreSurface.cs          # 核心自绘谱面画布（流式合行/单行/双栏、原位歌词坐标映射、和弦拖拽、键盘走位、叠行歌词、六线谱嵌入与 PNG 导出；拆分 ScoreSurface.Printing.cs 处理 A4 分页打印）
│   ├── LyricLineEditHelper.cs   # 谱面原位改词与断句时的和弦字位跟随平移、叠行同步切分算法
│   ├── ChordCatalog.cs          # 各调常用和弦、开放/横按指法库、候选把位集合与按调自定义和弦持久化
│   ├── ChordVoicingEngine.cs    # 零依赖乐理和弦解析与吉他指板人体工学把位推导引擎（支持任意延伸/变化/转位和弦自动出图）
│   ├── MusicTheoryCatalog.cs    # 乐理指板核心引擎：9 种调弦、21 种音阶、CAGED 五色拼图/每弦两音计算、自然音阶和弦解剖、进行预设与语音连接/色彩丰富推荐、8 节互动乐理课
│   ├── GuitarAudioService.cs    # 零依赖 Karplus-Strong 物理建模原声吉他拨弦合成器（支持单音、扫弦、分解与音阶上行试听）
│   ├── FretboardCanvas.cs       # 0–17 品全景 2D 矢量交互指板（枫木/玫瑰木、变调夹、CAGED 五色拼图框、每弦两音胶囊、靶向音染色、点弦发声）
│   ├── TheoryStudioView.cs      # 「🎼 乐理指板」第三工作模式主视图（互动 CAGED 课 / 和弦解剖库 / 和弦进行与靶向音 / 我的乐理收集本）
│   ├── ChordDiagramView.cs      # 六弦和弦指法图渲染组件
│   ├── ChordTranspose.cs        # 全谱半音升降调与按调名批量移调逻辑
│   ├── CustomChordDialog.cs     # 按调号新增/编辑自定义左手和弦与指法图弹窗
│   ├── LrcImporter.cs           # LRC / 纯文本歌词编码识别（UTF-8/GBK/GB18030）、元信息提取与过滤
│   ├── PartAnalyzer.cs          # 歌曲段落结构分析（前奏/主歌/副歌/桥段/尾奏等）与重复段识别
│   ├── PartArrangeDialog.cs     # 段落编排与顺序调整弹窗
│   ├── InsertPartDialog.cs      # 插入段落（含引用段/六线谱段）弹窗
│   ├── StackedLyricDialog.cs    # 多段歌词叠行排版与字位对齐编辑器
│   ├── TabEditorWindow.cs       # 独立六线谱（TAB）编辑器（节奏时值、扫弦、技巧标注、反复记号、联动编辑）
│   ├── TabPainter.cs            # 六线谱矢量绘制引擎（供编辑器与主谱面/PNG 导出共用）
│   ├── AiScoreImporter.cs       # AI 识谱 Prompt 模板（弹唱谱 / 指弹谱）与容错 JSON 解析器
│   ├── AiImportDialog.cs        # AI 识谱导入引导弹窗（一键复制 Prompt → 粘贴 JSON 导入/追加）
│   ├── SettingsDialog.cs        # 设置与曲库备份弹窗（自定义数据目录、自动历史快照、整库导出/恢复）
│   ├── MetronomeService.cs      # 零依赖内置节拍器（内存合成木鱼音效 + 防漂移节拍调度）
│   ├── AppDialogs.cs            # 统一卡片式无边框弹窗工厂与设计令牌（颜色、按钮、圆角、阴影）
│   ├── TrayService.cs           # Windows 系统托盘图标与最小化驻留服务
│   ├── UpdateService.cs         # 基于 GitHub Releases API 的自动检查更新与静默覆盖安装
│   └── Assets/                  # StringNote.ico（多尺寸应用/托盘/安装包图标，EmbeddedResource）、logo64.png（顶栏品牌标识，WPF Resource）
├── installer/StringNote.iss     # Inno Setup 6 安装包脚本（Per-User 免 UAC 安装、.xianpu.json 关联）
├── tests/Xianpu.Desktop.Tests/  # xUnit 单元测试：CoreLogicTests（移调、LRC、段落分析、AI 导入、曲库备份）、ViewModelTests
├── docs/                        # GitHub Pages 静态官网（index.html / styles.css / app.js / logo.png / favicon.png；app.js 通过 releases/latest 仅展示正式版）
├── .github/workflows/
│   ├── ci.yml                   # Push/PR 自动在 windows-latest 上验证 .NET 10 Release 构建与单元测试
│   └── release.yml              # 推送 v* 标签或手动触发：Windows 构建 + 便携包 + Inno 安装包 + GitHub Release
├── Build.ps1                    # 本地 Windows 构建与打包脚本
├── LICENSE                      # 非商业用途许可协议（个人免费使用，禁止商用）
├── 启动弦谱.cmd                 # 根目录快捷启动脚本
└── README.md                    # 项目中文说明文档
```

---

## 3. 设计与代码约定

### 3.1 视觉与 UI 规范
- **色彩体系**（见 `MainWindow.xaml` 与 `AppDialogs.cs`）：
  - 主品牌色（松林绿）：`#2F6B48`（Hover `#245538`，浅底 `#EAF3ED`，浅边 `#94BFA4`）
  - 中性底色与纸面：`#F3F5F8` / `#F8FAFC` / `#FFFFFF`
  - 文本层级：标题 `#1E293B`、正文 `#334155`、次要 `#64748B`
  - 警示色：`#DC2626`（Hover `#B91C1C`）
- **弹窗规范**：新弹窗统一使用 `AppDialogs.CreateCardWindow(...)` 创建无边框圆角卡片窗口，并按 `SystemParameters.WorkArea` 限制最大宽高，保证 13 寸小屏（如 1366×768 / 125%–150% 缩放）完整可见。
- **最小化 NuGet 依赖**：除微软官方 `CommunityToolkit.Mvvm` 外，仅使用 .NET 10 BCL + WPF + WinForms；新增第三方包前需充分评估，以保持单文件体积精简。

### 3.2 数据与兼容性
- **本地数据路径**：默认 `%LocalAppData%\Xianpu\library.json`，测试或隔离环境可通过环境变量 `XIANPU_DATA_DIR` 指定目录。
- **JSON 演进**：修改 `Models.cs` 时务必为新增属性提供合理默认值，并在 `LibraryStore.NormalizeSong` 中做好旧版 `.xianpu.json` / `library.json` 的向后兼容。
- **WPF / WinForms 命名空间冲突**：`.csproj` 已移除 `System.Windows.Forms` 与 `System.Drawing` 的隐式 `Using`，如个别文件需要 WinForms API（如托盘），请显式限定命名空间。

### 3.3 谱面与绘制原则
- **所见即所得**：主谱面 `ScoreSurface` 与 `TabPainter` 渲染逻辑同时服务于屏幕显示与高清 PNG 导出；导出 PNG 时不得将选中框、光标或悬停高亮画入图片。
- **和弦与字位**：`ChordPlacement` 支持 `Position` 与 `BeforeChar`（字前/字上定位），修改渲染时注意保持光标指示线位于歌词行内且不穿过指法图。

---

## 4. 构建、验证与发布

> 注意：本项目目标框架为 `net10.0-windows`，完整编译与桌面 GUI 运行需在 Windows 环境（或 GitHub Actions `windows-latest`）执行。

### 本地构建（Windows PowerShell）
```powershell
# 调试构建
dotnet build app/Xianpu.Desktop/Xianpu.Desktop.csproj

# 框架依赖打包
pwsh -ExecutionPolicy Bypass -File .\Build.ps1

# 自包含单文件打包 + Inno Setup 安装包（需安装 Inno Setup 6）
pwsh -ExecutionPolicy Bypass -File .\Build.ps1 -SelfContained -Version 1.0.0
```

### 分支与合并流程
- `main` 已开启分支保护，要求 `build-windows` 检查通过；所有改动请走 `feature 分支 → PR → CI 通过 → 合并`。

### 发布新版本
1. 更新 `app/Xianpu.Desktop/Xianpu.Desktop.csproj` 中的 `<Version>`。
2. 同步更新 `README.md`（及必要时更新 `docs/` 官网展示内容）。
3. 提交代码并打上语义化标签推送，触发 `.github/workflows/release.yml` 自动构建并发布到 GitHub Releases：
   ```bash
   git tag v1.0.1
   git push origin main --tags
   ```
   版本号含 `-`（如 `beta`）会自动标记为 GitHub Pre-release。注意：`UpdateService` 与官网 `docs/app.js` 均通过 `releases/latest` 获取更新，**只会推送正式版**，Pre-release 仅供手动下载测试。
4. 安装包 `AppId` 与安装目录（`%LocalAppData%\Programs\StringNote`）不得变更，否则旧版本无法原地覆盖升级。

---

## 5. 文档同步要求

每当新增用户可见功能或更改快捷键、文件格式、导入方式时，请同步检查并更新：
1. `README.md`：核心功能清单与使用说明。
2. `docs/index.html`：GitHub Pages 官网功能亮点与版本说明。
3. `AGENTS.md`：若有新增核心模块、目录或构建变更，及时补充模块索引。
