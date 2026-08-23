using WpfPoint = System.Windows.Point;

namespace Radial.Core;

public sealed class RadialMenu
{
    public const int SectorCount = 8;
    public WpfPoint Center { get; }
    public int SelectedSector { get; private set; } = 0;

    public RadialMenu(WpfPoint center) => Center = center;

    public int UpdateSelection(WpfPoint cursor)
    {
        var dx = cursor.X - Center.X;
        var dy = cursor.Y - Center.Y;
        if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1) return SelectedSector;

        // WPF's Y axis points down; atan2 is still normalized into clockwise screen angle.
        var angle = Math.Atan2(dy, dx) * 180 / Math.PI;
        angle = (angle + 360 + 22.5) % 360;
        SelectedSector = (int)(angle / 45);
        return SelectedSector;
    }
}
