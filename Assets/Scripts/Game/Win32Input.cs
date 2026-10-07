using System;
using System.Runtime.InteropServices;

namespace StellarCommand.Game
{
    /// <summary>
    /// Minimal Windows input injection: move the system cursor, click, scroll, press keys, and bring a
    /// window to the foreground. Used to drive the real Stellaris window from VR controllers.
    /// Keys are sent as hardware scan codes, which games that read raw/DirectInput accept
    /// more reliably than virtual-key codes.
    /// </summary>
    public static class Win32Input
    {
        public enum MouseButton { Left, Right }

        // Scan codes (set 1)
        public const ushort ScanEscape = 0x01;
        public const ushort ScanW = 0x11;
        public const ushort ScanA = 0x1E;
        public const ushort ScanS = 0x1F;
        public const ushort ScanD = 0x20;
        public const ushort ScanSpace = 0x39;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const uint InputMouse = 0;
        private const uint InputKeyboard = 1;

        private const uint MouseLeftDown = 0x0002;
        private const uint MouseLeftUp = 0x0004;
        private const uint MouseRightDown = 0x0008;
        private const uint MouseRightUp = 0x0010;
        private const uint MouseWheel = 0x0800;

        private const uint KeyFlagScanCode = 0x0008;
        private const uint KeyFlagUp = 0x0002;

        private const int ShowRestore = 9;

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int dx, dy;
            public uint mouseData, flags, time;
            public IntPtr extraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort vk, scan;
            public uint flags, time;
            public IntPtr extraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HardwareInput
        {
            public uint msg;
            public ushort paramL, paramH;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MouseInput mouse;
            [FieldOffset(0)] public KeyboardInput keyboard;
            [FieldOffset(0)] public HardwareInput hardware;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int x, y; }

        [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

        private static void Send(Input input)
        {
            SendInput(1, new[] { input }, Marshal.SizeOf<Input>());
        }

        private static void SendMouse(uint flags, uint data = 0)
        {
            var input = new Input { type = InputMouse };
            input.u.mouse = new MouseInput { flags = flags, mouseData = data };
            Send(input);
        }

        private static void SendKey(ushort scan, bool up)
        {
            var input = new Input { type = InputKeyboard };
            input.u.keyboard = new KeyboardInput { scan = scan, flags = KeyFlagScanCode | (up ? KeyFlagUp : 0u) };
            Send(input);
        }

        public static bool IsForeground(IntPtr window) => window != IntPtr.Zero && GetForegroundWindow() == window;

        /// <summary>Whether a key is held right now, even when this app is not the focused window.</summary>
        public static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

        /// <summary>Brings the Unity window (editor or player) back to the front.</summary>
        public static void FocusThisApp() =>
            BringToFront(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle);

        /// <summary>
        /// Brings a window to the front and gives it keyboard focus. Windows normally refuses this for
        /// background processes, so we attach to the current foreground thread's input queue first.
        /// </summary>
        public static void BringToFront(IntPtr window)
        {
            if (window == IntPtr.Zero) return;
            if (IsIconic(window)) ShowWindow(window, ShowRestore);

            IntPtr foreground = GetForegroundWindow();
            uint foregroundThread = GetWindowThreadProcessId(foreground, out _);
            uint thisThread = GetCurrentThreadId();

            bool attached = foregroundThread != 0 && foregroundThread != thisThread &&
                            AttachThreadInput(thisThread, foregroundThread, true);

            // Tapping Alt first is the classic way to be allowed to change the foreground window
            // from a background process; Windows otherwise often just flashes the taskbar button.
            Tap(0x38); // left Alt scan code
            SetForegroundWindow(window);

            if (attached) AttachThreadInput(thisThread, foregroundThread, false);
        }

        /// <summary>Current system cursor position in desktop pixels, to verify that SetCursorPos took effect.</summary>
        public static (int X, int Y) GetCursor()
        {
            GetCursorPos(out NativePoint p);
            return (p.x, p.y);
        }

        public static IntPtr Foreground() => GetForegroundWindow();

        public static void MoveCursor(int desktopX, int desktopY) => SetCursorPos(desktopX, desktopY);

        public static void MouseDown(MouseButton button) =>
            SendMouse(button == MouseButton.Left ? MouseLeftDown : MouseRightDown);

        public static void MouseUp(MouseButton button) =>
            SendMouse(button == MouseButton.Left ? MouseLeftUp : MouseRightUp);

        /// <summary>Positive notches scroll up (away from the user), like a mouse wheel.</summary>
        public static void Wheel(int notches) => SendMouse(MouseWheel, unchecked((uint)(notches * 120)));

        public static void KeyDown(ushort scanCode) => SendKey(scanCode, false);
        public static void KeyUp(ushort scanCode) => SendKey(scanCode, true);

        public static void Tap(ushort scanCode)
        {
            SendKey(scanCode, false);
            SendKey(scanCode, true);
        }
#else
        public static bool IsForeground(IntPtr window) => false;
        public static bool IsKeyDown(int virtualKey) => false;
        public static (int X, int Y) GetCursor() => (0, 0);
        public static IntPtr Foreground() => IntPtr.Zero;
        public static void FocusThisApp() { }
        public static void BringToFront(IntPtr window) { }
        public static void MoveCursor(int desktopX, int desktopY) { }
        public static void MouseDown(MouseButton button) { }
        public static void MouseUp(MouseButton button) { }
        public static void Wheel(int notches) { }
        public static void KeyDown(ushort scanCode) { }
        public static void KeyUp(ushort scanCode) { }
        public static void Tap(ushort scanCode) { }
#endif
    }
}
