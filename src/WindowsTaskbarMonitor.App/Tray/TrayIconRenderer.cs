using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace WindowsTaskbarMonitor.App.Tray;

internal static class TrayIconRenderer
{
    public static IntPtr Render(string label)
    {
        using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        using var background = new SolidBrush(GetAccentColor());
        graphics.FillEllipse(background, 1, 1, 30, 30);

        var fontSize = label.Length >= 3 ? 10.5f : 13.5f;
        using var font = new Font("Segoe UI Variable", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var foreground = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        graphics.DrawString(label, font, foreground, new RectangleF(0, 0, 32, 31), format);

        return bitmap.GetHicon();
    }

    private static Color GetAccentColor()
    {
        if (DwmGetColorizationColor(out var colorization, out _) == 0)
        {
            var red = (byte)((colorization >> 16) & 0xFF);
            var green = (byte)((colorization >> 8) & 0xFF);
            var blue = (byte)(colorization & 0xFF);
            return Color.FromArgb(255, red, green, blue);
        }

        return Color.FromArgb(255, 0, 120, 212);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetColorizationColor(out uint colorization, [MarshalAs(UnmanagedType.Bool)] out bool opaqueBlend);
}
