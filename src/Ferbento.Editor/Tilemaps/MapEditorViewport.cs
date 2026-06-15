namespace Ferbento.Editor.Tilemaps;

/// <summary>
/// Shared map viewport math for tile hit-testing and rendering (pan + zoom aligned).
/// </summary>
public readonly struct MapEditorViewport
{
    public const int MaxBitmapPixels = 16_777_216; // 4096×4096

    public int StartMapX { get; }
    public int StartMapY { get; }
    public int TilesWide { get; }
    public int TilesHigh { get; }
    public int CellPixels { get; }
    public int BitmapWidth { get; }
    public int BitmapHeight { get; }
    public int SubPixelOffsetX { get; }
    public int SubPixelOffsetY { get; }

    public MapEditorViewport(
        int startMapX, int startMapY, int tilesWide, int tilesHigh,
        int cellPixels, int subPixelOffsetX, int subPixelOffsetY)
    {
        StartMapX = startMapX;
        StartMapY = startMapY;
        TilesWide = tilesWide;
        TilesHigh = tilesHigh;
        CellPixels = cellPixels;
        SubPixelOffsetX = subPixelOffsetX;
        SubPixelOffsetY = subPixelOffsetY;
        BitmapWidth = Math.Max(1, tilesWide * cellPixels);
        BitmapHeight = Math.Max(1, tilesHigh * cellPixels);
    }

    public static MapEditorViewport Compute(
        int mapWidth, int mapHeight, int tileSize, int zoom,
        int panX, int panY, double panelWidth, double panelHeight)
    {
        var cell = Math.Max(1, tileSize * zoom);
        var panelW = Math.Max(1, panelWidth);
        var panelH = Math.Max(1, panelHeight);

        var scrollMapX = -panX / (double)cell;
        var scrollMapY = -panY / (double)cell;
        var startMapX = Math.Clamp((int)Math.Floor(scrollMapX), 0, Math.Max(0, mapWidth - 1));
        var startMapY = Math.Clamp((int)Math.Floor(scrollMapY), 0, Math.Max(0, mapHeight - 1));
        var subPixelOffsetX = (int)Math.Round((scrollMapX - startMapX) * cell);
        var subPixelOffsetY = (int)Math.Round((scrollMapY - startMapY) * cell);

        var visibleTilesW = (int)Math.Ceiling(panelW / cell) + 1;
        var visibleTilesH = (int)Math.Ceiling(panelH / cell) + 1;

        var maxTilesW = Math.Max(1, mapWidth - startMapX);
        var maxTilesH = Math.Max(1, mapHeight - startMapY);
        var tilesWide = Math.Clamp(visibleTilesW, 1, maxTilesW);
        var tilesHigh = Math.Clamp(visibleTilesH, 1, maxTilesH);

        return new MapEditorViewport(
            startMapX, startMapY, tilesWide, tilesHigh, cell,
            subPixelOffsetX, subPixelOffsetY).ClampBitmapSize();
    }

    private MapEditorViewport ClampBitmapSize()
    {
        var tilesWide = TilesWide;
        var tilesHigh = TilesHigh;
        var cell = CellPixels;
        while (tilesWide * tilesHigh * cell * cell > MaxBitmapPixels)
        {
            if (tilesWide >= tilesHigh && tilesWide > 1)
                tilesWide--;
            else if (tilesHigh > 1)
                tilesHigh--;
            else
                break;
        }

        if (tilesWide == TilesWide && tilesHigh == TilesHigh)
            return this;

        return new MapEditorViewport(
            StartMapX, StartMapY, tilesWide, tilesHigh, cell,
            SubPixelOffsetX, SubPixelOffsetY);
    }

    public (int mapX, int mapY) PointerToMapTile(double pointerX, double pointerY)
    {
        if (CellPixels <= 0) return (0, 0);

        var mapX = StartMapX + (int)Math.Floor((pointerX + SubPixelOffsetX) / CellPixels);
        var mapY = StartMapY + (int)Math.Floor((pointerY + SubPixelOffsetY) / CellPixels);
        return (mapX, mapY);
    }

    public (int viewTileX, int viewTileY, int pixelX, int pixelY) MapTileToView(int mapX, int mapY)
    {
        var viewTileX = mapX - StartMapX;
        var viewTileY = mapY - StartMapY;
        var pixelX = viewTileX * CellPixels - SubPixelOffsetX;
        var pixelY = viewTileY * CellPixels - SubPixelOffsetY;
        return (viewTileX, viewTileY, pixelX, pixelY);
    }

    public bool IsOnBitmap(int mapX, int mapY)
    {
        var viewTileX = mapX - StartMapX;
        var viewTileY = mapY - StartMapY;
        return viewTileX >= 0 && viewTileY >= 0 && viewTileX < TilesWide && viewTileY < TilesHigh;
    }
}
