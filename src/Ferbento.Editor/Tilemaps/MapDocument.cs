using Ferbento.Core.Chips;
using Ferbento.Profiles.Models;
using Ferbento.Profiles.Validation;

namespace Ferbento.Editor.Tilemaps;

public sealed class MapDocument
{
    private ChipBus? _bus;
    private HardwareProfile _profile = HardwareProfile.Pico8();
    private readonly HashSet<(int x, int y)> _placedCells = [];

    public int MapWidth => _bus?.Tilemap.Width ?? _profile.Map.Width;
    public int MapHeight => _bus?.Tilemap.Height ?? _profile.Map.Height;
    public int TileSize => _bus?.Sprite.TileSize ?? _profile.Map.TileSize;
    public int SheetWidth => _bus?.Sprite.SheetWidth ?? _profile.Sprites.SheetWidth;
    public int SheetHeight => _bus?.Sprite.SheetHeight ?? _profile.Sprites.SheetHeight;
    public int SpritesPerRow => _bus?.Sprite.SpritesPerRow ?? (SheetWidth / TileSize);
    public int SpriteCount => _bus?.Sprite.SpriteCount ?? (SpritesPerRow * (SheetHeight / TileSize));
    public bool UsesSharedMapRows => _bus?.Tilemap.UsesSharedMapRows ?? false;
    public int MapPrimaryRows => _bus?.Tilemap.MapPrimaryRows ?? MapHeight;
    public ColorChip? ColorChip => _bus?.Color;
    public ProfileFeatureGate Features => new(_profile);

    public void SetProfile(HardwareProfile profile) => _profile = profile;

    public void SetChipBus(ChipBus bus)
    {
        _bus = bus;
        _placedCells.Clear();
    }

    public bool IsCellPlaced(int mapX, int mapY) => _placedCells.Contains((mapX, mapY));

    public byte[] ReadRegion(int x, int y, int w, int h)
    {
        var data = new byte[w * h];
        if (_bus == null) return data;

        for (var row = 0; row < h; row++)
        for (var col = 0; col < w; col++)
        {
            var mx = x + col;
            var my = y + row;
            data[row * w + col] = mx >= 0 && my >= 0 && mx < MapWidth && my < MapHeight
                ? (byte)_bus.Tilemap.MGet(mx, my)
                : (byte)0;
        }
        return data;
    }

    public void WriteRegion(int x, int y, int w, int h, byte[] data, int currentLayer, bool setLayerFlags = true)
    {
        if (_bus == null) return;

        for (var row = 0; row < h; row++)
        for (var col = 0; col < w; col++)
        {
            var idx = row * w + col;
            if (idx >= data.Length) continue;
            var mx = x + col;
            var my = y + row;
            if (mx < 0 || my < 0 || mx >= MapWidth || my >= MapHeight) continue;
            _bus.Tilemap.MSet(mx, my, data[idx]);
            if (setLayerFlags && data[idx] != 0)
                SetLayerFlag(data[idx], currentLayer, true);
        }
    }

    public void SyncPlacedFromTiles(int x, int y, int w, int h, byte[] data)
    {
        for (var row = 0; row < h; row++)
        for (var col = 0; col < w; col++)
        {
            var idx = row * w + col;
            if (idx >= data.Length) continue;
            var mx = x + col;
            var my = y + row;
            if (mx < 0 || my < 0 || mx >= MapWidth || my >= MapHeight) continue;
            if (data[idx] == 0)
                _placedCells.Remove((mx, my));
            else
                _placedCells.Add((mx, my));
        }
    }

    public bool[] ReadPlacedRegion(int x, int y, int w, int h)
    {
        var placed = new bool[w * h];
        for (var row = 0; row < h; row++)
        for (var col = 0; col < w; col++)
        {
            var mx = x + col;
            var my = y + row;
            placed[row * w + col] = mx >= 0 && my >= 0 && mx < MapWidth && my < MapHeight && IsCellPlaced(mx, my);
        }
        return placed;
    }

    public void ApplyPlacedRegion(int x, int y, int w, int h, bool[] placed)
    {
        for (var row = 0; row < h; row++)
        for (var col = 0; col < w; col++)
        {
            var idx = row * w + col;
            if (idx >= placed.Length) continue;
            var mx = x + col;
            var my = y + row;
            if (mx < 0 || my < 0 || mx >= MapWidth || my >= MapHeight) continue;
            if (placed[idx])
                _placedCells.Add((mx, my));
            else
                _placedCells.Remove((mx, my));
        }
    }

    public int GetTile(int mapX, int mapY)
    {
        if (_bus == null || mapX < 0 || mapY < 0 || mapX >= MapWidth || mapY >= MapHeight) return 0;
        return _bus.Tilemap.MGet(mapX, mapY);
    }

    public void SetTile(int mapX, int mapY, int tileId, int currentLayer, bool setLayerFlag = true)
    {
        if (_bus == null || mapX < 0 || mapY < 0 || mapX >= MapWidth || mapY >= MapHeight) return;
        _bus.Tilemap.MSet(mapX, mapY, tileId);
        _placedCells.Add((mapX, mapY));
        if (setLayerFlag && tileId != 0)
            SetLayerFlag(tileId, currentLayer, true);
    }

    public void EraseTile(int mapX, int mapY)
    {
        if (_bus == null || mapX < 0 || mapY < 0 || mapX >= MapWidth || mapY >= MapHeight) return;
        _bus.Tilemap.MSet(mapX, mapY, 0);
        _placedCells.Remove((mapX, mapY));
    }

    public void SetLayerFlag(int tileId, int layer, bool enabled)
    {
        if (_bus == null) return;
        var current = _bus.Sprite.FGet(tileId);
        var mask = 1 << (layer & 7);
        var next = enabled ? current | mask : current & ~mask;
        _bus.Sprite.FSet(tileId, -1, next);
    }

    public int GetSpritePixel(int sheetX, int sheetY)
    {
        if (_bus == null || sheetX < 0 || sheetY < 0 || sheetX >= SheetWidth || sheetY >= SheetHeight) return 0;
        return _bus.Sprite.SGet(sheetX, sheetY);
    }
}
