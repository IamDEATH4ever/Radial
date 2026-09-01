namespace Radial.UI;

public sealed record LiquidGlassSettings
{
    public static LiquidGlassSettings Default { get; } = new();

    // Capture a halo around the overlay so refraction can sample the real desktop.
    public int BackdropPadding { get; init; } = 48;
    public float BackdropBlurSigma { get; init; } = 4.6f;

    // Lensing (pixel offsets at the rim). These must stay visible, not decorative.
    public float RefractionOffset { get; init; } = 2.9f;
    public float Distortion { get; init; } = 0.6f;
    public float ChromaticAberration { get; init; } = 12f;
    public float ShaderBlurRadius { get; init; } = 1.15f;
    public float GlassBlurSigma { get; init; } = 2.4f;   // subtle frosted-glass blur on the surface

    public float Saturation { get; init; } = 1.08f;
    public float Brightness { get; init; } = 0.96f;
    public float TintStrength { get; init; } = 0.22f;
    public byte SurfaceTintAlpha { get; init; } = 42;
    public byte SelectedTintAlpha { get; init; } = 95;   // more vivid blue on selected segment
    public byte EdgeHighlightAlpha { get; init; } = 60;    // lighter, more translucent outline
    public byte InternalHighlightAlpha { get; init; } = 36;

    public byte CursorLightAlpha { get; init; } = 38;
    public float CursorLightRadius { get; init; } = 96f;
    public float SpecularIntensity { get; init; } = 0.60f;
    public float HoverScale { get; init; } = 1.022f;
    public float SelectionShadowSigma { get; init; } = 8f;
    public float SelectionShadowAlpha { get; init; } = 0.08f;  // much softer shadow
    public float SelectedRefractionBoost { get; init; } = 1.35f;

    public float OpenDurationSeconds { get; init; } = 0.22f;
    public float CloseDurationSeconds { get; init; } = 0.16f;
    public float SelectionResponse { get; init; } = 22f;
    public float CursorResponse { get; init; } = 24f;
    public float RippleExpansionPerSecond { get; init; } = 20f;
    public float RippleFadePerSecond { get; init; } = 7.5f;
    public float OpenStartScale { get; init; } = 0.94f;
    public float CloseEndScale { get; init; } = 0.97f;
}
