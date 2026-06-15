using Ferbento.Core.Chips;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Sprites;

public sealed class SpriteDocument
{
    private ChipBus? _bus;
    private HardwareProfile _profile = HardwareProfile.Pico8();
    private int _currentSpriteIndex;
    private int _viewOriginSheetX;
    private int _viewOriginSheetY;
    private int _viewWidth = 8;
    private int _viewHeight = 8;

    public int TileSize => _bus?.Sprite.TileSize ?? _profile.Sprites.TileSize;
    public int SheetWidth => _bus?.Sprite.SheetWidth ?? _profile.Sprites.SheetWidth;
    public int SheetHeight => _bus?.Sprite.SheetHeight ?? _profile.Sprites.SheetHeight;
    public int SpritesPerRow => _bus?.Sprite.SpritesPerRow ?? (SheetWidth / TileSize);
    public int SpriteCount => _bus?.Sprite.SpriteCount ?? (SpritesPerRow * (SheetHeight / TileSize));
    public int PaletteCount => _bus?.Color.ColorCount ?? _profile.Palette.Colors;
    public bool UsesGfx2Bank => _bus?.Sprite.UsesGfx2Bank ?? false;
    public ColorChip? ColorChip => _bus?.Color;
    public int ViewWidth => _viewWidth;
    public int ViewHeight => _viewHeight;
    public (int sheetX, int sheetY) ViewOriginSheet => (_viewOriginSheetX, _viewOriginSheetY);

    public int CurrentSpriteIndex
    {
        get => _currentSpriteIndex;
        set => _currentSpriteIndex = Math.Clamp(value, 0, Math.Max(0, SpriteCount - 1));
    }

    public int GfxBank
    {
        get => UsesGfx2Bank && CurrentSpriteIndex >= 128 ? 1 : 0;
        set
        {
            if (!UsesGfx2Bank) return;
            var bankStart = value == 0 ? 0 : 128;
            CurrentSpriteIndex = Math.Clamp(CurrentSpriteIndex, bankStart, bankStart + 127);
            if (CurrentSpriteIndex < bankStart)
                CurrentSpriteIndex = bankStart;
        }
    }

    public (int sheetX, int sheetY) SpriteOrigin
    {
        get
        {
            var col = CurrentSpriteIndex % SpritesPerRow;
            var row = CurrentSpriteIndex / SpritesPerRow;
            return (col * TileSize, row * TileSize);
        }
    }

    public (int sheetX, int sheetY) SpriteOriginFor(int spriteIndex)
    {
        var col = spriteIndex % SpritesPerRow;
        var row = spriteIndex / SpritesPerRow;
        return (col * TileSize, row * TileSize);
    }

    public void SetProfile(HardwareProfile profile) => _profile = profile;

    public void SetChipBus(ChipBus bus) => _bus = bus;

    public void SetViewRegion(int sheetX, int sheetY, int width, int height)
    {
        var maxTiles = 4;
        var maxW = TileSize * maxTiles;
        var maxH = TileSize * maxTiles;
        _viewOriginSheetX = Math.Max(0, sheetX);
        _viewOriginSheetY = Math.Max(0, sheetY);
        _viewWidth = Math.Max(TileSize, Math.Min(width, maxW));
        _viewHeight = Math.Max(TileSize, Math.Min(height, maxH));
        if (_bus != null)
        {
            _viewWidth = Math.Max(TileSize, Math.Min(_viewWidth, SheetWidth - _viewOriginSheetX));
            _viewHeight = Math.Max(TileSize, Math.Min(_viewHeight, SheetHeight - _viewOriginSheetY));
        }
    }

    public void ResetViewToCurrentSprite()
    {
        var (ox, oy) = SpriteOrigin;
        SetViewRegion(ox, oy, TileSize, TileSize);
    }

    public byte[] ReadViewPixels() => ReadRegion(_viewOriginSheetX, _viewOriginSheetY, _viewWidth, _viewHeight);

    public void WriteViewPixels(byte[] data) =>
        WriteRegion(_viewOriginSheetX, _viewOriginSheetY, _viewWidth, _viewHeight, data);

    public int GetPixel(int sheetX, int sheetY)
    {
        if (_bus == null || sheetX < 0 || sheetY < 0 || sheetX >= SheetWidth || sheetY >= SheetHeight)
            return 0;
        return _bus.Sprite.SGet(sheetX, sheetY);
    }

    public void SetPixel(int sheetX, int sheetY, int color)
    {
        if (_bus == null || sheetX < 0 || sheetY < 0 || sheetX >= SheetWidth || sheetY >= SheetHeight)
            return;
        _bus.Sprite.SSet(sheetX, sheetY, color);
    }

    public int GetLocalPixel(int localX, int localY)
    {
        if (localX < 0 || localY < 0 || localX >= _viewWidth || localY >= _viewHeight) return 0;
        return GetPixel(_viewOriginSheetX + localX, _viewOriginSheetY + localY);
    }

    public void SetLocalPixel(int localX, int localY, int color)
    {
        if (_bus == null || localX < 0 || localY < 0 || localX >= _viewWidth || localY >= _viewHeight) return;
        SetPixel(_viewOriginSheetX + localX, _viewOriginSheetY + localY, color);
    }

    public byte[] GetSpritePixels(int spriteIndex)
    {
        var data = new byte[TileSize * TileSize];
        if (_bus == null) return data;
        var (ox, oy) = SpriteOriginFor(spriteIndex);
        for (var y = 0; y < TileSize; y++)
        for (var x = 0; x < TileSize; x++)
            data[y * TileSize + x] = (byte)GetPixel(ox + x, oy + y);
        return data;
    }

    public void SetSpritePixels(int spriteIndex, byte[] data)
    {
        if (_bus == null || data.Length < TileSize * TileSize) return;
        var (ox, oy) = SpriteOriginFor(spriteIndex);
        for (var y = 0; y < TileSize; y++)
        for (var x = 0; x < TileSize; x++)
            SetPixel(ox + x, oy + y, data[y * TileSize + x]);
    }

    public byte[] GetCurrentSpritePixels() => GetSpritePixels(CurrentSpriteIndex);

    public void SetCurrentSpritePixels(byte[] data) => SetSpritePixels(CurrentSpriteIndex, data);

    public int GetFlags(int spriteIndex) => _bus?.Sprite.FGet(spriteIndex) ?? 0;

    public int GetCurrentFlags() => GetFlags(CurrentSpriteIndex);

    public void SetFlags(int spriteIndex, int flags)
    {
        _bus?.Sprite.FSet(spriteIndex, -1, flags);
    }

    public void SetCurrentFlags(int flags) => SetFlags(CurrentSpriteIndex, flags);

    public int GetFlag(int spriteIndex, int bit) =>
        _bus?.Sprite.FGet(spriteIndex, bit & 7) ?? 0;

    public void SetFlag(int spriteIndex, int bit, bool value) =>
        _bus?.Sprite.FSet(spriteIndex, bit & 7, value ? 1 : 0);

    public byte[] ReadRegion(int sheetX, int sheetY, int width, int height)
    {
        var data = new byte[width * height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            data[y * width + x] = (byte)GetPixel(sheetX + x, sheetY + y);
        return data;
    }

    public void WriteRegion(int sheetX, int sheetY, int width, int height, byte[] data)
    {
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var idx = y * width + x;
            if (idx < data.Length)
                SetPixel(sheetX + x, sheetY + y, data[idx]);
        }
    }

    public byte[] ReadLocalRegion(int localX, int localY, int width, int height) =>
        ReadRegion(_viewOriginSheetX + localX, _viewOriginSheetY + localY, width, height);

    public void WriteLocalRegion(int localX, int localY, int width, int height, byte[] data) =>
        WriteRegion(_viewOriginSheetX + localX, _viewOriginSheetY + localY, width, height, data);

    public int FindNextFreeSpriteIndex(int startAfter = -1)
    {
        for (var i = startAfter + 1; i < SpriteCount; i++)
        {
            if (IsSpriteEmpty(i)) return i;
        }
        return -1;
    }

    public bool IsSpriteEmpty(int spriteIndex)
    {
        var pixels = GetSpritePixels(spriteIndex);
        return pixels.All(b => b == 0) && GetFlags(spriteIndex) == 0;
    }
}
