namespace PreventLockConsole
{
    public static class Logger
    {
        private static readonly object _logLock = new();

        private static readonly string _logPath =
            Path.Combine(AppContext.BaseDirectory, $"app-{DateTime.Now:yyyy-MM-dd}.log");

        public static void Log(string message)
        {
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                lock (_logLock)
                {
                    File.AppendAllText(_logPath, line);
                }
            }
            catch
            {
                // ignore logging errors
            }
        }
    }
}