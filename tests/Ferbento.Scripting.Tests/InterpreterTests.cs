using Ferbento.Scripting.Interpreter;
using Ferbento.Scripting.Runtime;

namespace Ferbento.Scripting.Tests;

public class InterpreterTests
{
    [Fact]
    public void ReturnConstant_Works()
    {
        var rt = new FerbentoRuntime(source: """
            func test():
                return 42
            """);
        rt.Loop.Boot();
        var result = rt.Sandbox.Call("test");
        Assert.Equal(42, result.AsInt());
    }

    [Fact]
    public void ReturnApiCall_Works()
    {
        var rt = new FerbentoRuntime(source: """
            func test():
                return abs(-7)
            """);
        rt.Loop.Boot();
        var result = rt.Sandbox.Call("test");
        Assert.Equal(7, result.AsInt());
    }

    [Fact]
    public void ReturnMathExpr_Works()
    {
        var rt = new FerbentoRuntime(source: """
            func test():
                return 3 + 4
            """);
        rt.Loop.Boot();
        var result = rt.Sandbox.Call("test");
        Assert.Equal(7, result.AsInt());
    }
}
