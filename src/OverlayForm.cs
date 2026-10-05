using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;

namespace LayoutOverlay;

/// <summary>Large, borderless, click-through, non-activating overlay that fades out.</summary>
public sealed class OverlayForm : Form
{
    // Sizes
    private const int FormWidth = 450;
    private const int FontSize = 28;
    private const float PaddingMultiplier = 1.5f;
    private const float LabelHeightMultiplier = 2.4f;
    private readonly float DeviceScale = 1;

    // Colors
    private const double FormOpacity = 0.7;
    private static readonly Color BackgroundColor = Color.FromArgb(128, 128, 128);
    private static readonly Color SelectionColor = Color.FromArgb(64, 64, 64);
    private static readonly Color ForegroundColor = Color.White;
    private static readonly Font LabelFont = new("Segoe UI", FontSize, FontStyle.Regular);

    private readonly System.Windows.Forms.Timer _hold = new();
    private readonly System.Windows.Forms.Timer _fade = new() { Interval = 25 };

    private readonly List<uint> _layouts = [];
    private uint _activeLayout;

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = BackgroundColor;
        DeviceScale = DeviceDpi / 96f;

        Size = new Size((int)(FormWidth * DeviceScale), (int)(FormWidth * DeviceScale));

        _hold.Tick += (_, _) =>
        {
            _hold.Stop();
            _fade.Start();
        };
        _fade.Tick += (_, _) =>
        {
            Opacity -= 0.05;
            if (Opacity < 0.05)
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
            cp.ExStyle |= Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW | Native.WS_EX_LAYERED | Native.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width <= 0 || Height <= 0) return;

        using var outline = RoundedRectangle(ClientRectangle, FontSize * DeviceScale);
        var previousRegion = Region;
        Region = new Region(outline);
        previousRegion?.Dispose();
    }

    public void ShowLayout(uint langId, int holdMilliseconds)
    {
        _hold.Stop();
        _fade.Stop();
        _hold.Interval = holdMilliseconds;
        _activeLayout = langId;
        _layouts.Clear();

        foreach (InputLanguage language in InputLanguage.InstalledInputLanguages)
        {
            uint layout = (uint)(language.Handle.ToInt64() & 0xFFFF);
            if (!_layouts.Contains(layout))
                _layouts.Add(layout);
        }

        if (!_layouts.Contains(langId))
            _layouts.Add(langId);

        float heightMultiplier = _layouts.Count * LabelHeightMultiplier + 2 * PaddingMultiplier;
        Size = new Size((int)(FormWidth * DeviceScale), (int)(heightMultiplier * FontSize * DeviceScale));

        // Center on the monitor where the mouse cursor is.
        var area = Screen.FromPoint(Cursor.Position).Bounds;
        Location = new Point(area.X + (area.Width - Width) / 2, area.Y + (area.Height - Height) / 2);

        Opacity = FormOpacity;
        if (!Visible)
            Show();

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

        float padding = PaddingMultiplier * FontSize * DeviceScale;
        using var selection = new SolidBrush(SelectionColor);
        using var foreground = new SolidBrush(ForegroundColor);
        using var selectionOutline = new Pen(Color.White, 3 * DeviceScale)
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
                using var highlight = RoundedRectangle(row, 14 * DeviceScale);
                g.FillPath(selection, highlight);
                g.DrawPath(selectionOutline, highlight);
            }

            row.Inflate(-12 * DeviceScale, 0);
            g.DrawString(LayoutName(_layouts[index]), LabelFont, foreground, row, sf);
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
            LabelFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
