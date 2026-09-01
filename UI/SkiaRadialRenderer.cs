using System;
using System.Diagnostics;
using System.Windows.Media;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

// ---------------------------------------------------------------------------
// NOTE ON BACKEND / SHADER EXECUTION
// ---------------------------------------------------------------------------
// SKElement uses SkiaSharp's CPU software rasterizer (no OpenGL/DirectX). The
// SKRuntimeEffect (SkSL) shader is therefore executed on the CPU by Skia's
// built-in interpreter. This is intentional: switching to SKGLElement (GPU)
// would require XAML changes + an OpenGL context on the WPF HWND, which is
// fragile on Windows with AllowsTransparency=True and a transparent window
// region. For a 360×360 surface running at ~60 fps the CPU path is fast
// enough, and it avoids the known WPF+OpenGL composition pitfalls.
//
// Liquid Glass is implemented as UV displacement (refraction) based on the
// radial glass SDF — NOT as blur. Blur is used only for the subtle drop-shadow
// beneath the selected segment.
// ---------------------------------------------------------------------------

namespace Radial.UI;

public sealed class SkiaRadialRenderer : IRadialRenderer
{
    // -----------------------------------------------------------------------
    // Geometry constants
    // -----------------------------------------------------------------------
    private const float Size             = 360f;
    private const float Center           = Size / 2f;
    private const float Radius           = 175f;
    private const float InnerRadius      = 65f;
    private const float SectorGapDegrees = 3.5f;   // refined: slightly tighter gap
    private const float SectorRadialGap  = 3.5f;   // refined: tighter radial gap
    private const float SectorCornerRadius = 12f;  // refined: slightly more rounded

    // -----------------------------------------------------------------------
    // SkSL Liquid Glass shader (CPU-executed via SKRuntimeEffect)
    //
    // Algorithm (derived from Aghajari / WPF-Liquid-Glass-Effect):
    //   1. Compute SDF distance from the nearest glass boundary (inner/outer arc).
    //   2. Convert SDF to a lens profile (circular cross-section).
    //   3. Offset UV by lens profile × direction → optical refraction.
    //   4. Add chromatic aberration: sample R/G/B at slightly different UVs.
    //   5. Apply saturation, brightness, glass tint.
    //   6. Cursor-driven specular highlight + rim lighting.
    // -----------------------------------------------------------------------
    private static readonly string SkSl = @"
uniform shader  Backdrop;       // Captured desktop image (un-blurred)
uniform float2  CanvasSize;     // Pixel dimensions of the 360×360 canvas
uniform float2  ImageSize;      // Pixel dimensions of the captured SKImage
uniform float2  GlassCenter;    // Center of the radial (canvas coords)
uniform float2  CursorPos;      // Cursor in canvas coords
uniform float2  Radii;          // x=inner radius, y=outer radius
uniform float4  Material;       // x=refractionScale, y=distortion, z=aberration, w=unused
uniform float4  Lighting;       // x=specular, y=selectedBoost, z=brightness, w=saturation
uniform float4  Tint;           // rgb=glass tint color, a=tint strength

half4 main(float2 fragCoord) {
    // --- Lens SDF ---
    float2 fromCenter = fragCoord - GlassCenter;
    float  r          = max(length(fromCenter), 1.0);
    float2 dir        = fromCenter / r;

    float inner    = Radii.x;
    float outer    = Radii.y;
    float thickness = max(outer - inner, 1.0);

    float distInner  = r - inner;
    float distOuter  = outer - r;
    float sdf        = min(distInner, distOuter);          // ≥0 inside the ring
    float normSdf    = clamp(sdf / (thickness * 0.30), 0.0, 1.0);

    // --- Circular lens profile (convex cross-section) ---
    float t = 1.0 - normSdf;
    float lens = 1.0 - sqrt(max(0.0, 1.0 - t * t));      // 0 at edges, 1 at peak
    lens *= Material.y;

    // --- UV displacement (refraction) ---
    float2 offset     = lens * dir * Material.x * thickness;
    float2 sampleCoord = fragCoord - offset;

    // --- Chromatic aberration along edge ---
    float  edgeFactor = smoothstep(0.08, 0.0, normSdf);
    float2 caShift    = dir * edgeFactor * Material.z;

    // Map from canvas coords to image UVs
    float2 toUV = ImageSize / CanvasSize;

    half4 glass;
    glass.r = Backdrop.eval((sampleCoord - caShift) * toUV).r;
    glass.g = Backdrop.eval( sampleCoord            * toUV).g;
    glass.b = Backdrop.eval((sampleCoord + caShift) * toUV).b;
    glass.a = 1.0;

    // --- Color grading ---
    float luma = dot(glass.rgb, half3(0.2126, 0.7152, 0.0722));
    glass.rgb  = mix(half3(luma), glass.rgb, Lighting.w);   // saturation
    glass.rgb *= Lighting.z;                                  // brightness
    glass.rgb  = mix(glass.rgb, half3(Tint.r, Tint.g, Tint.b), Tint.a); // tint

    // --- Lighting: cursor specular + rim glow ---
    float2 lightDir = normalize(CursorPos - GlassCenter + float2(0.001, 0.001));
    float  rim      = pow(clamp(1.0 - normSdf * 2.5, 0.0, 1.0), 2.0);
    float  spec     = pow(clamp(dot(dir, lightDir), 0.0, 1.0), 32.0) * Lighting.x;
    float  selBoost = Lighting.y;  // 0 for normal, 1 for selected
    glass.rgb      += (rim * 0.18 + spec) * (0.5 + selBoost * 0.5);

    return glass;
}
";

    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------
    private readonly SKElement              _surface;
    private readonly SKImage?               _backdrop;
    private readonly LiquidGlassSettings    _settings;
    private readonly SKRuntimeEffect?       _effect;

    // Fallback paints (used when backdrop/shader is unavailable)
    private readonly SKPaint _backgroundPaint;
    private readonly SKPaint _selectedPaint;

    // Overlay paints (always drawn on top of glass)
    private readonly SKPaint _internalHighlightPaint;
    private readonly SKPaint _borderPaint;
    private readonly SKPaint _iconTextPaint;
    private readonly SKFont  _iconTextFont;

    // Animation state
    private SKPoint _cursor       = new(Center, Center);
    private SKPoint _targetCursor = new(Center, Center);
    private int   _targetSector;
    private int   _segmentCount;
    private IReadOnlyList<string>? _labels;
    private int   _lastSelectedSector = -1;
    private float _currentAnimatedSector;
    private bool  _isAnimating;
    private float _rippleRadius;
    private float _rippleOpacity;
    private float _openProgress;
    private bool  _disposed;

    private readonly Stopwatch _stopwatch     = new();
    private double             _lastFrameTime;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------
    public SkiaRadialRenderer(SKElement surface, SKImage? backdrop = null, LiquidGlassSettings? settings = null)
    {
        _surface  = surface;
        _backdrop = backdrop;
        _settings = settings ?? LiquidGlassSettings.Default;

        // Compile SkSL shader (CPU-executed by Skia's interpreter)
        _effect = SKRuntimeEffect.CreateShader(SkSl, out var shaderError);
        if (_effect == null)
            Debug.WriteLine($"[SkiaRenderer] Shader compile failed: {shaderError}");

        // ── Fallback gradient paints ──────────────────────────────────────
        var lt = new SKPoint(0, 0);
        var rb = new SKPoint(Size, Size);

        _backgroundPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        _backgroundPaint.Shader = SKShader.CreateLinearGradient(lt, rb,
            new[] { new SKColor(72, 78, 88, _settings.SurfaceTintAlpha),
                    new SKColor(18, 23, 32,  (byte)Math.Min(140, _settings.SurfaceTintAlpha + 42)) },
            null, SKShaderTileMode.Clamp);

        _selectedPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        _selectedPaint.Shader = SKShader.CreateLinearGradient(lt, rb,
            new[] { new SKColor(84, 178, 232, _settings.SelectedTintAlpha),
                    new SKColor(20, 88,  156, _settings.SelectedTintAlpha) },
            null, SKShaderTileMode.Clamp);

        // ── Overlay: internal reflection (top-of-sector bright streak) ────
        _internalHighlightPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        _internalHighlightPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(Center, 20), new SKPoint(Center, Center + 20),
            new[] { new SKColor(255, 255, 255, _settings.InternalHighlightAlpha),
                    SKColors.Transparent },
            null, SKShaderTileMode.Clamp);

        // ── Overlay: glass edge bevel (top-left bright → bottom-right dim) ─
        _borderPaint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f, IsAntialias = true };
        _borderPaint.Shader = SKShader.CreateLinearGradient(lt, rb,
            new[] { new SKColor(255, 255, 255, _settings.EdgeHighlightAlpha),
                    new SKColor(255, 255, 255, 12) },
            null, SKShaderTileMode.Clamp);

        // ── Text / Icon ───────────────────────────────────────────────────
        _iconTextPaint = new SKPaint { IsAntialias = true };
        _iconTextFont  = new SKFont(
            SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold,
                SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 13.5f)
        {
            Edging    = SKFontEdging.SubpixelAntialias,
            Subpixel  = true
        };

        _surface.PaintSurface           += OnPaintSurface;
        CompositionTarget.Rendering     += OnRenderLoop;
        _stopwatch.Start();
        _isAnimating = true;   // drives open animation
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------
    public void Update(RadialRenderState state) => Update(state, false);

    public void Update(RadialRenderState state, bool isClick)
    {
        var countChanged = _segmentCount != Math.Clamp(state.SegmentCount, 0, 12);
        _segmentCount = Math.Clamp(state.SegmentCount, 0, 12);
        _labels = state.Labels;

        if (countChanged)
        {
            _lastSelectedSector   = -1;
            _targetSector         = state.SelectedSector;
            _currentAnimatedSector = state.SelectedSector;
        }

        if (_lastSelectedSector < 0)
        {
            _lastSelectedSector    = state.SelectedSector;
            _targetSector          = state.SelectedSector;
            _currentAnimatedSector = state.SelectedSector;
            _isAnimating           = true;
        }
        else if (_lastSelectedSector != state.SelectedSector)
        {
            var delta = state.SelectedSector - _lastSelectedSector;
            var half  = Math.Max(1, _segmentCount) / 2f;
            if (_segmentCount > 0)
            {
                if (delta > half)  delta -= _segmentCount;
                else if (delta < -half) delta += _segmentCount;
            }
            _targetSector      += delta;
            _lastSelectedSector = state.SelectedSector;
            _isAnimating        = true;
        }

        if (isClick)
        {
            _rippleRadius  = InnerRadius;
            _rippleOpacity = 1.0f;
            _isAnimating   = true;
        }

        if (!_disposed && !_isAnimating)
            _surface.InvalidateVisual();
    }

    public void UpdateCursor(System.Windows.Point cursor)
    {
        _targetCursor = new SKPoint(
            (float)Math.Clamp(cursor.X, 0, Size),
            (float)Math.Clamp(cursor.Y, 0, Size));
        _isAnimating = true;
    }

    // -----------------------------------------------------------------------
    // Render loop (per-frame interpolation)
    // -----------------------------------------------------------------------
    private void OnRenderLoop(object? sender, EventArgs e)
    {
        if (_disposed || !_isAnimating) return;

        var now     = _stopwatch.Elapsed.TotalSeconds;
        var elapsed = _lastFrameTime <= 0 ? 1f / 60f : (float)Math.Min(0.05, now - _lastFrameTime);
        _lastFrameTime = now;

        bool needsMore = false;

        // Opening: smooth ease-out-quart (feels like settling into place)
        if (_openProgress < 1f)
        {
            _openProgress = Math.Min(1f, _openProgress + elapsed / _settings.OpenDurationSeconds);
            needsMore |= _openProgress < 1f;
        }

        // Cursor spring (immediate response with exponential decay)
        _cursor = Approach(_cursor, _targetCursor, _settings.CursorResponse, elapsed);
        needsMore |= Distance(_cursor, _targetCursor) > 0.25f;

        // Selection spring (smooth arc transition)
        float diff = _targetSector - _currentAnimatedSector;
        if (Math.Abs(diff) > 0.005f)
        {
            _currentAnimatedSector += diff * (1f - MathF.Exp(-_settings.SelectionResponse * elapsed));
            needsMore = true;
        }
        else
        {
            _currentAnimatedSector = _targetSector;
        }

        // Click ripple
        if (_rippleOpacity > 0.01f)
        {
            _rippleRadius  += _settings.RippleExpansionPerSecond * elapsed;
            _rippleOpacity *= MathF.Exp(-_settings.RippleFadePerSecond * elapsed);
            needsMore = true;
        }

        _isAnimating = needsMore;
        _surface.InvalidateVisual();
    }

    // -----------------------------------------------------------------------
    // Paint
    // -----------------------------------------------------------------------
    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // ── Opening scale (ease-out-quart: responsive, no bounce) ─────────
        var t     = EaseOutQuart(_openProgress);
        var scale = _settings.OpenStartScale + (1f - _settings.OpenStartScale) * t;
        var alpha = t;

        canvas.Save();
        canvas.Scale(scale, scale, Center, Center);

        // ── Base sectors ──────────────────────────────────────────────────
        for (var sector = 0; sector < _segmentCount; sector++)
        {
            using var path = CreateSectorPath(sector);

            // Glass material: refracted backdrop OR fallback tinted gradient
            DrawGlassSector(canvas, path,
                refractionBoost: 1f,
                selectedBoost:   0f,
                tintR: 72/255f, tintG: 78/255f, tintB: 88/255f,
                tintA: _settings.SurfaceTintAlpha / 255f,
                alpha: alpha);

            // Internal highlight streak (top of segment)
            using var highlightPaint = FadedPaint(_internalHighlightPaint, alpha);
            canvas.DrawPath(path, highlightPaint);

            // Edge bevel
            using var borderPaint = FadedPaint(_borderPaint, alpha);
            canvas.DrawPath(path, borderPaint);

            DrawSectorIcon(canvas, sector, alpha);
        }

        // ── Selected segment overlay ──────────────────────────────────────
        if (_currentAnimatedSector >= 0 && _segmentCount > 0)
        {
            using var highlightPath = CreateAnimatedSectorPath(_currentAnimatedSector);

            // Subtle drop-shadow (only blur usage)
            var shadowAlpha = (byte)(255 * _settings.SelectionShadowAlpha * alpha);
            using var shadowPaint = new SKPaint
            {
                Style       = SKPaintStyle.Fill,
                Color       = new SKColor(0, 90, 180, shadowAlpha),
                ImageFilter = SKImageFilter.CreateBlur(
                    _settings.SelectionShadowSigma, _settings.SelectionShadowSigma)
            };

            canvas.Save();
            canvas.Scale(_settings.HoverScale, _settings.HoverScale, Center, Center);
            canvas.DrawPath(highlightPath, shadowPaint);

            // Selected glass: boosted refraction, vibrant blue tint, stronger specular
            DrawGlassSector(canvas, highlightPath,
                refractionBoost: _settings.SelectedRefractionBoost,
                selectedBoost:   1f,
                tintR: 30/255f, tintG: 140/255f, tintB: 255/255f,
                tintA: _settings.SelectedTintAlpha / 255f,
                alpha: alpha);

            using var hi2 = FadedPaint(_internalHighlightPaint, alpha);
            canvas.DrawPath(highlightPath, hi2);

            // Brighter edge bevel on selected
            using var selBorderPaint = new SKPaint
            {
                Style       = SKPaintStyle.Stroke,
                StrokeWidth = 0.8f,
                IsAntialias = true
            };
            selBorderPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(Size, Size),
                new[]
                {
                    new SKColor(255, 255, 255, (byte)(220 * alpha)),
                    new SKColor(180, 220, 255, (byte)(60 * alpha))
                },
                null, SKShaderTileMode.Clamp);
            canvas.DrawPath(highlightPath, selBorderPaint);
            canvas.Restore();
        }

        // ── Cursor light (radial glow follows the pointer) ────────────────
        using var cursorShader = SKShader.CreateRadialGradient(
            _cursor, _settings.CursorLightRadius,
            new[] { new SKColor(255, 255, 255, (byte)(_settings.CursorLightAlpha * alpha)),
                    SKColors.Transparent },
            null, SKShaderTileMode.Clamp);
        using var cursorPaint = new SKPaint
        {
            Style       = SKPaintStyle.Fill,
            IsAntialias = true,
            Shader      = cursorShader
        };
        canvas.Save();
        using var ringClip = new SKPathBuilder();
        ringClip.AddCircle(Center, Center, Radius);
        using var ringPath = ringClip.Detach();
        canvas.ClipPath(ringPath);
        canvas.DrawCircle(_cursor, _settings.CursorLightRadius, cursorPaint);
        canvas.Restore();

        // ── Click ripple ──────────────────────────────────────────────────
        if (_rippleOpacity > 0.01f)
        {
            using var ripplePaint = new SKPaint
            {
                Style       = SKPaintStyle.Stroke,
                StrokeWidth = 1.2f,
                IsAntialias = true,
                Color       = new SKColor(220, 241, 255, (byte)(68 * _rippleOpacity * alpha))
            };
            canvas.DrawCircle(Center, Center, _rippleRadius, ripplePaint);
        }

        canvas.Restore(); // opening scale
    }

    // -----------------------------------------------------------------------
    // Glass material draw helper
    // -----------------------------------------------------------------------
    private void DrawGlassSector(
        SKCanvas canvas, SKPath path,
        float refractionBoost, float selectedBoost,
        float tintR, float tintG, float tintB, float tintA,
        float alpha)
    {
        if (_backdrop is not null && _effect is not null)
        {
            var imgW = (float)_backdrop.Width;
            var imgH = (float)_backdrop.Height;

            var uniforms = new SKRuntimeEffectUniforms(_effect)
            {
                { "CanvasSize",   new float[] { Size,  Size  } },
                { "ImageSize",    new float[] { imgW,  imgH  } },
                { "GlassCenter",  new float[] { Center, Center } },
                { "CursorPos",    new float[] { _cursor.X, _cursor.Y } },
                { "Radii",        new float[] { InnerRadius, Radius } },
                {
                    "Material",   new float[]
                    {
                        _settings.RefractionOffset * refractionBoost,
                        _settings.Distortion,
                        _settings.ChromaticAberration,
                        0f
                    }
                },
                {
                    "Lighting",   new float[]
                    {
                        _settings.SpecularIntensity,
                        selectedBoost,
                        _settings.Brightness,
                        _settings.Saturation
                    }
                },
                { "Tint", new float[] { tintR, tintG, tintB, tintA } }
            };

            using var backdropShader = _backdrop.ToShader(
                SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, SKSamplingOptions.Default);
            using var children = new SKRuntimeEffectChildren(_effect)
            {
                { "Backdrop", backdropShader }
            };
            using var shader    = _effect.ToShader(uniforms, children);
            using var glassPaint = new SKPaint
            {
                Shader      = shader,
                IsAntialias = true,
                Color       = SKColors.White.WithAlpha((byte)(255 * alpha)),
                ImageFilter = SKImageFilter.CreateBlur(
                    _settings.GlassBlurSigma, _settings.GlassBlurSigma)
            };
            canvas.DrawPath(path, glassPaint);
        }
        else
        {
            // Fallback: tinted gradient
            var fallbackPaint = selectedBoost > 0.5f ? _selectedPaint : _backgroundPaint;
            using var fp2 = FadedPaint(fallbackPaint, alpha);
            canvas.DrawPath(path, fp2);
        }
    }

    // -----------------------------------------------------------------------
    // Icon / text
    // -----------------------------------------------------------------------
    private void DrawSectorIcon(SKCanvas canvas, int sector, float alpha)
    {
        if (_segmentCount == 0) return;

        float midAngle  = sector * (360f / _segmentCount) - 90f + (180f / _segmentCount);
        float midRadius = InnerRadius + (Radius - InnerRadius) / 2f;
        var   pt        = Point(midRadius, midAngle);
        float yOffset   = _iconTextFont.Size / 3f;

        var label = _labels?.ElementAtOrDefault(sector) ?? (sector + 1).ToString();

        // Subtle shadow for legibility on any background
        using var shadowPaint = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, (byte)(90 * alpha)) };
        canvas.DrawText(label, new SKPoint(pt.X + 0.8f, pt.Y + yOffset + 0.8f),
            SKTextAlign.Center, _iconTextFont, shadowPaint);

        _iconTextPaint.Color = SKColors.White.WithAlpha((byte)(230 * alpha));
        canvas.DrawText(label, new SKPoint(pt.X, pt.Y + yOffset),
            SKTextAlign.Center, _iconTextFont, _iconTextPaint);
    }

    // -----------------------------------------------------------------------
    // Path builders
    // -----------------------------------------------------------------------
    private SKPath CreateAnimatedSectorPath(float animatedIndex)
    {
        var size  = 360f / _segmentCount;
        var start = animatedIndex * size - 90f;
        return CreatePathForBounds(start, start + size);
    }

    private SKPath CreateSectorPath(int index)
    {
        var size  = 360f / _segmentCount;
        var start = index * size - 90f;
        return CreatePathForBounds(start, start + size);
    }

    private static SKPath CreatePathForBounds(float sectorStart, float sectorEnd)
    {
        var outerRadius = Radius      - SectorRadialGap;
        var innerRadius = InnerRadius + SectorRadialGap;
        var start       = sectorStart + SectorGapDegrees / 2f;
        var end         = sectorEnd   - SectorGapDegrees / 2f;
        var sweep       = end - start;

        var corner = MathF.Min(
            SectorCornerRadius,
            MathF.Min((outerRadius - innerRadius) / 2f,
                      (innerRadius * sweep * MathF.PI / 180f) / 2f));

        var outerInset = corner / outerRadius * 180f / MathF.PI;
        var innerInset = corner / innerRadius * 180f / MathF.PI;

        var outerOval = new SKRect(Center - outerRadius, Center - outerRadius,
                                   Center + outerRadius, Center + outerRadius);
        var innerOval = new SKRect(Center - innerRadius, Center - innerRadius,
                                   Center + innerRadius, Center + innerRadius);

        var startOuter  = Point(outerRadius - corner, start);
        var outerArcS   = Point(outerRadius,           start + outerInset);
        var outerArcE   = Point(outerRadius,           end   - outerInset);
        var endOuter    = Point(outerRadius - corner, end);
        var endInner    = Point(innerRadius + corner, end);
        var innerArcS   = Point(innerRadius,           end   - innerInset);
        var innerArcE   = Point(innerRadius,           start + innerInset);
        var startInner  = Point(innerRadius + corner, start);

        using var builder = new SKPathBuilder();
        builder.MoveTo(startOuter);
        builder.QuadTo(Point(outerRadius, start), outerArcS);
        builder.ArcTo(outerOval, start + outerInset, sweep - 2f * outerInset, false);
        builder.QuadTo(Point(outerRadius, end), endOuter);
        builder.LineTo(endInner);
        builder.QuadTo(Point(innerRadius, end), innerArcS);
        builder.ArcTo(innerOval, end - innerInset, -(sweep - 2f * innerInset), false);
        builder.QuadTo(Point(innerRadius, start), startInner);
        builder.Close();
        return builder.Detach();
    }

    // -----------------------------------------------------------------------
    // Math helpers
    // -----------------------------------------------------------------------
    private static SKPoint Point(float radius, float degrees)
    {
        var rad = degrees * MathF.PI / 180f;
        return new SKPoint(Center + radius * MathF.Cos(rad),
                           Center + radius * MathF.Sin(rad));
    }

    private static SKPoint Approach(SKPoint cur, SKPoint tgt, float response, float dt)
    {
        var k = 1f - MathF.Exp(-response * dt);
        return new SKPoint(cur.X + (tgt.X - cur.X) * k,
                           cur.Y + (tgt.Y - cur.Y) * k);
    }

    private static float Distance(SKPoint a, SKPoint b) => (a - b).Length;

    // Apple HIG: ease-out-quart feels natural and non-bouncy for opening
    private static float EaseOutQuart(float v)
    {
        v = 1f - v;
        return 1f - v * v * v * v;
    }

    // Create a cloned paint with the alpha scaled by factor
    private static SKPaint FadedPaint(SKPaint source, float alpha)
    {
        var p = source.Clone();
        p.Color = p.Color.WithAlpha((byte)(p.Color.Alpha * alpha));
        return p;
    }

    // -----------------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------------
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CompositionTarget.Rendering -= OnRenderLoop;
        _surface.PaintSurface       -= OnPaintSurface;

        _backgroundPaint.Shader?.Dispose();
        _selectedPaint.Shader?.Dispose();
        _internalHighlightPaint.Shader?.Dispose();
        _borderPaint.Shader?.Dispose();

        _backgroundPaint.Dispose();
        _selectedPaint.Dispose();
        _internalHighlightPaint.Dispose();
        _borderPaint.Dispose();
        _iconTextPaint.Dispose();
        _iconTextFont.Dispose();
        _effect?.Dispose();
        _backdrop?.Dispose();
    }
}
