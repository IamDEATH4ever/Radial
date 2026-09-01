using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Radial.UI;

public static class WindowBackdropHelper
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS { public int Left, Right, Top, Bottom; }

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE  = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE       = 38;
    private const int DWMWA_NCRENDERING_POLICY        = 2;
    private const int DWMNCRP_DISABLED                = 1;   // turn off all non-client rendering (shadow, border)
    private const int DWMSBT_TRANSIENTWINDOW          = 3;   // Acrylic

    public static void ApplyAcrylic(Window window)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero) return;

        // Suppress the DWM-drawn square drop-shadow / non-client chrome entirely.
        // Negative MARGINS tell DWM not to draw any shadow around the HWND.
        var noShadow = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(helper.Handle, ref noShadow);

        // Also disable non-client rendering policy to be sure.
        int ncrPolicy = DWMNCRP_DISABLED;
        DwmSetWindowAttribute(helper.Handle, DWMWA_NCRENDERING_POLICY, ref ncrPolicy, Marshal.SizeOf(typeof(int)));
    }
}