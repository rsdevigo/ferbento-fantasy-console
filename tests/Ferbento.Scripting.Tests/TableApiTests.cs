using Ferbento.Scripting.Interpreter;
using Ferbento.Scripting.Runtime;

namespace Ferbento.Scripting.Tests;

// Regression tests for BUG-001 (del did not shift elements nor compare by value).
public class TableApiTests
{
    private static ScriptValue Run(string body)
    {
        var indented = string.Join("\n", body.Split('\n').Select(l => "    " + l));
        var rt = new FerbentoRuntime(source: $"func test():\n{indented}\n");
        rt.Loop.Boot();
        return rt.Sandbox.Call("test");
    }

    [Fact]
    public void Del_ShiftsFollowingElements()
    {
        Assert.Equal(2, Run("var t = [1, 2, 3]\ndel(t, 2)\nreturn count(t)").AsInt());
        Assert.Equal(3, Run("var t = [1, 2, 3]\ndel(t, 2)\nreturn t[2]").AsInt());
        Assert.Equal(ScriptValueType.Nil, Run("var t = [1, 2, 3]\ndel(t, 2)\nreturn t[3]").Type);
    }

    [Fact]
    public void Del_ReturnsRemovedValue()
    {
        Assert.Equal(2, Run("var t = [1, 2, 3]\nreturn del(t, 2)").AsInt());
    }

    [Fact]
    public void Del_NotFound_ReturnsNilAndKeepsTable()
    {
        Assert.Equal(ScriptValueType.Nil, Run("var t = [1, 2, 3]\nreturn del(t, 9)").Type);
        Assert.Equal(3, Run("var t = [1, 2, 3]\ndel(t, 9)\nreturn count(t)").AsInt());
    }

    [Fact]
    public void Del_RemovesOnlyFirstOccurrence()
    {
        Assert.Equal(3, Run("var t = [5, 7, 5, 9]\ndel(t, 5)\nreturn count(t)").AsInt());
        Assert.Equal(5, Run("var t = [5, 7, 5, 9]\ndel(t, 5)\nreturn t[2]").AsInt());
    }

    [Fact]
    public void Del_UsesLuaEquality_NumberIsNotString()
    {
        Assert.Equal(ScriptValueType.Nil, Run("var t = [1, 2]\nreturn del(t, \"1\")").Type);
        Assert.Equal(2, Run("var t = [1, 2]\ndel(t, \"1\")\nreturn count(t)").AsInt());
    }

    [Fact]
    public void Del_ComparesTablesByReference()
    {
        Assert.Equal(2, Run("var t = [[1], [1]]\ndel(t, [1])\nreturn count(t)").AsInt());
        Assert.Equal(1, Run("var a = [1]\nvar t = [[1], a]\ndel(t, a)\nreturn count(t)").AsInt());
    }

    [Fact]
    public void Add_ReturnsValue()
    {
        Assert.Equal(4, Run("var t = [1, 2, 3]\nreturn add(t, 4)").AsInt());
    }

    [Fact]
    public void Add_WithIndex_InsertsAndShifts()
    {
        Assert.Equal(9, Run("var t = [1, 2, 3]\nadd(t, 9, 2)\nreturn t[2]").AsInt());
        Assert.Equal(2, Run("var t = [1, 2, 3]\nadd(t, 9, 2)\nreturn t[3]").AsInt());
        Assert.Equal(4, Run("var t = [1, 2, 3]\nadd(t, 9, 2)\nreturn count(t)").AsInt());
    }
}
