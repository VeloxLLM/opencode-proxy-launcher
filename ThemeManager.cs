using System;
using System.Windows;
using Microsoft.Win32;

namespace OpenCodeProxyLauncher
{
    /// <summary>
    /// 主题切换：运行时替换第一个合并字典（调色板），样式里的颜色全部走 DynamicResource，
    /// 所以切换后界面会即时刷新。
    /// </summary>
    internal static class ThemeManager
    {
        private const string LightSource = "ThemeLight.xaml";
        private const string DarkSource = "ThemeDark.xaml";

        public static bool IsDark { get; private set; }

        public static void Apply(bool dark)
        {
            IsDark = dark;

            var palette = new ResourceDictionary
            {
                Source = new Uri(dark ? DarkSource : LightSource, UriKind.Relative),
            };

            var merged = Application.Current.Resources.MergedDictionaries;

            // 约定：调色板永远是第一个
            if (merged.Count > 0)
            {
                merged[0] = palette;
            }
            else
            {
                merged.Add(palette);
            }
        }

        /// <summary>读取系统"应用"主题，用于首次启动时跟随系统。</summary>
        public static bool IsSystemDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                           @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AppsUseLightTheme");
                        if (value is int light)
                        {
                            return light == 0;
                        }
                    }
                }
            }
            catch
            {
                // 读不到就按浅色处理
            }

            return false;
        }
    }
}
