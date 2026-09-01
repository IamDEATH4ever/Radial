using System;
using System.Diagnostics;
using System.Windows.Media;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace Radial.UI;

public sealed class SkiaRadialRenderer : IRadialRenderer
{
    private const float Size = 360f;
    private const float Center = Size / 2f;
    private const float Radius = 175f;
    private const float InnerRadius = 65f; // Wider gap for the hollow center
    private const float SectorGapDegrees = 4f; 
    private const float SectorRadialGap = 4f;
    private const float SectorCornerRadius = 10f; // Softened corners matching the image

    private readonly SKGLElement _surface;
    private readonly SKPaint _backgroundPaint;
    private readonly SKPaint _selectedPaint;
    private readonly SKPaint _borderPaint;
    private readonly SKPaint _iconTextPaint;
    private readonly SKFont _iconTextFont;
    
    // Animation State
    private int _targetSector;
    private int _segmentCount;
    private IReadOnlyList<string>? _labels;
    private int _lastSelectedSector = -1;
    private float _currentAnimatedSector;
    private bool _isAnimating;
    private float _rippleRadius;
    private float _rippleOpacity;
    private Stopwatch _stopwatch = new();
    private bool _disposed;

    public SkiaRadialRenderer(SKGLElement surface)
    {
        _surface = surface;
        
        // Setup Directional Lighting (Top-Left to Bottom-Right)
        var lightStart = new SKPoint(0, 0);
        var lightEnd = new SKPoint(Size, Size);

        // 1. Unselected Glass (Dark, highly translucent)
        _backgroundPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        _backgroundPaint.Shader = SKShader.CreateLinearGradient(
            lightStart, lightEnd,
            new[] { new SKColor(60, 60, 65, 140), new SKColor(10, 10, 15, 180) },
            null, SKShaderTileMode.Clamp);

        // 2. Selected Glass (Vibrant Blue Glow)
        _selectedPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        _selectedPaint.Shader = SKShader.CreateLinearGradient(
            lightStart, lightEnd,
            new[] { new SKColor(0, 160, 255, 230), new SKColor(0, 50, 180, 230) },
            null, SKShaderTileMode.Clamp);

        // 3. Glass Edge / Bevel (Bright white top-left, fading to transparent bottom-right)
        _borderPaint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        _borderPaint.Shader = SKShader.CreateLinearGradient(
            lightStart, lightEnd,
            new[] { new SKColor(255, 255, 255, 180), new SKColor(255, 255, 255, 15) },
            null, SKShaderTileMode.Clamp);

        // 4. Text/Icon Paint
        _iconTextPaint = new SKPaint {
            Color = SKColors.White,
            IsAntialias = true
        };
        _iconTextFont = new SKFont(
            SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            14f)
        {
            Edging = SKFontEdging.Antialias,
            Subpixel = true
        };
        
        _surface.PaintSurface += OnPaintSurface;
        CompositionTarget.Rendering += OnRenderLoop;
        _stopwatch.Start();
    }

    public void Update(RadialRenderState state) => Update(state, false);

    public void Update(RadialRenderState state, bool isClick)
    {
        var countChanged = _segmentCount != Math.Clamp(state.SegmentCount, 0, 12);
        _segmentCount = Math.Clamp(state.SegmentCount, 0, 12);
        _labels = state.Labels;
        if (countChanged) { _lastSelectedSector = -1; _targetSector = state.SelectedSector; _currentAnimatedSector = state.SelectedSector; }
        if (_lastSelectedSector < 0)
        {
            _lastSelectedSector = state.SelectedSector;
            _targetSector = state.SelectedSector;
            _currentAnimatedSector = state.SelectedSector;
            _isAnimating = true;
        }
        else if (_lastSelectedSector != state.SelectedSector)
        {
            // Keep the animation angle continuous across the circular wrap.
            var delta = state.SelectedSector - _lastSelectedSector;
        var half = Math.Max(1, _segmentCount) / 2f;

            if (_segmentCount > 0) { if (delta > half) delta -= _segmentCount; else if (delta < -half) delta += _segmentCount; }

            _targetSector += delta;
            _lastSelectedSector = state.SelectedSector;
            _isAnimating = true;
        }

        if (isClick)
        {
            _rippleRadius = InnerRadius;
            _rippleOpacity = 1.0f;
            _isAnimating = true;
        }

        if (!_disposed && !_isAnimating)
            _surface.InvalidateVisual();
    }

    private void OnRenderLoop(object? sender, EventArgs e)
    {
        if (_disposed || !_isAnimating) return;

        bool needsMoreFrames = false;
        float diff = _targetSector - _currentAnimatedSector;
        
        if (Math.Abs(diff) > 0.01f)
        {
            _currentAnimatedSector += diff * 0.25f; 
            needsMoreFrames = true;
        }
        else
        {
            _currentAnimatedSector = _targetSector;
        }

        if (_rippleOpacity > 0.01f)
        {
            _rippleRadius += 10f;
            _rippleOpacity *= 0.85f;
            needsMoreFrames = true;
        }

        _isAnimating = needsMoreFrames;
        _surface.InvalidateVisual();
    }

    private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        
        // Draw Base Sectors
        for (var sector = 0; sector < _segmentCount; sector++)
        {
            using var path = CreateSectorPath(sector);
            canvas.DrawPath(path, _backgroundPaint);
            canvas.DrawPath(path, _borderPaint);

            // Draw Icon / Number in the center of the sector
            DrawSectorIcon(canvas, sector);
        }

        // Draw Animated Highlight Overlap
        if (_currentAnimatedSector >= 0)
        {
            using var highlightPath = CreateAnimatedSectorPath(_currentAnimatedSector);
            
            // Glowing Blue Drop shadow
            using var shadowPaint = new SKPaint { 
                Style = SKPaintStyle.StrokeAndFill, 
                Color = new SKColor(0, 100, 255, 120), 
                ImageFilter = SKImageFilter.CreateBlur(12, 12) 
            };
            
            canvas.DrawPath(highlightPath, shadowPaint);
            canvas.DrawPath(highlightPath, _selectedPaint);
            canvas.DrawPath(highlightPath, _borderPaint);
        }
        
        // Note: The center remains entirely transparent/hollow to match the reference image.
    }

    private void DrawSectorIcon(SKCanvas canvas, int sector)
    {
        // Calculate the physical center point of this pie slice
        float midAngle = sector * (360f / _segmentCount) - 90f + (180f / _segmentCount);
        float midRadius = InnerRadius + (Radius - InnerRadius) / 2f;
        var textPt = Point(midRadius, midAngle);
        
        // Offset Y slightly to visually center the text vertically
        float yOffset = _iconTextFont.Size / 3f;
        
        var label = _labels?.ElementAtOrDefault(sector) ?? (sector + 1).ToString();
        canvas.DrawText(label, new SKPoint(textPt.X, textPt.Y + yOffset), SKTextAlign.Center, _iconTextFont, _iconTextPaint);
    }

    private SKPath CreateAnimatedSectorPath(float animatedIndex)
    {
        var size = 360f / _segmentCount;
        var sectorStart = animatedIndex * size - 90f;
        return CreatePathForBounds(sectorStart, sectorStart + size);
    }

    private SKPath CreateSectorPath(int index)
    {
        var size = 360f / _segmentCount;
        var sectorStart = index * size - 90f;
        return CreatePathForBounds(sectorStart, sectorStart + size);
    }

    private static SKPath CreatePathForBounds(float sectorStart, float sectorEnd)
    {
        var outerRadius = Radius - SectorRadialGap;
        var innerRadius = InnerRadius + SectorRadialGap;
        var start = sectorStart + SectorGapDegrees / 2f;
        var end = sectorEnd - SectorGapDegrees / 2f;
        var sweep = end - start;

        var corner = MathF.Min(
            SectorCornerRadius,
            MathF.Min((outerRadius - innerRadius) / 2f, (innerRadius * sweep * MathF.PI / 180f) / 2f));
        
        var outerInset = corner / outerRadius * 180f / MathF.PI;
        var innerInset = corner / innerRadius * 180f / MathF.PI;
        var outerOval = new SKRect(Center - outerRadius, Center - outerRadius, Center + outerRadius, Center + outerRadius);
        var innerOval = new SKRect(Center - innerRadius, Center - innerRadius, Center + innerRadius, Center + innerRadius);

        var startOuter = Point(outerRadius - corner, start);
        var outerArcStart = Point(outerRadius, start + outerInset);
        var outerArcEnd = Point(outerRadius, end - outerInset);
        var endOuter = Point(outerRadius - corner, end);
        var endInner = Point(innerRadius + corner, end);
        var innerArcStart = Point(innerRadius, end - innerInset);
        var innerArcEnd = Point(innerRadius, start + innerInset);
        var startInner = Point(innerRadius + corner, start);

        using var builder = new SKPathBuilder();
        builder.MoveTo(startOuter);
        builder.QuadTo(Point(outerRadius, start), outerArcStart);
        builder.ArcTo(outerOval, start + outerInset, sweep - 2f * outerInset, false);
        builder.QuadTo(Point(outerRadius, end), endOuter);
        builder.LineTo(endInner);
        builder.QuadTo(Point(innerRadius, end), innerArcStart);
        builder.ArcTo(innerOval, end - innerInset, -(sweep - 2f * innerInset), false);
        builder.QuadTo(Point(innerRadius, start), startInner);
        builder.Close();
        return builder.Detach();
    }

    private static SKPoint Point(float radius, float degrees)
    {
        var radians = degrees * MathF.PI / 180f;
        return new SKPoint(Center + radius * MathF.Cos(radians), Center + radius * MathF.Sin(radians));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        CompositionTarget.Rendering -= OnRenderLoop;
        _surface.PaintSurface -= OnPaintSurface;
        
        // Clean up unmanaged Skia resources
        _backgroundPaint.Shader?.Dispose();
        _selectedPaint.Shader?.Dispose();
        _borderPaint.Shader?.Dispose();
        
        _backgroundPaint.Dispose();
        _selectedPaint.Dispose();
        _borderPaint.Dispose();
        _iconTextPaint.Dispose();
        _iconTextFont.Dispose();
    }
}
