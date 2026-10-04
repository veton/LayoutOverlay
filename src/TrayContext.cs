using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace LayoutOverlay;

/// <summary>Tray icon + polling timer that detects keyboard layout changes.</summary>
public sealed class TrayContext : ApplicationContext
{
    private const string RepositoryUrl = "https://github.com/veton/LayoutOverlay";

    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 100 };
    private readonly OverlayForm _overlay = new();
    private Icon? _trayIcon;

    private IntPtr _lastHwnd;
    private uint _lastLayout;

    public TrayContext()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("About", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Quit());

        _tray = new NotifyIcon
        {
            Text = "Layout Overlay",
            ContextMenuStrip = menu,
            Visible = false
        };

        _lastHwnd = Native.GetForegroundWindow();
        _lastLayout = Native.GetLayoutId(_lastHwnd);
        UpdateTrayIcon(_lastLayout);
        _tray.Visible = true;

        _poll.Tick += (_, _) => Poll();
        _poll.Start();
    }

    private static void ShowAbout()
    {
        var page = new TaskDialogPage
        {
            Caption = "About Layout Overlay",
            Heading = "Layout Overlay",
            Text =
                "Displays popup on keyboard layout change.\n\n" +
                $"Version {Application.ProductVersion}\n" +
                $"<a href=\"{RepositoryUrl}\">{RepositoryUrl}</a>",
            Icon = TaskDialogIcon.Information,
            EnableLinks = true,
            Buttons = { TaskDialogButton.OK }
        };
        page.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(RepositoryUrl) { UseShellExecute = true })?.Dispose();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show("Could not open the default browser.", "Layout Overlay",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        TaskDialog.ShowDialog(page);
    }

    private void Poll()
    {
        IntPtr hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        uint layout = Native.GetLayoutId(hwnd);

        // Switching to another window can change the layout too (layouts are per-window).
        // Only show the overlay when the layout changes inside the same window.
        if (hwnd != _lastHwnd)
        {
            if (layout != _lastLayout) UpdateTrayIcon(layout);
            _lastHwnd = hwnd;
            _lastLayout = layout;
            return;
        }

        if (layout != _lastLayout)
        {
            UpdateTrayIcon(layout);
            _lastLayout = layout;
            _overlay.ShowLayout(layout);
        }
    }

    private void UpdateTrayIcon(uint langId)
    {
        string code;
        try
        {
            code = new CultureInfo((int)langId).ThreeLetterISOLanguageName.ToUpperInvariant();
        }
        catch (CultureNotFoundException)
        {
            code = "UNK";
        }

        int size = Math.Max(32, SystemInformation.SmallIconSize.Width);
        using var bitmap = CreateTrayBitmap(code, size);

        IntPtr handle = bitmap.GetHicon();
        Icon nextIcon;
        try
        {
            using var borrowedIcon = Icon.FromHandle(handle);
            nextIcon = (Icon)borrowedIcon.Clone();
        }
        finally
        {
            Native.DestroyIcon(handle);
        }

        var previousIcon = _trayIcon;
        _tray.Icon = nextIcon;
        _trayIcon = nextIcon;
        _tray.Text = $"Layout Overlay - {code}";
        previousIcon?.Dispose();
    }

    private static Bitmap CreateTrayBitmap(string code, int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        using var font = new Font("Arial Narrow", size, FontStyle.Bold, GraphicsUnit.Pixel);
        using var glyphs = new GraphicsPath();
        using var format = StringFormat.GenericTypographic;
        glyphs.AddString(code, font.FontFamily, (int)font.Style, font.Size,
            PointF.Empty, format);

        var bounds = glyphs.GetBounds();
        float horizontalScale = size / bounds.Width;
        float verticalScale = size * 0.7f / bounds.Height;
        using var transform = new Matrix(horizontalScale, 0, 0, verticalScale,
            -bounds.X * horizontalScale, size * 0.15f - bounds.Y * verticalScale);
        glyphs.Transform(transform);

        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.FillPath(Brushes.White, glyphs);
        return bitmap;
    }

    private void Quit()
    {
        _poll.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        _trayIcon?.Dispose();
        _overlay.Dispose();
        Application.Exit();
    }
}
