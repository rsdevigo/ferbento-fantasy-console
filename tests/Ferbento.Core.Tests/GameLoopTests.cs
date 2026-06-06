using Ferbento.Scripting.Runtime;

namespace Ferbento.Core.Tests;

public class GameLoopTests
{
    [Fact]
    public void GameLoop_RunsInitUpdateDraw()
    {
        const string script = """
            func _init():
                pass

            func _update():
                pass

            func _draw():
                cls(0)
                flip()
            """;

        var runtime = new FerbentoRuntime(source: script);
        runtime.Loop.Boot();
        runtime.Loop.Step();

        Assert.Equal(1, runtime.Loop.FrameCount);
    }
}
