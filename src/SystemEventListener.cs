using System.Runtime.InteropServices;

namespace LayoutOverlay;

public sealed class SystemEventListener : IDisposable
{
    private const int DoubleClickThreshold = 350;
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 500 };

    private readonly Native.LowLevelKeyboardProc _keyboardHookProc;
    private readonly IntPtr _keyboardHook;
    private bool _winKeyDown;
    private uint? _previewLayout;
    private long _lastPreviewTime;
    private bool _disposed;

    public uint ForegroundWindowLayout { get; private set; }
    public event EventHandler<LayoutChangeEventArgs>? LayoutChanging;
    public event EventHandler<LayoutChangeEventArgs>? LayoutChanged;

    public SystemEventListener()
    {
        ForegroundWindowLayout = Native.GetLayoutId(Native.GetForegroundWindow());

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
                if (keyDown) _winKeyDown = true;
                if (keyUp)
                {
                    _winKeyDown = false;
                    if (_previewLayout != null)
                    {
                        LayoutChanged?.Invoke(this, new(_previewLayout.Value));
                        _previewLayout = null;
                    }
                }
                break;

            case Native.VK_SPACE when _winKeyDown:
                // React to both key-down and key-up as Windows intercepts second key-down
                long now = Environment.TickCount64;
                if (_lastPreviewTime < now - DoubleClickThreshold)
                {
                    _lastPreviewTime = now;
                    SelectNextLayout();
                }
                break;
        }

        return Native.CallNextHookEx(_keyboardHook, code, message, data);
    }

    private void SelectNextLayout()
    {
        uint currentLayout = _previewLayout ?? GetForegroundWindowLayout();
        if (currentLayout == 0) return;
        
        List<uint> layouts = InputLanguage.InstalledInputLanguages
            .Cast<InputLanguage>()
            .Select(language => (uint)(language.Handle.ToInt64() & 0xFFFF))
            .Distinct()
            .ToList();
        int currentIndex = layouts.IndexOf(currentLayout);
        uint nextLayout = layouts[(currentIndex + 1) % layouts.Count];
        _previewLayout = nextLayout;
        LayoutChanging?.Invoke(this, new(nextLayout));
    }

    private void Poll()
    {
        uint foregroundLayout = GetForegroundWindowLayout(); 
        if (foregroundLayout != 0 && foregroundLayout != ForegroundWindowLayout)
        {
            ForegroundWindowLayout = foregroundLayout;
            LayoutChanged?.Invoke(this, new(foregroundLayout));
        }
    }

    private static uint GetForegroundWindowLayout()
    {
        IntPtr hwnd = Native.GetForegroundWindow();
        return hwnd != IntPtr.Zero
            ? Native.GetLayoutId(hwnd)
            : 0;
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
