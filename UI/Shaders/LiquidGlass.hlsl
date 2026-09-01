// Liquid Glass lensing shader (algorithm reference).
// Radial executes the equivalent SkSL via SKRuntimeEffect so the effect can be
// clipped to radial sector paths. This HLSL documents the same model used by
// WPF-Liquid-Glass-Effect / Aghajari's Liquid Glass explanation:
//   SDF edge distance -> circular lens profile -> UV offset -> chromatic sample.
//
// Not compiled by MSBuild (WPF ps_3.0 cannot express this with child textures
// the way Skia runtime shaders can).

sampler2D Backdrop : register(s0);

float4 TextureSize;     // xy = canvas size in pixels
float4 GlassCenter;     // xy = radial center
float4 Cursor;          // xy = cursor in canvas space
float4 Radii;           // x = inner, y = outer
float4 Material;        // x = refraction, y = distortion, z = aberration, w = blur
float4 Lighting;        // x = specular, y = selected, z = brightness, w = saturation
float4 Tint;            // rgb tint, a = tint strength

float Saturate(float v) { return saturate(v); }

float4 SampleBackdrop(float2 coord)
{
    float2 uv = coord / TextureSize.xy;
    return tex2D(Backdrop, uv);
}

float4 BlurSample(float2 coord, float radius)
{
    float4 color = 0;
    float weight = 0;
    [unroll]
    for (int y = -2; y <= 2; y++)
    {
        [unroll]
        for (int x = -2; x <= 2; x++)
        {
            float w = exp(-0.5 * (x * x + y * y) / 2.0);
            color += SampleBackdrop(coord + float2(x, y) * radius) * w;
            weight += w;
        }
    }
    return color / max(weight, 0.0001);
}

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float2 frag = uv * TextureSize.xy;
    float2 fromCenter = frag - GlassCenter.xy;
    float radius = length(fromCenter);
    float2 dir = fromCenter / max(radius, 1.0);

    float inner = Radii.x;
    float outer = Radii.y;
    float thickness = max(outer - inner, 1.0);
    float distInner = radius - inner;
    float distOuter = outer - radius;
    float inversedSdf = min(distInner, distOuter) / thickness;

    float distFromCenter = 1.0 - saturate(inversedSdf / 0.30);
    float distortion = 1.0 - sqrt(max(0.0, 1.0 - distFromCenter * distFromCenter));
    distortion *= Material.y;

    float2 offset = distortion * dir * Material.x * thickness;
    float2 sampleCoord = frag - offset;

    float blurRadius = Material.w * (1.0 - distFromCenter * 0.5);
    float edge = smoothstep(0.0, 0.08, inversedSdf);
    float2 shift = dir * edge * Material.z;

    float4 glass;
    glass.r = BlurSample(sampleCoord - shift, blurRadius).r;
    glass.g = BlurSample(sampleCoord, blurRadius).g;
    glass.b = BlurSample(sampleCoord + shift, blurRadius).b;
    glass.a = 1.0;

    float luma = dot(glass.rgb, float3(0.2126, 0.7152, 0.0722));
    glass.rgb = lerp(luma.xxx, glass.rgb, Lighting.w);
    glass.rgb *= Lighting.z;
    glass.rgb = lerp(glass.rgb, Tint.rgb, Tint.a);

    float2 lightDir = normalize(Cursor.xy - GlassCenter.xy + 0.001);
    float rim = pow(saturate(1.0 - inversedSdf * 3.0), 2.0);
    float spec = pow(saturate(dot(dir, lightDir)), 28.0) * Lighting.x;
    glass.rgb += (rim * 0.22 + spec) * (0.55 + Lighting.y * 0.45);

    return glass;
}
