using System.Runtime.InteropServices;
using System.Text.Json;

// Console app that runs a hidden message loop for tray and hotkeys,
// uses SendInput for simulating mouse movement, and stores config
// in the application's folder (next to the executable).

namespace PreventLockConsole
{
    public class Config
    {
        public bool Enabled { get; set; } = true; // whether service can run at all
        public int MoveIntervalSeconds { get; set; } = 20; // how often to send input when idle
        public int IdleToStartSeconds { get; set; } = 30; // idle seconds before starting
        public int CheckIntervalMilliseconds { get; set; } = 1000; // how often to check idle
        public Hotkeys Hotkeys { get; set; } = new Hotkeys();
        public bool StartInTray { get; set; } = true; // whether to minimize to tray on start
    }

    public class Hotkeys
    {
        // default: Ctrl+Alt+P / Ctrl+Alt+E / Ctrl+Alt+Q
        public uint TogglePauseModifiers { get; set; } = HotkeyHelper.MOD_CONTROL | HotkeyHelper.MOD_ALT;
        public uint TogglePauseVKey { get; set; } = (uint)Keys.P;

        public uint ToggleEnableModifiers { get; set; } = HotkeyHelper.MOD_CONTROL | HotkeyHelper.MOD_ALT;
        public uint ToggleEnableVKey { get; set; } = (uint)Keys.E;

        public uint ExitModifiers { get; set; } = HotkeyHelper.MOD_CONTROL | HotkeyHelper.MOD_ALT;
        public uint ExitVKey { get; set; } = (uint)Keys.Q;
    }

    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("PreventLock 控制台（SendInput） - 启动中...");
            
            using var app = new PreventLockApplication();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                app.ExitApplication();
            };

            Console.WriteLine("命令：P=切换暂停，E=切换启用，Q=退出，S=显示状态");
            while (!app.IsExited)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.P) app.TogglePause();
                    else if (key.Key == ConsoleKey.E) app.ToggleEnable();
                    else if (key.Key == ConsoleKey.Q) app.ExitApplication();
                    else if (key.Key == ConsoleKey.S) app.ShowStatus();
                }

                Thread.Sleep(150);
            }

            Console.WriteLine("Exiting...");
        }
    }

    public class PreventLockApplication : IDisposable
    {
        private readonly string _appDir;
        private readonly string _configPath;
        private Config _config;

        private NotifyIcon? _trayIcon;
        private readonly System.Timers.Timer _checkTimer;
        private readonly System.Timers.Timer _moveTimer;

        private MessageWindow? _messageWindow;

        private bool _manualPaused = false; // paused by user
        private bool _running = false; // whether simulation is currently running
        private bool _disposed = false;

        private const int HOTKEY_ID_TOGGLE_PAUSE = 9001;
        private const int HOTKEY_ID_TOGGLE_ENABLE = 9002;
        private const int HOTKEY_ID_EXIT = 9003;


        private readonly Thread _uiThread;
        private volatile bool _exitRequested = false;

        public bool IsExited => _exitRequested;

        public PreventLockApplication()
        {
            _appDir = AppContext.BaseDirectory; // config in project/exe folder
            _configPath = Path.Combine(_appDir, "config.json");

            LoadOrCreateConfig();

            // start a UI thread to run message loop for NotifyIcon and hotkeys
            var uiStarted = new ManualResetEvent(false);
            _uiThread = new Thread(() =>
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                _messageWindow = new MessageWindow(HandleHotkey);

                RegisterHotkeys();

                _trayIcon = new NotifyIcon();
                
                try
                {
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    var names = assembly.GetManifestResourceNames();
                    // 列出所有嵌入资源名，方便调试（运行时会在控制台输出）
                    foreach (var n in names)
                    {
                        Console.WriteLine("嵌入资源: " + n);
                    }

                    string icoName = null;
                    foreach (var n in names)
                    {
                        if (n.EndsWith("preventlock.ico", StringComparison.OrdinalIgnoreCase))
                        {
                            icoName = n;
                            break;
                        }
                    }

                    if (icoName != null)
                    {
                        using var stream = assembly.GetManifestResourceStream(icoName);
                        if (stream != null)
                        {
                            stream.Position = 0;
                            _trayIcon.Icon = new Icon(stream);
                        }
                        else
                        {
                            Console.WriteLine("找到资源名但无法打开流: " + icoName);
                            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                        }
                    }
                    else
                    {
                        // 尝试从输出目录的 Resources 文件夹加载（fallback）
                        var fallback = Path.Combine(AppContext.BaseDirectory, "Resources", "preventlock.ico");
                        if (File.Exists(fallback))
                        {
                            _trayIcon.Icon = new Icon(fallback);
                        }
                        else
                        {
                            Console.WriteLine("未找到嵌入资源 preventlock.ico，也未在输出 Resources 目录找到文件。使用默认图标。");
                            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("load resources ico error: " + ex.Message);
                    _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                }
                
                _trayIcon.Text = "PreventLock（SendInput）";
                _trayIcon.Visible = true;
                
                var ctx = new ContextMenuStrip();
                ctx.Items.Add("Switch Pause (Ctrl+Alt+P)", null, (s, e) => TogglePause());
                ctx.Items.Add("Enable (Ctrl+Alt+E)", null, (s, e) => ToggleEnable());
                ctx.Items.Add(new ToolStripSeparator());
                ctx.Items.Add("Show Status", null, (s, e) => ShowStatus());
                ctx.Items.Add("Exit (Ctrl+Alt+Q)", null, (s, e) => ExitApplication());

                _trayIcon.ContextMenuStrip = ctx;
                _trayIcon.DoubleClick += (s, e) => ShowStatus();

                if (_config.StartInTray)
                {
                    // If the user wants start in tray, we won't show any form.
                }

                uiStarted.Set();
                // 启动时气泡提示
                try
                {
                    _trayIcon?.ShowBalloonTip(3000, "PreventLock", "PreventLock 已启动。右键菜单可操作。", ToolTipIcon.Info);
                }
                catch
                {
                }

                Application.Run();

                // cleanup when message loop ends
                UnregisterHotkeys();
                _trayIcon?.Dispose();
                _messageWindow?.Dispose();
            }) { IsBackground = true, Name = "PreventLock.UI" };

            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();
            uiStarted.WaitOne();

            // 初始化最后一次人为输入时间，避免把程序自身的模拟输入识别为人为操作
            InputHelper.InitializeLastInput();

            // timers for checking idle and performing simulation
            _checkTimer = new System.Timers.Timer(_config.CheckIntervalMilliseconds);
            _checkTimer.Elapsed += (s, e) => OnCheckTimer();
            _checkTimer.AutoReset = true;
            _checkTimer.Start();

            _moveTimer = new System.Timers.Timer(_config.MoveIntervalSeconds * 1000);
            _moveTimer.Elapsed += (s, e) => DoSimulate();
            _moveTimer.AutoReset = true;

            Console.WriteLine("PreventLock 已初始化，配置文件路径：" + _configPath);

            // 启动时输出小建议（中文）
            Console.WriteLine(@"小建议：
1) 将 IdleToStartSeconds 设置为比公司锁屏时间稍小的值（例如公司锁屏 180 秒，可设为 150~170）；
2) MoveIntervalSeconds 不要太短，避免频繁输入；建议 20~60 秒；
");
        }

        private void LoadOrCreateConfig()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var txt = File.ReadAllText(_configPath);
                    _config = JsonSerializer.Deserialize<Config>(txt) ?? new Config();
                }
                catch
                {
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
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_config, options);
            // 写入标准配置文件（供程序读取）
            File.WriteAllText(_configPath, json);

            // 同时写入带中文注释的配置说明文件，方便用户阅读和修改（注意：此文件仅作说明，程序不解析注释文件）
            try
            {
                var commentPath = Path.Combine(_appDir, "config.comment.json");
                var commentHeader = @"
// PreventLock 配置说明（中文注释）
//
// Enabled: 是否启用自动防锁服务（true/false）
// MoveIntervalSeconds: 空闲时每隔多少秒模拟一次鼠标移动（整数，秒）
// IdleToStartSeconds: 多久无人操作后开始模拟（整数，秒）
// CheckIntervalMilliseconds: 检查空闲状态的间隔（毫秒）
// StartInTray: 是否以托盘模式启动（true/false）
//
// Hotkeys: 热键配置（可读字符串形式，示例：Ctrl+Alt+P）
//   写法规则：用“+”连接多个键名（不区分大小写），支持 Ctrl / Alt / Shift / Win 作为修饰键。
//   支持的主键：A-Z, 0-9, F1-F24, Esc, Tab, Enter, Space, Up, Down, Left, Right 等。
//   示例：Ctrl+Alt+P, Shift+F5, Win+Esc
//
// 示例 Hotkeys 字段：
//   ""Hotkeys"": {
//       ""TogglePause"": ""Ctrl+Alt+P"",
//       ""ToggleEnable"": ""Ctrl+Alt+E"",
//       ""Exit"": ""Ctrl+Alt+Q""
//   }
";


                File.WriteAllText(commentPath, commentHeader + json);
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
            ShowTrayText("PreventLock: running");
        }

        private void StopSimulation()
        {
            _running = false;
            _moveTimer.Stop();
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 已停止模拟");
            ShowTrayText("PreventLock: paused");
        }

        private void DoSimulate()
        {
            var idleSec = InputHelper.GetIdleSeconds();
            if (idleSec < _config.IdleToStartSeconds) return; // user came back

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 模拟输入触发（空闲 {idleSec:F1} 秒）");
            InputHelper.SimulateTinyMouseJitter_SendInput();
        }

        public void TogglePause()
        {
            _manualPaused = !_manualPaused;
            if (_manualPaused)
            {
                StopSimulation();
                Console.WriteLine("已手动暂停");
            }
            else
            {
                Console.WriteLine("已取消手动暂停");
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
            }
            else
            {
                Console.WriteLine("已通过切换启用");
            }
        }

        public void ShowStatus()
        {
            var idle = InputHelper.GetIdleSeconds();
            var status = _config.Enabled ? "Enabled" : "Disabled";
            status += _manualPaused ? ", Manually paused" : (_running ? ", Running" : ", Idle (not simulating)");
            var msg = $"状态：{status} 空闲秒数：{idle} 配置：{_configPath}";
            Console.WriteLine(msg);
            _trayIcon?.ShowBalloonTip(2000, "PreventLock Status", msg, ToolTipIcon.Info);
        }

        internal void ShowTrayText(string text)
        {
            if (_trayIcon != null)
            {
                try
                {
                    _trayIcon.Text = text;
                }
                catch
                {
                }
            }
        }

        private void RegisterHotkeys()
        {
            try
            {
                if (_messageWindow == null) return;
                HotkeyHelper.RegisterHotkey(_messageWindow.Handle, HOTKEY_ID_TOGGLE_PAUSE,
                    _config.Hotkeys.TogglePauseModifiers, _config.Hotkeys.TogglePauseVKey);
                HotkeyHelper.RegisterHotkey(_messageWindow.Handle, HOTKEY_ID_TOGGLE_ENABLE,
                    _config.Hotkeys.ToggleEnableModifiers, _config.Hotkeys.ToggleEnableVKey);
                HotkeyHelper.RegisterHotkey(_messageWindow.Handle, HOTKEY_ID_EXIT, _config.Hotkeys.ExitModifiers,
                    _config.Hotkeys.ExitVKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine("警告：全局热键注册失败，可能需要以管理员权限运行。" + ex.Message);
            }
        }

        private void UnregisterHotkeys()
        {
            try
            {
                if (_messageWindow == null) return;
                HotkeyHelper.UnregisterHotkey(_messageWindow.Handle, HOTKEY_ID_TOGGLE_PAUSE);
                HotkeyHelper.UnregisterHotkey(_messageWindow.Handle, HOTKEY_ID_TOGGLE_ENABLE);
                HotkeyHelper.UnregisterHotkey(_messageWindow.Handle, HOTKEY_ID_EXIT);
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

            // stop UI thread message loop
            try
            {
                Application.ExitThread();
                Application.Exit();
            }
            catch
            {
            }

            // allow UI thread to cleanup
            Thread.Sleep(200);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _checkTimer?.Stop();
            _moveTimer?.Stop();
            UnregisterHotkeys();
            _messageWindow?.Dispose();
            _trayIcon?.Dispose();
        }
    }

    // message-only window to receive WM_HOTKEY
    public class MessageWindow : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private readonly Action<int> _onHotkey;

        public MessageWindow(Action<int> onHotkey)
        {
            _onHotkey = onHotkey;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                var id = m.WParam.ToInt32();
                _onHotkey?.Invoke(id);
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            DestroyHandle();
        }
    }

    public static class HotkeyHelper
    {
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public static void RegisterHotkey(IntPtr hWnd, int id, uint modifiers, uint vkey)
        {
            if (!RegisterHotKey(hWnd, id, modifiers, vkey))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public static void UnregisterHotkey(IntPtr hWnd, int id)
        {
            UnregisterHotKey(hWnd, id);
        }
    }

    public static class InputHelper
    {
        // LASTINPUTINFO / idle calculation with simulated-input filtering
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        static extern ulong GetTickCount64();

        // track last human input tick separately from simulated input tick
        private static ulong _lastSimulatedTick = 0;
        private static ulong _lastHumanInputTick = 0;

        private static readonly object _tickLock = new object();

        // initialize last human input tick at startup
        public static void InitializeLastInput()
        {
            var li = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (!GetLastInputInfo(ref li)) return;
            lock (_tickLock)
            {
                _lastHumanInputTick = li.dwTime;
            }
        }

        public static double GetIdleSeconds()
        {
            var lastInput = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (!GetLastInputInfo(ref lastInput)) return 0;

            var now = GetTickCount64();
            ulong lastInputTick = lastInput.dwTime;

            lock (_tickLock)
            {
                // if the last input reported is newer than our recorded human tick
                // and it's also newer than the last simulated tick, we assume it's real human input
                if (lastInputTick > _lastHumanInputTick && lastInputTick > _lastSimulatedTick)
                {
                    _lastHumanInputTick = lastInputTick;
                }

                // if _lastHumanInputTick is still zero (very early), fall back to lastInputTick
                if (_lastHumanInputTick == 0)
                {
                    _lastHumanInputTick = lastInputTick;
                }

                ulong diff = now - _lastHumanInputTick;
                if ((long)diff < 0) diff = 0;
                return diff / 1000.0;
            }
        }

        // SendInput P/Invoke
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public INPUTUNION U;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        const uint INPUT_MOUSE = 0;
        const uint MOUSEEVENTF_MOVE = 0x0001;

        public static void SimulateTinyMouseJitter_SendInput()
        {
            // Move +1 then -1 using SendInput to avoid cursor jump
            var inputs = new INPUT[2];

            inputs[0].type = INPUT_MOUSE;
            inputs[0].U.mi = new MOUSEINPUT
                { dx = 1, dy = 0, mouseData = 0, dwFlags = MOUSEEVENTF_MOVE, time = 0, dwExtraInfo = UIntPtr.Zero };

            inputs[1].type = INPUT_MOUSE;
            inputs[1].U.mi = new MOUSEINPUT
                { dx = -1, dy = 0, mouseData = 0, dwFlags = MOUSEEVENTF_MOVE, time = 0, dwExtraInfo = UIntPtr.Zero };

            var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());

            // record the tick of simulated input so we can ignore it when computing idle
            lock (_tickLock)
            {
                _lastSimulatedTick = GetTickCount64();
            }
        }
    }
}