using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ferbento.Core.Chips;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.ViewModels;

public sealed class TilemapEditorViewModel : INotifyPropertyChanged
{
    public UndoRedoStack UndoRedo { get; } = new(256);

    private ChipBus? _bus;
    private HardwareProfile _profile = HardwareProfile.Pico8();
    private byte[]? _clipboard;
    private int _clipboardW;
    private int _clipboardH;

    public event Action? Edited;

    public bool CanUndo => UndoRedo.UndoCount > 0;
    public bool CanRedo => UndoRedo.RedoCount > 0;

    private int _currentLayer;
    public int CurrentLayer
    {
        get => _currentLayer;
        set { _currentLayer = Math.Clamp(value, 0, 7); OnPropertyChanged(); }
    }

    private string _selectedTool = "pencil";
    public string SelectedTool
    {
        get => _selectedTool;
        set { _selectedTool = value; OnPropertyChanged(); }
    }

    private int _selectedTile;
    public int SelectedTile
    {
        get => _selectedTile;
        set { _selectedTile = Math.Clamp(value, 0, Math.Max(0, SpriteCount - 1)); OnPropertyChanged(); }
    }

    public int MapWidth => _bus?.Tilemap.Width ?? _profile.Map.Width;
    public int MapHeight => _bus?.Tilemap.Height ?? _profile.Map.Height;
    public int TileSize => _bus?.Sprite.TileSize ?? _profile.Sprites.TileSize;
    public int SheetWidth => _bus?.Sprite.SheetWidth ?? _profile.Sprites.SheetWidth;
    public int SheetHeight => _bus?.Sprite.SheetHeight ?? _profile.Sprites.SheetHeight;
    public int SpritesPerRow => _bus?.Sprite.SpritesPerRow ?? (SheetWidth / TileSize);
    public int SpriteCount => _bus?.Sprite.SpriteCount ?? (SpritesPerRow * (SheetHeight / TileSize));

    public TilemapEditorViewModel()
    {
        UndoRedo.StateChanged += () =>
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        };
    }

    public void SetProfile(HardwareProfile profile)
    {
        _profile = profile;
        OnPropertyChanged(nameof(MapWidth));
        OnPropertyChanged(nameof(MapHeight));
        OnPropertyChanged(nameof(TileSize));
        OnPropertyChanged(nameof(SheetWidth));
        OnPropertyChanged(nameof(SheetHeight));
        OnPropertyChanged(nameof(SpritesPerRow));
        OnPropertyChanged(nameof(SpriteCount));
        SelectedTile = Math.Min(SelectedTile, Math.Max(0, SpriteCount - 1));
    }

    public void SetChipBus(ChipBus bus)
    {
        _bus = bus;
        OnPropertyChanged(nameof(MapWidth));
        OnPropertyChanged(nameof(MapHeight));
        OnPropertyChanged(nameof(TileSize));
        OnPropertyChanged(nameof(SheetWidth));
        OnPropertyChanged(nameof(SheetHeight));
        OnPropertyChanged(nameof(SpritesPerRow));
        OnPropertyChanged(nameof(SpriteCount));
    }

    public ColorChip? ColorChip => _bus?.Color;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public byte[] ReadMapRegion(int x, int y, int w, int h)
    {
        var data = new byte[w * h];
        if (_bus == null) return data;

        for (var row = 0; row < h; row++)
        for (var col = 0; col < w; col++)
            data[row * w + col] = (byte)_bus.Tilemap.MGet(x + col, y + row);
        return data;
    }

    public void WriteMapRegion(int x, int y, int w, int h, byte[] data)
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
            SetLayerFlag(data[idx], CurrentLayer, true);
        }
        Edited?.Invoke();
    }

    public void PushUndoRegion(int x, int y, int w, int h, string name)
    {
        var data = ReadMapRegion(x, y, w, h);
        UndoRedo.Push(name, data, 0, data.Length);
    }

    public void UndoRegion(int x, int y, int w, int h)
    {
        var current = ReadMapRegion(x, y, w, h);
        var entry = UndoRedo.Undo(current, 0, current.Length);
        if (entry != null) WriteMapRegion(x, y, w, h, entry.Data);
    }

    public void RedoRegion(int x, int y, int w, int h)
    {
        var current = ReadMapRegion(x, y, w, h);
        var entry = UndoRedo.Redo(current, 0, current.Length);
        if (entry != null) WriteMapRegion(x, y, w, h, entry.Data);
    }

    public void PaintTile(int mapX, int mapY)
    {
        if (_bus == null) return;
        if (mapX < 0 || mapY < 0 || mapX >= MapWidth || mapY >= MapHeight) return;
        _bus.Tilemap.MSet(mapX, mapY, SelectedTile);
        SetLayerFlag(SelectedTile, CurrentLayer, true);
        Edited?.Invoke();
    }

    public void FloodFill(int mapX, int mapY)
    {
        if (_bus == null) return;
        if (mapX < 0 || mapY < 0 || mapX >= MapWidth || mapY >= MapHeight) return;

        var target = _bus.Tilemap.MGet(mapX, mapY);
        if (target == SelectedTile) return;

        PushUndoRegion(0, 0, MapWidth, MapHeight, "fill");
        var stack = new Stack<(int x, int y)>();
        stack.Push((mapX, mapY));

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            if (x < 0 || y < 0 || x >= MapWidth || y >= MapHeight) continue;
            if (_bus.Tilemap.MGet(x, y) != target) continue;
            _bus.Tilemap.MSet(x, y, SelectedTile);
            SetLayerFlag(SelectedTile, CurrentLayer, true);
            stack.Push((x + 1, y));
            stack.Push((x - 1, y));
            stack.Push((x, y + 1));
            stack.Push((x, y - 1));
        }
        Edited?.Invoke();
    }

    public void FillRect(int x0, int y0, int x1, int y1)
    {
        var left = Math.Min(x0, x1);
        var right = Math.Max(x0, x1);
        var top = Math.Min(y0, y1);
        var bottom = Math.Max(y0, y1);
        PushUndoRegion(left, top, right - left + 1, bottom - top + 1, "rect");

        for (var y = top; y <= bottom; y++)
        for (var x = left; x <= right; x++)
            PaintTile(x, y);
    }

    public void CopySelection(int x, int y, int w, int h)
    {
        _clipboard = ReadMapRegion(x, y, w, h);
        _clipboardW = w;
        _clipboardH = h;
    }

    public void PasteSelection(int x, int y)
    {
        if (_clipboard == null || _clipboardW <= 0 || _clipboardH <= 0) return;
        PushUndoRegion(x, y, _clipboardW, _clipboardH, "paste");
        WriteMapRegion(x, y, _clipboardW, _clipboardH, _clipboard);
    }

    public bool HasClipboard => _clipboard != null;

    private void SetLayerFlag(int tileId, int layer, bool enabled)
    {
        if (_bus == null) return;
        var current = _bus.Sprite.FGet(tileId);
        var mask = 1 << (layer & 7);
        var next = enabled ? current | mask : current & ~mask;
        _bus.Sprite.FSet(tileId, -1, next);
    }
}
