using System.Collections.Generic;

namespace OpenCodeProxyLauncher
{
    /// <summary>
    /// 极简本地化：key => [中文, English]。
    /// 界面文案统一走 Strings.T("key")，切换语言后由界面重新取一遍即可。
    /// </summary>
    internal static class Strings
    {
        public const string Chinese = "zh";
        public const string English = "en";

        public static string Language { get; private set; } = Chinese;

        public static bool IsEnglish
        {
            get { return Language == English; }
        }

        public static void SetLanguage(string language)
        {
            Language = string.Equals(language, English, System.StringComparison.OrdinalIgnoreCase)
                ? English
                : Chinese;
        }

        public static string T(string key)
        {
            string[] pair;
            if (Table.TryGetValue(key, out pair))
            {
                return IsEnglish ? pair[1] : pair[0];
            }

            return key;
        }

        public static string T(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // ---- 窗口 / 标题栏 ----
            { "app.title",              new[] { "OpenCode 代理启动器", "OpenCode Proxy Launcher" } },
            { "tip.theme",              new[] { "切换深色 / 浅色", "Toggle dark / light" } },
            { "tip.language",           new[] { "切换中文 / English", "Switch Chinese / English" } },
            { "tip.min",                new[] { "最小化", "Minimize" } },
            { "tip.close",              new[] { "关闭", "Close" } },

            // ---- 标签页 ----
            { "tab.settings",           new[] { "设置", "Settings" } },
            { "tab.usage",              new[] { "用量", "Usage" } },
            { "tab.log",                new[] { "日志", "Log" } },
            { "log.hint",               new[] { "这里记录代理检测与启动 OpenCode 的详细过程。",
                                                "Detailed output of proxy tests and OpenCode launches." } },

            // ---- 设置卡片 ----
            { "card.settings",          new[] { "代理设置", "Proxy settings" } },
            { "card.settings.sub",      new[] { "OpenCode 只在进程启动时读取代理环境变量，这里负责把它喂对。",
                                                "OpenCode reads proxy env vars only at process start — this makes sure they are correct." } },
            { "field.proxy",            new[] { "代理地址", "Proxy URL" } },
            { "field.path",             new[] { "OpenCode", "OpenCode" } },
            { "field.noproxy",          new[] { "直连绕过", "No proxy" } },
            { "btn.browse",             new[] { "浏览...", "Browse..." } },
            { "btn.autodetect",         new[] { "自动检测", "Auto detect" } },

            // ---- WebUI ----
            { "webui.enable",           new[] { "启用 WebUI", "Enable WebUI" } },
            { "webui.port",             new[] { "端口", "Port" } },
            { "webui.open",             new[] { "打开", "Open" } },
            { "webui.hint",             new[] { "默认开启。可在浏览器里启动本机 OpenCode（仅监听 127.0.0.1）。",
                                                "On by default. You can launch the local OpenCode from a browser (binds to 127.0.0.1 only)." } },
            { "webui.running",          new[] { "运行中：{0}", "Running: {0}" } },
            { "webui.startFail",        new[] { "WebUI 启动失败：{0}", "Failed to start WebUI: {0}" } },
            { "webui.portInvalid",      new[] { "端口不合法（1-65535）", "Invalid port (1-65535)" } },
            { "webui.openFail",         new[] { "无法打开浏览器：{0}", "Cannot open browser: {0}" } },

            // ---- 按钮 / 复选框 ----
            { "chk.closeAfter",         new[] { "启动 OpenCode 后自动关闭本窗口", "Close this window after launching OpenCode" } },
            { "btn.launch",             new[] { "启动 OpenCode", "Launch OpenCode" } },
            { "btn.check",              new[] { "检测代理", "Test proxy" } },
            { "btn.save",               new[] { "保存设置", "Save settings" } },
            { "btn.clear",              new[] { "清空日志", "Clear log" } },

            // ---- 状态行 ----
            { "status.ready",           new[] { "就绪", "Ready" } },
            { "status.checking",        new[] { "正在检测...", "Testing..." } },
            { "status.saved",           new[] { "设置已保存", "Settings saved" } },
            { "status.found",           new[] { "已找到 OpenCode", "OpenCode found" } },
            { "status.notFound",        new[] { "未找到 OpenCode.exe", "OpenCode.exe not found" } },
            { "status.verifying",       new[] { "正在确认 OpenCode 是否稳定运行...", "Verifying OpenCode is running..." } },
            { "status.launched",        new[] { "已启动", "Launched" } },
            { "status.launchFailed",    new[] { "启动失败", "Launch failed" } },
            { "status.exited",          new[] { "OpenCode 启动后立即退出", "OpenCode exited immediately" } },
            { "status.checkFailed",     new[] { "检测异常", "Test failed" } },

            // ---- 日志 ----
            { "log.welcome1",           new[] { "就绪。建议先点「检测代理」，确认出口 IP 不是中国，再启动 OpenCode。",
                                                "Ready. Run \"Test proxy\" first to confirm the exit IP is not in China, then launch OpenCode." } },
            { "log.settingsFile",       new[] { "设置文件：{0}", "Settings file: {0}" } },
            { "log.detected",           new[] { "自动检测到 OpenCode.exe：{0}", "Auto-detected OpenCode.exe: {0}" } },
            { "log.notDetected",        new[] { "没有在默认位置找到 OpenCode.exe，请点「浏览...」手动选择。",
                                                "OpenCode.exe was not found in the default location — please pick it manually." } },
            { "log.saved",              new[] { "设置已保存到 {0}", "Settings saved to {0}" } },
            { "log.checkStart",         new[] { "=== {0} 开始检测 ===", "=== {0} proxy test started ===" } },
            { "log.conclusion",         new[] { "结论：{0}", "Result: {0}" } },
            { "log.checkError",         new[] { "检测异常：{0}", "Test error: {0}" } },
            { "log.exited",             new[] { "✗ OpenCode 启动后立刻退出了（退出码 {0}），说明它没能真正跑起来。",
                                                "✗ OpenCode exited immediately after start (exit code {0}) — it did not really run." } },
            { "log.exitedHint1",        new[] { "  常见原因：启动它的进程上下文受限（例如由别的程序以受限方式拉起）。",
                                                "  Common cause: the launching process runs in a restricted context." } },
            { "log.exitedHint2",        new[] { "  请关闭本窗口，直接在资源管理器里双击本启动器再试一次。",
                                                "  Close this window and double-click the launcher in File Explorer instead." } },
            { "log.stable",             new[] { "✓ OpenCode 已稳定运行。", "✓ OpenCode is running." } },
            { "log.webuiStarted",       new[] { "WebUI 已启用：{0}", "WebUI enabled: {0}" } },
            { "log.webuiStopped",       new[] { "WebUI 已停止。", "WebUI stopped." } },
            { "log.languageChanged",    new[] { "界面语言已切换为中文。", "Interface language switched to English." } },

            // ---- 弹窗 ----
            { "dlg.launchFailedTitle",  new[] { "启动失败", "Launch failed" } },
            { "dlg.notRunningTitle",    new[] { "启动未成功", "Launch unsuccessful" } },
            { "dlg.notRunningBody",     new[] { "OpenCode 启动后立刻退出了（退出码 {0}）。\n\n它没能真正跑起来。请关闭本窗口，直接在资源管理器里双击本启动器再试一次。",
                                                "OpenCode exited immediately after start (exit code {0}).\n\nIt did not really run. Close this window and double-click the launcher in File Explorer instead." } },

            // ---- 代理体检 ----
            { "check.proxy",            new[] { "代理地址：{0}", "Proxy: {0}" } },
            { "check.badUrl",           new[] { "✗ 代理地址格式不正确：{0}", "✗ Invalid proxy URL: {0}" } },
            { "check.badUrlSummary",    new[] { "代理地址无效", "Invalid proxy URL" } },
            { "check.portOk",           new[] { "✓ 代理端口 {0} 在监听", "✓ Proxy port {0} is listening" } },
            { "check.portFail",         new[] { "✗ 代理端口 {0} 连不上（代理软件没启动？）",
                                                "✗ Cannot reach proxy port {0} (is the proxy client running?)" } },
            { "check.portSummary",      new[] { "代理端口不通", "Proxy port unreachable" } },
            { "check.exit",             new[] { "✓ 经代理出口 IP：{0}（{1} / {2}）", "✓ Exit IP via proxy: {0} ({1} / {2})" } },
            { "check.exitCN",           new[] { "⚠ 出口仍是中国，地区限制可能依然生效",
                                                "⚠ Exit is still in China; region restrictions may still apply" } },
            { "check.ipFail",           new[] { "✗ 经代理访问 ip-api.com 失败：{0}", "✗ Failed to reach ip-api.com via proxy: {0}" } },
            { "check.httpsOk",          new[] { "✓ HTTPS 经代理连通性：HTTP {0}", "✓ HTTPS via proxy: HTTP {0}" } },
            { "check.httpsWarn",       new[] { "⚠ HTTPS 经代理有响应，但状态码 HTTP {0}（隧道是通的，是目标站点拒绝了本次请求）",
                                                "⚠ HTTPS via proxy responded, but with HTTP {0} (the tunnel works; the site rejected this request)" } },
            { "check.httpsFail",        new[] { "✗ HTTPS 经代理失败：{0}", "✗ HTTPS via proxy failed: {0}" } },
            { "check.appOk",            new[] { "✓ 找到 OpenCode.exe：{0}", "✓ OpenCode.exe found: {0}" } },
            { "check.appFail",          new[] { "✗ 找不到 OpenCode.exe：{0}", "✗ OpenCode.exe not found: {0}" } },
            { "check.doneOk",           new[] { "检测通过（出口 {0}）", "Passed (exit {0})" } },
            { "check.doneWarn",         new[] { "检测完成，但有告警", "Completed with warnings" } },

            // ---- 用量 ----
            { "usage.title",            new[] { "OpenCode 用量", "OpenCode usage" } },
            { "usage.account",          new[] { "账户额度", "Account quota" } },
            { "usage.local",            new[] { "本地累计", "Local totals" } },
            { "usage.unit",             new[] { "单位", "Unit" } },
            { "usage.refresh",          new[] { "刷新用量", "Refresh usage" } },
            { "usage.rolling",          new[] { "5 小时", "5-hour" } },
            { "usage.weekly",           new[] { "每周", "Weekly" } },
            { "usage.monthly",          new[] { "每月", "Monthly" } },
            { "usage.windowHint",       new[] { "三个窗口 = 月额度的 20% / 50% / 100%，按模型计算",
                                                "The three windows = 20% / 50% / 100% of the monthly limit, per model" } },
            { "usage.daily",            new[] { "近 7 天", "Last 7 days" } },
            { "usage.dailyEmpty",       new[] { "近 7 天没有用量", "No usage in the last 7 days" } },
            { "usage.cacheHit",         new[] { "缓存命中率", "Cache hit" } },
            { "usage.sessions",         new[] { "会话", "Sessions" } },
            { "usage.messages",         new[] { "消息", "Messages" } },
            { "usage.cost",             new[] { "花费", "Cost" } },
            { "usage.input",            new[] { "输入", "Input" } },
            { "usage.output",           new[] { "输出", "Output" } },
            { "usage.reasoning",        new[] { "推理", "Reasoning" } },
            { "usage.cacheRead",        new[] { "缓存读", "Cache read" } },
            { "usage.today",            new[] { "今日", "Today" } },
            { "usage.loading",          new[] { "正在读取用量...", "Loading usage..." } },
            { "usage.failed",           new[] { "本地用量读取失败：{0}", "Failed to read local usage: {0}" } },
            { "usage.accountFailed",    new[] { "账户额度获取失败：{0}", "Failed to get account quota: {0}" } },
            { "usage.noKey",            new[] { "未找到 opencode-go 的 API key（auth.json）",
                                                "No opencode-go API key found (auth.json)" } },
            { "usage.updated",          new[] { "更新于 {0}", "Updated {0}" } },
            { "usage.dbMissing",        new[] { "找不到 OpenCode 数据库", "OpenCode database not found" } },

            // ---- WebUI 页面 ----
            { "webui.page.title",       new[] { "OpenCode 代理启动器 · WebUI", "OpenCode Proxy Launcher · WebUI" } },
            { "webui.page.proxy",       new[] { "代理地址", "Proxy URL" } },
            { "webui.page.path",        new[] { "OpenCode 路径", "OpenCode path" } },
            { "webui.page.state",       new[] { "运行状态", "Status" } },
            { "webui.page.stateRow",    new[] { "状态", "State" } },
            { "webui.page.running",     new[] { "运行中", "Running" } },
            { "webui.page.stopped",     new[] { "未运行", "Not running" } },
            { "webui.page.launch",      new[] { "启动 OpenCode", "Launch OpenCode" } },
            { "webui.page.stop",        new[] { "停止 OpenCode", "Stop OpenCode" } },
            { "webui.stopped",          new[] { "已停止 {0} 个 OpenCode 进程", "Stopped {0} OpenCode process(es)" } },
            { "webui.stopNone",         new[] { "OpenCode 未在运行", "OpenCode is not running" } },
            { "webui.stopFailed",       new[] { "停止失败：{0}", "Failed to stop: {0}" } },
            { "log.export",             new[] { "导出日志", "Export log" } },
            { "log.exported",           new[] { "日志已导出：{0}", "Log exported: {0}" } },
            { "log.exportFailed",       new[] { "日志导出失败：{0}", "Log export failed: {0}" } },
            { "webui.page.check",       new[] { "检测代理", "Test proxy" } },
            { "webui.page.refresh",     new[] { "刷新状态", "Refresh" } },
            { "webui.page.log",         new[] { "输出", "Output" } },
            { "webui.page.launched",    new[] { "已启动 OpenCode", "OpenCode launched" } },
            { "webui.page.refreshed",   new[] { "状态已刷新", "Status refreshed" } },
            { "webui.page.langHint",    new[] { "界面语言跟随启动器设置", "Language follows the launcher setting" } },
        };
    }
}
