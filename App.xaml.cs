using System;
using System.Windows;

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
    }
}
