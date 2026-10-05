using System.Drawing.Drawing2D;
using System.Globalization;

namespace LayoutOverlay;

public static class TrayIconGenerator
{
    public static Icon Create(uint langId)
    {
        int size = Math.Max(32, SystemInformation.SmallIconSize.Width);
        using var bitmap = CreateBitmap(GetLanguageCode(langId), size);
        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var borrowedIcon = Icon.FromHandle(handle);
            return (Icon)borrowedIcon.Clone();
        }
        finally
        {
            Native.DestroyIcon(handle);
        }
    }

    public static string GetLanguageCode(uint langId)
    {
        try
        {
            return new CultureInfo((int)langId).ThreeLetterISOLanguageName.ToUpperInvariant();
        }
        catch (CultureNotFoundException)
        {
            return "UNK";
        }
    }

    private static Bitmap CreateBitmap(string code, int size)
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
        float verticalScale = size * 0.6f / bounds.Height;
        using var transform = new Matrix(horizontalScale, 0, 0, verticalScale,
            -bounds.X * horizontalScale, size * 0.2f - bounds.Y * verticalScale);
        glyphs.Transform(transform);

        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.FillPath(Brushes.White, glyphs);
        return bitmap;
    }
}