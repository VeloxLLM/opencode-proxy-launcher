using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenCodeProxyLauncher
{
    internal sealed class AccountWindow
    {
        public string Status { get; set; }

        public double Percent { get; set; }

        public string ResetsAt { get; set; }

        public string ResetsIn { get; set; }
    }

    internal sealed class AccountUsage
    {
        public bool Ok { get; set; }

        public string Error { get; set; }

        public string Endpoint { get; set; }

        public AccountWindow Rolling { get; set; }

        public AccountWindow Weekly { get; set; }

        public AccountWindow Monthly { get; set; }
    }

    /// <summary>
    /// 通过官方接口读取 opencode-go 的账户用量（额度占用百分比）。
    ///
    /// GET https://opencode.ai/zen/go/v1/usage
    /// Authorization: Bearer &lt;auth.json 里 opencode-go 的 key&gt;
    /// </summary>
    internal static class AccountUsageReader
    {
        public const string Endpoint = "https://opencode.ai/zen/go/v1/usage";

        public static string AuthFilePath
        {
            get
            {
                string xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                if (!string.IsNullOrWhiteSpace(xdg))
                {
                    return Path.Combine(xdg, "opencode", "auth.json");
                }

                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, ".local", "share", "opencode", "auth.json");
            }
        }

        public static async Task<AccountUsage> ReadAsync(AppSettings settings)
        {
            var result = new AccountUsage { Endpoint = Endpoint };

            try
            {
                string key = ReadApiKey();
                if (string.IsNullOrEmpty(key))
                {
                    result.Error = "no-api-key";
                    return result;
                }

                Uri proxyUri = null;
                Uri.TryCreate(settings.ProxyUrl, UriKind.Absolute, out proxyUri);

                using (var handler = new HttpClientHandler())
                {
                    if (proxyUri != null)
                    {
                        handler.Proxy = new WebProxy(proxyUri);
                        handler.UseProxy = true;
                    }

                    using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) })
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenCode-ProxyLauncher");

                        string json = await client.GetStringAsync(Endpoint);

                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            JsonElement usage;
                            if (!doc.RootElement.TryGetProperty("usage", out usage))
                            {
                                result.Error = "unexpected-response";
                                return result;
                            }

                            result.Rolling = ReadWindow(usage, "rolling");
                            result.Weekly = ReadWindow(usage, "weekly");
                            result.Monthly = ReadWindow(usage, "monthly");
                        }
                    }
                }

                result.Ok = true;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            return result;
        }

        private static AccountWindow ReadWindow(JsonElement usage, string name)
        {
            JsonElement node;
            if (!usage.TryGetProperty(name, out node))
            {
                return null;
            }

            var window = new AccountWindow();

            JsonElement value;
            if (node.TryGetProperty("status", out value))
            {
                window.Status = value.ToString();
            }

            if (node.TryGetProperty("percent", out value) && value.ValueKind == JsonValueKind.Number)
            {
                window.Percent = value.GetDouble();
            }

            if (node.TryGetProperty("resetsAt", out value))
            {
                window.ResetsAt = value.ToString();
                window.ResetsIn = HumanizeReset(value.ToString());
            }

            return window;
        }

        private static string HumanizeReset(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso))
            {
                return "";
            }

            DateTimeOffset when;
            if (!DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out when))
            {
                return "";
            }

            TimeSpan left = when - DateTimeOffset.UtcNow;
            if (left.TotalSeconds <= 0)
            {
                return Strings.IsEnglish ? "resetting" : "即将重置";
            }

            if (left.TotalDays >= 1)
            {
                return Strings.IsEnglish
                    ? string.Format("{0}d {1}h", (int)left.TotalDays, left.Hours)
                    : string.Format("{0}天{1}小时", (int)left.TotalDays, left.Hours);
            }

            if (left.TotalHours >= 1)
            {
                return Strings.IsEnglish
                    ? string.Format("{0}h {1}m", (int)left.TotalHours, left.Minutes)
                    : string.Format("{0}小时{1}分", (int)left.TotalHours, left.Minutes);
            }

            return Strings.IsEnglish
                ? string.Format("{0}m", left.Minutes)
                : string.Format("{0}分钟", left.Minutes);
        }

        /// <summary>从 opencode 的 auth.json 里取 opencode-go 的 API key。</summary>
        private static string ReadApiKey()
        {
            string path = AuthFilePath;
            if (!File.Exists(path))
            {
                return null;
            }

            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
            {
                JsonElement root = doc.RootElement;

                JsonElement entry;
                if (root.TryGetProperty("opencode-go", out entry))
                {
                    JsonElement key;
                    if (entry.TryGetProperty("key", out key))
                    {
                        return key.GetString();
                    }
                }

                // 退一步：任意一个带 key 的条目
                foreach (JsonProperty property in root.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    JsonElement key;
                    if (property.Value.TryGetProperty("key", out key))
                    {
                        return key.GetString();
                    }
                }
            }

            return null;
        }
    }
}
