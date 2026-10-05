using System.Diagnostics;

namespace LayoutOverlay;

public sealed class TrayApplication : ApplicationContext
{
    private const string RepositoryUrl = "https://github.com/veton/LayoutOverlay";

    private readonly SystemEventListener _systemEvents = new();
    private readonly OverlayForm _overlay = new();
    private readonly NotifyIcon _tray;
    private Icon? _trayIcon;

    public TrayApplication()
    {
        ContextMenuStrip menu = new();
        menu.Items.Add("About", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Quit());

        uint initialLayout = _systemEvents.CurrentLayout;
        string initialCode = TrayIconGenerator.GetLanguageCode(initialLayout);
        _tray = new NotifyIcon
        {
            Text = $"Layout Overlay - {initialCode}",
            ContextMenuStrip = menu,
            Icon = TrayIconGenerator.Create(initialLayout),
            Visible = false
        };
        _trayIcon = _tray.Icon;
        _systemEvents.LayoutChanging += OnLayoutRequested;
        _systemEvents.LayoutChanged += OnLayoutRequested;
        _systemEvents.ForegroundLayoutChanged += OnForegroundLayoutChanged;
        _tray.Visible = true;
    }

    private void OnLayoutRequested(object? sender, LayoutEventArgs e) => _overlay.ShowLayout(e.LayoutId);

    private void OnForegroundLayoutChanged(object? sender, LayoutEventArgs e)
    {
        var nextIcon = TrayIconGenerator.Create(e.LayoutId);
        var previousIcon = _trayIcon;
        _tray.Icon = nextIcon;
        _trayIcon = nextIcon;
        _tray.Text = $"Layout Overlay - {TrayIconGenerator.GetLanguageCode(e.LayoutId)}";
        previousIcon?.Dispose();
    }

    private static void ShowAbout()
    {
        TaskDialogPage page = new()
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

    private void Quit()
    {
        _systemEvents.LayoutChanging -= OnLayoutRequested;
        _systemEvents.LayoutChanged -= OnLayoutRequested;
        _systemEvents.ForegroundLayoutChanged -= OnForegroundLayoutChanged;
        _overlay.Dispose();
        _systemEvents.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _trayIcon?.Dispose();
        Application.Exit();
    }
}
