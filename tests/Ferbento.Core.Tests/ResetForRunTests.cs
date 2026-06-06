using Ferbento.Core.Chips;
using Ferbento.Core.Memory;
using Ferbento.Profiles.BuiltIn;
using Ferbento.Scripting.Runtime;

namespace Ferbento.Core.Tests;

public class ResetForRunTests
{
    [Fact]
    public void Boot_WithPreserveCartData_KeepsSpriteSheet()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        bus.Sprite.SSet(0, 0, 7);
        bus.Sprite.FSet(0, 0, 1);

        var runtime = new FerbentoRuntime(
            bus: bus,
            source: "func _init():\n\tpass",
            preserveCartData: true);
        runtime.Loop.Boot();

        Assert.Equal(7, bus.Sprite.SGet(0, 0));
        Assert.Equal(1, bus.Sprite.FGet(0, 0));
    }

    [Fact]
    public void Boot_WithoutPreserveCartData_ClearsSpriteSheet()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        bus.Sprite.SSet(0, 0, 7);

        var runtime = new FerbentoRuntime(
            bus: bus,
            source: "func _init():\n\tpass",
            preserveCartData: false);
        runtime.Loop.Boot();

        Assert.Equal(0, bus.Sprite.SGet(0, 0));
    }

    [Fact]
    public void ResetForRun_ResetsDisplayDrawState()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        bus.Display.DrawState.CameraX = 42;
        bus.Display.DrawState.DrawPalette[3] = 9;

        bus.ResetForRun();

        Assert.Equal(0, bus.Display.DrawState.CameraX);
        Assert.Equal(3, bus.Display.DrawState.DrawPalette[3]);
    }
}
