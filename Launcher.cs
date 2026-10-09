using System;
using System.Diagnostics;
using System.IO;

namespace OpenCodeProxyLauncher
{
    /// <summary>
    /// 以指定代理环境变量拉起 OpenCode 桌面版。
    ///
    /// OpenCode 桌面版没有内置代理设置，它只在【进程启动时】从环境变量读取代理
    /// （内部调用 http.setGlobalProxyFromEnv()），主进程和它 fork 出来的 sidecar
    /// 子进程都是如此。所以只要启动瞬间环境变量是对的，模型请求就一定会走代理。
    ///
    /// 这里用 UseShellExecute=false + ProcessStartInfo.EnvironmentVariables 注入，
    /// 只影响这一个子进程，不改动任何系统设置。
    /// </summary>
    internal static class Launcher
    {
        public static LaunchResult Launch(AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.OpenCodePath) || !File.Exists(settings.OpenCodePath))
            {
                return new LaunchResult(false, "找不到 OpenCode.exe：" + settings.OpenCodePath, null);
            }

            string proxy = (settings.ProxyUrl ?? "").Trim();
            if (proxy.Length == 0)
            {
                return new LaunchResult(false, "代理地址为空", null);
            }

            var startInfo = new ProcessStartInfo(settings.OpenCodePath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(settings.OpenCodePath) ?? ".",
            };

            // Windows 上环境变量键名大小写不敏感，因此这会覆盖从父进程继承来的任何旧值
            startInfo.EnvironmentVariables["HTTP_PROXY"] = proxy;
            startInfo.EnvironmentVariables["HTTPS_PROXY"] = proxy;
            startInfo.EnvironmentVariables["ALL_PROXY"] = proxy;
            startInfo.EnvironmentVariables["http_proxy"] = proxy;
            startInfo.EnvironmentVariables["https_proxy"] = proxy;

            string noProxy = string.IsNullOrWhiteSpace(settings.NoProxy) ? "127.0.0.1,localhost,::1" : settings.NoProxy.Trim();
            startInfo.EnvironmentVariables["NO_PROXY"] = noProxy;
            startInfo.EnvironmentVariables["no_proxy"] = noProxy;

            try
            {
                Process process = Process.Start(startInfo);
                WriteLog("launched pid=" + process.Id + " proxy=" + proxy + " noProxy=" + noProxy);
                return new LaunchResult(true, "已启动 OpenCode（pid " + process.Id + "），代理 = " + proxy, process);
            }
            catch (Exception ex)
            {
                WriteLog("failed: " + ex.Message);
                return new LaunchResult(false, "启动失败：" + ex.Message, null);
            }
        }

        /// <summary>进程是否还活着（null 视为已退出）。</summary>
        public static bool IsAlive(Process process)
        {
            if (process == null)
            {
                return false;
            }

            try
            {
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>阻塞等待若干毫秒，确认进程没有立刻退出。</summary>
        public static bool WaitForAlive(Process process, int milliseconds)
        {
            const int step = 250;
            int elapsed = 0;

            while (elapsed < milliseconds)
            {
                if (!IsAlive(process))
                {
                    return false;
                }

                System.Threading.Thread.Sleep(step);
                elapsed += step;
            }

            return true;
        }

        /// <summary>把启动记录写到 %TEMP%\opencode-proxy-launcher.log，方便事后排查。</summary>
        private static void WriteLog(string message)
        {
            try
            {
                string path = Path.Combine(Path.GetTempPath(), "opencode-proxy-launcher.log");
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch
            {
                // 记日志失败不影响启动
            }
        }
    }

    internal sealed class LaunchResult
    {
        public LaunchResult(bool ok, string message, Process process)
        {
            Ok = ok;
            Message = message;
            Process = process;
        }

        public bool Ok { get; private set; }

        public string Message { get; private set; }

        /// <summary>已启动的进程；失败时为 null。调用方可据此确认它没有立刻退出。</summary>
        public Process Process { get; private set; }

        public int ProcessId
        {
            get { return Process != null ? Process.Id : 0; }
        }
    }
}
