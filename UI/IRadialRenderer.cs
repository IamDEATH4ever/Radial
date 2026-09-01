using System;

namespace Radial.UI;

public readonly record struct RadialRenderState(int SelectedSector, int SegmentCount = 1, IReadOnlyList<string>? Labels = null);

public interface IRadialRenderer : IDisposable
{
    void Update(RadialRenderState state);
}
