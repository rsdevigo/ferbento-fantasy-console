using Ferbento.Core.Chips;
using Ferbento.Core.Memory;
using Ferbento.Profiles.BuiltIn;

namespace Ferbento.Core.Tests;

public class TilemapChipTests
{
    [Fact]
    public void Pico8_MGet_Row32_UsesSharedRegion()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        bus.Tilemap.MSet(0, 32, 42);
        Assert.Equal(42, bus.Tilemap.MGet(0, 32));
        Assert.Equal(42, bus.Memory.Memory.Peek(MemoryMap.Gfx2Start));
    }

    [Fact]
    public void Pico8_MGet_Row31_UsesPrimaryRegion()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        bus.Tilemap.MSet(0, 31, 99);
        Assert.Equal(99, bus.Tilemap.MGet(0, 31));
        Assert.Equal(99, bus.Memory.Memory.Peek(MemoryMap.MapStart + 31 * 128));
    }

    [Fact]
    public void Pico8_SharedRow_PokeAtGfx2_VisibleInMap()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        bus.Memory.Memory.Poke(MemoryMap.Gfx2Start + 10, 77);
        Assert.Equal(77, bus.Tilemap.MGet(10, 32));
    }

    [Fact]
    public void GameBoy_LinearMapAddressing()
    {
        var bus = new ChipBus(BuiltInProfiles.GameBoy());
        bus.Tilemap.MSet(5, 10, 12);
        Assert.Equal(12, bus.Tilemap.MGet(5, 10));
        Assert.False(bus.Tilemap.UsesSharedMapRows);
    }
}
