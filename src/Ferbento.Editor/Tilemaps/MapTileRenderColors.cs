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

    public static bool SpriteTileIsEmpty(ChipBus bus, int tileId, int tileSize, int spritesPerRow, int transparentIndex)
    {
        var col = tileId % spritesPerRow;
        var row = tileId / spritesPerRow;
        var sx = col * tileSize;
        var sy = row * tileSize;
        for (var py = 0; py < tileSize; py++)
        for (var px = 0; px < tileSize; px++)
        {
            if (bus.Sprite.SGet(sx + px, sy + py) != transparentIndex)
                return false;
        }
        return true;
    }
}
