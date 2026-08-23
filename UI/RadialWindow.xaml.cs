using System.Windows;
using WpfPoint = System.Windows.Point;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Radial.Core;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using WpfSize = System.Windows.Size;

namespace Radial.UI;

public partial class RadialWindow : Window
{
    private const double Diameter = 360, Radius = 170, InnerRadius = 38;
    private readonly RadialMenu _menu;
    public int SelectedSector => _menu.SelectedSector;

    public RadialWindow(WpfPoint center)
    {
        InitializeComponent();
        _menu = new RadialMenu(center);
        Left = center.X - Diameter / 2;
        Top = center.Y - Diameter / 2;
        Render();
    }

    public void UpdateCursor(WpfPoint position) { _menu.UpdateSelection(position); Render(); }

    private void Render()
    {
        MenuCanvas.Children.Clear();
        for (var i = 0; i < RadialMenu.SectorCount; i++)
        {
            var path = new Path { Data = SectorGeometry(i), Fill = i == _menu.SelectedSector ? MediaBrushes.DeepSkyBlue : MediaBrushes.White, Opacity = 0.88, Stroke = MediaBrushes.DimGray, StrokeThickness = 2 };
            MenuCanvas.Children.Add(path);
            var angle = (i + .5) * 45 * Math.PI / 180;
            var label = new TextBlock { Text = (i + 1).ToString(), Foreground = MediaBrushes.Black, FontSize = 20, FontWeight = FontWeights.Bold };
            Canvas.SetLeft(label, 180 + Math.Cos(angle) * 105 - 7); Canvas.SetTop(label, 180 + Math.Sin(angle) * 105 - 14); MenuCanvas.Children.Add(label);
        }
        var center = new Ellipse { Width = InnerRadius * 2, Height = InnerRadius * 2, Fill = new SolidColorBrush(MediaColor.FromArgb(190, 25, 25, 25)) };
        Canvas.SetLeft(center, 180 - InnerRadius); Canvas.SetTop(center, 180 - InnerRadius); MenuCanvas.Children.Add(center);
    }

    private static Geometry SectorGeometry(int index)
    {
        var start = index * 45 - 22.5; var end = start + 45;
        WpfPoint P(double radius, double degrees) { var r = degrees * Math.PI / 180; return new WpfPoint(180 + radius * Math.Cos(r), 180 + radius * Math.Sin(r)); }
        var figure = new PathFigure { StartPoint = P(InnerRadius, start), IsClosed = true };
        figure.Segments.Add(new LineSegment(P(Radius, start), true));
        figure.Segments.Add(new ArcSegment(P(Radius, end), new WpfSize(Radius, Radius), 45, false, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(P(InnerRadius, end), true));
        return new PathGeometry(new[] { figure });
    }
}
