using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OpenCodeProxyLauncher
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 先定主题和语言，再建窗口，避免闪一下再变
            AppSettings settings = AppSettings.Load();
            ThemeManager.Apply(settings.DarkMode ?? ThemeManager.IsSystemDark());
            Strings.SetLanguage(settings.Language ?? DetectSystemLanguage());

            var window = new MainWindow();

            // 开发用：把界面渲染成 PNG（用于 README 截图）
            //   OpenCode-ProxyLauncher.exe --shot out.png
            if (e.Args.Length >= 2 && string.Equals(e.Args[0], "--shot", StringComparison.OrdinalIgnoreCase))
            {
                // 可选的第三个参数：要展示的标签页序号（0=设置 1=用量 2=日志）
                if (e.Args.Length >= 3)
                {
                    int tab;
                    if (int.TryParse(e.Args[2], out tab))
                    {
                        window.SelectTab(tab);
                    }
                }

                window.Show();

                // 等入场动画跑完、用量（含账户额度接口）加载完再截图
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(4500) };
                timer.Tick += delegate
                {
                    timer.Stop();
                    try
                    {
                        SaveScreenshot(window, e.Args[1]);
                    }
                    catch
                    {
                        // 截图失败不影响正常使用
                    }

                    Shutdown();
                };
                timer.Start();
                return;
            }

            window.Show();
        }

        /// <summary>跟随系统 UI 语言：中文环境用中文，其余用英文。</summary>
        private static string DetectSystemLanguage()
        {
            try
            {
                string name = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                return string.Equals(name, "zh", StringComparison.OrdinalIgnoreCase)
                    ? Strings.Chinese
                    : Strings.English;
            }
            catch
            {
                return Strings.Chinese;
            }
        }

        private static void SaveScreenshot(Window window, string path)
        {
            var root = window.Content as FrameworkElement;
            if (root == null)
            {
                return;
            }

            int width = (int)window.Width;
            int height = (int)window.Height;

            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            string full = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (FileStream stream = File.Create(full))
            {
                encoder.Save(stream);
            }
        }
    }
}
