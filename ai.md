# OpenCode 代理启动器 交接

> 本文件是给 AI 的工作交接文档，请先阅读此文档再继续对本仓库进行操作。

## 一、还没做完的事 & 动手前注意

### 尚未完成 / 待办

- [ ] 发 v0.4 的 GitHub Release：打 tag `v0.4`，把 `dist\OpenCode-ProxyLauncher.exe` 作为附件上传（文案见 `GITHUB.md`）
- [ ] **支持 macOS** —— 环境变量注入逻辑可复用；需要把 WPF 换成 Avalonia / MAUI，并处理 `.app` 包启动
- [ ] **支持 Linux** —— 同上；另外 `winsqlite3.dll` 那套要换成系统 SQLite（或内嵌 sqlite 库）
- [ ] 补 `LICENSE` 文件（README 声明 MIT，但仓库里还没有这个文件）
- [ ] 可选增强：窗口尺寸 / 位置记忆、日志区「导出」按钮、检测结果行内着色
- [ ] 可选：把「检测代理」依赖的 `ip-api.com` 做成可配置（部分地区访问不稳）
- [ ] 可选：WebUI 增加「停止 OpenCode」按钮（目前只能启动）

### 动手前注意事项

1. **不要从自动化 / agent 进程直接启动 `OpenCode.exe`** —— Electron GUI 在该上下文下会 `exit=0` 秒退，连日志目录都不建。要给用户启动 GUI，用 `explorer.exe "<完整路径>"`。
2. 往 GitHub 推东西**必须走代理**，且要在沙箱外执行：
   `env HTTP_PROXY=http://127.0.0.1:2080 HTTPS_PROXY=... http_proxy=... https_proxy=... git push`
   直接推会 `Send failure: Connection was reset`。
3. **不要引入 NuGet 包**。本机 NuGet 还原会卡死（实测 14 分钟无输出）。读 SQLite 用的是系统自带 `winsqlite3.dll` 的 P/Invoke（见 `Sqlite.cs`），零依赖。
4. 发布必须写 `-p:SelfContained=false`。`--self-contained false` 这种命令行写法在 .NET 10 下会被忽略，照样产出自包含的大包。
5. WPF 单文件打包**必须**加 `-p:IncludeNativeLibrariesForSelfExtract=true`，否则 exe 旁边会散出 `wpfgfx_cor3.dll` 等 5 个原生库。
6. `Theme.xaml` 里的颜色一律用 `DynamicResource`。用 `StaticResource` 会让运行时主题切换失效。
7. 新增界面文案时，**同时往 `Strings.cs` 的 `table` 里加中英两条**，并在 `MainWindow.ApplyLanguage()` 里应用，否则切语言时会漏。
8. 改完界面可以用 `OpenCode-ProxyLauncher.exe --shot <png>` 重新出 README 截图；程序内已延迟 900ms 等入场动画跑完，**别把这段删掉**。
9. 不要提交 `bin/` `obj/` `dist/`（`.gitignore` 已忽略）。`dist/` 是构建产物，只作为 Release 附件上传。
10. **统计"今日用量"必须用 `message` 表**（逐条消息的 `time_created` + `data.tokens`），
    不能用 `session.time_created` —— 一个会话可能昨天创建、今天还在用，用 session 表会让"今日"恒为 0。
11. `opencode stats` 这个 CLI 命令在本机会报数据库查询错误（CLI 与 DB schema 不匹配），别指望它。
12. 用 `--shot` 连续截多张图时，**每张之间要 sleep 足够久**（≥8 秒），否则多个实例重叠会串图。
13. 不要擅自 `git push`，提交推送前先问。

## 二、历史记录（我们做了什么、怎么做的）

- **2026-10-09 22:18** — 写入用户级代理环境变量：`HTTP_PROXY` / `HTTPS_PROXY` / `ALL_PROXY` = `http://127.0.0.1:2080`，`NO_PROXY` = `127.0.0.1,localhost,::1`。用 Python `winreg` 直写 `HKCU\Environment` 并广播 `WM_SETTINGCHANGE`（未使用 cmd / setx）。
- **2026-10-09 22:20–23:40** — 解包 `app.asar` 定位 OpenCode 的代理机制：主进程与 sidecar 各调用一次 `useEnvProxy()` → `http.setGlobalProxyFromEnv()`，即**只从环境变量读代理，且只在进程启动时读一次**。用本地探针实测：设置 `HTTPS_PROXY` 后 `fetch` 确实发出 `CONNECT api.github.com:443`。
- **2026-10-09 23:40** — 用读进程内存（PEB）的方式核对环境块：`explorer.exe` 有变量，但 `StartMenuExperienceHost.exe` / `SearchHost.exe` 是 10-08 开机启动的旧环境。重启这两个宿主后，新实例确认带上了变量。
- **2026-10-10 02:0x** — 用 `curl` 对照验证 `opencode-go` 接口（`https://opencode.ai/zen/go/v1`）：经代理 2080 → `HTTP 400 MissingSessionID`（已通过地区校验）；直连 → `HTTP 403 RegionError`。
- **2026-10-10 02:20** — 写第一版 C# 启动器（命令行版，放在临时工作区）。
- **2026-10-10 02:26** — 迁到 `D:\code\opencode-proxy-launcher`，改成 WinForms GUI。
- **2026-10-10 02:29** — 反馈「WinForms 太难看」→ 换 **WPF** 重写：`Theme.xaml` + `MainWindow.xaml`，并加 `--shot` 参数把界面渲染成 PNG 供 README 使用。
- **2026-10-10 02:32** — 增加浅色 / 深色主题（`ThemeLight.xaml` / `ThemeDark.xaml` + `ThemeManager.cs`）、入场动效、主按钮渐变。
- **2026-10-10 03:14** — 版本号按要求改为 `v0.1`，发布并启动。
- **2026-10-10 03:19** — 核查「opencode 没有启动」：定位到 agent 进程上下文起不了 Electron GUI；改用 `explorer.exe "…\OpenCode.exe"` 成功拉起，PEB 实测主进程 pid=13424（父进程 `explorer.exe`）带 `HTTP_PROXY=http://127.0.0.1:2080`。同日加固：启动后实测 3.5 秒确认进程存活，启动记录写 `%TEMP%\opencode-proxy-launcher.log`。
- **2026-10-10 03:34** — 打成**单文件自包含** exe（61.4MB，目标机器免装 .NET 运行时）。
- **2026-10-10 03:38** — 按 `https://ai.drx.ac.cn/aimd` 的三段式模板生成本文件 `ai.md`。
- **2026-10-10 03:5x** — 发布到 `VeloxLLM/opencode-proxy-launcher`（公开），建 Release `v0.1`（附件为单文件 exe），设置 About 描述与 10 个 topics。
- **2026-10-10 04:0x** — **v0.2**：新增中英文切换（`Strings.cs`）、WebUI（`WebUiServer.cs`，默认关闭）、OpenCode 用量面板（`UsageReader.cs` + `Sqlite.cs`，P/Invoke `winsqlite3.dll` 只读 `opencode.db`）；README 重写为带徽章 / 截图的版本；新增 `docs/ui-en.png`、`docs/webui.png`。
- **2026-10-10 04:1x** — **v0.3**：
  - 修复「今日 token 恒为 0」—— 原来按 `session.time_created` 统计，把"昨天创建、今天还在用"的会话算到昨天。改为按 **`message` 表**（逐条消息的 `cost` / `tokens`，`role='assistant'`）统计，累计 / 今日 / 按模型全部改用它。
  - 新增**账户额度**：`AccountUsageReader.cs` 调 `GET https://opencode.ai/zen/go/v1/usage`（key 取自 `auth.json` 的 `opencode-go`），返回 rolling / weekly / monthly 百分比与重置倒计时。
  - 新增 **`Units.cs`**：默认 / 万 / 千万 / 亿 四种数字单位，GUI 用分段按钮、WebUI 用 localStorage 记忆。
  - 用量卡片进 GUI（之前只有 WebUI 有）；WebUI 的 `/api/usage` 改为返回 `{ local, account }`。
  - 默认值调整：**WebUI 默认开启**、**「启动后自动关闭」默认不勾选**。
- **2026-10-10 04:3x** — **v0.4**：
  - 主界面重构为**三个标签页**（设置 / 用量 / 日志），窗口高度 900→600，解决"日志太下面"。
  - 确认 **rolling = 5 小时窗口**（官方文档：5-hour 20% / weekly 50% / monthly 100%，按模型算），
    标签改为「5 小时 / 5-hour」，并加了 `usage.windowHint` 悬浮说明。
  - 新增**缓存命中率** = cache read / (cache read + input)。
  - 移除「近 7 天」图表（用户反馈不需要）。
  - README / GITHUB.md 改为**中英双语（中文在前，英文在后）**；新增 macOS / Linux 支持的 TODO。

当前仓库状态：

```text
opencode-proxy-launcher/
├── .gitignore
├── OpenCodeProxyLauncher.csproj       net10.0-windows + WPF，Version 0.3
├── App.xaml / App.xaml.cs             入口 + --shot 截图模式 + 语言/主题初始化
├── MainWindow.xaml / .xaml.cs         主界面 + 入场动效 + 启动存活确认 + WebUI 托管
├── Theme.xaml                         样式（颜色全走 DynamicResource）
├── ThemeLight.xaml / ThemeDark.xaml   两套调色板
├── ThemeManager.cs                    运行时切主题 / 首次跟随系统
├── Strings.cs                         中英文文案表
├── Units.cs                           数字单位（默认 / 万 / 千万 / 亿）
├── WebUiServer.cs                     内置 WebUI（TcpListener 迷你 HTTP 服务）
├── UsageReader.cs                     读取本地用量统计（message + session 表）
├── AccountUsageReader.cs              调官方接口读账户额度
├── Sqlite.cs                          winsqlite3.dll 只读 P/Invoke 封装
├── AppSettings.cs                     %APPDATA%\OpenCodeProxyLauncher\settings.json
├── Launcher.cs                        注入环境变量并启动 OpenCode
├── ProxyChecker.cs                    代理体检逻辑
├── README.md / GITHUB.md / ai.md
├── dist\OpenCode-ProxyLauncher.exe    发布产物（单文件，构建后生成）
└── docs\ui-light.png · ui-dark.png · ui-en.png · webui.png
```

## 三、为什么做这件事

- **目标**：让 OpenCode 桌面版稳定走本地代理（`127.0.0.1:2080`），绕开 `opencode-go` 的地区限制。
- **痛点**：OpenCode 桌面版**没有任何内置代理设置**，只在进程启动时从环境变量读一次。而 Windows 上真正创建进程的宿主（开始菜单宿主、旧终端窗口、常驻 IDE 等）经常持有过期、或被别的程序注入过的环境 —— 于是请求直连出去，报 `AI_APICallError: This model is not available in your country`。
- **价值**：启动器自己显式设好环境变量再创建进程，**不依赖任何宿主环境**，也不改系统设置；再配一个「检测代理」和本地用量面板，出问题一眼定位、花了多少一目了然。
- **复用方式**：任何人下载 `dist\OpenCode-ProxyLauncher.exe` 双击即用；这份 `ai.md` 让下一个接手的 AI 只读一个文件就能继续工作。
