using System.Runtime.InteropServices;
using SkiaSharp;

namespace Radial.UI;

public sealed class DesktopBackdrop : IDisposable
{
    public DesktopBackdrop(SKImage image, int padding)
    {
        Image = image;
        Padding = padding;
    }

    public SKImage Image { get; }
    public int Padding { get; }

    public void Dispose()
    {
        Image.Dispose();
    }
}

/// <summary>
/// Captures the desktop behind the Radial overlay. Adapted from the MIT-licensed
/// WPF-Liquid-Glass-Effect sample (hide overlay, BitBlt, restore).
/// </summary>
public static class DesktopBackdropCapture
{
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;
    private const int SrcCopy = 0x00CC0020;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const uint BiRgb = 0;

    public static DesktopBackdrop? CaptureBehindWindow(IntPtr hwnd, LiquidGlassSettings settings)
    {
        if (hwnd == IntPtr.Zero) return null;

        var hidden = false;
        try
        {
            if (IsWindow(hwnd))
            {
                ShowWindow(hwnd, SwHide);
                hidden = true;
            }

            if (!GetWindowRect(hwnd, out var window) && hidden)
                GetWindowRect(hwnd, out window);

            var width = Math.Max(1, window.Right - window.Left);
            var height = Math.Max(1, window.Bottom - window.Top);
            var pad = Math.Max(0, settings.BackdropPadding);
            var capture = ClampToVirtualScreen(window.Left - pad, window.Top - pad, width + pad * 2, height + pad * 2);
            if (capture.Width <= 0 || capture.Height <= 0) return null;

            var image = CaptureRegion(capture.X, capture.Y, capture.Width, capture.Height);
            if (image is null) return null;

            var leftPad = window.Left - capture.X;
            var topPad = window.Top - capture.Y;
            // Use the actual inset after virtual-screen clamping so refraction UVs stay aligned.
            _ = leftPad;
            _ = topPad;
            return new DesktopBackdrop(image, pad);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hidden) ShowWindow(hwnd, SwShowNoActivate);
        }
    }

    private static (int X, int Y, int Width, int Height) ClampToVirtualScreen(int x, int y, int width, int height)
    {
        var vx = GetSystemMetrics(SmXVirtualScreen);
        var vy = GetSystemMetrics(SmYVirtualScreen);
        var vw = GetSystemMetrics(SmCxVirtualScreen);
        var vh = GetSystemMetrics(SmCyVirtualScreen);
        var left = Math.Max(x, vx);
        var top = Math.Max(y, vy);
        var right = Math.Min(x + width, vx + vw);
        var bottom = Math.Min(y + height, vy + vh);
        return (left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }



    private static SKImage? CaptureRegion(int x, int y, int width, int height)
    {
        var screenDc = IntPtr.Zero;
        var memDc = IntPtr.Zero;
        var hBitmap = IntPtr.Zero;
        var oldBitmap = IntPtr.Zero;
        try
        {
            screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero) return null;
            memDc = CreateCompatibleDC(screenDc);
            if (memDc == IntPtr.Zero) return null;
            hBitmap = CreateCompatibleBitmap(screenDc, width, height);
            if (hBitmap == IntPtr.Zero) return null;
            oldBitmap = SelectObject(memDc, hBitmap);
            if (!BitBlt(memDc, 0, 0, width, height, screenDc, x, y, SrcCopy))
                return null;
            return ImageFromHBitmap(hBitmap, width, height);
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero && memDc != IntPtr.Zero) SelectObject(memDc, oldBitmap);
            if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
            if (memDc != IntPtr.Zero) DeleteDC(memDc);
            if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static SKImage? ImageFromHBitmap(IntPtr hBitmap, int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var bitmap = new SKBitmap(info);
        var header = new BitmapInfoHeader
        {
            Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
            Width = width,
            Height = -height,
            Planes = 1,
            BitCount = 32,
            Compression = BiRgb
        };
        var copied = GetDIBits(GetDC(IntPtr.Zero), hBitmap, 0, (uint)height, bitmap.GetPixels(), ref header, 0);
        if (copied == 0)
        {
            bitmap.Dispose();
            return null;
        }
        return SKImage.FromBitmap(bitmap);
    }

    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out WinRect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, int rop);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint lines, IntPtr bits, ref BitmapInfoHeader info, uint usage);

    [StructLayout(LayoutKind.Sequential)]
    private struct WinRect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }
}
