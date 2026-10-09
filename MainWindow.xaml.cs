using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace OpenCodeProxyLauncher
{
    public partial class MainWindow : Window, IWebUiHost
    {
        private readonly AppSettings _settings;

        private WebUiServer _webUi;
        private bool _busy;
        private bool _suppressWebUiToggle;
        private string _statusKey = "status.ready";
        private string _statusBrush = "TextSecondaryBrush";
        private UsageReport _usage;
        private AccountUsage _account;
        private bool _usageBusy;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public MainWindow()
        {
            InitializeComponent();

            _settings = AppSettings.Load();
            ApplySettingsToUi();
            ApplyLanguage();
            UpdateThemeButton();
            SetStatus("status.ready", "TextSecondaryBrush");

            StartWebUiIfEnabled();

            Log(Strings.T("log.welcome1"));
            Log(Strings.T("log.settingsFile", AppSettings.FilePath));

            UpdateUnitButtons();
            RenderUsage();
            _ = RefreshUsageAsync(false);
        }

        private static string AppVersion
        {
            get
            {
                try
                {
                    Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                    return version == null ? "0.2" : version.Major + "." + version.Minor;
                }
                catch
                {
                    return "0.2";
                }
            }
        }

        /// <summary>供 --shot 截图模式选择要展示的标签页。</summary>
        public void SelectTab(int index)
        {
            if (index >= 0 && index < MainTabs.Items.Count)
            {
                MainTabs.SelectedIndex = index;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Windows 11：给无边框窗口加圆角
            try
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                int round = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(handle, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref round, sizeof(int));
            }
            catch
            {
                // 旧系统上忽略即可
            }
        }

        // ================= 入场动效 =================

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ContentRoot.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                });

            AnimateIn(MainTabs, 40);
            AnimateIn(ButtonRow, 120);
        }

        private static void AnimateIn(UIElement element, int delayMilliseconds)
        {
            if (element == null)
            {
                return;
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var offset = new TranslateTransform(0, 16);

            element.RenderTransform = offset;
            element.Opacity = 0;

            offset.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(360))
                {
                    BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds),
                    EasingFunction = ease,
                });

            element.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
                {
                    BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds),
                    EasingFunction = ease,
                });
        }

        // ================= 窗口控制 =================

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_webUi != null)
            {
                _webUi.Dispose();
                _webUi = null;
            }

            base.OnClosed(e);
        }

        // ================= 语言 =================

        private void LangButton_Click(object sender, RoutedEventArgs e)
        {
            string next = Strings.IsEnglish ? Strings.Chinese : Strings.English;
            Strings.SetLanguage(next);

            _settings.Language = next;
            _settings.Save();

            ApplyLanguage();
            Log(Strings.T("log.languageChanged"));
        }

        private void ApplyLanguage()
        {
            Title = Strings.T("app.title");
            AppTitleText.Text = Strings.T("app.title");

            LangButton.Content = Strings.IsEnglish ? "EN" : "中";
            LangButton.ToolTip = Strings.T("tip.language");
            MinButton.ToolTip = Strings.T("tip.min");
            CloseButton.ToolTip = Strings.T("tip.close");

            TabSettings.Header = Strings.T("tab.settings");
            TabUsage.Header = Strings.T("tab.usage");
            TabLog.Header = Strings.T("tab.log");
            LogHint.Text = Strings.T("log.hint");

            SettingsSubtitle.Text = Strings.T("card.settings.sub");
            ProxyLabel.Text = Strings.T("field.proxy");
            PathLabel.Text = Strings.T("field.path");
            NoProxyLabel.Text = Strings.T("field.noproxy");
            BrowseButton.Content = Strings.T("btn.browse");
            DetectButton.Content = Strings.T("btn.autodetect");

            WebUiCheck.Content = Strings.T("webui.enable");
            WebUiPortLabel.Text = Strings.T("webui.port");
            WebUiOpenButton.Content = Strings.T("webui.open");
            UpdateWebUiHint();

            CloseAfterCheck.Content = Strings.T("chk.closeAfter");
            LaunchButton.Content = Strings.T("btn.launch");
            CheckButton.Content = Strings.T("btn.check");
            SaveButton.Content = Strings.T("btn.save");
            ClearButton.Content = Strings.T("btn.clear");

            UsageUnitLabel.Text = Strings.T("usage.unit");
            UsageRefreshButton.ToolTip = Strings.T("usage.refresh");
            AccountLabel.Text = Strings.T("usage.account");
            LocalLabel.Text = Strings.T("usage.local");

            UpdateUnitButtons();
            RenderUsage();

            SetStatus(_statusKey, _statusBrush);
        }

        // ================= 用量 =================

        private void Unit_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button == null)
            {
                return;
            }

            _settings.NumberUnit = Units.Normalize(Convert.ToString(button.Tag));
            _settings.Save();

            UpdateUnitButtons();
            RenderUsage();
        }

        private void UpdateUnitButtons()
        {
            string current = Units.Normalize(_settings.NumberUnit);

            SetSegment(UnitRaw, current == Units.Raw, Units.Label(Units.Raw));
            SetSegment(UnitWan, current == Units.Wan, Units.Label(Units.Wan));
            SetSegment(UnitQianWan, current == Units.QianWan, Units.Label(Units.QianWan));
            SetSegment(UnitYi, current == Units.Yi, Units.Label(Units.Yi));
        }

        private void SetSegment(System.Windows.Controls.Button button, bool active, string label)
        {
            button.Content = label;
            button.Style = (Style)FindResource(active ? "SegmentOn" : "SegmentOff");
        }

        private async void UsageRefreshButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            await RefreshUsageAsync(true);
        }

        private async Task RefreshUsageAsync(bool announce)
        {
            if (_usageBusy)
            {
                return;
            }

            _usageBusy = true;

            if (announce)
            {
                Log(Strings.T("usage.loading"));
            }

            try
            {
                _usage = UsageReader.Read();
            }
            catch (Exception ex)
            {
                _usage = new UsageReport { Ok = false, Error = ex.Message };
            }

            RenderUsage();

            try
            {
                _account = await AccountUsageReader.ReadAsync(_settings);
            }
            catch (Exception ex)
            {
                _account = new AccountUsage { Ok = false, Error = ex.Message };
            }

            RenderUsage();

            if (announce)
            {
                if (_usage != null && !_usage.Ok)
                {
                    Log(Strings.T("usage.failed", _usage.Error ?? "?"));
                }

                if (_account != null && !_account.Ok)
                {
                    Log(_account.Error == "no-api-key"
                        ? Strings.T("usage.noKey")
                        : Strings.T("usage.accountFailed", _account.Error ?? "?"));
                }
            }

            _usageBusy = false;
        }

        private void RenderUsage()
        {
            AccountChips.Children.Clear();
            LocalChips.Children.Clear();

            string unit = Units.Normalize(_settings.NumberUnit);

            // ---- 账户额度 ----
            if (_account == null)
            {
                AccountChips.Children.Add(MakeChip(Strings.T("usage.loading"), "…"));
            }
            else if (!_account.Ok)
            {
                AccountChips.Children.Add(MakeChip(
                    Strings.T("usage.account"),
                    _account.Error == "no-api-key" ? Strings.T("usage.noKey") : (_account.Error ?? "?")));
            }
            else
            {
                AddAccountChip(Strings.T("usage.rolling"), _account.Rolling);
                AddAccountChip(Strings.T("usage.weekly"), _account.Weekly);
                AddAccountChip(Strings.T("usage.monthly"), _account.Monthly);
            }

            // ---- 本地累计 ----
            if (_usage == null)
            {
                LocalChips.Children.Add(MakeChip(Strings.T("usage.loading"), "…"));
                return;
            }

            if (!_usage.Ok)
            {
                string message = _usage.Error == "database-not-found"
                    ? Strings.T("usage.dbMissing")
                    : (_usage.Error ?? "?");

                LocalChips.Children.Add(MakeChip(Strings.T("usage.local"), message));
                return;
            }

            LocalChips.Children.Add(MakeChip(Strings.T("usage.sessions"), _usage.Sessions.ToString("N0")));
            LocalChips.Children.Add(MakeChip(Strings.T("usage.cost"), "$" + _usage.Cost.ToString("F4")));
            LocalChips.Children.Add(MakeChip(Strings.T("usage.input"), Units.Number(_usage.Input, unit)));
            LocalChips.Children.Add(MakeChip(Strings.T("usage.output"), Units.Number(_usage.Output, unit)));
            LocalChips.Children.Add(MakeChip(Strings.T("usage.reasoning"), Units.Number(_usage.Reasoning, unit)));
            LocalChips.Children.Add(MakeChip(Strings.T("usage.cacheRead"), Units.Number(_usage.CacheRead, unit)));

            long cacheBase = _usage.CacheRead + _usage.Input;
            if (cacheBase > 0)
            {
                double hit = 100.0 * _usage.CacheRead / cacheBase;
                LocalChips.Children.Add(MakeChip(Strings.T("usage.cacheHit"), hit.ToString("0.0") + "%"));
            }

            long today = _usage.TodayInput + _usage.TodayOutput;
            LocalChips.Children.Add(MakeChip(Strings.T("usage.today"), Units.Number(today, unit)));
            LocalChips.Children.Add(MakeChip(Strings.T("usage.cost") + "(" + Strings.T("usage.today") + ")",
                "$" + _usage.TodayCost.ToString("F4")));
        }

        private void AddAccountChip(string label, AccountWindow window)
        {
            if (window == null)
            {
                return;
            }

            string value = window.Percent.ToString("0.#") + "%";
            if (!string.IsNullOrEmpty(window.ResetsIn))
            {
                value += "  ·  " + window.ResetsIn;
            }

            Border chip = MakeChip(label, value);
            chip.ToolTip = Strings.T("usage.windowHint");
            AccountChips.Children.Add(chip);
        }

        private Border MakeChip(string label, string value)
        {
            var labelText = new System.Windows.Controls.TextBlock
            {
                Text = label,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
            };
            labelText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextSecondaryBrush");

            var valueText = new System.Windows.Controls.TextBlock
            {
                Text = value,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(7, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            valueText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextPrimaryBrush");

            var panel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            panel.Children.Add(labelText);
            panel.Children.Add(valueText);

            var border = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(11, 6, 11, 6),
                Margin = new Thickness(0, 0, 8, 8),
                Child = panel,
            };
            border.SetResourceReference(Border.BackgroundProperty, "LogBgBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

            return border;
        }

        // ================= 主题 =================

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            bool dark = !ThemeManager.IsDark;
            ThemeManager.Apply(dark);
            UpdateThemeButton();

            _settings.DarkMode = dark;
            _settings.Save();
        }

        private void UpdateThemeButton()
        {
            ThemeButton.Content = ThemeManager.IsDark ? "\uE706" : "\uE708";
            ThemeButton.ToolTip = Strings.T("tip.theme");
        }

        // ================= 设置 =================

        private void ApplySettingsToUi()
        {
            ProxyBox.Text = _settings.ProxyUrl;
            PathBox.Text = _settings.OpenCodePath;
            NoProxyBox.Text = _settings.NoProxy;
            CloseAfterCheck.IsChecked = _settings.CloseAfterLaunch;

            _suppressWebUiToggle = true;
            WebUiCheck.IsChecked = _settings.WebUiEnabled;
            WebUiPortBox.Text = _settings.WebUiPort.ToString();
            _suppressWebUiToggle = false;
        }

        private void CollectUiToSettings()
        {
            _settings.ProxyUrl = (ProxyBox.Text ?? "").Trim();
            _settings.OpenCodePath = (PathBox.Text ?? "").Trim();
            _settings.NoProxy = (NoProxyBox.Text ?? "").Trim();
            _settings.CloseAfterLaunch = CloseAfterCheck.IsChecked == true;
            _settings.WebUiEnabled = WebUiCheck.IsChecked == true;

            int port;
            if (int.TryParse((WebUiPortBox.Text ?? "").Trim(), out port) && port >= 1 && port <= 65535)
            {
                _settings.WebUiPort = port;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "OpenCode.exe",
                Filter = "OpenCode.exe|OpenCode.exe|*.exe|*.exe",
            };

            if (dialog.ShowDialog(this) == true)
            {
                PathBox.Text = dialog.FileName;
            }
        }

        private void DetectButton_Click(object sender, RoutedEventArgs e)
        {
            string found = AppSettings.DetectOpenCodePath();

            if (found.Length > 0)
            {
                PathBox.Text = found;
                Log(Strings.T("log.detected", found));
                SetStatus("status.found", "SuccessBrush");
            }
            else
            {
                Log(Strings.T("log.notDetected"));
                SetStatus("status.notFound", "WarnBrush");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            CollectUiToSettings();
            _settings.Save();
            Log(Strings.T("log.saved", AppSettings.FilePath));
            SetStatus("status.saved", "SuccessBrush");
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            LogBox.Clear();
        }

        // ================= WebUI =================

        private void StartWebUiIfEnabled()
        {
            if (_settings.WebUiEnabled)
            {
                StartWebUi();
            }
            else
            {
                UpdateWebUiHint();
            }
        }

        private bool StartWebUi()
        {
            StopWebUi(false);

            int port;
            if (!int.TryParse((WebUiPortBox.Text ?? "").Trim(), out port) || port < 1 || port > 65535)
            {
                Log(Strings.T("webui.portInvalid"));
                SetStatus("status.launchFailed", "ErrorBrush");

                _suppressWebUiToggle = true;
                WebUiCheck.IsChecked = false;
                _suppressWebUiToggle = false;

                return false;
            }

            _settings.WebUiPort = port;
            _settings.WebUiEnabled = true;
            _settings.Save();

            try
            {
                _webUi = new WebUiServer(port, _settings.WebUiToken, this);
                _webUi.Start();
                Log(Strings.T("log.webuiStarted", _webUi.Url));
                return true;
            }
            catch (Exception ex)
            {
                _webUi = null;
                Log(Strings.T("webui.startFail", ex.Message));

                _suppressWebUiToggle = true;
                WebUiCheck.IsChecked = false;
                _suppressWebUiToggle = false;

                return false;
            }
            finally
            {
                UpdateWebUiHint();
            }
        }

        private void StopWebUi(bool save)
        {
            if (_webUi != null)
            {
                _webUi.Dispose();
                _webUi = null;
                Log(Strings.T("log.webuiStopped"));
            }

            if (save)
            {
                _settings.WebUiEnabled = false;
                _settings.Save();
            }

            UpdateWebUiHint();
        }

        private void UpdateWebUiHint()
        {
            bool running = _webUi != null && _webUi.IsRunning;

            WebUiHint.Text = running
                ? Strings.T("webui.running", _webUi.Url)
                : Strings.T("webui.hint");

            WebUiOpenButton.IsEnabled = running;
        }

        private void WebUiCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressWebUiToggle)
            {
                return;
            }

            if (WebUiCheck.IsChecked == true)
            {
                StartWebUi();
            }
            else
            {
                StopWebUi(true);
            }
        }

        private void WebUiOpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (_webUi == null || !_webUi.IsRunning)
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(_webUi.Url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log(Strings.T("webui.openFail", ex.Message));
            }
        }

        // ================= IWebUiHost =================

        WebUiStatus IWebUiHost.GetStatus()
        {
            return new WebUiStatus
            {
                ProxyUrl = _settings.ProxyUrl,
                OpenCodePath = _settings.OpenCodePath,
                OpenCodeRunning = IsOpenCodeRunning(),
                Language = Strings.Language,
                Version = AppVersion,
            };
        }

        UsageReport IWebUiHost.GetUsage()
        {
            return UsageReader.Read();
        }

        async Task<AccountUsage> IWebUiHost.GetAccountUsageAsync()
        {
            try
            {
                return await AccountUsageReader.ReadAsync(_settings);
            }
            catch (Exception ex)
            {
                return new AccountUsage { Ok = false, Error = ex.Message };
            }
        }

        WebUiAction IWebUiHost.LaunchOpenCode()
        {
            CollectUiToSettings();
            _settings.Save();

            LaunchResult result = Launcher.Launch(_settings);
            var action = new WebUiAction { Ok = result.Ok, Message = result.Message };

            if (!result.Ok)
            {
                action.Lines.Add("✗ " + result.Message);
                return action;
            }

            action.Lines.Add("✓ " + result.Message);

            // 和界面里一样，实测确认它没有立刻退出
            if (!Launcher.WaitForAlive(result.Process, 3500))
            {
                int exitCode = -1;
                try
                {
                    exitCode = result.Process.ExitCode;
                }
                catch
                {
                    // 拿不到退出码就算了
                }

                action.Ok = false;
                action.Message = Strings.T("status.exited");
                action.Lines.Add(Strings.T("log.exited", exitCode));
                action.Lines.Add(Strings.T("log.exitedHint1"));
                action.Lines.Add(Strings.T("log.exitedHint2"));
                return action;
            }

            action.Lines.Add(Strings.T("log.stable"));
            return action;
        }

        async Task<WebUiAction> IWebUiHost.CheckProxyAsync()
        {
            CollectUiToSettings();

            var action = new WebUiAction { Ok = true, Message = Strings.T("status.checking") };

            try
            {
                CheckResult result = await ProxyChecker.CheckAsync(_settings);
                action.Lines.AddRange(result.Lines);
                action.Lines.Add(Strings.T("log.conclusion", result.Summary));
                action.Message = result.Summary;
            }
            catch (Exception ex)
            {
                action.Ok = false;
                action.Message = Strings.T("log.checkError", ex.Message);
                action.Lines.Add(action.Message);
            }

            return action;
        }

        private static bool IsOpenCodeRunning()
        {
            try
            {
                return Process.GetProcessesByName("OpenCode").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        // ================= 检测 =================

        private async void CheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy)
            {
                return;
            }

            CollectUiToSettings();
            SetBusy(true);
            SetStatus("status.checking", "TextSecondaryBrush");
            Log("");
            Log(Strings.T("log.checkStart", DateTime.Now.ToString("HH:mm:ss")));

            try
            {
                CheckResult result = await ProxyChecker.CheckAsync(_settings);

                foreach (string line in result.Lines)
                {
                    Log(line);
                }

                Log(Strings.T("log.conclusion", result.Summary));

                bool exitIsFine = !string.IsNullOrEmpty(result.ExitCountry)
                                  && !string.Equals(result.ExitCountry, "CN", StringComparison.OrdinalIgnoreCase);

                if (exitIsFine)
                {
                    SetStatusText(result.Summary, "SuccessBrush");
                }
                else
                {
                    SetStatusText(result.Summary, "WarnBrush");
                }
            }
            catch (Exception ex)
            {
                Log(Strings.T("log.checkError", ex.Message));
                SetStatus("status.checkFailed", "ErrorBrush");
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ================= 启动 =================

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy)
            {
                return;
            }

            CollectUiToSettings();
            _settings.Save();

            LaunchResult result = Launcher.Launch(_settings);
            Log("");

            if (!result.Ok)
            {
                Log("✗ " + result.Message);
                SetStatus("status.launchFailed", "ErrorBrush");
                MessageBox.Show(this, result.Message, Strings.T("dlg.launchFailedTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Log("✓ " + result.Message);
            SetStatus("status.verifying", "TextSecondaryBrush");
            SetBusy(true);

            bool alive = await WaitForAliveAsync(result.Process, 3500);

            SetBusy(false);

            if (!alive)
            {
                int exitCode = -1;
                try
                {
                    exitCode = result.Process.ExitCode;
                }
                catch
                {
                    // 拿不到退出码就算了
                }

                Log(Strings.T("log.exited", exitCode));
                Log(Strings.T("log.exitedHint1"));
                Log(Strings.T("log.exitedHint2"));
                SetStatus("status.exited", "ErrorBrush");

                MessageBox.Show(this, Strings.T("dlg.notRunningBody", exitCode), Strings.T("dlg.notRunningTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Log(Strings.T("log.stable"));
            SetStatus("status.launched", "SuccessBrush");

            if (_settings.CloseAfterLaunch)
            {
                Close();
            }
        }

        private static async Task<bool> WaitForAliveAsync(Process process, int milliseconds)
        {
            const int step = 250;
            int elapsed = 0;

            while (elapsed < milliseconds)
            {
                if (!Launcher.IsAlive(process))
                {
                    return false;
                }

                await Task.Delay(step);
                elapsed += step;
            }

            return true;
        }

        // ================= 辅助 =================

        /// <summary>按本地化 key 设置状态（语言切换后会重新应用）。</summary>
        private void SetStatus(string statusKey, string brushKey)
        {
            _statusKey = statusKey;
            _statusBrush = brushKey;
            SetStatusText(Strings.T(statusKey), brushKey);
        }

        /// <summary>直接设置一段已经是最终文案的状态。</summary>
        private void SetStatusText(string text, string brushKey)
        {
            _statusKey = text;
            _statusBrush = brushKey;

            Brush brush = (Brush)FindResource(brushKey);
            StatusText.Text = text;
            StatusText.Foreground = brush;
            StatusDot.Fill = brush;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            LaunchButton.IsEnabled = !busy;
            CheckButton.IsEnabled = !busy;
            SaveButton.IsEnabled = !busy;
            ClearButton.IsEnabled = !busy;
            Cursor = busy ? Cursors.Wait : Cursors.Arrow;
        }

        private void Log(string message)
        {
            LogBox.AppendText(message + Environment.NewLine);
            LogBox.ScrollToEnd();
        }
    }
}
