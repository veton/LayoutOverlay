using System.Runtime.InteropServices;

namespace LayoutOverlay;

public static class Native
{
    internal const int WS_EX_TRANSPARENT = 0x20;         // click-through
    internal const int WS_EX_TOOLWINDOW = 0x80;          // no Alt+Tab entry
    internal const int WS_EX_LAYERED = 0x80000;
    internal const int WS_EX_NOACTIVATE = 0x08000000;    // never steals focus

    internal const int WH_KEYBOARD_LL = 13;
    internal const int WM_KEYDOWN = 0x0100;
    internal const int WM_KEYUP = 0x0101;
    internal const int WM_SYSKEYDOWN = 0x0104;
    internal const int WM_SYSKEYUP = 0x0105;

    internal const uint VK_SPACE = 0x20;
    internal const uint VK_LWIN = 0x5B;
    internal const uint VK_RWIN = 0x5C;

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardHookData
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback,
        IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? moduleName);

    /// <summary>Returns the language ID (low word of the HKL) active in the given window's thread.</summary>
    public static uint GetLayoutId(IntPtr hwnd)
    {
        uint threadId = GetWindowThreadProcessId(hwnd, out _);
        long hkl = GetKeyboardLayout(threadId).ToInt64();
        return (uint)(hkl & 0xFFFF);
    }
}
