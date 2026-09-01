using WpfPoint = System.Windows.Point;

namespace Radial.Core;

public sealed class RadialMenu
{
    public WpfPoint Center { get; }
    public int ItemCount { get; private set; }
    public int SelectedSector { get; private set; } = -1;

    public RadialMenu(WpfPoint center, int itemCount = 1) { Center = center; ItemCount = Math.Clamp(itemCount, 0, 12); SelectedSector = ItemCount == 0 ? -1 : 0; }
    public void SetItemCount(int count) { ItemCount = Math.Clamp(count, 0, 12); SelectedSector = ItemCount == 0 ? -1 : Math.Clamp(SelectedSector, 0, ItemCount - 1); }

    public int UpdateSelection(WpfPoint cursor)
    {
        var dx = cursor.X - Center.X;
        var dy = cursor.Y - Center.Y;
        if (ItemCount == 0 || (Math.Abs(dx) < 1 && Math.Abs(dy) < 1)) return SelectedSector;

        // WPF's Y axis points down; atan2 is still normalized into clockwise screen angle.
        var angle = Math.Atan2(dy, dx) * 180 / Math.PI;
        angle = (angle + 360 + (180.0 / ItemCount)) % 360;
        SelectedSector = Math.Min(ItemCount - 1, (int)(angle / (360.0 / ItemCount)));
        return SelectedSector;
    }
}
