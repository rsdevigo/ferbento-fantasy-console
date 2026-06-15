using Ferbento.Core.Exceptions;
using Ferbento.Profiles.Models;

namespace Ferbento.Core.Chips;

public sealed class TilemapChip : IChip
{
    public string Name => "Tilemap";

    private ChipBus _bus = null!;
    private HardwareProfile _profile = HardwareProfile.Pico8();
    private int _width = 128;
    private int _height = 64;
    private int _mapPrimaryRows;
    private bool _usesSharedMapRows;

    public int Width => _width;
    public int Height => _height;
    public bool UsesSharedMapRows => _usesSharedMapRows;
    public int MapPrimaryRows => _mapPrimaryRows;

    public void Configure(HardwareProfile profile)
    {
        _profile = profile;
        _width = profile.Map.Width;
        _height = profile.Map.Height;
        _usesSharedMapRows = ChipMemoryLayout.UsesSharedMapRows(profile);
        _mapPrimaryRows = profile.Memory.MapLayout?.MapPrimaryRows ?? _height;
    }

    public void Initialize(ChipBus bus) => _bus = bus;

    public void Reset() { }

    public void Tick() { }

    public int MGet(int x, int y)
    {
        ValidateCoord(x, y);
        return _bus.Memory.Memory.Peek(MapAddress(x, y));
    }

    public void MSet(int x, int y, int value)
    {
        ValidateCoord(x, y);
        _bus.Memory.Memory.Poke(MapAddress(x, y), (byte)(value & 0xFF));
    }

    private int MapAddress(int x, int y) =>
        ChipMemoryLayout.MapAddress(_profile, x, y, _width);

    private void ValidateCoord(int x, int y)
    {
        if (x < 0 || y < 0 || x >= _width || y >= _height)
            throw new HardwareLimitException($"Map coordinate ({x},{y}) out of bounds.");
    }
}
