using Ferbento.Scripting.Interpreter;
using Ferbento.Scripting.Runtime;

namespace Ferbento.Scripting.Tests;

// Regression tests for BUG-003 (a call statement returning a value ended the function).
public class ExpressionStatementTests
{
    private static ScriptValue Run(string body)
    {
        var indented = string.Join("\n", body.Split('\n').Select(l => "    " + l));
        var rt = new FerbentoRuntime(source: $"func test():\n{indented}\n");
        rt.Loop.Boot();
        return rt.Sandbox.Call("test");
    }

    [Fact]
    public void CallStatementWithValue_DoesNotEndFunction()
    {
        Assert.Equal(7, Run("var t = []\nadd(t, 1)\nadd(t, 2)\nreturn 7").AsInt());
        Assert.Equal(7, Run("abs(-3)\nreturn 7").AsInt());
    }

    [Fact]
    public void CallStatementInsideIfAndWhile_DoesNotEndFunction()
    {
        Assert.Equal(3, Run("var t = []\nif true:\n    add(t, 1)\nvar i = 0\nwhile i < 2:\n    add(t, i)\n    i = i + 1\nreturn count(t)").AsInt());
    }

    [Fact]
    public void FunctionWithoutReturn_ReturnsNil()
    {
        Assert.Equal(ScriptValueType.Nil, Run("abs(-3)").Type);
    }
}
