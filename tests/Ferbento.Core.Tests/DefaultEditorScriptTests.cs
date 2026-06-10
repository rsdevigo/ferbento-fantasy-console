using Ferbento.Scripting.Runtime;

namespace Ferbento.Core.Tests;

public class DefaultEditorScriptTests
{
    private const string DefaultScript = """
        # Ferbento - Pico-8 compatible GDScript

        func _init():
            pass

        func _update():
            if btn(0):
                pass

        func _draw():
            cls(0)
            spr(0, 56, 56)
            flip()
        """;

    [Fact]
    public void DefaultEditorScript_RunsWithoutError()
    {
        var runtime = new FerbentoRuntime(source: DefaultScript);
        runtime.Loop.Boot();
        runtime.Loop.Step();
        Assert.Equal(1, runtime.Loop.FrameCount);
    }
}
