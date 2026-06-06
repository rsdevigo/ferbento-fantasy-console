using Ferbento.Core.Exceptions;
using Ferbento.Scripting.Runtime;

namespace Ferbento.Scripting.Tests;

public class SandboxTests
{
    [Fact]
    public void TokenLimit_Exceeded_Throws()
    {
        var manyVars = string.Join(" ", Enumerable.Repeat("x", 9000));
        var source = $"func _init():\n    {manyVars}\n";
        var ex = Assert.Throws<HardwareLimitException>(() =>
        {
            var _ = new FerbentoRuntime(source: source);
        });
        Assert.Contains("token", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalScript_UnderTokenLimit()
    {
        var source = """
            func _init():
                cls(0)
            func _update():
                if btn(0):
                    pass
            func _draw():
                circfill(64, 64, 20, 8)
                flip()
            """;
        var rt = new FerbentoRuntime(source: source);
        rt.Loop.Boot();
        Assert.InRange(rt.Sandbox.Context.TokenCount, 1, 8192);
    }

    [Fact]
    public void ScriptWithManyTokens_CloseToLimit()
    {
        var manyTokens = string.Join(" ", Enumerable.Repeat("v", 8000));
        var source = $"func test():\n    return {manyTokens}\n";
        var rt = new FerbentoRuntime(source: source);
        Assert.InRange(rt.Sandbox.Context.TokenCount, 8000, 8192);
    }
}
