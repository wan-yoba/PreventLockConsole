namespace PreventLockConsole
{
    public static class HotkeyParser
    {
        public static bool TryParse(string s, out uint modifiers, out uint vkey)
        {
            modifiers = 0;
            vkey = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            var parts = s.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var raw in parts)
            {
                var p = raw.Trim();
                if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    p.Equals("Control", StringComparison.OrdinalIgnoreCase)) modifiers |= HotkeyHelper.MOD_CONTROL;
                else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= HotkeyHelper.MOD_ALT;
                else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= HotkeyHelper.MOD_SHIFT;
                else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                         p.Equals("Windows", StringComparison.OrdinalIgnoreCase)) modifiers |= HotkeyHelper.MOD_WIN;
                else
                {
                    if (Enum.TryParse<Keys>(p, true, out var key))
                    {
                        vkey = (uint)key;
                    }
                    else if (p.Length == 1 && char.IsDigit(p[0]))
                    {
                        vkey = (uint)((int)Keys.D0 + (p[0] - '0'));
                    }
                    else if (p.Length == 1 && char.IsLetter(p[0]))
                    {
                        if (Enum.TryParse<Keys>(p.ToUpperInvariant(), out var k2)) vkey = (uint)k2;
                        else return false;
                    }
                    else return false;
                }
            }

            return vkey != 0;
        }
    }
}