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
让 OpenCode 桌面版稳定走本地代理的 Windows 启动器。OpenCode 只在进程启动时读环境变量；本工具自己注入正确的 HTTP_PROXY 后拉起它，并提供代理体检、账户额度与本地用量面板（兼容 OpenCode v1/v2），以及默认开启的 WebUI。不写注册表、不改系统代理。
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
opencode-v2
```

## 最新 Release（v2.0.0）

- **Tag**：`v2.0.0`
- **Title**：`v2.0.0 — OpenCode v2 用量统计兼容`

**Release 说明：**

```markdown
## OpenCode 代理启动器 v2.0.0

让 OpenCode 桌面版稳定走本地代理的 Windows 启动器。

### 改动

- **支持 OpenCode v2 用量数据** —— 自动识别 `session_message` / `session_v2` 与旧版 `message` / `session`
- **适配 v2 消息结构** —— assistant 由 `type` 列判定，provider/model 从 `data.model` 读取
- **保留 v1 兼容** —— 旧版数据库无需迁移，统计口径不变
- **补齐 MIT LICENSE**，版本号统一为 2.0.0

### 下载

`OpenCode-ProxyLauncher.exe` —— 单文件自包含，约 62MB，**无需安装 .NET 运行时**，下载后双击即用。

### 路线图

- [ ] 支持 macOS
- [ ] 支持 Linux
```

## 历史 Release

- `v0.4` —— 标签页界面、5 小时窗口、缓存命中率、窗口位置记忆、日志导出、WebUI 停止 OpenCode
- `v0.3` —— 账户额度接口、用量面板进 GUI、数字单位、修复今日用量恒为 0
- `v0.2` —— 中英文切换、WebUI（默认关闭）、OpenCode 用量面板、README 重写
- `v0.1` —— 首个版本：WPF 图形界面，一键以指定代理启动 OpenCode

---

# English

## About / Description (paste into "Edit repository details → Description")

```
A Windows launcher that makes the OpenCode desktop app use your local proxy. OpenCode reads HTTP_PROXY/HTTPS_PROXY only at process start, so a stale parent environment breaks it. This tool injects the right env vars itself, plus a proxy health check, an account-quota & local-usage panel (OpenCode v1/v2), and a built-in WebUI.
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
opencode-v2
```

## Latest release (v2.0.0)

- **Tag**: `v2.0.0`
- **Title**: `v2.0.0 — OpenCode v2 usage support`

**Release notes:**

```markdown
## OpenCode Proxy Launcher v2.0.0

A Windows launcher that makes the OpenCode desktop app use your local proxy.

### Changes

- **OpenCode v2 usage data** — auto-detects `session_message` / `session_v2` versus legacy `message` / `session`
- **v2 message layout** — assistant messages identified by the `type` column; provider/model read from `data.model`
- **v1 still supported** — older databases need no migration and keep the same accounting
- **MIT LICENSE added**, version aligned to 2.0.0

### Download

`OpenCode-ProxyLauncher.exe` — single-file, self-contained, ~62 MB, **no .NET runtime required**.

### Roadmap

- [ ] macOS support
- [ ] Linux support
```

## Previous releases

- `v0.4` — tabbed UI, 5-hour window, cache hit rate, window position memory, log export, WebUI stop
- `v0.3` — account quota endpoint, usage panel in the GUI, number units, fixed "today" always 0
- `v0.2` — Chinese/English toggle, WebUI (off by default), OpenCode usage panel, README rewrite
- `v0.1` — first release: WPF GUI, launch OpenCode with the right proxy
