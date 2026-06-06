using Ferbento.Core.Exceptions;
using Ferbento.Core.Memory;
using Ferbento.Profiles.Models;

namespace Ferbento.Core.Chips;

public sealed class TilemapChip : IChip
{
    public string Name => "Tilemap";

    private ChipBus _bus = null!;
    private int _width = 128;
    private int _height = 64;

    public int Width => _width;
    public int Height => _height;

    public void Configure(HardwareProfile profile)
    {
        _width = profile.Map.Width;
        _height = profile.Map.Height;
    }

    public void Initialize(ChipBus bus) => _bus = bus;

    public void Reset() { }

    public void Tick() { }

    public int MGet(int x, int y)
    {
        ValidateCoord(x, y);
        return _bus.Memory.Memory.Peek(MemoryMap.MapStart + y * _width + x);
    }

    public void MSet(int x, int y, int value)
    {
        ValidateCoord(x, y);
        _bus.Memory.Memory.Poke(MemoryMap.MapStart + y * _width + x, (byte)(value & 0xFF));
    }

    private void ValidateCoord(int x, int y)
    {
        if (x < 0 || y < 0 || x >= _width || y >= _height)
            throw new HardwareLimitException($"Map coordinate ({x},{y}) out of bounds.");
    }
}
