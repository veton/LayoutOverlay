using System.Runtime.InteropServices;

namespace LayoutOverlay;

public sealed class SystemEventListener : IDisposable
{
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 100 };

    private readonly Native.LowLevelKeyboardProc _keyboardHookProc;
    private readonly IntPtr _keyboardHook;
    private bool _winKeyDown;
    private bool _spaceKeyDown;
    private uint? _previewLayout;
    private bool _disposed;

    public uint CurrentLayout { get; private set; }
    public event EventHandler<LayoutChangeEventArgs>? LayoutChanging;
    public event EventHandler<LayoutChangeEventArgs>? LayoutChanged;

    public SystemEventListener()
    {
        CurrentLayout = Native.GetLayoutId(Native.GetForegroundWindow());

        _keyboardHookProc = HandleKeyboardEvent;
        _keyboardHook = Native.SetWindowsHookEx(
            Native.WH_KEYBOARD_LL, _keyboardHookProc, Native.GetModuleHandle(null), 0);
        
        _poll.Tick += (_, _) => Poll();
        _poll.Start();
    }

    private IntPtr HandleKeyboardEvent(int code, IntPtr message, IntPtr data)
    {
        if (code < 0)
            return Native.CallNextHookEx(_keyboardHook, code, message, data);

        int messageId = message.ToInt32();
        uint key = Marshal.PtrToStructure<Native.KeyboardHookData>(data).VirtualKey;
        bool keyDown = messageId is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;
        bool keyUp = messageId is Native.WM_KEYUP or Native.WM_SYSKEYUP;

        switch (key)
        {
            case Native.VK_LWIN or Native.VK_RWIN:
                bool wasDown = _winKeyDown;
                if (keyDown) _winKeyDown = true;
                else if (keyUp) _winKeyDown = false;
                if (_winKeyDown != wasDown)
                    _previewLayout = null;
                break;

            case Native.VK_SPACE:
                if (keyDown && !_spaceKeyDown)
                {
                    _spaceKeyDown = true;
                    if (_winKeyDown)
                        PreviewNextLayout();
                }
                else if (keyUp)
                {
                    _spaceKeyDown = false;
                }
                break;
        }

        return Native.CallNextHookEx(_keyboardHook, code, message, data);
    }

    private void PreviewNextLayout()
    {
        IntPtr hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        uint actualLayout = Native.GetLayoutId(hwnd);
        uint currentLayout = _previewLayout ?? actualLayout;
        List<uint> layouts = InputLanguage.InstalledInputLanguages
            .Cast<InputLanguage>()
            .Select(language => (uint)(language.Handle.ToInt64() & 0xFFFF))
            .Distinct()
            .ToList();
        int currentIndex = layouts.IndexOf(currentLayout);
        uint nextLayout = layouts.Count > 1
            ? layouts[(currentIndex + 1 + layouts.Count) % layouts.Count]
            : currentLayout;
        _previewLayout = nextLayout;
        LayoutChanging?.Invoke(this, new(nextLayout));
    }

    private void Poll()
    {
        IntPtr hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        uint layout = Native.GetLayoutId(hwnd);
        if (layout != CurrentLayout)
        {
            CurrentLayout = layout;
            LayoutChanged?.Invoke(this, new(layout));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _poll.Stop();
        _poll.Dispose(); 

        if (_keyboardHook != IntPtr.Zero)
            Native.UnhookWindowsHookEx(_keyboardHook);
    }
}
