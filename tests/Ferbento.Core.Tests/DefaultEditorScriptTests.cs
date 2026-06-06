using Ferbento.Scripting.Runtime;

namespace Ferbento.Core.Tests;

public class DefaultEditorScriptTests
{
    private const string DefaultScript = """
        # Ferbento — Pico-8 compatible GDScript

        func _init():
            cls(0)

        func _update():
            if btn(0):
                pass

        func _draw():
            circfill(64, 64, 20, 8)
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
