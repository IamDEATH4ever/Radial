using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Radial.UI;

public static class WindowBackdropHelper
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic

    public static void ApplyAcrylic(Window window)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero) return;

        int backdropType = DWMSBT_TRANSIENTWINDOW;
        DwmSetWindowAttribute(helper.Handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, Marshal.SizeOf(typeof(int)));
        
        // Optional: Force dark mode for better contrast with the white highlight
        int darkMode = 1;
        DwmSetWindowAttribute(helper.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, Marshal.SizeOf(typeof(int)));
    }
}