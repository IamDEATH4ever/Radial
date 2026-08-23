using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Radial.Core;
using Forms = System.Windows.Forms;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace Radial.UI;

public partial class RadialWindow : Window
{
    private const double Center = 180;
    private const double Radius = 175;
    private const double InnerRadius = 48;

    // Individual glass-tile look: gap between neighboring sectors, gap between
    // the inner/outer rings, and the corner radius applied to each tile.
    private const double SectorGapDeg = 3.0;
    private const double SectorRadialGap = 6.0;
    private const double SectorCornerRadius = 10.0;

    private readonly RadialMenu _menu;

    // Vector Path Icons
    private static readonly string[] SectorIconData = new[]
    {
        "M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z", // Folder
        "M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M18,20H6V4H13V9H18V20Z", // Document
        "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M12,6A6,6 0 0,0 6,12A6,6 0 0,0 12,18A6,6 0 0,0 18,12A6,6 0 0,0 12,6Z", // Globe
        "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.49,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.51,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.51,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.49,21.58L14.87,18.93C15.5,18.68 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z", // Settings
        "M12,3v10.55c-.59-.34-1.27-.55-2-.55-2.21,0-4,1.79-4,4s1.79,4,4,4 4-1.79,4-4V7h4V3h-6z", // Music
        "M19,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3M9.5,13.5L7,11L8.41,9.59L12.24,13.41L11,14.66L9.5,13.5M13,17H7V15H13V17Z", // Terminal
        "M4,4H7L9,2H15L17,4H20A2,2 0 0,1 22,6V18A2,2 0 0,1 20,20H4A2,2 0 0,1 2,18V6A2,2 0 0,1 4,4M12,7A5,5 0 0,0 7,12A5,5 0 0,0 12,17A5,5 0 0,0 17,12A5,5 0 0,0 12,7Z", // Camera
        "M20,18H4V6H20M20,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V6C22,4.89 21.1,4 20,4Z" // Monitor
    };

    public int SelectedSector => _menu.SelectedSector;

    public RadialWindow(WpfPoint center)
    {
        InitializeComponent();
        _menu = new RadialMenu(center);
        Width = 360;
        Height = 360;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var cursor = new System.Drawing.Point((int)Math.Round(center.X), (int)Math.Round(center.Y));
        var workArea = Forms.Screen.FromPoint(cursor).WorkingArea;
        Left = Math.Clamp(cursor.X - Center, workArea.Left, workArea.Right - 360);
        Top = Math.Clamp(cursor.Y - Center, workArea.Top, workArea.Bottom - 360);

        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            var region = CreateEllipticRgn(0, 0, 360, 360);
            SetWindowRgn(handle, region, true);
            EnableBlur(handle);
        };

        Render();
    }

    public void UpdateCursor(WpfPoint position) 
    { 
        _menu.UpdateSelection(position); 
        Render(); 
    }

    private void Render()
    {
        MenuCanvas.Children.Clear();

        for (var i = 0; i < RadialMenu.SectorCount; i++)
        {
            bool isSelected = (i == _menu.SelectedSector);

            double midAngleDeg = (i * 45) - 90 + 22.5;
            double midAngleRad = midAngleDeg * Math.PI / 180;

            // 1. Sector Geometry - an individually rounded, inset glass tile
            var path = new Path
            {
                Data = SectorGeometry(i),
                Fill = GlassSectorBrush(isSelected),
                Stroke = GlassSectorStroke(isSelected),
                StrokeThickness = 1.1,
                StrokeLineJoin = PenLineJoin.Round,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Opacity = isSelected ? 0.55 : 0.35,
                    BlurRadius = 14,
                    ShadowDepth = 3,
                    Direction = 270
                }
            };
            MenuCanvas.Children.Add(path);

            // 2. Sector Number Label
            double numberRadius = InnerRadius + 22;
            var numLabel = new TextBlock
            {
                Text = (i + 1).ToString(),
                Foreground = isSelected ? MediaBrushes.White : new SolidColorBrush(MediaColor.FromArgb(180, 255, 255, 255)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };
            Canvas.SetLeft(numLabel, Center + Math.Cos(midAngleRad) * numberRadius - 4);
            Canvas.SetTop(numLabel, Center + Math.Sin(midAngleRad) * numberRadius - 7);
            MenuCanvas.Children.Add(numLabel);

            // 3. Sector Vector Icon
            double iconRadius = InnerRadius + 70;
            var iconPath = new Path
            {
                Data = Geometry.Parse(SectorIconData[i % SectorIconData.Length]),
                Fill = MediaBrushes.White,
                Width = 20,
                Height = 20,
                Stretch = Stretch.Uniform
            };

            Canvas.SetLeft(iconPath, Center + Math.Cos(midAngleRad) * iconRadius - 10);
            Canvas.SetTop(iconPath, Center + Math.Sin(midAngleRad) * iconRadius - 10);
            MenuCanvas.Children.Add(iconPath);
        }

        // Center Hole Ring Overlay
        var centerCircle = new Ellipse
        {
            Width = InnerRadius * 2,
            Height = InnerRadius * 2,
            Fill = new SolidColorBrush(MediaColor.FromArgb(235, 18, 20, 26)),
            Stroke = new SolidColorBrush(MediaColor.FromArgb(40, 255, 255, 255)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(centerCircle, Center - InnerRadius);
        Canvas.SetTop(centerCircle, Center - InnerRadius);
        MenuCanvas.Children.Add(centerCircle);
    }

    /// <summary>
    /// Builds one sector as its own rounded-corner "glass tile", inset from its
    /// neighbors (SectorGapDeg) and from the inner/outer rings (SectorRadialGap),
    /// with each of its 4 corners filleted by SectorCornerRadius.
    /// </summary>
    private static Geometry SectorGeometry(int index)
    {
        double sectorStartDeg = (index * 45) - 90;
        double sectorEndDeg = sectorStartDeg + 45;

        double outerR = Radius - SectorRadialGap;
        double innerR = InnerRadius + SectorRadialGap;

        double startDeg = sectorStartDeg + SectorGapDeg / 2;
        double endDeg = sectorEndDeg - SectorGapDeg / 2;

        // Clamp the corner radius so it can't collapse the geometry if the
        // constants above are ever tuned to something extreme.
        double sweepRad = (endDeg - startDeg) * Math.PI / 180;
        double r = new[]
        {
            SectorCornerRadius,
            (outerR - innerR) / 2.0,
            (outerR * sweepRad) / 2.0,
            (innerR * sweepRad) / 2.0
        }.Min();

        // Degrees of arc "consumed" by the corner fillet at each radius.
        double dOuter = r / outerR * 180 / Math.PI;
        double dInner = r / innerR * 180 / Math.PI;

        WpfPoint P(double radius, double degrees)
        {
            double rad = degrees * Math.PI / 180;
            return new WpfPoint(Center + radius * Math.Cos(rad), Center + radius * Math.Sin(rad));
        }

        var outerArcStart = P(outerR, startDeg + dOuter);
        var outerArcEnd = P(outerR, endDeg - dOuter);
        var innerArcStart = P(innerR, endDeg - dInner);
        var innerArcEnd = P(innerR, startDeg + dInner);

        var startOuterRadial = P(outerR - r, startDeg);
        var startInnerRadial = P(innerR + r, startDeg);
        var endOuterRadial = P(outerR - r, endDeg);
        var endInnerRadial = P(innerR + r, endDeg);

        var figure = new PathFigure { StartPoint = startOuterRadial, IsClosed = true };

        // Outer-start corner fillet -> outer arc -> outer-end corner fillet
        figure.Segments.Add(new ArcSegment(outerArcStart, new WpfSize(r, r), 0, false, SweepDirection.Clockwise, true));
        figure.Segments.Add(new ArcSegment(outerArcEnd, new WpfSize(outerR, outerR), 0, false, SweepDirection.Clockwise, true));
        figure.Segments.Add(new ArcSegment(endOuterRadial, new WpfSize(r, r), 0, false, SweepDirection.Clockwise, true));

        // Straight radial edge down to the inner ring
        figure.Segments.Add(new LineSegment(endInnerRadial, true));

        // Inner-end corner fillet -> inner arc (reverse) -> inner-start corner fillet
        figure.Segments.Add(new ArcSegment(innerArcStart, new WpfSize(r, r), 0, false, SweepDirection.Clockwise, true));
        figure.Segments.Add(new ArcSegment(innerArcEnd, new WpfSize(innerR, innerR), 0, false, SweepDirection.Counterclockwise, true));
        figure.Segments.Add(new ArcSegment(startInnerRadial, new WpfSize(r, r), 0, false, SweepDirection.Clockwise, true));

        // Straight radial edge back up to the outer ring, closing the figure
        figure.Segments.Add(new LineSegment(startOuterRadial, true));

        return new PathGeometry(new[] { figure });
    }

    /// <summary>Per-tile fill: a flat translucent tint, letting the window's acrylic material do the visual work behind it.</summary>
    private static MediaBrush GlassSectorBrush(bool isSelected) =>
        isSelected
            ? new SolidColorBrush(MediaColor.FromArgb(220, 22, 119, 232))
            : new SolidColorBrush(MediaColor.FromArgb(60, 255, 255, 255));

    /// <summary>Thin flat divider between tiles - just enough to separate them, no glass affectation.</summary>
    private static MediaBrush GlassSectorStroke(bool isSelected) =>
        isSelected
            ? new SolidColorBrush(MediaColor.FromArgb(150, 200, 225, 255))
            : new SolidColorBrush(MediaColor.FromArgb(50, 255, 255, 255));

    private static void EnableBlur(IntPtr handle)
    {
        // ACCENT_ENABLE_BLURBEHIND alone renders basically no visible blur on modern
        // Windows unless paired with a non-zero-alpha GradientColor. ACCENT_ENABLE_ACRYLICBLURBEHIND
        // is the real Windows acrylic material (blur + subtle noise + tint). GradientColor is
        // 0xAABBGGRR - tune the leading alpha byte (0x66 below, ~40%) to trade off more
        // see-through vs. more tint.
        var accent = new AccentPolicy
        {
            AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
            GradientColor = unchecked((int)0x662A201E)
        };
        var size = Marshal.SizeOf(accent);
        var pointer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, pointer, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = size,
                Data = pointer
            };
            SetWindowCompositionAttribute(handle, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateEllipticRgn(int left, int top, int right, int bottom);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool redraw);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    private enum AccentState { ACCENT_ENABLE_BLURBEHIND = 3, ACCENT_ENABLE_ACRYLICBLURBEHIND = 4 }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public int SizeOfData;
        public IntPtr Data;
    }

    private enum WindowCompositionAttribute { WCA_ACCENT_POLICY = 19 }

}