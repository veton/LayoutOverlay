using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;

namespace LayoutOverlay;

/// <summary>Large, borderless, click-through, non-activating overlay that fades out.</summary>
public sealed class OverlayForm : Form
{
    private const float PopupScale = 1.25f;
    private const int WS_EX_TRANSPARENT = 0x20;         // click-through
    private const int WS_EX_TOOLWINDOW = 0x80;          // no Alt+Tab entry
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_NOACTIVATE = 0x08000000;    // never steals focus

    private readonly System.Windows.Forms.Timer _hold = new() { Interval = 800 };
    private readonly System.Windows.Forms.Timer _fade = new() { Interval = 25 };
    private readonly Font _labelFont = new("Segoe UI", 32, FontStyle.Regular);

    private readonly List<uint> _layouts = new();
    private uint _activeLayout;

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(153, 153, 153);

        float scale = DeviceDpi / 96f;
        Size = new Size((int)(400 * scale), (int)(300 * scale));

        _hold.Tick += (_, _) => { _hold.Stop(); _fade.Start(); };
        _fade.Tick += (_, _) =>
        {
            Opacity -= 0.06;
            if (Opacity <= 0.05)
            {
                _fade.Stop();
                Hide();
            }
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width <= 0 || Height <= 0) return;
        using var outline = RoundedRectangle(ClientRectangle, 32 * DeviceDpi / 96f * PopupScale);
        var previousRegion = Region;
        Region = new Region(outline);
        previousRegion?.Dispose();
    }

    public void ShowLayout(uint langId)
    {
        _hold.Stop();
        _fade.Stop();
        _activeLayout = langId;
        _layouts.Clear();
        foreach (InputLanguage language in InputLanguage.InstalledInputLanguages)
        {
            uint layout = (uint)(language.Handle.ToInt64() & 0xFFFF);
            if (!_layouts.Contains(layout)) _layouts.Add(layout);
        }
        if (!_layouts.Contains(langId)) _layouts.Add(langId);

        float scale = DeviceDpi / 96f * PopupScale;
        Size = new Size((int)(340 * scale), (int)((28 + 82 * _layouts.Count + 28) * scale));

        // Center on the monitor where the mouse cursor is.
        var area = Screen.FromPoint(Cursor.Position).Bounds;
        Location = new Point(area.X + (area.Width - Width) / 2, area.Y + (area.Height - Height) / 2);

        Opacity = 0.7;
        if (!Visible) Show();
        Invalidate();
        _hold.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        using var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };

        float scale = DeviceDpi / 96f * PopupScale;
        float padding = 28 * scale;
        using var selection = new SolidBrush(Color.FromArgb(68, 68, 68));
        using var foreground = new SolidBrush(Color.White);
        using var selectionOutline = new Pen(Color.White, 2 * scale)
        {
            Alignment = PenAlignment.Inset
        };
        float rowHeight = (ClientSize.Height - padding * 2) / Math.Max(1, _layouts.Count);
        for (int index = 0; index < _layouts.Count; index++)
        {
            var row = new RectangleF(padding, padding + index * rowHeight,
                ClientSize.Width - padding * 2, rowHeight);
            if (_layouts[index] == _activeLayout)
            {
                using var highlight = RoundedRectangle(row, 14 * scale);
                g.FillPath(selection, highlight);
                g.DrawPath(selectionOutline, highlight);
            }

            row.Inflate(-12 * scale, 0);
            g.DrawString(LayoutName(_layouts[index]), _labelFont, foreground, row, sf);
        }
    }

    private static string LayoutName(uint langId)
    {
        try
        {
            var culture = new CultureInfo((int)langId);
            return culture.IsNeutralCulture ? culture.EnglishName : culture.Parent.EnglishName;
        }
        catch (CultureNotFoundException)
        {
            return "0x" + langId.ToString("X4");
        }
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hold.Dispose();
            _fade.Dispose();
            _labelFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
