using Ferbento.Core.Chips;
using Ferbento.Core.Memory;
using Ferbento.Profiles.BuiltIn;

namespace Ferbento.Core.Tests;

public class SpriteFlagTests
{
    [Fact]
    public void FSet_FGet_RoundTrip_AllBits()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        const int sprite = 42;

        for (var bit = 0; bit < 8; bit++)
        {
            bus.Sprite.FSet(sprite, bit, 1);
            Assert.Equal(1, bus.Sprite.FGet(sprite, bit));
        }

        Assert.Equal(0xFF, bus.Sprite.FGet(sprite));
        Assert.Equal(0xFF, bus.Memory.Memory.Peek(MemoryMap.GfxPropsStart + sprite));

        bus.Sprite.FSet(sprite, -1, 0xA5);
        Assert.Equal(0xA5, bus.Sprite.FGet(sprite));
    }

    [Fact]
    public void Pico8Profile_SpriteSheet_Has256Tiles()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        Assert.Equal(128, bus.Sprite.SheetWidth);
        Assert.Equal(128, bus.Sprite.SheetHeight);
        Assert.Equal(8, bus.Sprite.TileSize);
        Assert.Equal(256, bus.Sprite.SpriteCount);
        Assert.True(bus.Sprite.UsesGfx2Bank);
    }

    [Fact]
    public void Gfx2Bank_UpperHalfUsesGfx2Region()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        bus.Sprite.SSet(0, 64, 5);
        var addr = MemoryMap.Gfx2Start;
        var lo = bus.Memory.Memory.Peek(addr);
        Assert.Equal(5, lo & 0xF);
        Assert.Equal(5, bus.Sprite.SGet(0, 64));
    }
}
