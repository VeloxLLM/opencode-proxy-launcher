using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenCodeProxyLauncher
{
    /// <summary>
    /// 简单的代理体检：端口是否在监听、经代理的出口 IP/国家、HTTPS 连通性、目标程序是否存在。
    /// </summary>
    internal static class ProxyChecker
    {
        public static async Task<CheckResult> CheckAsync(AppSettings settings)
        {
            var result = new CheckResult();

            Uri proxyUri;
            if (!Uri.TryCreate(settings.ProxyUrl, UriKind.Absolute, out proxyUri))
            {
                result.Lines.Add(Strings.T("check.badUrl", settings.ProxyUrl));
                result.Summary = Strings.T("check.badUrlSummary");
                return result;
            }

            result.Lines.Add(Strings.T("check.proxy", proxyUri));

            // 1) 端口是否在监听
            bool portOpen = await IsPortOpenAsync(proxyUri.Host, proxyUri.Port);
            string endpoint = proxyUri.Host + ":" + proxyUri.Port;
            result.Lines.Add(portOpen ? Strings.T("check.portOk", endpoint) : Strings.T("check.portFail", endpoint));

            if (!portOpen)
            {
                result.Summary = Strings.T("check.portSummary");
                return result;
            }

            // 2) 经代理的出口 IP / 国家（关键：中国 IP 会被 opencode-go 拒绝）
            try
            {
                string json = await GetStringViaProxyAsync(
                    proxyUri,
                    "http://ip-api.com/json/?fields=countryCode,country,query");

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    string countryCode = ReadString(root, "countryCode");
                    string country = ReadString(root, "country");
                    string ip = ReadString(root, "query");

                    result.Lines.Add(Strings.T("check.exit", ip, country, countryCode));

                    if (string.Equals(countryCode, "CN", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Lines.Add(Strings.T("check.exitCN"));
                    }
                    else
                    {
                        result.ExitCountry = countryCode;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Lines.Add(Strings.T("check.ipFail", Short(ex.Message)));
            }

            // 3) HTTPS 连通性
            //
            // 关键判定：只要**拿到 HTTP 响应**，就说明「HTTPS 经代理」这条隧道是通的。
            // 非 2xx（比如 GitHub 对某些代理出口 IP 返回 403）是目标站点拒绝了本次请求，
            // 不是代理的问题 —— 所以分开成"警告"而不是"失败"。
            try
            {
                using (var handler = new HttpClientHandler { Proxy = new WebProxy(proxyUri), UseProxy = true })
                using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) })
                {
                    if (!client.DefaultRequestHeaders.UserAgent.TryParseAdd("OpenCode-ProxyLauncher"))
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "OpenCode-ProxyLauncher");
                    }

                    using (HttpResponseMessage response = await client.GetAsync("https://api.github.com"))
                    {
                        int code = (int)response.StatusCode;

                        if (response.IsSuccessStatusCode)
                        {
                            result.Lines.Add(Strings.T("check.httpsOk", code));
                        }
                        else
                        {
                            result.Lines.Add(Strings.T("check.httpsWarn", code));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Lines.Add(Strings.T("check.httpsFail", Short(ex.Message)));
            }

            // 4) 目标程序
            if (!string.IsNullOrWhiteSpace(settings.OpenCodePath) && File.Exists(settings.OpenCodePath))
            {
                result.Lines.Add(Strings.T("check.appOk", settings.OpenCodePath));
                result.FoundApp = true;
            }
            else
            {
                result.Lines.Add(Strings.T("check.appFail", settings.OpenCodePath));
            }

            result.Summary = result.ExitCountry != null
                ? Strings.T("check.doneOk", result.ExitCountry)
                : Strings.T("check.doneWarn");

            return result;
        }

        private static async Task<string> GetStringViaProxyAsync(Uri proxyUri, string url)
        {
            using (var handler = new HttpClientHandler { Proxy = new WebProxy(proxyUri), UseProxy = true })
            using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) })
            {
                return await client.GetStringAsync(url);
            }
        }

        private static async Task<bool> IsPortOpenAsync(string host, int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    Task connect = client.ConnectAsync(host, port);
                    Task finished = await Task.WhenAny(connect, Task.Delay(2000));
                    return finished == connect && client.Connected;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string ReadString(JsonElement element, string name)
        {
            JsonElement value;
            if (element.TryGetProperty(name, out value))
            {
                return value.ToString();
            }

            return "?";
        }

        private static string Short(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return "(?)";
            }

            return message.Length > 120 ? message.Substring(0, 120) + "..." : message;
        }
    }

    internal sealed class CheckResult
    {
        public List<string> Lines { get; private set; } = new List<string>();

        public string Summary { get; set; } = "";

        public string ExitCountry { get; set; }

        public bool FoundApp { get; set; }
    }
}
