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
                    switch (key.Key)
                    {
                        case ConsoleKey.P:
                            app.TogglePause();
                            break;
                        case ConsoleKey.E:
                            app.ToggleEnable();
                            break;
                        case ConsoleKey.Q:
                            app.ExitApplication();
                            break;
                        case ConsoleKey.S:
                            app.ShowStatus();
                            break;
                    }
                }

                Thread.Sleep(150);
            }

            Console.WriteLine("Exiting...");
        }
    }
}