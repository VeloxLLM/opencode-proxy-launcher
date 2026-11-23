using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace OpenCodeProxyLauncher
{
    internal sealed class UsageModelRow
    {
        public string Provider { get; set; }

        public string Model { get; set; }

        public long Messages { get; set; }

        public double Cost { get; set; }

        public long Input { get; set; }

        public long Output { get; set; }
    }

    internal sealed class UsageSessionRow
    {
        public string Title { get; set; }

        public string Provider { get; set; }

        public string Model { get; set; }

        public string Time { get; set; }

        public double Cost { get; set; }

        public long Input { get; set; }

        public long Output { get; set; }
    }

    internal sealed class UsageReport
    {
        public bool Ok { get; set; }

        public string Error { get; set; }

        public string Database { get; set; }

        /// <summary>实际读取到的 schema："v2" (session_message/session_v2) 或 "v1"。</summary>
        public string Schema { get; set; }

        // ---- 累计（来自逐条消息表，v1=message / v2=session_message）----
        public long Messages { get; set; }

        public long Sessions { get; set; }

        public double Cost { get; set; }

        public long Input { get; set; }

        public long Output { get; set; }

        public long Reasoning { get; set; }

        public long CacheRead { get; set; }

        public long CacheWrite { get; set; }

        // ---- 今日 ----
        public long TodayMessages { get; set; }

        public double TodayCost { get; set; }

        public long TodayInput { get; set; }

        public long TodayOutput { get; set; }

        public long TodayReasoning { get; set; }

        public List<UsageModelRow> Models { get; set; }

        public List<UsageSessionRow> Recent { get; set; }

        public UsageReport()
        {
            Models = new List<UsageModelRow>();
            Recent = new List<UsageSessionRow>();
        }
    }

    /// <summary>
    /// 读取 OpenCode 本地用量统计。
    ///
    /// 数据来源：~/.local/share/opencode/opencode.db
    /// - v1：`message` / `session`
    /// - v2：`session_message` / `session_v2`（OpenCode 2.x 当前写入的表）
    ///
    /// 逐条消息的 cost / tokens（含 input / output / reasoning / cache）是最细粒度，
    /// **按天统计必须用它** —— 用 session.time_created 会把
    /// "昨天创建、今天还在用"的会话算成昨天，导致"今日"永远是 0。
    /// </summary>
    internal static class UsageReader
    {
        /// <summary>v1 只统计 assistant 消息；v2 用 type 列，data 里没有 role。</summary>
        private const string AssistantOnly = "json_extract(data,'$.role')='assistant'";

        private const string AssistantOnlyV2 = "type = 'assistant'";

        public static string DatabasePath
        {
            get
            {
                string xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                if (!string.IsNullOrWhiteSpace(xdg))
                {
                    return Path.Combine(xdg, "opencode", "opencode.db");
                }

                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, ".local", "share", "opencode", "opencode.db");
            }
        }

        public static UsageReport Read()
        {
            var report = new UsageReport();
            string path = DatabasePath;
            report.Database = path;

            try
            {
                if (!File.Exists(path))
                {
                    report.Error = "database-not-found";
                    return report;
                }

                if (!Sqlite.IsAvailable())
                {
                    report.Error = "sqlite-unavailable";
                    return report;
                }

                bool v2 = IsV2(path);
                report.Schema = v2 ? "v2" : "v1";

                ReadTotals(report, path, v2);
                ReadToday(report, path, v2);
                ReadModels(report, path, v2);
                ReadSessionSummary(report, path, v2);
                ReadRecent(report, path, v2);

                report.Ok = true;
            }
            catch (Exception ex)
            {
                report.Error = ex.Message;
            }

            return report;
        }

        /// <summary>检查 v2 的消息表是否存在；存在即以 v2 schema 读取。</summary>
        private static bool IsV2(string path)
        {
            List<object[]> rows = Sqlite.Query(
                path,
                "SELECT 1 FROM sqlite_master WHERE type='table' AND name='session_message' LIMIT 1");
            return rows.Count > 0;
        }

        private static void ReadTotals(UsageReport report, string path, bool v2)
        {
            List<object[]> rows = Sqlite.Query(
                path,
                "SELECT COUNT(*), IFNULL(SUM(json_extract(data,'$.cost')),0), "
                + "IFNULL(SUM(json_extract(data,'$.tokens.input')),0), "
                + "IFNULL(SUM(json_extract(data,'$.tokens.output')),0), "
                + "IFNULL(SUM(json_extract(data,'$.tokens.reasoning')),0), "
                + "IFNULL(SUM(json_extract(data,'$.tokens.cache.read')),0), "
                + "IFNULL(SUM(json_extract(data,'$.tokens.cache.write')),0) "
                + "FROM " + (v2 ? "session_message" : "message")
                + " WHERE " + (v2 ? AssistantOnlyV2 : AssistantOnly));

            if (rows.Count == 0)
            {
                return;
            }

            object[] r = rows[0];
            report.Messages = ToLong(r, 0);
            report.Cost = ToDouble(r, 1);
            report.Input = ToLong(r, 2);
            report.Output = ToLong(r, 3);
            report.Reasoning = ToLong(r, 4);
            report.CacheRead = ToLong(r, 5);
            report.CacheWrite = ToLong(r, 6);
        }

        private static void ReadToday(UsageReport report, string path, bool v2)
        {
            long since = new DateTimeOffset(DateTime.Today).ToUnixTimeMilliseconds();

            List<object[]> rows = Sqlite.Query(
                path,
                "SELECT COUNT(*), IFNULL(SUM(json_extract(data,'$.cost')),0), "
                + "IFNULL(SUM(json_extract(data,'$.tokens.input')),0), "
                + "IFNULL(SUM(json_extract(data,'$.tokens.output')),0), "
                + "IFNULL(SUM(json_extract(data,'$.tokens.reasoning')),0) "
                + "FROM " + (v2 ? "session_message" : "message")
                + " WHERE " + (v2 ? AssistantOnlyV2 : AssistantOnly)
                + " AND time_created >= " + since.ToString(CultureInfo.InvariantCulture));

            if (rows.Count == 0)
            {
                return;
            }

            object[] r = rows[0];
            report.TodayMessages = ToLong(r, 0);
            report.TodayCost = ToDouble(r, 1);
            report.TodayInput = ToLong(r, 2);
            report.TodayOutput = ToLong(r, 3);
            report.TodayReasoning = ToLong(r, 4);
        }

        private static void ReadModels(UsageReport report, string path, bool v2)
        {
            string providerExpr = v2 ? "json_extract(data,'$.model.providerID')" : "json_extract(data,'$.providerID')";
            string modelExpr = v2 ? "json_extract(data,'$.model.id')" : "json_extract(data,'$.modelID')";
            List<object[]> rows = Sqlite.Query(
                path,
                "SELECT " + providerExpr + ", " + modelExpr + ", "
                + "COUNT(*), IFNULL(SUM(json_extract(data,'$.cost')),0), "
                + "IFNULL(SUM(json_extract(data,'$.tokens.input')),0), "
                + "IFNULL(SUM(json_extract(data,'$.tokens.output')),0) "
                + "FROM " + (v2 ? "session_message" : "message")
                + " WHERE " + (v2 ? AssistantOnlyV2 : AssistantOnly) + " GROUP BY 1, 2 "
                + "ORDER BY SUM(IFNULL(json_extract(data,'$.tokens.input'),0) "
                + "+ IFNULL(json_extract(data,'$.tokens.output'),0)) DESC LIMIT 15");

            foreach (object[] r in rows)
            {
                report.Models.Add(new UsageModelRow
                {
                    Provider = Clean(Convert.ToString(r[0])),
                    Model = Clean(Convert.ToString(r[1])),
                    Messages = ToLong(r, 2),
                    Cost = ToDouble(r, 3),
                    Input = ToLong(r, 4),
                    Output = ToLong(r, 5),
                });
            }
        }

        private static void ReadSessionSummary(UsageReport report, string path, bool v2)
        {
            List<object[]> rows = Sqlite.Query(
                path, "SELECT COUNT(*) FROM " + (v2 ? "session_v2" : "session"));

            if (rows.Count > 0)
            {
                report.Sessions = ToLong(rows[0], 0);
            }
        }

        private static void ReadRecent(UsageReport report, string path, bool v2)
        {
            // 按最后活动时间排序：昨天创建、今天还在用的会话也会排在前面
            List<object[]> rows = Sqlite.Query(
                path,
                "SELECT title, model, IFNULL(cost,0), IFNULL(tokens_input,0), IFNULL(tokens_output,0), time_updated "
                + "FROM " + (v2 ? "session_v2" : "session")
                + " ORDER BY time_updated DESC LIMIT 12");

            foreach (object[] r in rows)
            {
                string provider;
                string model;
                SplitModel(Convert.ToString(r[1]), out provider, out model);

                report.Recent.Add(new UsageSessionRow
                {
                    Title = Truncate(Convert.ToString(r[0]), 60),
                    Provider = provider,
                    Model = model,
                    Cost = ToDouble(r, 2),
                    Input = ToLong(r, 3),
                    Output = ToLong(r, 4),
                    Time = FormatTime(ToLong(r, 5)),
                });
            }
        }

        /// <summary>session.model 列是 JSON：{"id":"...","providerID":"...","variant":"..."}</summary>
        private static void SplitModel(string raw, out string provider, out string model)
        {
            provider = "-";
            model = "-";

            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(raw))
                {
                    JsonElement root = doc.RootElement;
                    JsonElement value;

                    if (root.TryGetProperty("providerID", out value))
                    {
                        provider = Clean(value.GetString());
                    }

                    if (root.TryGetProperty("id", out value))
                    {
                        model = Clean(value.GetString());
                    }
                }
            }
            catch
            {
                model = raw;
            }
        }

        private static string FormatTime(long unixMilliseconds)
        {
            if (unixMilliseconds <= 0)
            {
                return "-";
            }

            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds)
                    .ToLocalTime()
                    .ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
            }
            catch
            {
                return "-";
            }
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "-";
            }

            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static long ToLong(object[] row, int index)
        {
            if (index >= row.Length || row[index] == null)
            {
                return 0;
            }

            if (row[index] is long)
            {
                return (long)row[index];
            }

            if (row[index] is double)
            {
                return (long)(double)row[index];
            }

            long parsed;
            return long.TryParse(Convert.ToString(row[index]), out parsed) ? parsed : 0;
        }

        private static double ToDouble(object[] row, int index)
        {
            if (index >= row.Length || row[index] == null)
            {
                return 0;
            }

            if (row[index] is double)
            {
                return (double)row[index];
            }

            if (row[index] is long)
            {
                return (long)row[index];
            }

            double parsed;
            return double.TryParse(Convert.ToString(row[index]), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : 0;
        }
    }
}
