namespace PreventLockConsole
{
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
}