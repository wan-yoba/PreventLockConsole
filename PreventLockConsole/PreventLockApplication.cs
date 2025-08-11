using System.Reflection;

namespace PreventLockConsole
{
    public class PreventLockApplication : IDisposable
    {
        private readonly string _appDir;
        private readonly string _configPath;
        private Config _config;

        private NotifyIcon? _trayIcon;
        private System.Timers.Timer _checkTimer = null;
        private System.Timers.Timer _moveTimer = null;

        private MessageWindow? _messageWindow;
        private Form? _uiContextForm;
        private ToolStripMenuItem? menuTogglePause;
        private ToolStripMenuItem? menuSetting;
        private ToolStripMenuItem? menuToggleEnable;
        private ToolStripMenuItem? menuShowStatus;
        private ToolStripMenuItem? menuExit;
        private ToolStripMenuItem? menuLangEn;
        private ToolStripMenuItem? menuLangZh;

        private bool _manualPaused = false;
        private bool _running = false;
        private bool _disposed = false;

        private const int HOTKEY_ID_TOGGLE_PAUSE = 9001;
        private const int HOTKEY_ID_TOGGLE_ENABLE = 9002;
        private const int HOTKEY_ID_EXIT = 9003;

        private readonly Thread _uiThread;
        private volatile bool _exitRequested = false;
        public bool IsExited => _exitRequested;

        public PreventLockApplication()
        {
            _appDir = AppContext.BaseDirectory;
            _configPath = Path.Combine(_appDir, "config.json");

            LoadOrCreateConfig();
            ParseHotkeys();

            // load language resources based on config
            Language.Load(_config.Language ?? "zh-CN");

            var uiStarted = new ManualResetEvent(false);
            _uiThread = new Thread(() =>
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                _uiContextForm = new Form()
                {
                    ShowInTaskbar = false, Size = new System.Drawing.Size(0, 0), FormBorderStyle = FormBorderStyle.None,
                    StartPosition = FormStartPosition.Manual, Location = new System.Drawing.Point(-2000, -2000)
                };
                _uiContextForm = new Form()
                {
                    ShowInTaskbar = false, Size = new System.Drawing.Size(0, 0), FormBorderStyle = FormBorderStyle.None,
                    StartPosition = FormStartPosition.Manual, Location = new System.Drawing.Point(-2000, -2000)
                };
                _messageWindow = new MessageWindow(HandleHotkey);

                _trayIcon = new NotifyIcon();
                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    foreach (var n in assembly.GetManifestResourceNames()) Console.WriteLine("嵌入资源: " + n);
                    string icoName = null;
                    foreach (var n in assembly.GetManifestResourceNames())
                        if (n.EndsWith("preventlock.ico", StringComparison.OrdinalIgnoreCase))
                        {
                            icoName = n;
                            break;
                        }

                    if (icoName != null)
                    {
                        using var stream = assembly.GetManifestResourceStream(icoName);
                        if (stream != null) _trayIcon.Icon = new System.Drawing.Icon(stream);
                        else
                        {
                            Logger.Log("找到资源名但无法打开流: " + icoName);
                            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                        }
                    }
                    else
                    {
                        var fallback = Path.Combine(AppContext.BaseDirectory, "Resources", "preventlock.ico");
                        if (File.Exists(fallback)) _trayIcon.Icon = new System.Drawing.Icon(fallback);
                        else
                        {
                            Logger.Log("未找到 preventlock.ico，使用默认图标");
                            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log("load resources ico error: " + ex.Message);
                    _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                }

                _trayIcon.Text = "PreventLock（SendInput）";
                _trayIcon.Visible = true;


                var ctx = new ContextMenuStrip();

                // language submenu
                var langMenu = new ToolStripMenuItem(Language.Get("Language"));
                menuLangEn = new ToolStripMenuItem(Language.Get("English"))
                {
                    Checked = (_config.Language?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true)
                };
                menuLangZh = new ToolStripMenuItem(Language.Get("Chinese"))
                {
                    Checked = (_config.Language?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true)
                };
                menuLangEn.Click += (s, e) => SwitchLanguage("en");
                menuLangZh.Click += (s, e) => SwitchLanguage("zh-CN");
                langMenu.DropDownItems.Add(menuLangEn);
                langMenu.DropDownItems.Add(menuLangZh);

                // main menu items (store references)
                menuTogglePause = new ToolStripMenuItem(Language.Get("TogglePause"), null, (s, e) => TogglePause());
                menuToggleEnable = new ToolStripMenuItem(Language.Get("ToggleEnable"), null, (s, e) => ToggleEnable());
                menuShowStatus = new ToolStripMenuItem(Language.Get("ShowStatus"), null, (s, e) => ShowStatus());
                menuExit = new ToolStripMenuItem(Language.Get("Exit"), null, (s, e) => ExitApplication());
                menuSetting = new ToolStripMenuItem(Language.Get("Setting"), null, (s, e) => ShowSettingsForm());

                ctx.Items.Add(menuTogglePause);
                ctx.Items.Add(menuToggleEnable);
                ctx.Items.Add(langMenu);
                ctx.Items.Add(new ToolStripSeparator());
                ctx.Items.Add(menuShowStatus);
                ctx.Items.Add(menuSetting);
                ctx.Items.Add(menuExit);

                _trayIcon.ContextMenuStrip = ctx;

                _trayIcon.DoubleClick += (s, e) => ShowStatus();

                try
                {
                    RegisterHotkeys();
                }
                catch
                {
                }

                uiStarted.Set();
                try
                {
                    _trayIcon?.ShowBalloonTip(3000, "PreventLock", "PreventLock 已启动。右键菜单可操作。", ToolTipIcon.Info);
                }
                catch
                {
                }

                Application.Run(_uiContextForm);

                UnregisterHotkeys();
                _trayIcon?.Dispose();
                _messageWindow?.Dispose();
            }) { IsBackground = true, Name = "PreventLock.UI" };

            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();
            uiStarted.WaitOne();

            InputHelper.InitializeLastInput();

            _checkTimer = new System.Timers.Timer(_config.CheckIntervalMilliseconds);
            _checkTimer.Elapsed += (s, e) => OnCheckTimer();
            _checkTimer.AutoReset = true;
            _checkTimer.Start();

            _moveTimer = new System.Timers.Timer(_config.MoveIntervalSeconds * 1000);
            _moveTimer.Elapsed += (s, e) => DoSimulate();
            _moveTimer.AutoReset = true;

            Console.WriteLine("PreventLock 已初始化，配置文件路径：" + _configPath);
            Logger.Log("PreventLock 启动，配置路径: " + _configPath);
        }

        private void LoadOrCreateConfig()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var txt = File.ReadAllText(_configPath);
                    _config = System.Text.Json.JsonSerializer.Deserialize<Config>(txt) ?? new Config();
                }
                catch (Exception ex)
                {
                    Logger.Log("读取配置失败，使用默认配置。错误: " + ex.Message);
                    _config = new Config();
                    SaveConfig();
                }
            }
            else
            {
                _config = new Config();
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(_config, options);
            File.WriteAllText(_configPath, json);
            try
            {
                var commentPath = Path.Combine(_appDir, "config.comment.json");
                var commentHeader = @"// PreventLock 配置说明（中文注释）\r\n";
                File.WriteAllText(commentPath, commentHeader + json);
            }
            catch (Exception ex)
            {
                Logger.Log("写入注释配置失败: " + ex.Message);
            }
        }

        private void ParseHotkeys()
        {
            try
            {
                var hk = _config.Hotkeys;
                // ensure defaults present
                if (string.IsNullOrWhiteSpace(hk.TogglePause)) hk.TogglePause = "Ctrl+Alt+P";
                if (string.IsNullOrWhiteSpace(hk.ToggleEnable)) hk.ToggleEnable = "Ctrl+Alt+E";
                if (string.IsNullOrWhiteSpace(hk.Exit)) hk.Exit = "Ctrl+Alt+Q";
                Logger.Log("热键解析 (strings): Pause=" + hk.TogglePause + ", Enable=" + hk.ToggleEnable + ", Exit=" +
                           hk.Exit);
            }
            catch (Exception ex)
            {
                Logger.Log("解析热键失败: " + ex.Message);
            }
        }


        private void SwitchLanguage(string langCode)
        {
            try
            {
                _config.Language = langCode;
                SaveConfig();
                Language.Load(langCode);

                if (_uiContextForm != null && !_uiContextForm.IsDisposed)
                {
                    _uiContextForm.BeginInvoke(new Action(() => ApplyLanguageToUi()));
                }
                else
                {
                    ApplyLanguageToUi();
                }

                Console.WriteLine(Language.Get("TrayStarted"));
            }
            catch (Exception ex)
            {
                Console.WriteLine("SwitchLanguage error: " + ex.Message);
            }
        }

        private void ApplyLanguageToUi()
        {
            try
            {
                if (_trayIcon != null)
                {
                    _trayIcon.Text = Language.Get("TrayTooltip");
                    if (menuTogglePause != null) menuTogglePause.Text = Language.Get("TogglePause");
                    if (menuToggleEnable != null) menuToggleEnable.Text = Language.Get("ToggleEnable");
                    if (menuShowStatus != null) menuShowStatus.Text = Language.Get("ShowStatus");
                    if (menuExit != null) menuExit.Text = Language.Get("Exit");
                    if (menuLangEn != null) menuLangEn.Text = Language.Get("English");
                    if (menuLangZh != null) menuLangZh.Text = Language.Get("Chinese");

                    if (menuLangEn != null)
                        menuLangEn.Checked =
                            Language.CurrentLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase);
                    if (menuLangZh != null)
                        menuLangZh.Checked =
                            Language.CurrentLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
            }
        }

        private void OnCheckTimer()
        {
            var idleSec = InputHelper.GetIdleSeconds();
            if (_manualPaused || !_config.Enabled)
            {
                if (_running) StopSimulation();
                return;
            }

            if (idleSec >= _config.IdleToStartSeconds)
            {
                if (!_running) StartSimulation();
            }
            else
            {
                if (_running) StopSimulation();
            }
        }

        private void StartSimulation()
        {
            _running = true;
            _moveTimer.Interval = Math.Max(100, _config.MoveIntervalSeconds * 1000);
            _moveTimer.Start();
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 已开始模拟（空闲）。每 {_config.MoveIntervalSeconds} 秒触发一次。");
            Logger.Log("开始模拟，每 " + _config.MoveIntervalSeconds + "s");
            if (_config.UseExecutionState)
            {
                InputHelper.ApplyExecutionState(true);
                Logger.Log("启用 SetThreadExecutionState");
            }
        }

        private void StopSimulation()
        {
            _running = false;
            _moveTimer.Stop();
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 已停止模拟");
            Logger.Log("停止模拟");
            if (_config.UseExecutionState)
            {
                InputHelper.ApplyExecutionState(false);
                Logger.Log("恢复 SetThreadExecutionState");
            }
        }

        private void DoSimulate()
        {
            var idleSec = InputHelper.GetIdleSeconds();
            if (idleSec < _config.IdleToStartSeconds) return;
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 模拟输入触发（空闲 {idleSec:F1} 秒）");
            Logger.Log("模拟输入触发，空闲: " + idleSec);
            if (!_config.UseExecutionState)
            {
                InputHelper.SimulateTinyMouseJitter_SendInput();
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 模拟输入已发送");
                Logger.Log("模拟输入已发送");
            }
        }

        public void TogglePause()
        {
            _manualPaused = !_manualPaused;
            if (_manualPaused)
            {
                StopSimulation();
                Console.WriteLine("已手动暂停");
                Logger.Log("已手动暂停");
            }
            else
            {
                Console.WriteLine("已取消手动暂停");
                Logger.Log("已取消手动暂停");
            }
        }

        public void ToggleEnable()
        {
            _config.Enabled = !_config.Enabled;
            SaveConfig();
            if (!_config.Enabled)
            {
                StopSimulation();
                Console.WriteLine("已通过切换禁用");
                Logger.Log("已通过切换禁用");
            }
            else
            {
                Console.WriteLine("已通过切换启用");
                Logger.Log("已通过切换启用");
            }
        }

        public void ShowStatus()
        {
            var idle = InputHelper.GetIdleSeconds();
            var status = _config.Enabled ? "Enabled" : "Disabled";
            status += _manualPaused ? ", Manually paused" : (_running ? ", Running" : ", Idle (not simulating)");
            var msg = $"状态：{status}\\r\\n空闲秒数：{idle}\\r\\n配置：{_configPath}";
            Console.WriteLine(msg);
            _trayIcon?.ShowBalloonTip(2000, "PreventLock Status", msg, ToolTipIcon.Info);
        }

        private void RegisterHotkeys()
        {
            try
            {
                if (_messageWindow == null) return;
                var hk = _config.Hotkeys;
                if (HotkeyParser.TryParse(hk.TogglePause, out var pm, out var pv))
                    HotkeyHelper.RegisterHotKey(_messageWindow.Handle, HOTKEY_ID_TOGGLE_PAUSE, pm, pv);
                if (HotkeyParser.TryParse(hk.ToggleEnable, out var em, out var ev))
                    HotkeyHelper.RegisterHotKey(_messageWindow.Handle, HOTKEY_ID_TOGGLE_ENABLE, em, ev);
                if (HotkeyParser.TryParse(hk.Exit, out var xm, out var xv))
                    HotkeyHelper.RegisterHotKey(_messageWindow.Handle, HOTKEY_ID_EXIT, xm, xv);
            }
            catch (Exception ex)
            {
                Logger.Log("注册热键失败: " + ex.Message);
            }
        }

        private void UnregisterHotkeys()
        {
            try
            {
                if (_messageWindow == null) return;
                HotkeyHelper.UnregisterHotKey(_messageWindow.Handle, HOTKEY_ID_TOGGLE_PAUSE);
                HotkeyHelper.UnregisterHotKey(_messageWindow.Handle, HOTKEY_ID_TOGGLE_ENABLE);
                HotkeyHelper.UnregisterHotKey(_messageWindow.Handle, HOTKEY_ID_EXIT);
            }
            catch
            {
            }
        }

        private void HandleHotkey(int id)
        {
            switch (id)
            {
                case HOTKEY_ID_TOGGLE_PAUSE: TogglePause(); break;
                case HOTKEY_ID_TOGGLE_ENABLE: ToggleEnable(); break;
                case HOTKEY_ID_EXIT: ExitApplication(); break;
            }
        }

        public void ExitApplication()
        {
            if (_exitRequested) return;
            _exitRequested = true;
            _checkTimer?.Stop();
            _moveTimer?.Stop();
            try
            {
                if (_uiContextForm != null) _uiContextForm.Invoke(new Action(() => { _uiContextForm.Close(); }));
            }
            catch
            {
            }

            Thread.Sleep(200);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _checkTimer?.Stop();
            _moveTimer?.Stop();
            UnregisterHotkeys();
            try
            {
                _messageWindow?.Dispose();
            }
            catch
            {
            }

            try
            {
                _trayIcon?.Dispose();
            }
            catch
            {
            }
        }

        private void ShowSettingsForm()
        {
            try
            {
                if (_uiContextForm != null)
                {
                    _uiContextForm.BeginInvoke(new Action(() =>
                    {
                        using var f = new SettingsForm(_config);
                        if (f.ShowDialog() == DialogResult.OK)
                        {
                            SaveConfig();
                            ParseHotkeys();
                            UnregisterHotkeys();
                            RegisterHotkeys();
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                Logger.Log("打开设置窗口失败: " + ex.Message);
            }
        }
    }
}