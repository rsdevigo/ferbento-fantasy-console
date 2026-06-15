using Ferbento.Editor.Tilemaps;

namespace Ferbento.Editor.Tests;

public class MapEditorViewportTests
{
    [Fact]
    public void PointerToMapTile_MatchesTileScreenOrigin()
    {
        var vp = MapEditorViewport.Compute(
            mapWidth: 128, mapHeight: 64, tileSize: 8, zoom: 16,
            panX: 0, panY: 0, panelWidth: 512, panelHeight: 384);

        var (mapX, mapY) = vp.PointerToMapTile(96, 64);
        var (screenX, screenY) = TileOrigin(vp, mapX, mapY);

        Assert.Equal(0, mapX);
        Assert.Equal(0, mapY);
        Assert.Equal(-vp.SubPixelOffsetX, screenX);
        Assert.Equal(-vp.SubPixelOffsetY, screenY);
    }

    [Fact]
    public void Compute_ClampsBitmapPixels()
    {
        var vp = MapEditorViewport.Compute(
            mapWidth: 128, mapHeight: 64, tileSize: 8, zoom: 1,
            panX: 0, panY: 0, panelWidth: 8000, panelHeight: 8000);

        Assert.True(vp.BitmapWidth * vp.BitmapHeight <= MapEditorViewport.MaxBitmapPixels);
    }

    [Fact]
    public void MapTileRenderColors_UsesCheckerboardForTransparentPixels()
    {
        var bg = MapTileRenderColors.Checkerboard(3, 4);
        var pixel = MapTileRenderColors.SpritePixel(0, 3, 4, null);
        Assert.Equal(bg, pixel);
    }

    private static (int x, int y) TileOrigin(MapEditorViewport vp, int mapX, int mapY) =>
        ((mapX - vp.StartMapX) * vp.CellPixels - vp.SubPixelOffsetX,
         (mapY - vp.StartMapY) * vp.CellPixels - vp.SubPixelOffsetY);
}
