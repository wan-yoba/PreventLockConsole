using System.Runtime.InteropServices;

namespace PreventLockConsole
{
    public static class InputHelper
    {
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

        private static ulong _lastSimulatedTick = 0;
        private static ulong _lastHumanInputTick = 0;
        private static readonly object _tickLock = new object();

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
                if (lastInputTick > _lastHumanInputTick && lastInputTick > _lastSimulatedTick)
                    _lastHumanInputTick = lastInputTick;
                if (_lastHumanInputTick == 0) _lastHumanInputTick = lastInputTick;
                ulong diff = now - _lastHumanInputTick;
                if ((long)diff < 0) diff = 0;
                return diff / 1000.0;
            }
        }

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

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        const uint INPUT_MOUSE = 0;
        const uint MOUSEEVENTF_MOVE = 0x0001;

        public static void SimulateTinyMouseJitter_SendInput()
        {
            var inputs = new INPUT[2];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].U.mi = new MOUSEINPUT
                { dx = 1, dy = 0, mouseData = 0, dwFlags = MOUSEEVENTF_MOVE, time = 0, dwExtraInfo = UIntPtr.Zero };
            inputs[1].type = INPUT_MOUSE;
            inputs[1].U.mi = new MOUSEINPUT
                { dx = -1, dy = 0, mouseData = 0, dwFlags = MOUSEEVENTF_MOVE, time = 0, dwExtraInfo = UIntPtr.Zero };
            var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            lock (_tickLock)
            {
                _lastSimulatedTick = GetTickCount64();
            }
        }

        [DllImport("kernel32.dll")]
        static extern uint SetThreadExecutionState(uint esFlags);

        const uint ES_CONTINUOUS = 0x80000000;
        const uint ES_DISPLAY_REQUIRED = 0x00000002;

        public static void ApplyExecutionState(bool keepDisplay)
        {
            if (keepDisplay) SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED);
            else SetThreadExecutionState(ES_CONTINUOUS);
        }
    }
}