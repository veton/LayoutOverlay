using System.Runtime.InteropServices;

namespace LayoutOverlay;

public static class Native
{
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

    /// <summary>Returns the language ID (low word of the HKL) active in the given window's thread.</summary>
    public static uint GetLayoutId(IntPtr hwnd)
    {
        uint threadId = GetWindowThreadProcessId(hwnd, out _);
        long hkl = GetKeyboardLayout(threadId).ToInt64();
        return (uint)(hkl & 0xFFFF);
    }
}
