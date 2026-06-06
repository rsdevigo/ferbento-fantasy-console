using Ferbento.Core.Chips;
using Ferbento.Core.Memory;
using Ferbento.Profiles.BuiltIn;
using Ferbento.Profiles.Models;

namespace Ferbento.Core.Tests;

public class ColorChipTests
{
    [Fact]
    public void SetColor_PersistsToUserDataRegion()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        bus.Color.SetColor(1, 0xABCDEF);

        var magic = bus.Memory.Memory.Peek4(MemoryMap.UserDataStart);
        Assert.Equal(ColorChip.PalettePersistMagic, (int)magic);

        Assert.Equal(0xABCDEFu, bus.Color.GetColor(1));

        var saved = (byte[])bus.Memory.Memory.Buffer.Clone();
        var bus2 = new ChipBus(BuiltInProfiles.Pico8());
        Array.Copy(saved, bus2.Memory.Memory.Buffer, saved.Length);
        bus2.Color.LoadFromMemory();

        Assert.Equal(0xABCDEFu, bus2.Color.GetColor(1));
    }

    [Theory]
    [InlineData("Game Boy", 4, 32, 32)]
    [InlineData("Pico-8", 16, 128, 64)]
    public void Profile_DrivesColorAndMapCounts(string name, int colors, int mapW, int mapH)
    {
        var profile = BuiltInProfiles.GetByName(name)!;
        var bus = new ChipBus(profile);

        Assert.Equal(colors, bus.Color.ColorCount);
        Assert.Equal(mapW, bus.Tilemap.Width);
        Assert.Equal(mapH, bus.Tilemap.Height);
    }

    [Fact]
    public void GameBoyProfile_HasFourDefaultColors()
    {
        var bus = new ChipBus(BuiltInProfiles.GameBoy());
        Assert.Equal(4, bus.Color.ColorCount);
        Assert.Equal(0x0F380Fu, bus.Color.GetColor(0));
        Assert.Equal(0x9BBC0Fu, bus.Color.GetColor(3));
    }
}
