using System.ComponentModel;
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
        _systemEvents.LayoutChanging += OnLayoutChanging;
        _systemEvents.LayoutChanged += OnLayoutChanged;
        _tray.Visible = true;
    }

    private void OnLayoutChanging(object? sender, LayoutChangeEventArgs e) => _overlay.ShowLayout(e.LayoutId, 10_000);

    private void OnLayoutChanged(object? sender, LayoutChangeEventArgs e) => _overlay.ShowLayout(e.LayoutId, 1_000);

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
                ProcessStartInfo startInfo = new(RepositoryUrl) { UseShellExecute = true };
                Process.Start(startInfo)?.Dispose();
            }
            catch (Win32Exception)
            {
                MessageBox.Show("Could not open the default browser.", "Layout Overlay",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        TaskDialog.ShowDialog(page);
    }

    private void Quit()
    {
        _systemEvents.LayoutChanging -= OnLayoutChanging;
        _systemEvents.LayoutChanged -= OnLayoutChanged;
        
        _overlay.Dispose();
        _systemEvents.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _trayIcon?.Dispose();
        Application.Exit();
    }
}
