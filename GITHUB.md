# GitHub 仓库信息 / Repository details

> 复制粘贴用。中文在前，英文在后。

## 仓库名 / Repository name

```
opencode-proxy-launcher
```

---

# 中文

## About / Description（填到「Edit repository details → Description」）

```
让 OpenCode 桌面版稳定走本地代理的 Windows 启动器。OpenCode 没有内置代理设置、只在进程启动时读环境变量；本工具自己注入正确的 HTTP_PROXY 后拉起它，并提供代理体检、账户额度与本地用量面板，以及默认开启的 WebUI。不写注册表、不改系统代理。
```

## Topics

```
opencode
proxy
http-proxy
windows
wpf
dotnet
csharp
launcher
webui
usage-stats
china
region-unblock
```

## 最新 Release（v0.4）

- **Tag**：`v0.4`
- **Title**：`v0.4 — 标签页界面 · 5 小时窗口 · 缓存命中率`

**Release 说明：**

```markdown
## OpenCode 代理启动器 v0.4

让 OpenCode 桌面版稳定走本地代理的 Windows 启动器。

### 改动

- **界面改为标签页** —— 设置 / 用量 / 日志 三个页签，窗口从 900px 高降到 600px，日志不再被挤到最下面
- **账户额度窗口标注为「5 小时」** —— 经官方文档确认：5-hour 20%、weekly 50%、monthly 100%（同一月额度的三个尺度）
- **新增「缓存命中率」** —— cache read / (cache read + input)
- **移除「近 7 天」图表**（按反馈）
- 用量数据取自 message 表（逐条消息），「今日」统计准确

### 下载

`OpenCode-ProxyLauncher.exe` —— 单文件自包含，约 62MB，**无需安装 .NET 运行时**，下载后双击即用。

### 路线图

- [ ] 支持 macOS
- [ ] 支持 Linux
```

## 历史 Release

- `v0.3` —— 账户额度接口、用量面板进 GUI、数字单位、修复今日用量恒为 0
- `v0.2` —— 中英文切换、WebUI（默认关闭）、OpenCode 用量面板、README 重写
- `v0.1` —— 首个版本：WPF 图形界面，一键以指定代理启动 OpenCode

---

# English

## About / Description (paste into "Edit repository details → Description")

```
A Windows launcher that makes the OpenCode desktop app use your local proxy. OpenCode reads HTTP_PROXY/HTTPS_PROXY only at process start, so a stale parent environment breaks it. This tool injects the right env vars itself, plus a proxy health check, an account-quota & local-usage panel, and a built-in WebUI.
```

## Bilingual variant (中英双语版，如果描述栏想两种语言都放)

```
让 OpenCode 桌面版稳定走本地代理的 Windows 启动器 ——一键以正确的代理环境变量启动，并提供代理体检、账户额度与本地用量面板，以及默认开启的 WebUI。
A Windows launcher that makes the OpenCode desktop app use your local proxy — injects the right env vars at launch, plus a proxy health check, account quota & usage panel, and a built-in WebUI.
```

## Topics

```
opencode
proxy
http-proxy
windows
wpf
dotnet
csharp
launcher
webui
usage-stats
china
region-unblock
```

## Latest release (v0.4)

- **Tag**: `v0.4`
- **Title**: `v0.4 — Tabbed UI · 5-hour window · cache hit rate`

**Release notes:**

```markdown
## OpenCode Proxy Launcher v0.4

A Windows launcher that makes the OpenCode desktop app use your local proxy.

### Changes

- **Tabbed UI** — Settings / Usage / Log tabs; window height reduced from 900 px to 600 px, so the log is no longer buried at the bottom
- **Account quota window labelled "5 小时" / "5-hour"** — confirmed against the official docs: 5-hour 20%, weekly 50%, monthly 100% of the monthly limit
- **New "cache hit rate"** — cache read / (cache read + input)
- **Removed the "last 7 days" chart**
- Usage data comes from the per-message `message` table, so "today" is accurate

### Download

`OpenCode-ProxyLauncher.exe` — single-file, self-contained, ~62 MB, **no .NET runtime required**.

### Roadmap

- [ ] macOS support
- [ ] Linux support
```

## Previous releases

- `v0.3` — account quota endpoint, usage panel in the GUI, number units, fixed "today" always 0
- `v0.2` — Chinese/English toggle, WebUI (off by default), OpenCode usage panel, README rewrite
- `v0.1` — first release: WPF GUI, launch OpenCode with the right proxy
