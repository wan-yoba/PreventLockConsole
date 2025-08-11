using System.Text.Json;

namespace PreventLockConsole
{
    public static class Language
    {
        private static Dictionary<string?, string> _dict = new(StringComparer.OrdinalIgnoreCase);
        public static string CurrentLanguage { get; private set; } = "zh-CN";
        private static readonly string _appDir = AppContext.BaseDirectory;
        private static readonly string _enFile = Path.Combine(_appDir, "lang.en.json");
        private static readonly string _zhFile = Path.Combine(_appDir, "lang.zh-CN.json");

        private static readonly Dictionary<string, string> _defaultEn = new()
        {
            ["Startup"] = "PreventLock (SendInput) - starting...",
            ["Commands"] = "Commands: P=TogglePause, E=ToggleEnable, Q=Quit, S=ShowStatus",
            ["Initialized"] = "PreventLock initialized. Config path: {0}",
            ["Suggestion"] =
                "Suggestions:\\n1) Set IdleToStartSeconds a little less than company lock time.\\n2) MoveIntervalSeconds 20-60s recommended.\\n3) Enable log file if you want silent run.",
            ["TrayTooltip"] = "PreventLock (SendInput)",
            ["TrayStarted"] = "PreventLock started. Right-click the tray icon for options.",
            ["TogglePause"] = "Toggle Pause",
            ["ToggleEnable"] = "Enable/Disable",
            ["ShowStatus"] = "Show Status",
            ["Exit"] = "Exit",
            ["Setting"] = "Setting",
            ["Language"] = "Language",
            ["English"] = "English",
            ["Chinese"] = "中文",
            ["StatusTitle"] = "PreventLock Status",
            ["StatusMsg"] = "Status: {0}\\nIdle seconds: {1}\\nConfig: {2}",
            ["StartSim"] = "Started simulation (idle). Interval: {0}s",
            ["StopSim"] = "Stopped simulation",
            ["SimTriggered"] = "Simulate input triggered (idle {0:F1}s)",
            ["SimSent"] = "Simulated input sent",
            ["ManualPaused"] = "Manually paused",
            ["ManualResume"] = "Manual pause cleared",
            ["Disabled"] = "Disabled via toggle",
            ["Enabled"] = "Enabled via toggle",
            ["HotkeyWarning"] = "Warning: failed to register some global hotkeys. Try running as administrator. {0}",
            ["IconNotFound"] = "Icon not found, using default.",
            ["LoadIconError"] = "Load icon error: {0}",
            ["Exiting"] = "Exiting..."
        };

        private static readonly Dictionary<string, string> _defaultZh = new()
        {
            ["Startup"] = "PreventLock 控制台（SendInput） - 启动中...",
            ["Commands"] = "命令：P=切换暂停，E=切换启用，Q=退出，S=显示状态",
            ["Initialized"] = "PreventLock 已初始化，配置文件路径：{0}",
            ["Suggestion"] =
                "小建议：\\n1) 将 IdleToStartSeconds 设置为比公司锁屏时间稍小的值。\\n2) MoveIntervalSeconds 不要太短，建议 20~60 秒。\\n3) 若需静默运行，可启用日志。",
            ["TrayTooltip"] = "PreventLock（SendInput）",
            ["TrayStarted"] = "PreventLock 已启动。右键图标打开菜单。",
            ["TogglePause"] = "切换暂停",
            ["ToggleEnable"] = "启用/禁用",
            ["ShowStatus"] = "显示状态",
            ["Exit"] = "退出",
            ["Language"] = "语言",
            ["English"] = "English",
            ["Chinese"] = "中文",
            ["Setting"] = "配置",
            ["StatusTitle"] = "PreventLock 状态",
            ["StatusMsg"] = "状态：{0}\\n空闲秒数：{1}\\n配置：{2}",
            ["StartSim"] = "已开始模拟（空闲）。每 {0} 秒触发一次。",
            ["StopSim"] = "已停止模拟",
            ["SimTriggered"] = "模拟输入触发（空闲 {0:F1} 秒）",
            ["SimSent"] = "模拟输入已发送",
            ["ManualPaused"] = "已手动暂停",
            ["ManualResume"] = "已取消手动暂停",
            ["Disabled"] = "已通过切换禁用",
            ["Enabled"] = "已通过切换启用",
            ["HotkeyWarning"] = "警告：全局热键注册失败，可能需要以管理员权限运行。{0}",
            ["IconNotFound"] = "未找到图标，使用默认图标。",
            ["LoadIconError"] = "加载图标出错：{0}",
            ["Exiting"] = "退出..."
        };

        public static void EnsureFilesExist()
        {
            try
            {
                if (!File.Exists(_enFile))
                {
                    File.WriteAllText(_enFile,
                        JsonSerializer.Serialize(_defaultEn, new JsonSerializerOptions { WriteIndented = true }));
                }

                if (!File.Exists(_zhFile))
                {
                    File.WriteAllText(_zhFile,
                        JsonSerializer.Serialize(_defaultZh, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch
            {
            }
        }

        public static void Load(string langCode)
        {
            EnsureFilesExist();
            CurrentLanguage = string.IsNullOrWhiteSpace(langCode) ? "zh-CN" : langCode;
            string path = CurrentLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? _zhFile : _enFile;
            try
            {
                var txt = File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(txt);
                if (dict != null)
                {
                    _dict = new Dictionary<string?, string>(dict, StringComparer.OrdinalIgnoreCase);
                    return;
                }
            }
            catch
            {
            }

            _dict = CurrentLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                ? new(_defaultZh)
                : new(_defaultEn);
        }

        public static string? Get(string? key, params object[]? args)
        {
            if (key == null) return string.Empty;
            if (_dict.TryGetValue(key, out var v))
            {
                return args is { Length: > 0 } ? string.Format(v, args) : v;
            }

            return args is { Length: > 0 } ? string.Format(key, args) : key;
        }
    }
}