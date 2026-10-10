using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCodeProxyLauncher
{
    internal sealed class WebUiStatus
    {
        public string ProxyUrl { get; set; }

        public string OpenCodePath { get; set; }

        public bool OpenCodeRunning { get; set; }

        public string Language { get; set; }

        public string Version { get; set; }
    }

    internal sealed class WebUiAction
    {
        public bool Ok { get; set; }

        public string Message { get; set; }

        public List<string> Lines { get; set; }

        public WebUiAction()
        {
            Lines = new List<string>();
        }
    }

    /// <summary>/api/usage 的返回体：本地用量 + 账户额度。</summary>
    internal sealed class UsagePayload
    {
        public UsageReport Local { get; set; }

        public AccountUsage Account { get; set; }
    }

    /// <summary>WebUI 需要宿主（主窗口）提供的能力。</summary>
    internal interface IWebUiHost
    {
        WebUiStatus GetStatus();

        UsageReport GetUsage();

        Task<AccountUsage> GetAccountUsageAsync();

        WebUiAction LaunchOpenCode();

        WebUiAction StopOpenCode();

        Task<WebUiAction> CheckProxyAsync();
    }

    /// <summary>
    /// 内置 WebUI：一个只监听 127.0.0.1 的迷你 HTTP 服务。
    ///
    /// 用 TcpListener 自己实现极简 HTTP，不依赖 HttpListener（后者需要 URL ACL / 管理员权限）。
    /// 所有 /api/* 请求都必须带 token，token 只出现在服务端渲染的页面里，
    /// 因此跨站页面拿不到它，可以防住 CSRF。
    /// </summary>
    internal sealed class WebUiServer : IDisposable
    {
        private readonly int _port;
        private readonly string _token;
        private readonly IWebUiHost _host;

        private TcpListener _listener;
        private CancellationTokenSource _cancellation;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public WebUiServer(int port, string token, IWebUiHost host)
        {
            _port = port;
            _token = token;
            _host = host;
        }

        public bool IsRunning { get; private set; }

        public string Url
        {
            get { return "http://127.0.0.1:" + _port + "/?token=" + _token; }
        }

        public string PlainUrl
        {
            get { return "http://127.0.0.1:" + _port + "/"; }
        }

        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            _cancellation = new CancellationTokenSource();
            IsRunning = true;

            Task.Run(() => AcceptLoopAsync(_cancellation.Token));
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;

            try
            {
                if (_cancellation != null)
                {
                    _cancellation.Cancel();
                }
            }
            catch
            {
                // 忽略
            }

            try
            {
                if (_listener != null)
                {
                    _listener.Stop();
                }
            }
            catch
            {
                // 忽略
            }

            _listener = null;
        }

        public void Dispose()
        {
            Stop();
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    client = await _listener.AcceptTcpClientAsync();
                }
                catch
                {
                    return; // 监听器已停止
                }

                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    string request = await ReadHeadersAsync(stream);
                    if (request == null)
                    {
                        return;
                    }

                    string[] lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    string[] requestLine = lines[0].Split(' ');
                    if (requestLine.Length < 2)
                    {
                        await WriteAsync(stream, 400, "text/plain; charset=utf-8", "bad request");
                        return;
                    }

                    string method = requestLine[0].ToUpperInvariant();
                    string path = requestLine[1];
                    string query = "";

                    int question = path.IndexOf('?');
                    if (question >= 0)
                    {
                        query = path.Substring(question + 1);
                        path = path.Substring(0, question);
                    }

                    string headerToken = null;
                    for (int i = 1; i < lines.Length; i++)
                    {
                        int colon = lines[i].IndexOf(':');
                        if (colon > 0 && lines[i].Substring(0, colon).Trim().Equals("X-Token", StringComparison.OrdinalIgnoreCase))
                        {
                            headerToken = lines[i].Substring(colon + 1).Trim();
                        }
                    }

                    string suppliedToken = headerToken ?? GetQueryValue(query, "token");

                    // 页面本身不需要 token（它由服务端渲染，里面已内嵌 token）
                    if (path == "/" || path == "/index.html")
                    {
                        await WriteAsync(stream, 200, "text/html; charset=utf-8", BuildPage());
                        return;
                    }

                    if (!path.StartsWith("/api/", StringComparison.Ordinal))
                    {
                        await WriteAsync(stream, 404, "text/plain; charset=utf-8", "not found");
                        return;
                    }

                    if (!string.Equals(suppliedToken, _token, StringComparison.Ordinal))
                    {
                        await WriteAsync(stream, 403, "application/json; charset=utf-8", "{\"ok\":false,\"error\":\"forbidden\"}");
                        return;
                    }

                    await HandleApiAsync(stream, method, path);
                }
            }
            catch
            {
                // 单个连接出错不影响服务
            }
        }

        private async Task HandleApiAsync(NetworkStream stream, string method, string path)
        {
            string json;

            if (path == "/api/status")
            {
                json = JsonSerializer.Serialize(_host.GetStatus(), JsonOptions);
            }
            else if (path == "/api/usage")
            {
                var payload = new UsagePayload
                {
                    Local = _host.GetUsage(),
                    Account = await _host.GetAccountUsageAsync(),
                };

                json = JsonSerializer.Serialize(payload, JsonOptions);
            }
            else if (path == "/api/launch" && method == "POST")
            {
                json = JsonSerializer.Serialize(_host.LaunchOpenCode(), JsonOptions);
            }
            else if (path == "/api/stop" && method == "POST")
            {
                json = JsonSerializer.Serialize(_host.StopOpenCode(), JsonOptions);
            }
            else if (path == "/api/check" && method == "POST")
            {
                WebUiAction action = await _host.CheckProxyAsync();
                json = JsonSerializer.Serialize(action, JsonOptions);
            }
            else
            {
                await WriteAsync(stream, 404, "application/json; charset=utf-8", "{\"ok\":false,\"error\":\"not-found\"}");
                return;
            }

            await WriteAsync(stream, 200, "application/json; charset=utf-8", json);
        }

        private static async Task<string> ReadHeadersAsync(NetworkStream stream)
        {
            var builder = new StringBuilder();
            var buffer = new byte[4096];

            while (builder.Length < 64 * 1024)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    break;
                }

                builder.Append(Encoding.UTF8.GetString(buffer, 0, read));

                if (builder.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal) >= 0)
                {
                    return builder.ToString();
                }
            }

            return builder.Length > 0 ? builder.ToString() : null;
        }

        private static async Task WriteAsync(NetworkStream stream, int status, string contentType, string body)
        {
            byte[] payload = Encoding.UTF8.GetBytes(body);

            var head = new StringBuilder();
            head.Append("HTTP/1.1 ").Append(status).Append(status == 200 ? " OK" : " Error").Append("\r\n");
            head.Append("Content-Type: ").Append(contentType).Append("\r\n");
            head.Append("Content-Length: ").Append(payload.Length).Append("\r\n");
            head.Append("Cache-Control: no-store\r\n");
            head.Append("Connection: close\r\n\r\n");

            byte[] headerBytes = Encoding.ASCII.GetBytes(head.ToString());

            await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
            await stream.WriteAsync(payload, 0, payload.Length);
            await stream.FlushAsync();
        }

        private static string GetQueryValue(string query, string key)
        {
            foreach (string pair in query.Split('&'))
            {
                int equals = pair.IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }

                if (pair.Substring(0, equals).Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(pair.Substring(equals + 1));
                }
            }

            return null;
        }

        private string BuildPage()
        {
            string lang = Strings.Language;

            return PageTemplate
                .Replace("__LANG__", lang)
                .Replace("__TITLE__", Strings.T("app.title"))
                .Replace("__SUBTITLE__", "v" + (_host.GetStatus().Version ?? "") + " · " + Strings.T("webui.page.langHint"))
                .Replace("__TOKEN__", _token)
                .Replace("__LBL_STATUS__", Strings.T("webui.page.state"))
                .Replace("__LBL_STATE__", Strings.T("webui.page.stateRow"))
                .Replace("__LBL_PROXY__", Strings.T("webui.page.proxy"))
                .Replace("__LBL_PATH__", Strings.T("webui.page.path"))
                .Replace("__LBL_ACTIONS__", Strings.IsEnglish ? "Actions" : "操作")
                .Replace("__LBL_LAUNCH__", Strings.T("webui.page.launch"))
                .Replace("__LBL_STOP__", Strings.T("webui.page.stop"))
                .Replace("__LBL_CHECK__", Strings.T("webui.page.check"))
                .Replace("__LBL_REFRESH__", Strings.T("webui.page.refresh"))
                .Replace("__LBL_USAGE__", Strings.T("usage.title"))
                .Replace("__LBL_ACCOUNT__", Strings.T("usage.account"))
                .Replace("__LBL_LOCAL__", Strings.T("usage.local"))
                .Replace("__LBL_UNIT__", Strings.T("usage.unit"))
                .Replace("__U_RAW__", Units.Label(Units.Raw))
                .Replace("__U_WAN__", Units.Label(Units.Wan))
                .Replace("__U_QWAN__", Units.Label(Units.QianWan))
                .Replace("__U_YI__", Units.Label(Units.Yi))
                .Replace("__LBL_MESSAGES__", Strings.T("usage.messages"))
                .Replace("__JS_CACHEHIT__", Strings.T("usage.cacheHit"))
                .Replace("__JS_ROLLING__", Strings.T("usage.rolling"))
                .Replace("__JS_WEEKLY__", Strings.T("usage.weekly"))
                .Replace("__JS_MONTHLY__", Strings.T("usage.monthly"))
                .Replace("__JS_NO_KEY__", Strings.T("usage.noKey"))
                .Replace("__LBL_MODEL__", Strings.IsEnglish ? "Model" : "模型")
                .Replace("__LBL_SESSIONS__", Strings.IsEnglish ? "Sessions" : "会话")
                .Replace("__LBL_COST__", Strings.IsEnglish ? "Cost" : "花费")
                .Replace("__LBL_IN__", Strings.IsEnglish ? "Input" : "输入")
                .Replace("__LBL_OUT__", Strings.IsEnglish ? "Output" : "输出")
                .Replace("__LBL_RECENT__", Strings.IsEnglish ? "Recent sessions" : "最近会话")
                .Replace("__LBL_OUTPUT__", Strings.T("webui.page.log"))
                .Replace("__JS_RUNNING__", Strings.T("webui.page.running"))
                .Replace("__JS_STOPPED__", Strings.T("webui.page.stopped"))
                .Replace("__JS_LAUNCHED__", Strings.T("webui.page.launched"))
                .Replace("__JS_REFRESHED__", Strings.T("webui.page.refreshed"))
                .Replace("__JS_WORKING__", Strings.T("status.checking"))
                .Replace("__JS_FAILED__", Strings.T("status.launchFailed"))
                .Replace("__JS_NO_USAGE__", Strings.IsEnglish ? "No usage data" : "暂无用量数据")
                .Replace("__JS_TODAY__", Strings.IsEnglish ? "Today tokens" : "今日 token")
                .Replace("__JS_ALLTIME__", Strings.IsEnglish ? "All time" : "累计")
                .Replace("__JS_SESSIONS__", Strings.IsEnglish ? "Sessions" : "会话数")
                .Replace("__JS_COST__", Strings.IsEnglish ? "Total cost (USD)" : "累计花费 (USD)")
                .Replace("__JS_INPUT__", Strings.IsEnglish ? "Input tokens" : "输入 token")
                .Replace("__JS_OUTPUT__", Strings.IsEnglish ? "Output tokens" : "输出 token")
                .Replace("__JS_REASONING__", Strings.IsEnglish ? "Reasoning tokens" : "推理 token")
                .Replace("__JS_CACHEREAD__", Strings.IsEnglish ? "Cache read" : "缓存读取");
        }

        private const string PageTemplate = """
<!DOCTYPE html>
<html lang="__LANG__">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>__TITLE__</title>
<style>
  :root{
    --bg:#f4f6fa; --card:#fff; --border:#e6eaf1; --text:#1e2633; --muted:#7c8899;
    --accent:#4c6fff; --ok:#16b364; --warn:#f0a020; --err:#e5484d; --log:#f8fafc;
  }
  @media (prefers-color-scheme: dark){
    :root{ --bg:#12141a; --card:#1b1e26; --border:#2a2f3a; --text:#e9edf4; --muted:#8c95a6; --log:#161920; }
  }
  *{box-sizing:border-box}
  body{margin:0;padding:28px 20px 48px;background:var(--bg);color:var(--text);
       font-family:"Microsoft YaHei UI","Segoe UI",system-ui,sans-serif;font-size:14px;line-height:1.6}
  .wrap{max-width:900px;margin:0 auto}
  header{display:flex;align-items:center;gap:12px;margin-bottom:20px}
  .logo{width:34px;height:34px;border-radius:10px;background:var(--accent);color:#fff;
        display:flex;align-items:center;justify-content:center;font-weight:700;font-size:13px}
  h1{font-size:17px;margin:0;font-weight:600}
  .sub{color:var(--muted);font-size:12.5px}
  .card{background:var(--card);border:1px solid var(--border);border-radius:12px;padding:18px;margin-bottom:16px}
  .card h2{font-size:13.5px;margin:0 0 14px;font-weight:600}
  .row{display:flex;justify-content:space-between;gap:16px;padding:7px 0;border-bottom:1px solid var(--border);font-size:13px}
  .row:last-child{border-bottom:0}
  .row .k{color:var(--muted);flex:0 0 130px}
  .row .v{word-break:break-all;text-align:right}
  .btns{display:flex;gap:10px;flex-wrap:wrap}
  button{border:0;border-radius:8px;padding:10px 18px;font-size:13px;cursor:pointer;font-family:inherit}
  .primary{background:var(--accent);color:#fff}
  .ghost{background:var(--card);color:var(--text);border:1px solid var(--border)}
  #btnStop:hover{border-color:var(--err);color:var(--err)}
  button:disabled{opacity:.5;cursor:default}
  .stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:12px}
  .stat{background:var(--log);border:1px solid var(--border);border-radius:10px;padding:12px}
  .stat .n{font-size:19px;font-weight:600;font-variant-numeric:tabular-nums}
  .stat .l{color:var(--muted);font-size:12px;margin-top:2px}
  table{width:100%;border-collapse:collapse;font-size:12.5px}
  th,td{text-align:left;padding:7px 8px;border-bottom:1px solid var(--border)}
  th{color:var(--muted);font-weight:500}
  td.num,th.num{text-align:right;font-variant-numeric:tabular-nums}
  .dot{display:inline-block;width:8px;height:8px;border-radius:50%;background:var(--muted);margin-right:7px;vertical-align:middle}
  .dot.on{background:var(--ok)}
  pre{background:var(--log);border:1px solid var(--border);border-radius:8px;padding:12px;
      margin:0;max-height:280px;overflow:auto;font-size:12px;
      font-family:"Cascadia Mono",Consolas,monospace;white-space:pre-wrap;word-break:break-all}
  .empty{color:var(--muted);font-size:12.5px}
  .sect{margin-top:18px}
  .usagehead{display:flex;justify-content:space-between;align-items:center;gap:12px;
             margin-bottom:10px;flex-wrap:wrap}
  .usagehead .lbl{color:var(--muted);font-size:12.5px}
  .units{display:flex;align-items:center;gap:6px}
  .unit{background:var(--card);color:var(--muted);border:1px solid var(--border);
        border-radius:7px;padding:4px 10px;font-size:11.5px;cursor:pointer;font-family:inherit}
  .unit.on{background:var(--accent);color:#fff;border-color:var(--accent)}
</style>
</head>
<body>
<div class="wrap">
  <header>
    <div class="logo">OC</div>
    <div>
      <h1>__TITLE__</h1>
      <div class="sub">__SUBTITLE__</div>
    </div>
  </header>

  <div class="card">
    <h2>__LBL_STATUS__</h2>
    <div class="row"><span class="k">__LBL_PROXY__</span><span class="v" id="proxy">-</span></div>
    <div class="row"><span class="k">__LBL_PATH__</span><span class="v" id="path">-</span></div>
    <div class="row"><span class="k">__LBL_STATE__</span><span class="v"><span class="dot" id="dot"></span><span id="state">-</span></span></div>
  </div>

  <div class="card">
    <h2>__LBL_ACTIONS__</h2>
    <div class="btns">
      <button class="primary" id="btnLaunch">__LBL_LAUNCH__</button>
      <button class="ghost" id="btnCheck">__LBL_CHECK__</button>
      <button class="ghost" id="btnStop">__LBL_STOP__</button>
      <button class="ghost" id="btnRefresh">__LBL_REFRESH__</button>
    </div>
  </div>

  <div class="card">
    <h2>__LBL_USAGE__</h2>

    <div class="usagehead"><span class="lbl">__LBL_ACCOUNT__</span></div>
    <div class="stats" id="account"><div class="empty">…</div></div>

    <div class="usagehead" style="margin-top:18px">
      <span class="lbl">__LBL_LOCAL__</span>
      <span class="units">
        <span class="lbl">__LBL_UNIT__</span>
        <button class="unit" data-unit="raw">__U_RAW__</button>
        <button class="unit" data-unit="wan">__U_WAN__</button>
        <button class="unit" data-unit="qianwan">__U_QWAN__</button>
        <button class="unit" data-unit="yi">__U_YI__</button>
      </span>
    </div>
    <div class="stats" id="stats"><div class="empty">…</div></div>
    <div class="sect">
      <table>
        <thead><tr><th>__LBL_MODEL__</th><th class="num">__LBL_MESSAGES__</th><th class="num">__LBL_COST__</th><th class="num">__LBL_IN__</th><th class="num">__LBL_OUT__</th></tr></thead>
        <tbody id="models"></tbody>
      </table>
    </div>
    <div class="sect">
      <table>
        <thead><tr><th>__LBL_RECENT__</th><th>__LBL_MODEL__</th><th class="num">__LBL_COST__</th><th class="num">__LBL_IN__</th><th class="num">__LBL_OUT__</th></tr></thead>
        <tbody id="recent"></tbody>
      </table>
    </div>
  </div>

  <div class="card">
    <h2>__LBL_OUTPUT__</h2>
    <pre id="log">-</pre>
  </div>
</div>

<script>
const TOKEN = "__TOKEN__";
const T = {
  running: "__JS_RUNNING__",
  stopped: "__JS_STOPPED__",
  launched: "__JS_LAUNCHED__",
  refreshed: "__JS_REFRESHED__",
  working: "__JS_WORKING__",
  failed: "__JS_FAILED__",
  noUsage: "__JS_NO_USAGE__",
  today: "__JS_TODAY__",
  sessions: "__JS_SESSIONS__",
  cost: "__JS_COST__",
  input: "__JS_INPUT__",
  output: "__JS_OUTPUT__",
  reasoning: "__JS_REASONING__",
  cacheRead: "__JS_CACHEREAD__",
  rolling: "__JS_ROLLING__",
  weekly: "__JS_WEEKLY__",
  monthly: "__JS_MONTHLY__",
  cacheHit: "__JS_CACHEHIT__",
  noKey: "__JS_NO_KEY__"
};

const $ = id => document.getElementById(id);
const num = n => (n || 0).toLocaleString();
const money = c => "$" + (c || 0).toFixed(4);
const cacheHit = u => {
  const base = (u.cacheRead || 0) + (u.input || 0);
  return base > 0 ? (100 * u.cacheRead / base).toFixed(1) + "%" : "-";
};
const esc = s => String(s == null ? "-" : s).replace(/[&<>]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;" }[c]));

// ---- 数字单位：默认 / 万 / 千万 / 亿 ----
let unit = localStorage.getItem("oc-unit") || "raw";

function scale(v) {
  if (v >= 1000) return v.toLocaleString(undefined, { maximumFractionDigits: 0 });
  if (v >= 100) return v.toFixed(1);
  return v.toFixed(2);
}

function fmt(n) {
  n = n || 0;
  if (unit === "wan") return scale(n / 1e4) + "万";
  if (unit === "qianwan") return scale(n / 1e7) + "千万";
  if (unit === "yi") return scale(n / 1e8) + "亿";
  return n.toLocaleString();
}

function applyUnitButtons() {
  document.querySelectorAll(".unit").forEach(b => b.classList.toggle("on", b.dataset.unit === unit));
}

document.querySelectorAll(".unit").forEach(b => {
  b.onclick = () => {
    unit = b.dataset.unit;
    localStorage.setItem("oc-unit", unit);
    applyUnitButtons();
    loadUsage();
  };
});

async function api(path, method) {
  const sep = path.indexOf("?") >= 0 ? "&" : "?";
  const res = await fetch(path + sep + "token=" + encodeURIComponent(TOKEN), {
    method: method || "GET",
    headers: { "X-Token": TOKEN }
  });
  return await res.json();
}

function stat(value, label) {
  return '<div class="stat"><div class="n">' + value + '</div><div class="l">' + label + '</div></div>';
}

async function loadStatus() {
  const s = await api("/api/status");
  $("proxy").textContent = s.proxyUrl || "-";
  $("path").textContent = s.openCodePath || "-";
  $("dot").className = "dot" + (s.openCodeRunning ? " on" : "");
  $("state").textContent = s.openCodeRunning ? T.running : T.stopped;
}

async function loadUsage() {
  const payload = await api("/api/usage");
  const u = payload.local || {};
  const a = payload.account || {};

  // ---- 账户额度 ----
  if (a.ok) {
    const cell = (label, w) => w
      ? stat((w.percent || 0).toFixed(0) + "%" + (w.resetsIn ? " · " + w.resetsIn : ""), label)
      : "";
    const html = cell(T.rolling, a.rolling) + cell(T.weekly, a.weekly) + cell(T.monthly, a.monthly);
    $("account").innerHTML = html || '<div class="empty">-</div>';
  } else {
    $("account").innerHTML = '<div class="empty">' + esc(a.error === "no-api-key" ? T.noKey : (a.error || "-")) + "</div>";
  }

  // ---- 本地用量 ----
  if (!u.ok) {
    $("stats").innerHTML = '<div class="empty">' + T.noUsage + " (" + esc(u.error) + ")</div>";
    return;
  }

  $("stats").innerHTML =
    stat(num(u.sessions), T.sessions) +
    stat(money(u.cost), T.cost) +
    stat(fmt(u.input), T.input) +
    stat(fmt(u.output), T.output) +
    stat(fmt(u.reasoning), T.reasoning) +
    stat(fmt(u.cacheRead), T.cacheRead) +
    stat(cacheHit(u), T.cacheHit) +
    stat(fmt((u.todayInput || 0) + (u.todayOutput || 0)), T.today);

  $("models").innerHTML = (u.models || []).map(m =>
    "<tr><td>" + esc(m.provider) + " / " + esc(m.model) + "</td><td class='num'>" + num(m.messages) +
    "</td><td class='num'>" + money(m.cost) + "</td><td class='num'>" + fmt(m.input) +
    "</td><td class='num'>" + fmt(m.output) + "</td></tr>").join("") || '<tr><td colspan="5" class="empty">-</td></tr>';

  $("recent").innerHTML = (u.recent || []).map(s =>
    "<tr><td>" + esc(s.title) + "<br><span class='empty'>" + esc(s.time) + "</span></td><td>" + esc(s.model) +
    "</td><td class='num'>" + money(s.cost) + "</td><td class='num'>" + fmt(s.input) +
    "</td><td class='num'>" + fmt(s.output) + "</td></tr>").join("") || '<tr><td colspan="5" class="empty">-</td></tr>';
}

function setLog(text) {
  $("log").textContent = text && text.length ? text : "-";
}

function busy(on) {
  ["btnLaunch", "btnCheck", "btnStop", "btnRefresh"].forEach(id => $(id).disabled = on);
}

async function refreshAll() {
  await loadStatus();
  await loadUsage();
}

$("btnRefresh").onclick = async () => { busy(true); await refreshAll(); setLog(T.refreshed); busy(false); };

$("btnLaunch").onclick = async () => {
  busy(true);
  setLog("…");
  try {
    const r = await api("/api/launch", "POST");
    setLog((r.ok ? "✓ " : "✗ ") + (r.message || "") + "\n" + (r.lines || []).join("\n"));
    await loadStatus();
  } catch (e) {
    setLog(String(e));
  }
  busy(false);
};

$("btnStop").onclick = async () => {
  busy(true);
  setLog("…");
  try {
    const r = await api("/api/stop", "POST");
    setLog((r.ok ? "✓ " : "✗ ") + (r.message || "") + "\n" + (r.lines || []).join("\n"));
    await loadStatus();
  } catch (e) {
    setLog(String(e));
  }
  busy(false);
};

$("btnCheck").onclick = async () => {
  busy(true);
  setLog(T.working);
  try {
    const r = await api("/api/check", "POST");
    setLog((r.lines || []).join("\n"));
    await loadStatus();
  } catch (e) {
    setLog(String(e));
  }
  busy(false);
};

refreshAll().catch(e => setLog(String(e)));
applyUnitButtons();
setInterval(loadStatus, 10000);
</script>
</body>
</html>
""";
    }
}
