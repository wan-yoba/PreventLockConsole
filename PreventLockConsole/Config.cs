namespace PreventLockConsole
{
    public class Config
    {
        public bool Enabled { get; set; } = true;
        public int MoveIntervalSeconds { get; set; } = 20;
        public int IdleToStartSeconds { get; set; } = 30;
        public int CheckIntervalMilliseconds { get; set; } = 1000;
        public Hotkeys Hotkeys { get; set; } = new();
        public string Language { get; set; } = "zh-CN"; // language code (zh-CN or en)
        public bool StartInTray { get; set; } = true;
        public bool UseExecutionState { get; set; } = false;
    }

    public class Hotkeys
    {
        public string TogglePause { get; set; } = "Ctrl+Alt+P";
        public string ToggleEnable { get; set; } = "Ctrl+Alt+E";
        public string Exit { get; set; } = "Ctrl+Alt+Q";
    }
}