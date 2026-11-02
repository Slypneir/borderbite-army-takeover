using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Core.EditorBridge
{
    /// <summary>
    /// Windows-only "global" input + capture for the bridge: a full-editor / full-screen screenshot
    /// (GDI BitBlt → PNG via Texture2D, no System.Drawing needed) and real OS mouse control
    /// (mouse_event). Intrusive by nature — it drives the real cursor and briefly brings Unity to the
    /// foreground for a capture, then RESTORES the prior foreground/minimized state so, if the user
    /// was in another app (or Unity was minimized), things go back exactly as they were.
    /// </summary>
    public static class OsInput
    {
#if UNITY_EDITOR_WIN
        // ---- capture -----------------------------------------------------------------------------

        /// <summary>
        /// Bring Unity forward, capture ("main" = the editor window, "screen" = whole virtual desktop)
        /// after <paramref name="delayMs"/> (so the compositor settles), write a PNG, then restore the
        /// previous foreground window + re-minimize Unity if it had been minimized. Async over frames
        /// (returns immediately; the PNG appears after ~delayMs — poll the file like the game shot).
        /// </summary>
        public static void CaptureEditor(string absPath, string mode, int delayMs, bool restore)
        {
            var prev = GetForegroundWindow();
            var unity = Process.GetCurrentProcess().MainWindowHandle;
            if (unity == IntPtr.Zero) { Debug.LogError("[OsInput] No Unity main window handle."); return; }

            var wasMinimized = IsIconic(unity);
            var wasForeground = prev == unity;

            if (wasMinimized) ShowWindow(unity, SW_RESTORE);
            ForceForeground(unity);
            InternalEditorUtilityRepaint();

            var captureAt = EditorApplication.timeSinceStartup + Mathf.Max(0, delayMs) / 1000.0;
            void Tick()
            {
                if (EditorApplication.timeSinceStartup < captureAt) return;
                EditorApplication.update -= Tick;
                try
                {
                    GetRect(mode, unity, out var x, out var y, out var w, out var h);
                    var png = CaptureRect(x, y, w, h);
                    if (png != null) File.WriteAllBytes(absPath, png);
                }
                catch (Exception e) { Debug.LogError($"[OsInput] capture failed: {e.Message}"); }
                finally
                {
                    if (restore)
                    {
                        if (!wasForeground && prev != IntPtr.Zero && prev != unity) ForceForeground(prev);
                        if (wasMinimized) ShowWindow(unity, SW_MINIMIZE);
                    }
                }
            }
            EditorApplication.update += Tick;
        }

        private static void GetRect(string mode, IntPtr unity, out int x, out int y, out int w, out int h)
        {
            if (mode == "screen")
            {
                x = GetSystemMetrics(SM_XVIRTUALSCREEN);
                y = GetSystemMetrics(SM_YVIRTUALSCREEN);
                w = GetSystemMetrics(SM_CXVIRTUALSCREEN);
                h = GetSystemMetrics(SM_CYVIRTUALSCREEN);
            }
            else // "main" — the Unity editor window
            {
                GetWindowRect(unity, out var r);
                x = r.left; y = r.top; w = r.right - r.left; h = r.bottom - r.top;
            }
        }

        /// <summary>GDI BitBlt a screen region into a PNG (via a Texture2D, so no System.Drawing dep).</summary>
        public static byte[] CaptureRect(int x, int y, int w, int h)
        {
            if (w <= 0 || h <= 0) return null;

            var screenDC = GetDC(IntPtr.Zero);
            var memDC = CreateCompatibleDC(screenDC);
            var hbmp = CreateCompatibleBitmap(screenDC, w, h);
            var old = SelectObject(memDC, hbmp);
            try
            {
                BitBlt(memDC, 0, 0, w, h, screenDC, x, y, SRCCOPY);

                var bmi = new BITMAPINFOHEADER
                {
                    biSize = 40, biWidth = w, biHeight = -h, // negative → top-down rows
                    biPlanes = 1, biBitCount = 32, biCompression = 0,
                };
                var bits = new byte[w * h * 4];
                GetDIBits(memDC, hbmp, 0, (uint)h, bits, ref bmi, 0);

                var px = new Color32[w * h];
                for (var row = 0; row < h; row++)
                {
                    var src = row * w * 4;             // top-down source
                    var dst = (h - 1 - row) * w;       // Texture2D row 0 is the bottom → flip
                    for (var col = 0; col < w; col++)
                    {
                        var s = src + col * 4;          // BGRA
                        px[dst + col] = new Color32(bits[s + 2], bits[s + 1], bits[s + 0], 255);
                    }
                }

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.SetPixels32(px);
                tex.Apply(false);
                var png = tex.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tex);
                return png;
            }
            finally
            {
                SelectObject(memDC, old);
                DeleteObject(hbmp);
                DeleteDC(memDC);
                ReleaseDC(IntPtr.Zero, screenDC);
            }
        }

        // ---- real OS mouse -----------------------------------------------------------------------

        public static void MouseMove(int x, int y) => SetCursorPos(x, y);

        public static void MouseClick(int? x, int? y, string button, bool doubleClick)
        {
            if (x.HasValue && y.HasValue) SetCursorPos(x.Value, y.Value);
            ButtonDownUp(button);
            if (doubleClick) ButtonDownUp(button);
        }

        public static void MouseDrag(int fromX, int fromY, int toX, int toY, string button, int steps)
        {
            steps = Mathf.Max(1, steps);
            SetCursorPos(fromX, fromY);
            System.Threading.Thread.Sleep(20);
            ButtonDown(button);
            for (var i = 1; i <= steps; i++)
            {
                var t = i / (float)steps;
                SetCursorPos(Mathf.RoundToInt(Mathf.Lerp(fromX, toX, t)), Mathf.RoundToInt(Mathf.Lerp(fromY, toY, t)));
                System.Threading.Thread.Sleep(12);
            }
            ButtonUp(button);
        }

        private static void ButtonDownUp(string button) { ButtonDown(button); ButtonUp(button); }

        private static void ButtonDown(string button)
        {
            switch (button)
            {
                case "right": mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero); break;
                case "middle": mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, UIntPtr.Zero); break;
                default: mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero); break;
            }
        }

        private static void ButtonUp(string button)
        {
            switch (button)
            {
                case "right": mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero); break;
                case "middle": mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, UIntPtr.Zero); break;
                default: mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero); break;
            }
        }

        // ---- real OS keyboard (SendInput) --------------------------------------------------------
        // Real synthetic keystrokes to whatever window has focus — intended for testing the game in
        // Play mode (WASD/space/etc.) and typing into fields. Bring the Unity/Game window forward first
        // (focusEditor) so the keys land where intended.

        /// <summary>Tap a named key N times. Each tap holds for <paramref name="holdMs"/> before release
        /// (0 = instant). Keys: single letters/digits, "space","enter","esc","tab","up".."right",
        /// "shift","ctrl","alt","f1".."f12", etc.</summary>
        public static void KeyPress(string key, int count, int holdMs)
        {
            var vk = Vk(key);
            if (vk == 0) { Debug.LogError($"[OsInput] Unknown key '{key}'."); return; }
            count = Mathf.Max(1, count);
            for (var i = 0; i < count; i++)
            {
                SendVk(vk, false);
                System.Threading.Thread.Sleep(Mathf.Max(0, holdMs));
                SendVk(vk, true);
                if (i < count - 1) System.Threading.Thread.Sleep(20);
            }
        }

        /// <summary>Press and HOLD a key down (release later with KeyUp). Useful for continuous movement.</summary>
        public static void KeyDown(string key) { var vk = Vk(key); if (vk != 0) SendVk(vk, false); }
        public static void KeyUp(string key) { var vk = Vk(key); if (vk != 0) SendVk(vk, true); }

        /// <summary>Press a chord (e.g. ["ctrl","s"]): all down in order, then all up in reverse.</summary>
        public static void Hotkey(string[] keys)
        {
            if (keys == null || keys.Length == 0) return;
            var vks = new System.Collections.Generic.List<ushort>();
            foreach (var k in keys) { var v = Vk(k); if (v != 0) vks.Add(v); }
            foreach (var v in vks) { SendVk(v, false); System.Threading.Thread.Sleep(10); }
            for (var i = vks.Count - 1; i >= 0; i--) { SendVk(vks[i], true); System.Threading.Thread.Sleep(10); }
        }

        /// <summary>Type a string as real keystrokes (Unicode — works for text fields incl. Cyrillic).</summary>
        public static void TypeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var ch in text)
            {
                if (ch == '\n') { SendVk(0x0D, false); SendVk(0x0D, true); }
                else { SendUnicode(ch, false); SendUnicode(ch, true); }
                System.Threading.Thread.Sleep(5);
            }
        }

        private static void SendVk(ushort vk, bool up)
        {
            var inp = new INPUT { type = INPUT_KEYBOARD };
            inp.U.ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = 0,
                dwFlags = (uint)((up ? KEYEVENTF_KEYUP : 0) | (IsExtended(vk) ? KEYEVENTF_EXTENDEDKEY : 0)),
                time = 0,
                dwExtraInfo = IntPtr.Zero,
            };
            SendInput(1, new[] { inp }, Marshal.SizeOf(typeof(INPUT)));
        }

        private static void SendUnicode(char ch, bool up)
        {
            var inp = new INPUT { type = INPUT_KEYBOARD };
            inp.U.ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = ch,
                dwFlags = (uint)(KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0)),
                time = 0,
                dwExtraInfo = IntPtr.Zero,
            };
            SendInput(1, new[] { inp }, Marshal.SizeOf(typeof(INPUT)));
        }

        private static bool IsExtended(ushort vk)
        {
            switch (vk)
            {
                case 0x25: case 0x26: case 0x27: case 0x28: // arrows
                case 0x2D: case 0x2E:                        // insert, delete
                case 0x24: case 0x23:                        // home, end
                case 0x21: case 0x22:                        // pageup, pagedown
                    return true;
                default: return false;
            }
        }

        /// <summary>Map a friendly key name to a Win32 virtual-key code (0 = unknown).</summary>
        private static ushort Vk(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            var k = key.Trim().ToLowerInvariant();
            if (k.Length == 1)
            {
                var c = k[0];
                if (c >= 'a' && c <= 'z') return (ushort)(0x41 + (c - 'a'));
                if (c >= '0' && c <= '9') return (ushort)(0x30 + (c - '0'));
            }
            switch (k)
            {
                case "space": return 0x20;
                case "enter": case "return": return 0x0D;
                case "esc": case "escape": return 0x1B;
                case "tab": return 0x09;
                case "backspace": return 0x08;
                case "delete": case "del": return 0x2E;
                case "insert": case "ins": return 0x2D;
                case "home": return 0x24;
                case "end": return 0x23;
                case "pageup": case "pgup": return 0x21;
                case "pagedown": case "pgdn": return 0x22;
                case "up": return 0x26;
                case "down": return 0x28;
                case "left": return 0x25;
                case "right": return 0x27;
                case "shift": return 0x10;
                case "ctrl": case "control": return 0x11;
                case "alt": return 0x12;
                case "capslock": return 0x14;
                default:
                    if (k.Length >= 2 && k[0] == 'f' && int.TryParse(k.Substring(1), out var n) && n >= 1 && n <= 12)
                        return (ushort)(0x70 + (n - 1)); // F1..F12
                    return 0;
            }
        }

        // ---- foreground helpers ------------------------------------------------------------------

        private static void ForceForeground(IntPtr hwnd)
        {
            var fg = GetForegroundWindow();
            var fgThread = GetWindowThreadProcessId(fg, out _);
            var thisThread = GetCurrentThreadId();
            var attached = fgThread != thisThread && AttachThreadInput(fgThread, thisThread, true);
            // Only un-minimize — do NOT SW_RESTORE a maximized window (that would shrink a full-screen
            // editor to its smaller windowed size).
            if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
            if (attached) AttachThreadInput(fgThread, thisThread, false);
        }

        private static void InternalEditorUtilityRepaint()
        {
            try { UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); } catch { }
        }

        // ---- P/Invoke ----------------------------------------------------------------------------

        private const int SW_RESTORE = 9;
        private const int SW_MINIMIZE = 6;
        private const int SRCCOPY = 0x00CC0020;
        private const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020, MOUSEEVENTF_MIDDLEUP = 0x0040;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth, biHeight;
            public ushort biPlanes, biBitCount;
            public uint biCompression, biSizeImage;
            public int biXPelsPerMeter, biYPelsPerMeter;
            public uint biClrUsed, biClrImportant;
        }

        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hdc, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr h);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER bmi, uint usage);

        // ---- keyboard (SendInput) ----
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001, KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_UNICODE = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        // Padding member so the union is sized to the largest INPUT variant (MOUSEINPUT), matching the
        // 40-byte x64 INPUT the OS expects for cbSize; we only ever write the keyboard variant.
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT_PAD { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT_PAD mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint type; public InputUnion U; }

        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
#else
        public static void CaptureEditor(string absPath, string mode, int delayMs, bool restore) =>
            Debug.LogError("[OsInput] Global screenshot is Windows-only.");
        public static byte[] CaptureRect(int x, int y, int w, int h) => null;
        public static void MouseMove(int x, int y) { }
        public static void MouseClick(int? x, int? y, string button, bool doubleClick) { }
        public static void MouseDrag(int fromX, int fromY, int toX, int toY, string button, int steps) { }
        public static void KeyPress(string key, int count, int holdMs) => Debug.LogError("[OsInput] OS keyboard is Windows-only.");
        public static void KeyDown(string key) { }
        public static void KeyUp(string key) { }
        public static void Hotkey(string[] keys) { }
        public static void TypeText(string text) { }
#endif
    }
}
