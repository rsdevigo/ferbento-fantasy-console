using Ferbento.Core.Chips;
using Ferbento.Profiles.BuiltIn;
using Ferbento.Scripting.Runtime;

namespace Ferbento.Scripting.Tests;

public class SprApiTests
{
    [Fact]
    public void Spr_DrawsSpriteFromEditorSheet_ToFrontBuffer()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        bus.Sprite.SSet(0, 0, 8);
        bus.Sprite.SSet(4, 4, 8);

        const string script = """
            func _init():
                pass

            func _draw():
                cls(0)
                spr(0, 60, 60)
                flip()
            """;

        var runtime = new FerbentoRuntime(bus: bus, source: script, preserveCartData: true);
        runtime.Loop.Boot();
        runtime.Loop.Step();

        Assert.Equal(8, bus.Sprite.SGet(0, 0));
        Assert.NotEqual(0, bus.Display.FrontBuffer.GetPixel(60, 60));
    }

    [Fact]
    public void Spr_InEditorMode_AutoFlips_WhenFlipOmitted()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        bus.Sprite.SSet(0, 0, 8);

        const string script = """
            func _draw():
                cls(0)
                spr(0, 60, 60)
            """;

        var runtime = new FerbentoRuntime(bus: bus, source: script, preserveCartData: true);
        runtime.Loop.Boot();
        runtime.Loop.Step();

        Assert.NotEqual(0, bus.Display.FrontBuffer.GetPixel(60, 60));
    }

    [Fact]
    public void Spr_ColorZero_IsTransparent()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        bus.Sprite.SSet(0, 0, 0);
        bus.Sprite.SSet(4, 4, 0);

        const string script = """
            func _draw():
                cls(1)
                spr(0, 60, 60)
                flip()
            """;

        var runtime = new FerbentoRuntime(bus: bus, source: script, preserveCartData: true);
        runtime.Loop.Boot();
        runtime.Loop.Step();

        Assert.Equal(1, bus.Display.FrontBuffer.GetPixel(60, 60));
    }
}
