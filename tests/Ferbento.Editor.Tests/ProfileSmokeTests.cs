using Ferbento.Core.Chips;
using Ferbento.Editor.Sprites;
using Ferbento.Profiles.BuiltIn;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Tests;

public class ProfileSmokeTests
{
    public static IEnumerable<object[]> ProfileDimensions =>
        new List<object[]>
        {
            new object[] { BuiltInProfiles.Pico8(), 8, 256 },
            new object[] { BuiltInProfiles.GameBoy(), 8, 256 },
            new object[] { BuiltInProfiles.Snes(), 16, 256 },
        };

    [Theory]
    [MemberData(nameof(ProfileDimensions))]
    public void Controller_SetProfile_ReportsCorrectDimensions(HardwareProfile profile, int tileSize, int spriteCount)
    {
        var ctrl = new SpriteEditorController();
        ctrl.SetProfile(profile);
        Assert.Equal(tileSize, ctrl.TileSize);
        Assert.Equal(spriteCount, ctrl.SpriteCount);
    }

    [Fact]
    public void Controller_Pico8_DrawFlipUndoRedo_Works()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        var ctrl = new SpriteEditorController();
        ctrl.SetChipBus(bus);
        ctrl.SetProfile(BuiltInProfiles.Pico8());

        ctrl.Document.SetLocalPixel(0, 0, 1);
        ctrl.FlipHorizontal();
        ctrl.Undo();
        ctrl.Redo();
    }

    [Fact]
    public void Pico8_UsesGfx2Bank()
    {
        var ctrl = new SpriteEditorController();
        ctrl.SetChipBus(new ChipBus(BuiltInProfiles.Pico8()));
        ctrl.SetProfile(BuiltInProfiles.Pico8());
        Assert.True(ctrl.UsesGfx2Bank);
        ctrl.CurrentSpriteIndex = 128;
        Assert.Equal(1, ctrl.GfxBank);
    }
}
