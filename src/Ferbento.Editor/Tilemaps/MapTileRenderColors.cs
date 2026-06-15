using Ferbento.Core.Chips;

namespace Ferbento.Editor.Tilemaps;

public static class MapTileRenderColors
{
    public static uint Checkerboard(int mapX, int mapY) =>
        ((mapX + mapY) & 1) == 0 ? 0xFF1A1A1Au : 0xFF222222u;

    public static uint SpritePixel(int colorIndex, int mapX, int mapY, ColorChip? colorChip)
    {
        var transparent = colorChip?.TransparentIndex ?? 0;
        if (colorIndex == transparent)
            return Checkerboard(mapX, mapY);

        return Pico8Colors.ToBgra(colorIndex, colorChip, Checkerboard(mapX, mapY));
    }
}
