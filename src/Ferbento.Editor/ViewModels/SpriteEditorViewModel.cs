using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ferbento.Core.Chips;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.ViewModels;

public sealed class SpriteEditorViewModel : INotifyPropertyChanged
{
    public UndoRedoStack UndoRedo { get; } = new(256);

    private ChipBus? _bus;
    private HardwareProfile _profile = HardwareProfile.Pico8();
    private byte[]? _clipboard;
    private byte[]? _flagsClipboard;

    public event Action? Edited;

    public bool CanUndo => UndoRedo.UndoCount > 0;
    public bool CanRedo => UndoRedo.RedoCount > 0;

    public int TileSize => _bus?.Sprite.TileSize ?? _profile.Sprites.TileSize;
    public int SheetWidth => _bus?.Sprite.SheetWidth ?? _profile.Sprites.SheetWidth;
    public int SheetHeight => _bus?.Sprite.SheetHeight ?? _profile.Sprites.SheetHeight;
    public int SpritesPerRow => _bus?.Sprite.SpritesPerRow ?? (SheetWidth / TileSize);
    public int SpriteCount => _bus?.Sprite.SpriteCount ?? (SpritesPerRow * (SheetHeight / TileSize));
    public int PaletteCount => _bus?.Color.ColorCount ?? _profile.Palette.Colors;
    public bool UsesGfx2Bank => _bus?.Sprite.UsesGfx2Bank ?? false;

    private int _selectedColor;
    public int SelectedColor
    {
        get => _selectedColor;
        set
        {
            var max = Math.Max(0, PaletteCount - 1);
            _selectedColor = Math.Clamp(value, 0, max);
            OnPropertyChanged();
        }
    }

    private int _gfxBank;
    public int GfxBank
    {
        get => _gfxBank;
        private set
        {
            if (_gfxBank == value) return;
            _gfxBank = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SpriteLabel));
        }
    }

    private string _selectedTool = "pencil";
    public string SelectedTool
    {
        get => _selectedTool;
        set { _selectedTool = value; OnPropertyChanged(); }
    }

    private int _currentSpriteIndex;
    public int CurrentSpriteIndex
    {
        get => _currentSpriteIndex;
        set
        {
            var max = Math.Max(0, SpriteCount - 1);
            var clamped = Math.Clamp(value, 0, max);
            if (_currentSpriteIndex == clamped) return;
            _currentSpriteIndex = clamped;
            if (UsesGfx2Bank)
                GfxBank = clamped >= 128 ? 1 : 0;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SpriteLabel));
            NotifyFlagsChanged();
        }
    }

    public string SpriteLabel =>
        UsesGfx2Bank && GfxBank == 1
            ? $"Sprite #{CurrentSpriteIndex} (GFX2)"
            : $"Sprite #{CurrentSpriteIndex}";

    public void SwitchGfxBank(int bank)
    {
        if (!UsesGfx2Bank) return;
        GfxBank = bank == 0 ? 0 : 1;
        var bankStart = GfxBank * 128;
        CurrentSpriteIndex = Math.Clamp(CurrentSpriteIndex, bankStart, bankStart + 127);
        if (CurrentSpriteIndex < bankStart)
            CurrentSpriteIndex = bankStart;
    }

    public SpriteEditorViewModel()
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
        OnPropertyChanged(nameof(TileSize));
        OnPropertyChanged(nameof(SheetWidth));
        OnPropertyChanged(nameof(SheetHeight));
        OnPropertyChanged(nameof(SpritesPerRow));
        OnPropertyChanged(nameof(SpriteCount));
        OnPropertyChanged(nameof(PaletteCount));
        OnPropertyChanged(nameof(UsesGfx2Bank));
        CurrentSpriteIndex = Math.Min(CurrentSpriteIndex, Math.Max(0, SpriteCount - 1));
    }

    public void SetChipBus(ChipBus bus) => _bus = bus;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public (int sheetX, int sheetY) SpriteOrigin
    {
        get
        {
            var col = CurrentSpriteIndex % SpritesPerRow;
            var row = CurrentSpriteIndex / SpritesPerRow;
            return (col * TileSize, row * TileSize);
        }
    }

    private int PixelCount => TileSize * TileSize;

    public byte[] ReadCurrentSprite()
    {
        var data = new byte[PixelCount];
        if (_bus == null) return data;

        var (ox, oy) = SpriteOrigin;
        for (var y = 0; y < TileSize; y++)
        for (var x = 0; x < TileSize; x++)
            data[y * TileSize + x] = (byte)_bus.Sprite.SGet(ox + x, oy + y);
        return data;
    }

    public void WriteCurrentSprite(byte[] data)
    {
        if (_bus == null || data.Length < PixelCount) return;

        var (ox, oy) = SpriteOrigin;
        for (var y = 0; y < TileSize; y++)
        for (var x = 0; x < TileSize; x++)
            _bus.Sprite.SSet(ox + x, oy + y, data[y * TileSize + x]);
        Edited?.Invoke();
    }

    public int GetFlag(int bit) =>
        _bus?.Sprite.FGet(CurrentSpriteIndex, bit & 7) ?? 0;

    public int GetAllFlags() =>
        _bus?.Sprite.FGet(CurrentSpriteIndex) ?? 0;

    public void SetFlag(int bit, bool value)
    {
        if (_bus == null) return;
        _bus.Sprite.FSet(CurrentSpriteIndex, bit & 7, value ? 1 : 0);
        NotifyFlagsChanged();
        Edited?.Invoke();
    }

    public void ToggleFlag(int bit)
    {
        if (_bus == null) return;
        var current = GetFlag(bit);
        SetFlag(bit, current == 0);
    }

    private void NotifyFlagsChanged()
    {
        for (var i = 0; i < 8; i++)
            OnPropertyChanged($"Flag{i}");
        OnPropertyChanged(nameof(AllFlags));
    }

    public int AllFlags => GetAllFlags();

    public void PushUndo(string name)
    {
        var data = ReadCurrentSprite();
        var flags = (byte)GetAllFlags();
        var combined = new byte[data.Length + 1];
        Array.Copy(data, combined, data.Length);
        combined[^1] = flags;
        UndoRedo.Push(name, combined, 0, combined.Length);
    }

    public void Undo()
    {
        var data = ReadCurrentSprite();
        var flags = (byte)GetAllFlags();
        var combined = new byte[data.Length + 1];
        Array.Copy(data, combined, data.Length);
        combined[^1] = flags;
        var entry = UndoRedo.Undo(combined, 0, combined.Length);
        if (entry == null) return;
        WriteCurrentSprite(entry.Data);
        if (entry.Data.Length > data.Length)
            _bus?.Sprite.FSet(CurrentSpriteIndex, -1, entry.Data[^1]);
        NotifyFlagsChanged();
    }

    public void Redo()
    {
        var data = ReadCurrentSprite();
        var flags = (byte)GetAllFlags();
        var combined = new byte[data.Length + 1];
        Array.Copy(data, combined, data.Length);
        combined[^1] = flags;
        var entry = UndoRedo.Redo(combined, 0, combined.Length);
        if (entry == null) return;
        WriteCurrentSprite(entry.Data);
        if (entry.Data.Length > data.Length)
            _bus?.Sprite.FSet(CurrentSpriteIndex, -1, entry.Data[^1]);
        NotifyFlagsChanged();
    }

    public void SetPixel(int localX, int localY, int color)
    {
        if (_bus == null || localX < 0 || localY < 0 || localX >= TileSize || localY >= TileSize) return;
        var (ox, oy) = SpriteOrigin;
        _bus.Sprite.SSet(ox + localX, oy + localY, color);
        Edited?.Invoke();
    }

    public int GetPixel(int localX, int localY)
    {
        if (_bus == null || localX < 0 || localY < 0 || localX >= TileSize || localY >= TileSize) return 0;
        var (ox, oy) = SpriteOrigin;
        return _bus.Sprite.SGet(ox + localX, oy + localY);
    }

    public void FloodFill(int localX, int localY, int newColor)
    {
        if (_bus == null) return;

        var target = GetPixel(localX, localY);
        var maxColor = Math.Max(0, PaletteCount - 1);
        newColor = Math.Clamp(newColor, 0, maxColor);
        if (target == newColor) return;

        PushUndo("fill");
        var stack = new Stack<(int x, int y)>();
        stack.Push((localX, localY));

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            if (x < 0 || x >= TileSize || y < 0 || y >= TileSize) continue;
            if (GetPixel(x, y) != target) continue;
            SetPixel(x, y, newColor);
            stack.Push((x + 1, y));
            stack.Push((x - 1, y));
            stack.Push((x, y + 1));
            stack.Push((x, y - 1));
        }
    }

    public void FlipHorizontal()
    {
        PushUndo("flip-h");
        var data = ReadCurrentSprite();
        for (var y = 0; y < TileSize; y++)
        for (var x = 0; x < TileSize / 2; x++)
            (data[y * TileSize + x], data[y * TileSize + (TileSize - 1 - x)]) =
                (data[y * TileSize + (TileSize - 1 - x)], data[y * TileSize + x]);
        WriteCurrentSprite(data);
    }

    public void FlipVertical()
    {
        PushUndo("flip-v");
        var data = ReadCurrentSprite();
        for (var y = 0; y < TileSize / 2; y++)
        for (var x = 0; x < TileSize; x++)
            (data[y * TileSize + x], data[(TileSize - 1 - y) * TileSize + x]) =
                (data[(TileSize - 1 - y) * TileSize + x], data[y * TileSize + x]);
        WriteCurrentSprite(data);
    }

    public void Rotate90()
    {
        PushUndo("rotate");
        var src = ReadCurrentSprite();
        var dst = new byte[PixelCount];
        for (var y = 0; y < TileSize; y++)
        for (var x = 0; x < TileSize; x++)
            dst[x * TileSize + (TileSize - 1 - y)] = src[y * TileSize + x];
        WriteCurrentSprite(dst);
    }

    public void CopySprite()
    {
        _clipboard = ReadCurrentSprite();
        _flagsClipboard = [(byte)GetAllFlags()];
    }

    public void PasteSprite()
    {
        if (_clipboard == null) return;
        PushUndo("paste");
        WriteCurrentSprite(_clipboard);
        if (_flagsClipboard != null)
            _bus?.Sprite.FSet(CurrentSpriteIndex, -1, _flagsClipboard[0]);
        NotifyFlagsChanged();
    }

    public void FillSprite(int color)
    {
        PushUndo("fill-all");
        var data = new byte[PixelCount];
        var maxColor = Math.Max(0, PaletteCount - 1);
        Array.Fill(data, (byte)Math.Clamp(color, 0, maxColor));
        WriteCurrentSprite(data);
    }

    public int ReadSheetPixel(int sheetX, int sheetY)
    {
        if (_bus == null) return 0;
        return _bus.Sprite.SGet(sheetX, sheetY);
    }

    public ColorChip? ColorChip => _bus?.Color;
}
