using System.Runtime.InteropServices;

namespace LayoutOverlay;

public sealed class SystemEventListener : IDisposable
{
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 100 };
    private readonly Native.LowLevelKeyboardProc _keyboardHookProc;
    private readonly IntPtr _keyboardHook;
    private IntPtr _lastHwnd;
    private uint _lastLayout;
    private bool _winKeyDown;
    private IntPtr _previewHwnd;
    private uint? _previewLayout;
    private bool _disposed;

    public uint CurrentLayout => _lastLayout;

    public event EventHandler<LayoutEventArgs>? LayoutChanging;
    public event EventHandler<LayoutEventArgs>? LayoutChanged;
    public event EventHandler<LayoutEventArgs>? ForegroundLayoutChanged;

    public SystemEventListener()
    {
        _lastHwnd = Native.GetForegroundWindow();
        _lastLayout = Native.GetLayoutId(_lastHwnd);

        _keyboardHookProc = HandleKeyboardEvent;
        _keyboardHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _keyboardHookProc,
            Native.GetModuleHandle(null), 0);

        _poll.Tick += (_, _) => Poll();
        _poll.Start();
    }

    private IntPtr HandleKeyboardEvent(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0)
        {
            int messageId = message.ToInt32();
            uint key = Marshal.PtrToStructure<Native.KeyboardHookData>(data).VirtualKey;
            bool keyDown = messageId is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;
            bool keyUp = messageId is Native.WM_KEYUP or Native.WM_SYSKEYUP;

            if (key is Native.VK_LWIN or Native.VK_RWIN)
            {
                if (keyDown)
                {
                    if (!_winKeyDown)
                    {
                        _previewHwnd = IntPtr.Zero;
                        _previewLayout = null;
                    }
                    _winKeyDown = true;
                }
                else if (keyUp)
                {
                    _winKeyDown = false;
                    _previewHwnd = IntPtr.Zero;
                    _previewLayout = null;
                }
            }
            else if (key == Native.VK_SPACE && keyUp && _winKeyDown)
            {
                PreviewNextLayout();
            }
        }

        return Native.CallNextHookEx(_keyboardHook, code, message, data);
    }

    private void PreviewNextLayout()
    {
        IntPtr hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        uint currentLayout = _previewHwnd == hwnd && _previewLayout.HasValue
            ? _previewLayout.Value
            : Native.GetLayoutId(hwnd);
        var layouts = InputLanguage.InstalledInputLanguages
            .Cast<InputLanguage>()
            .Select(language => (uint)(language.Handle.ToInt64() & 0xFFFF))
            .Distinct()
            .ToList();
        int currentIndex = layouts.IndexOf(currentLayout);
        uint nextLayout = layouts.Count > 1
            ? layouts[(currentIndex + 1 + layouts.Count) % layouts.Count]
            : currentLayout;
        _previewHwnd = hwnd;
        _previewLayout = nextLayout;
        LayoutChanging?.Invoke(this, new LayoutEventArgs(nextLayout));
    }

    private void Poll()
    {
        IntPtr hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        uint layout = Native.GetLayoutId(hwnd);

        // Layouts are per-window; only report a change within the same window.
        if (hwnd != _lastHwnd)
        {
            bool layoutChanged = layout != _lastLayout;
            _lastHwnd = hwnd;
            _lastLayout = layout;
            if (layoutChanged)
                ForegroundLayoutChanged?.Invoke(this, new LayoutEventArgs(layout));
            return;
        }

        if (layout != _lastLayout)
        {
            _lastLayout = layout;
            LayoutChanged?.Invoke(this, new LayoutEventArgs(layout));
            ForegroundLayoutChanged?.Invoke(this, new LayoutEventArgs(layout));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _poll.Stop();
        _poll.Dispose();
        if (_keyboardHook != IntPtr.Zero) Native.UnhookWindowsHookEx(_keyboardHook);
    }
}

public sealed class LayoutEventArgs(uint layoutId) : EventArgs
{
    public uint LayoutId { get; } = layoutId;
}