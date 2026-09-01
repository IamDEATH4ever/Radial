using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Radial.Core;
using Radial.Models;
using Forms = System.Windows.Forms;
using WpfPoint = System.Windows.Point;

namespace Radial.UI;

public partial class RadialWindow : Window
{
    private const double Center = 180;
    private readonly RadialMenu _menu;
    private SkiaRadialRenderer? _renderer;
    private readonly ProfileManager? _profiles;
    private readonly ApplicationProfile? _profile;
    private int _wheelIndex;

    public int SelectedSector => _menu.SelectedSector;
    public Macro? SelectedMacro => CurrentActions.ElementAtOrDefault(_menu.SelectedSector);

    public RadialWindow(WpfPoint center, ApplicationProfile? profile = null, ProfileManager? profiles = null)
    {
        InitializeComponent();
        _profile = profile; _profiles = profiles;
        _menu = new RadialMenu(center, CurrentActions.Count);
        Width = 360;
        Height = 360;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var cursor = new System.Drawing.Point((int)Math.Round(center.X), (int)Math.Round(center.Y));
        var workArea = Forms.Screen.FromPoint(cursor).WorkingArea;
        Left = Math.Clamp(cursor.X - Center, workArea.Left, workArea.Right - 360);
        Top = Math.Clamp(cursor.Y - Center, workArea.Top, workArea.Bottom - 360);

        SourceInitialized += (_, _) => InitializeRenderer();
        Closed += (_, _) => _renderer?.Dispose();
    }

    public void UpdateCursor(WpfPoint position)
    {
        _menu.UpdateSelection(position);
        _renderer?.UpdateCursor(new WpfPoint(position.X - Left, position.Y - Top));
        _renderer?.Update(State, false);
    }

    private RadialWheel? CurrentWheel => _profile?.Wheels.ElementAtOrDefault(_wheelIndex);
    private IReadOnlyList<Macro> CurrentActions => CurrentWheel is { } wheel ? wheel.Macros.Take(12).ToList() : Array.Empty<Macro>();
    private RadialRenderState State => new(_menu.SelectedSector, CurrentActions.Count, CurrentActions.Select(a => a.Name).ToList());
    public void SwitchWheel(int delta) { if (_profile is null || _profile.Wheels.Count == 0) return; _wheelIndex = (_wheelIndex + delta % _profile.Wheels.Count + _profile.Wheels.Count) % _profile.Wheels.Count; _menu.SetItemCount(CurrentActions.Count); _renderer?.Update(State, false); }
    public Task ExecuteSelectedAsync(IntPtr targetWindow) { var macro = CurrentActions.ElementAtOrDefault(_menu.SelectedSector); return macro is null ? Task.CompletedTask : new MacroPlayer().PlayAsync(macro, targetWindow); }

    private void InitializeRenderer()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var region = CreateEllipticRgn(0, 0, 360, 360);
        SetWindowRgn(handle, region, true);
        WindowBackdropHelper.ApplyAcrylic(this);
        var backdrop = DesktopBackdropCapture.CaptureBehindWindow(handle, LiquidGlassSettings.Default);
        _renderer = new SkiaRadialRenderer(SkiaSurface, backdrop?.Image, LiquidGlassSettings.Default);
        _renderer.Update(State, false);
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateEllipticRgn(int left, int top, int right, int bottom);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool redraw);
}
