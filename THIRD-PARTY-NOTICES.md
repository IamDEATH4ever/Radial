# Third-Party Notices

## WPF Liquid Glass Effect

Backdrop-capture technique (hide the overlay, BitBlt the desktop, restore the overlay)
and the overall glass-layer approach were adapted from:

- https://github.com/dragosniamtu/WPF-Liquid-Glass-Effect
- License: MIT (see `Licenses/WPF-Liquid-Glass-Effect-LICENSE.txt`)
- Copyright (c) 2026 XAML Templates contributors

This project does not copy the demo window template. The refraction/distortion
math is implemented as an SkSL runtime shader so it can be clipped to Radial's
existing sector geometry.

## Shader concept

Lens distortion, edge chromatic aberration, and distance-based blur follow the
public explanation in:

- AmirHossein Aghajari, "Liquid Glass: iOS Effect Explanation", Medium, 24 Nov 2025
- https://medium.com/@aghajari/liquid-glass-ios-effect-explanation-dabadd6414ae

The same model is used by the WPF sample above. The HLSL in `UI/Shaders/LiquidGlass.hlsl`
documents that algorithm; Radial executes the equivalent SkSL at runtime.

## Apple

Apple and Liquid Glass are trademarks or design terms associated with Apple Inc.
This project is not affiliated with, sponsored by, or endorsed by Apple Inc.
