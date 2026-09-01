using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfPoint = System.Windows.Point;

namespace Radial.UI;

public class RadialMenuSlice : Shape
{
    public static readonly DependencyProperty StartAngleProperty =
        DependencyProperty.Register(nameof(StartAngle), typeof(double), typeof(RadialMenuSlice), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EndAngleProperty =
        DependencyProperty.Register(nameof(EndAngle), typeof(double), typeof(RadialMenuSlice), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty InnerRadiusProperty =
        DependencyProperty.Register(nameof(InnerRadius), typeof(double), typeof(RadialMenuSlice), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OuterRadiusProperty =
        DependencyProperty.Register(nameof(OuterRadius), typeof(double), typeof(RadialMenuSlice), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double StartAngle { get => (double)GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }
    public double EndAngle { get => (double)GetValue(EndAngleProperty); set => SetValue(EndAngleProperty, value); }
    public double InnerRadius { get => (double)GetValue(InnerRadiusProperty); set => SetValue(InnerRadiusProperty, value); }
    public double OuterRadius { get => (double)GetValue(OuterRadiusProperty); set => SetValue(OuterRadiusProperty, value); }

    protected override Geometry DefiningGeometry
    {
        get
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                var startRad = (StartAngle - 90) * Math.PI / 180.0;
                var endRad = (EndAngle - 90) * Math.PI / 180.0;
                var innerStart = new WpfPoint(InnerRadius * Math.Cos(startRad), InnerRadius * Math.Sin(startRad));
                var outerStart = new WpfPoint(OuterRadius * Math.Cos(startRad), OuterRadius * Math.Sin(startRad));
                var outerEnd = new WpfPoint(OuterRadius * Math.Cos(endRad), OuterRadius * Math.Sin(endRad));
                var innerEnd = new WpfPoint(InnerRadius * Math.Cos(endRad), InnerRadius * Math.Sin(endRad));

                context.BeginFigure(innerStart, true, true);
                context.LineTo(outerStart, true, false);
                context.ArcTo(outerEnd, new System.Windows.Size(OuterRadius, OuterRadius), 0, false, SweepDirection.Clockwise, true, false);
                context.LineTo(innerEnd, true, false);
                context.ArcTo(innerStart, new System.Windows.Size(InnerRadius, InnerRadius), 0, false, SweepDirection.Counterclockwise, true, false);
            }

            geometry.Transform = new TranslateTransform(OuterRadius, OuterRadius);
            return geometry;
        }
    }
}
