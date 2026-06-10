using Ferbento.Core.Chips;
using Ferbento.Profiles.BuiltIn;
using Ferbento.Scripting.Runtime;

namespace Ferbento.Compatibility.Tests;

public class SpriteEditorSprTests
{
    [Fact]
    public void EditorDraw_Spr_RendersMatchingPixel()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());

        // Simulate sprite-editor fill: write pixels directly to the GFX sheet.
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
            bus.Sprite.SSet(x, y, 8);

        const string script = """
            func _draw():
                cls(0)
                spr(0, 64, 64)
                flip()
            """;

        var runtime = new FerbentoRuntime(bus: bus, source: script, preserveCartData: true);
        runtime.Loop.Boot();
        runtime.Loop.Step();

        Assert.Equal(8, bus.Sprite.SGet(0, 0));
        Assert.NotEqual(0, bus.Display.FrontBuffer.GetPixel(64, 64));
    }
}
