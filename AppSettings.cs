using System;
using System.IO;
using System.Text.Json;

namespace OpenCodeProxyLauncher
{
    /// <summary>
    /// 启动器设置。保存在 %APPDATA%\OpenCodeProxyLauncher\settings.json
    /// </summary>
    internal sealed class AppSettings
    {
        public string ProxyUrl { get; set; } = "http://127.0.0.1:2080";

        public string OpenCodePath { get; set; } = "";

        public string NoProxy { get; set; } = "127.0.0.1,localhost,::1";

        /// <summary>启动 OpenCode 后自动关闭本窗口（默认不勾选）。</summary>
        public bool CloseAfterLaunch { get; set; }

        /// <summary>深色模式；null 表示跟随系统。</summary>
        public bool? DarkMode { get; set; }

        /// <summary>界面语言："zh" / "en"；null 表示跟随系统。</summary>
        public string Language { get; set; }

        /// <summary>是否启用 WebUI（默认开启）。</summary>
        public bool WebUiEnabled { get; set; } = true;

        /// <summary>WebUI 监听端口。</summary>
        public int WebUiPort { get; set; } = 8710;

        /// <summary>WebUI 访问令牌，首次使用时自动生成。</summary>
        public string WebUiToken { get; set; }

        /// <summary>用量数字显示单位：raw / wan / qianwan / yi。</summary>
        public string NumberUnit { get; set; } = Units.Raw;

        /// <summary>窗口位置与尺寸记忆（为 null 表示还没记过）。</summary>
        public double? WindowX { get; set; }

        public double? WindowY { get; set; }

        public double? WindowWidth { get; set; }

        public double? WindowHeight { get; set; }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        public static string DirectoryPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "OpenCodeProxyLauncher");
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(DirectoryPath, "settings.json"); }
        }

        /// <summary>自动探测 OpenCode 桌面版的默认安装位置。</summary>
        public static string DetectOpenCodePath()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidates =
            {
                Path.Combine(local, "Programs", "@opencode-aidesktop", "OpenCode.exe"),
                Path.Combine(local, "Programs", "opencode", "OpenCode.exe"),
                Path.Combine(local, "OpenCode", "OpenCode.exe"),
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return "";
        }

        public static AppSettings Load()
        {
            AppSettings settings = null;

            try
            {
                if (File.Exists(FilePath))
                {
                    settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions);
                }
            }
            catch
            {
                // 设置损坏时回退到默认值
            }

            if (settings == null)
            {
                settings = new AppSettings();
            }

            if (string.IsNullOrWhiteSpace(settings.OpenCodePath))
            {
                settings.OpenCodePath = DetectOpenCodePath();
            }

            if (string.IsNullOrWhiteSpace(settings.WebUiToken))
            {
                settings.WebUiToken = NewToken();
            }

            if (settings.WebUiPort < 1 || settings.WebUiPort > 65535)
            {
                settings.WebUiPort = 8710;
            }

            settings.NumberUnit = Units.Normalize(settings.NumberUnit);

            return settings;
        }

        private static string NewToken()
        {
            var bytes = new byte[16];
            using (var random = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            var builder = new System.Text.StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch
            {
                // 保存失败不影响使用
            }
        }
    }
}
