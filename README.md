<div align="center">

# OpenCode 代理启动器

**让 OpenCode 桌面版稳定走本地代理的 Windows 启动器**

A Windows launcher that makes the OpenCode desktop app use your local proxy.

[![Release](https://img.shields.io/github/v/release/VeloxLLM/opencode-proxy-launcher?label=release&color=4c6fff)](https://github.com/VeloxLLM/opencode-proxy-launcher/releases)
[![Platform](https://img.shields.io/badge/platform-Windows-4c6fff)](#)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](#)
[![UI](https://img.shields.io/badge/UI-WPF-4c6fff)](#)
[![i18n](https://img.shields.io/badge/i18n-中文%20%7C%20English-16b364)](#)
[![License](https://img.shields.io/badge/license-MIT-16b364)](#许可--license)
[![Built with DeepSeek V4.1 Flash](https://img.shields.io/badge/built%20with-DeepSeek%20V4.1%20Flash-4c6fff)](#致谢--credits)

[中文](#中文) · [English](#english)

</div>

---

# 中文

## 它解决什么问题

OpenCode 桌面版**没有任何内置的代理设置项**。它只在**进程启动的那一刻**从环境变量里读代理，而且主进程和它 fork 出来的后端 sidecar 各读一次：

```js
// OpenCode 桌面版内部实现（主进程 / sidecar 各有一份，逻辑相同）
function useEnvProxy() {
  http.setGlobalProxyFromEnv();   // 读 HTTP_PROXY / HTTPS_PROXY / NO_PROXY
}
```

坑就出在"启动它的那个进程"上。创建 OpenCode 进程的宿主可能是资源管理器、开始菜单宿主进程、某个终端窗口、某个 IDE —— 这些进程**经常持有过期的环境**，或者被别的程序注入过自己的代理变量。OpenCode 继承了这份错误的环境，请求就直连出去了。

对于有地区限制的模型（例如 `opencode-go` 的部分模型），症状很明确：

```
AI_APICallError: This model is not available in your country
```

本工具的做法很直接：**自己显式设好环境变量，再创建 OpenCode 进程**。不依赖任何宿主进程，也不修改系统设置。

## 功能

- **一键启动** —— 以指定代理环境变量拉起 OpenCode，只影响这一个子进程
- **启动后存活确认** —— 拉起后实测 3.5 秒，进程若立刻退出会明确报错，不会假装成功
- **代理体检** —— 端口监听 / **经代理的出口 IP 与国家** / HTTPS 连通性 / 程序路径
- **用量面板（GUI 与 WebUI 都有）**
  - **账户额度** —— 通过官方接口 `GET https://opencode.ai/zen/go/v1/usage` 读取 **5 小时 / 每周 / 每月**三个窗口的额度占用百分比与重置倒计时
  - **本地统计** —— 会话数、累计花费、输入 / 输出 / 推理 / 缓存 token、**缓存命中率**、按模型分布、最近会话、**今日用量**
  - **四种数字单位** —— 默认 / 万 / 千万 / 亿，一键切换
- **WebUI（默认开启）** —— 在浏览器里**启动 / 停止** OpenCode、跑体检、看用量
- **窗口尺寸与位置记忆** —— 关掉再打开，还在原来的位置和大小
- **日志导出** —— 把检测与启动的完整记录一键存成 txt
- **深色 / 浅色主题** + **中文 / English** —— 首次启动跟随系统，标题栏一键切换并记住
- **零系统污染** —— 不写注册表、不改系统代理、不影响其它程序

> 关于三个窗口：官方文档说明 *"Each model has the following usage limits: 5-hour — 20% of the monthly limit; weekly — 50%; and monthly — 100%"*，即它们是**同一个月额度在三个尺度上的占用比例**，且**按模型**分别计算。

## 界面

主窗口分为三个标签页：

- **设置** —— 代理地址、OpenCode 路径、直连绕过（NO_PROXY）、WebUI 开关与端口、「启动后自动关闭」
- **用量** —— 账户额度（5 小时 / 每周 / 每月 + 重置倒计时）与本地统计（会话数、花费、各类 token、缓存命中率、按模型分布、最近会话、今日用量），右上角可切换 **默认 / 万 / 千万 / 亿** 四种数字单位
- **日志** —— 代理检测与启动过程的完整输出

底部常驻四个按钮（启动 OpenCode / 检测代理 / 保存设置 / 清空日志）与状态指示点。

标题栏右侧可切换**语言**（中 / EN）与**主题**（深色 / 浅色），首次启动跟随系统。

## WebUI

**默认开启。** 端口默认 `8710`，点「打开」即可在浏览器里操作；不需要的话取消勾选即可。

打开后可以：

- **启动 OpenCode** —— 效果和界面里点按钮完全一致（同样带代理环境变量 + 存活确认）
- **停止 OpenCode** —— 先请求关闭窗口，2 秒后仍未退出才结束进程
- **检测代理** —— 直接看到经代理的出口 IP 与国家
- **查看用量** —— 账户额度与本地统计，并支持 **默认 / 万 / 千万 / 亿** 四种单位切换

**安全**：只监听 `127.0.0.1`；所有 `/api/*` 请求都必须带 token，而 token 只出现在服务端渲染的页面里 —— 跨站页面既读不到页面内容也拿不到 token，因此可以防住 CSRF。

## 快速开始

1. 到 [Releases](https://github.com/VeloxLLM/opencode-proxy-launcher/releases) 下载 `OpenCode-ProxyLauncher.exe`
2. 双击运行
3. 在「设置」页确认「代理地址」（默认 `http://127.0.0.1:2080`）和「OpenCode」路径
4. 点 **检测代理**，确认出口 IP **不是**中国
5. 点 **启动 OpenCode**

> Release 里提供的是 **单文件 · 自包含** 版本 —— 只有一个 exe，**不需要安装 .NET 运行时**。

## 原理

核心只有几行：用 `UseShellExecute = false` 创建进程，并把代理写进子进程的环境变量表。

```csharp
var startInfo = new ProcessStartInfo(settings.OpenCodePath)
{
    UseShellExecute = false,   // 必须为 false，EnvironmentVariables 才生效
};

startInfo.EnvironmentVariables["HTTP_PROXY"]  = proxy;
startInfo.EnvironmentVariables["HTTPS_PROXY"] = proxy;
startInfo.EnvironmentVariables["ALL_PROXY"]   = proxy;
startInfo.EnvironmentVariables["NO_PROXY"]    = settings.NoProxy;

Process.Start(startInfo);
```

Windows 的环境变量键名**大小写不敏感**，所以这会覆盖掉从父进程继承来的任何旧值 —— 这正是本工具可靠的原因。

## 项目结构

```
opencode-proxy-launcher/
├── OpenCodeProxyLauncher.csproj   net10.0-windows + WPF
├── app.ico                        应用图标
├── App.xaml / App.xaml.cs         应用入口
├── MainWindow.xaml / .xaml.cs     主界面（设置 / 用量 / 日志 三个标签页）
├── Theme.xaml                     样式（颜色全部走 DynamicResource）
├── ThemeLight.xaml / ThemeDark.xaml   两套调色板
├── ThemeManager.cs                运行时切换主题 / 跟随系统
├── Strings.cs                     中英文文案表
├── Units.cs                       数字单位（默认 / 万 / 千万 / 亿）
├── WebUiServer.cs                 内置 WebUI（TcpListener 实现的迷你 HTTP 服务）
├── UsageReader.cs                 读取本地用量统计（message + session 表）
├── AccountUsageReader.cs          调用官方接口读取账户额度
├── Sqlite.cs                      winsqlite3.dll 的只读 P/Invoke 封装
├── AppSettings.cs                 设置持久化 + 路径自动探测
├── Launcher.cs                    注入环境变量并启动 OpenCode
├── ProxyChecker.cs                体检逻辑
├── ai.md                          给 AI 的工作交接文档（中文）
```

## 配置文件

`%APPDATA%\OpenCodeProxyLauncher\settings.json`

```json
{
  "ProxyUrl": "http://127.0.0.1:2080",
  "OpenCodePath": "C:\\Users\\you\\AppData\\Local\\Programs\\@opencode-aidesktop\\OpenCode.exe",
  "NoProxy": "127.0.0.1,localhost,::1",
  "CloseAfterLaunch": false,
  "DarkMode": null,
  "Language": null,
  "WebUiEnabled": true,
  "WebUiPort": 8710,
  "WebUiToken": "（自动生成）",
  "NumberUnit": "raw"
}
```

`DarkMode` / `Language` 为 `null` 表示跟随系统；`NumberUnit` 取 `raw` / `wan` / `qianwan` / `yi`。

## 从源码构建

```bash
git clone https://github.com/VeloxLLM/opencode-proxy-launcher.git
cd opencode-proxy-launcher

# ① 单文件 · 自包含（推荐分发）—— 只有一个 exe，目标机器无需装 .NET，约 62MB
dotnet publish -c Release -r win-x64 \
  -p:SelfContained=true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none -p:DebugSymbols=false -o dist

# ② 单文件 · 依赖运行时 —— 约 200KB，但目标机器需装 .NET 10 桌面运行时
dotnet publish -c Release -r win-x64 \
  -p:SelfContained=false -p:PublishSingleFile=true \
  -p:DebugType=none -p:DebugSymbols=false -o dist
```

| 方式 | 产物 | 体积 | 需要装 .NET |
| --- | --- | --- | --- |
| ① 自包含 | 单个 exe | ~62 MB | ❌ 不需要 |
| ② 依赖运行时 | 单个 exe | ~200 KB | ✅ 需要 .NET 10 桌面运行时 |

**两个容易踩的坑：**

1. `--self-contained false` 这种命令行写法在 .NET 10 下会被忽略、照样产出自包含的大包，必须写成 `-p:SelfContained=false`。
2. WPF 的原生库（`wpfgfx_cor3.dll`、`PresentationNative_cor3.dll` 等）**默认不会被塞进单文件**，不加 `IncludeNativeLibrariesForSelfExtract=true` 就会在 exe 旁边多出 5 个 dll。

## OpenCode 版本兼容性

- **已实测验证**：OpenCode 桌面版 **v1.18.35**（官方仓库 [`anomalyco/opencode`](https://github.com/anomalyco/opencode) 目前最新的正式 release，2026-10-06）
  - 本机安装路径：`%LOCALAPPDATA%\Programs\@opencode-aidesktop\OpenCode.exe`
- **关于 OpenCode v2**：官网已预告 v2，但截至目前官方仓库还没有 v2 的正式 release tag。
  本启动器只做一件事 —— **创建进程时注入环境变量**，与 OpenCode 自身的版本无关；
  因此 v1 / v2 通用，只要桌面版仍然是"启动时读环境变量"这个机制。
  若 v2 换了安装目录，用界面里的「自动检测」或「浏览...」重新指定即可。

## 常见问题

**Q：为什么不直接做 DLL 注入？**

环境变量是进程**启动时**读取的，而且 OpenCode 的后端 sidecar 是 Electron 用 `utilityProcess.fork()` 起的**独立子进程**。事后往主进程注入 DLL，改不了已经启动的进程环境，更管不到子进程 —— 注入成功也没用。

**Q：我已经设了系统/用户环境变量，还需要这个工具吗？**

如果你的启动方式能拿到那些变量，就不需要。但 Windows 上真正创建进程的宿主经常持有**过期环境**，这正是踩坑的根源。

**Q：会影响其它程序吗？**

不会。只对 OpenCode 这一个子进程设置环境变量，不写注册表、不改系统代理。

**Q：支持 SOCKS5 吗？**

当前填写的是 HTTP 代理地址。Clash / Throne / v2rayN 等客户端的 mixed 或 HTTP 端口都可以直接用。

**Q：启动后还是报地区限制？**

先点「检测代理」，重点看**出口 IP 的国家**。如果显示中国，说明代理本身没在正常工作。

**Q：点了「启动 OpenCode」但窗口没出来？**

启动器会实测 3.5 秒确认进程存活，立刻退出会明确报错。真遇到这种情况，最可能是**启动器本身被别的程序以受限方式拉起**（比如从沙箱里启动）。解决办法：关闭窗口，**直接在资源管理器里双击 exe**。

**Q：用量数据是从哪来的？**

- **账户额度**：官方接口 `GET https://opencode.ai/zen/go/v1/usage`，用 `~/.local/share/opencode/auth.json` 里 `opencode-go` 的 key 鉴权。
- **本地统计**：只读 `~/.local/share/opencode/opencode.db`。累计与今日用量取自 **`message` 表**（逐条消息）—— 注意用 `session.time_created` 会把"昨天创建、今天还在用"的会话算到昨天，导致今日恒为 0。

## 已知限制

- 仅支持 Windows
- 只负责启动 OpenCode **桌面版（GUI）**，不含 CLI / TUI
- 体检依赖 `ip-api.com`（免费版仅 HTTP）
- 用量统计依赖系统自带的 `winsqlite3.dll`（Windows 10+ 均有）

## 路线图 / Roadmap

- [ ] **支持 macOS** —— 环境变量注入逻辑一致，需要把 WPF 界面换成 Avalonia 或 MAUI，并处理 `.app` 包启动方式
- [ ] **支持 Linux** —— 同上；另外代理环境变量、`winsqlite3.dll` 那套要换成系统的 SQLite

## 致谢

本项目由 **DeepSeek V4.1 Flash** 与作者结对完成 —— 从逆向 OpenCode 的代理机制、定位"启动宿主环境过期"这个隐蔽的坑，到 WPF 界面、WebUI、用量面板与本文档，全程由 AI 协作产出。

## 许可

MIT

---

# English

## What it solves

The OpenCode desktop app has **no built-in proxy setting**. It reads the proxy from environment variables **only at process start** — and both the main process and the backend sidecar it forks read it once each:

```js
// Inside the OpenCode desktop app (main process and sidecar, same logic)
function useEnvProxy() {
  http.setGlobalProxyFromEnv();   // reads HTTP_PROXY / HTTPS_PROXY / NO_PROXY
}
```

The trap is *who launches it*. The process that creates OpenCode may be Explorer, a Start-menu host, an old terminal, or an IDE — these **often carry a stale environment**, or one polluted by another program's proxy variables. OpenCode inherits that wrong environment and sends requests directly.

For region-restricted models (e.g. some `opencode-go` models) the symptom is unmistakable:

```
AI_APICallError: This model is not available in your country
```

This tool does the obvious thing: **it sets the environment variables itself, then creates the OpenCode process**. No dependency on any host process, and no system-wide changes.

## Features

- **One-click launch** — starts OpenCode with the right proxy env vars, affecting only that child process
- **Post-launch liveness check** — waits 3.5 s and reports clearly if the process died immediately instead of pretending success
- **Proxy health check** — port listening / **exit IP and country through the proxy** / HTTPS reachability / app path
- **Usage panel (in both the GUI and the WebUI)**
  - **Account quota** — via the official endpoint `GET https://opencode.ai/zen/go/v1/usage`: percent used and reset countdown for the **5-hour / weekly / monthly** windows
  - **Local stats** — sessions, total cost, input / output / reasoning / cache tokens, **cache hit rate**, per-model breakdown, recent sessions, **today's usage**
  - **Four number units** — Raw / 万 (10⁴) / 千万 (10⁷) / 亿 (10⁸), one click to switch
- **WebUI (on by default)** — **launch / stop** OpenCode, run the health check and view usage from a browser
- **Window size & position memory** — reopens where you left it
- **Log export** — save the full test/launch record to a txt file with one click
- **Dark / light theme** + **Chinese / English** — follows the system on first run, toggle in the title bar, remembered
- **Zero system footprint** — no registry writes, no system proxy changes, no effect on other programs

> About the three windows: the official docs state *"Each model has the following usage limits: 5-hour — 20% of the monthly limit; weekly — 50%; and monthly — 100%."* They are **the same monthly allowance measured over three horizons**, computed **per model**.

## UI

The main window has three tabs:

- **Settings** — proxy URL, OpenCode path, no-proxy list, WebUI toggle and port, "close after launch"
- **Usage** — account quota (5-hour / weekly / monthly + reset countdown) and local stats (sessions, cost, token types, cache hit rate, per-model breakdown, recent sessions, today), with **Raw / 10⁴ / 10⁷ / 10⁸** unit switching in the top-right
- **Log** — full output of proxy tests and launches

Four buttons (Launch OpenCode / Test proxy / Save settings / Clear log) and the status dot are always pinned at the bottom.

The title bar toggles **language** (中 / EN) and **theme** (dark / light), following the system on first run.

## WebUI

**On by default.** Default port is `8710`; click "Open" to use it in a browser. Uncheck to turn it off.

From the page you can:

- **Launch OpenCode** — identical to the button in the app (same env vars + liveness check)
- **Stop OpenCode** — asks the windows to close first; terminates the processes after 2 s if they're still up
- **Test the proxy** — see the exit IP and country through the proxy
- **View usage** — account quota and local stats, with the **Raw / 万 / 千万 / 亿** unit switch

**Security**: it binds to `127.0.0.1` only. Every `/api/*` request requires a token, and the token is only rendered into the server-side page — a cross-origin page can read neither the page nor the token, which blocks CSRF.

## Quick start

1. Download `OpenCode-ProxyLauncher.exe` from [Releases](https://github.com/VeloxLLM/opencode-proxy-launcher/releases)
2. Double-click it
3. On the **Settings** tab, confirm **Proxy URL** (default `http://127.0.0.1:2080`) and the OpenCode path
4. Click **Test proxy** and make sure the exit country is **not** China
5. Click **Launch OpenCode**

> The release build is **single-file and self-contained** — one exe, **no .NET runtime required**.

## How it works

The core is a few lines: create the process with `UseShellExecute = false` and write the proxy into the child's environment block.

```csharp
var startInfo = new ProcessStartInfo(settings.OpenCodePath)
{
    UseShellExecute = false,   // must be false for EnvironmentVariables to apply
};

startInfo.EnvironmentVariables["HTTP_PROXY"]  = proxy;
startInfo.EnvironmentVariables["HTTPS_PROXY"] = proxy;
startInfo.EnvironmentVariables["ALL_PROXY"]   = proxy;
startInfo.EnvironmentVariables["NO_PROXY"]    = settings.NoProxy;

Process.Start(startInfo);
```

Windows environment variable names are **case-insensitive**, so this overrides any stale value inherited from the parent — that is exactly why this approach is reliable.

## Project layout

```
opencode-proxy-launcher/
├── OpenCodeProxyLauncher.csproj   net10.0-windows + WPF
├── app.ico                        application icon
├── App.xaml / App.xaml.cs         entry point
├── MainWindow.xaml / .xaml.cs     main window (Settings / Usage / Log tabs)
├── Theme.xaml                     styles (all colors via DynamicResource)
├── ThemeLight.xaml / ThemeDark.xaml   two palettes
├── ThemeManager.cs                runtime theme switching / follow system
├── Strings.cs                     Chinese + English string table
├── Units.cs                       number units (raw / 万 / 千万 / 亿)
├── WebUiServer.cs                 built-in WebUI (mini HTTP over TcpListener)
├── UsageReader.cs                 reads local usage (message + session tables)
├── AccountUsageReader.cs          calls the official quota endpoint
├── Sqlite.cs                      read-only P/Invoke wrapper over winsqlite3.dll
├── AppSettings.cs                 settings persistence + app path detection
├── Launcher.cs                    injects env vars and starts OpenCode
├── ProxyChecker.cs                proxy health check
├── ai.md                          AI handover document (Chinese)
```

## Configuration

`%APPDATA%\OpenCodeProxyLauncher\settings.json`

```json
{
  "ProxyUrl": "http://127.0.0.1:2080",
  "OpenCodePath": "C:\\Users\\you\\AppData\\Local\\Programs\\@opencode-aidesktop\\OpenCode.exe",
  "NoProxy": "127.0.0.1,localhost,::1",
  "CloseAfterLaunch": false,
  "DarkMode": null,
  "Language": null,
  "WebUiEnabled": true,
  "WebUiPort": 8710,
  "WebUiToken": "(auto-generated)",
  "NumberUnit": "raw"
}
```

`DarkMode` / `Language` set to `null` means "follow the system"; `NumberUnit` is one of `raw` / `wan` / `qianwan` / `yi`.

## Build from source

```bash
git clone https://github.com/VeloxLLM/opencode-proxy-launcher.git
cd opencode-proxy-launcher

# (1) Single-file, self-contained (recommended) — one exe, no .NET needed, ~62 MB
dotnet publish -c Release -r win-x64 \
  -p:SelfContained=true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none -p:DebugSymbols=false -o dist

# (2) Single-file, framework-dependent — ~200 KB, needs .NET 10 Desktop Runtime
dotnet publish -c Release -r win-x64 \
  -p:SelfContained=false -p:PublishSingleFile=true \
  -p:DebugType=none -p:DebugSymbols=false -o dist
```

| Variant | Output | Size | Needs .NET |
| --- | --- | --- | --- |
| (1) Self-contained | one exe | ~62 MB | ❌ No |
| (2) Framework-dependent | one exe | ~200 KB | ✅ .NET 10 Desktop Runtime |

**Two easy-to-hit pitfalls:**

1. `--self-contained false` on the command line is *ignored* by .NET 10 and still produces a huge self-contained bundle. Use `-p:SelfContained=false`.
2. WPF native libraries (`wpfgfx_cor3.dll`, `PresentationNative_cor3.dll`, …) are **not bundled into the single file by default** — without `IncludeNativeLibrariesForSelfExtract=true` you get 5 extra DLLs next to the exe.

## OpenCode version compatibility

- **Verified against**: OpenCode desktop **v1.18.35** — the latest official release in
  [`anomalyco/opencode`](https://github.com/anomalyco/opencode) (2026-10-06)
  - Default install path: `%LOCALAPPDATA%\Programs\@opencode-aidesktop\OpenCode.exe`
- **About OpenCode v2**: v2 has been announced on the website, but there is no official v2 release tag yet.
  This launcher only does one thing — **injects env vars when creating the process** — which is
  independent of OpenCode's own version, so it works with both v1 and v2 as long as the desktop app
  still reads environment variables at startup. If v2 moves the install location, use
  "Auto detect" or "Browse…" to point at the new path.

## FAQ

**Q: Why not just inject a DLL?**

Environment variables are read **at process start**, and OpenCode's backend sidecar is a **separate child process** forked via Electron's `utilityProcess.fork()`. Injecting into the main process afterwards cannot change an already-running process's environment, let alone the child's.

**Q: I already set system/user environment variables — do I still need this?**

If whatever launches OpenCode can see them, no. But the process that actually creates the child on Windows often carries a **stale environment**, which is the root of the problem.

**Q: Does it affect other programs?**

No. The environment variables are set only for this one child process. No registry writes, no system proxy changes.

**Q: Does it support SOCKS5?**

The field takes an HTTP proxy URL. Clash / Throne / v2rayN mixed or HTTP ports work as-is.

**Q: Still getting the region error after launching?**

Click **Test proxy** and check the **exit country**. If it says China, the proxy itself isn't working.

**Q: I clicked "Launch OpenCode" but no window appeared.**

The launcher waits 3.5 s and reports clearly if the process died. When this happens, the most likely cause is that **the launcher itself was started by another program in a restricted context** (e.g. from a sandbox). Close the window and **double-click the exe from File Explorer** instead.

**Q: Where does the usage data come from?**

- **Account quota**: the official endpoint `GET https://opencode.ai/zen/go/v1/usage`, authenticated with the `opencode-go` key from `~/.local/share/opencode/auth.json`.
- **Local stats**: read-only access to `~/.local/share/opencode/opencode.db`. Totals and today's usage come from the **`message` table** (per-message records) — using `session.time_created` would count a session created yesterday but still active today as "yesterday", making today's usage always 0.

## Known limitations

- Windows only
- Only launches the OpenCode **desktop app (GUI)**, not the CLI / TUI
- The health check relies on `ip-api.com` (HTTP only on the free tier)
- Usage stats rely on the system-provided `winsqlite3.dll` (present on Windows 10+)

## Roadmap

- [ ] **macOS support** — the env-var injection logic is identical; needs the WPF UI ported to Avalonia or MAUI, plus `.app` bundle launching
- [ ] **Linux support** — same as above; also replace the `winsqlite3.dll` approach with the system SQLite

## Credits

Built by **DeepSeek V4.1 Flash** together with the author — from reverse-engineering OpenCode's proxy mechanism and pinpointing the subtle "stale launcher environment" trap, all the way to the WPF UI, the WebUI, the usage panel and this document.

## License

MIT
