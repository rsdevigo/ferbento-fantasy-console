using Ferbento.Core.Memory;
using Ferbento.Profiles.Models;

namespace Ferbento.Core.Chips;

internal static class ChipMemoryLayout
{
    public static bool UsesSharedMapRows(HardwareProfile profile) =>
        profile.Memory.MapLayout is { MapPrimaryRows: > 0 } layout
        && profile.Map.Height > layout.MapPrimaryRows;

    public static bool UsesGfx2Bank(HardwareProfile profile) =>
        profile.Memory.MapLayout is not null && profile.Sprites.SheetHeight > 64;

    public static int MapAddress(HardwareProfile profile, int x, int y, int mapWidth)
    {
        var layout = profile.Memory.MapLayout;
        if (layout is { MapPrimaryRows: > 0 } && profile.Map.Height > layout.MapPrimaryRows)
        {
            return y < layout.MapPrimaryRows
                ? layout.MapDataStart + y * mapWidth + x
                : layout.SharedRegionStart + (y - layout.MapPrimaryRows) * mapWidth + x;
        }

        // Profiles without shared layout: linear map bytes from RAM base.
        return y * mapWidth + x;
    }

    public static int GfxSecondaryStart(HardwareProfile profile) =>
        profile.Memory.MapLayout?.SharedRegionStart ?? MemoryMap.Gfx2Start;
}
