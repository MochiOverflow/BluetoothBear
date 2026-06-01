using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace BluetoothBear;

/// <summary>
/// Builds the tray icons at runtime so the app needs no embedded .ico asset.
/// Blue "B" = at least one device connected; gray = nothing connected.
/// </summary>
internal static class TrayIcons
{
    public static Icon Connected { get; } = Build(Color.FromArgb(0, 120, 215));   // Windows accent blue
    public static Icon Idle { get; } = Build(Color.FromArgb(120, 120, 120));      // gray

    private static Icon Build(Color fill)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            using var brush = new SolidBrush(fill);
            g.FillEllipse(brush, 1, 1, size - 2, size - 2);

            using var font = new Font("Segoe UI", 16f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString("B", font, Brushes.White, new RectangleF(0, 0, size, size), format);
        }

        IntPtr hIcon = bmp.GetHicon();
        // Clone into a managed Icon so we can free the unmanaged handle immediately.
        using var temp = Icon.FromHandle(hIcon);
        var icon = (Icon)temp.Clone();
        DestroyIcon(hIcon);
        return icon;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
