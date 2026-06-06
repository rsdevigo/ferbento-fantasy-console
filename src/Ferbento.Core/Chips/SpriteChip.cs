using Ferbento.Core.Exceptions;
using Ferbento.Core.Memory;
using Ferbento.Profiles.Models;

namespace Ferbento.Core.Chips;

public sealed class SpriteChip : IChip
{
    public string Name => "Sprite";

    private ChipBus _bus = null!;
    private int _sheetWidth = 128;
    private int _sheetHeight = 128;
    private int _tileSize = 8;

    public void Configure(HardwareProfile profile)
    {
        _sheetWidth = profile.Sprites.SheetWidth;
        _sheetHeight = profile.Sprites.SheetHeight;
        _tileSize = profile.Sprites.TileSize;
    }

    public void Initialize(ChipBus bus) => _bus = bus;

    public void Reset() { }

    public void Tick() { }

    public int SGet(int x, int y)
    {
        var addr = GfxAddress(x, y);
        var lo = _bus.Memory.Memory.Peek(addr);
        var hi = _bus.Memory.Memory.Peek(addr + 1);
        return (x & 1) == 0 ? lo & 0xF : (hi >> 4) & 0xF;
    }

    public void SSet(int x, int y, int color)
    {
        var addr = GfxAddress(x, y);
        color &= 0xF;
        if ((x & 1) == 0)
            _bus.Memory.Memory.Poke(addr, (byte)((_bus.Memory.Memory.Peek(addr) & 0xF0) | color));
        else
            _bus.Memory.Memory.Poke(addr + 1, (byte)((_bus.Memory.Memory.Peek(addr + 1) & 0x0F) | (color << 4)));
    }

    public int FGet(int sprite, int flag = -1)
    {
        var addr = MemoryMap.GfxPropsStart + sprite;
        var flags = _bus.Memory.Memory.Peek(addr);
        if (flag < 0) return flags;
        return (flags >> (flag & 7)) & 1;
    }

    public void FSet(int sprite, int flag, int value)
    {
        var addr = MemoryMap.GfxPropsStart + sprite;
        var flags = _bus.Memory.Memory.Peek(addr);
        if (flag < 0)
        {
            _bus.Memory.Memory.Poke(addr, (byte)value);
            return;
        }
        var mask = 1 << (flag & 7);
        flags = value != 0 ? (byte)(flags | mask) : (byte)(flags & ~mask);
        _bus.Memory.Memory.Poke(addr, flags);
    }

    private int GfxAddress(int x, int y)
    {
        if (x < 0 || y < 0 || x >= _sheetWidth || y >= _sheetHeight)
            throw new HardwareLimitException($"Sprite coordinate ({x},{y}) out of bounds.");
        return MemoryMap.GfxStart + (y * _sheetWidth + x) / 2;
    }
}
