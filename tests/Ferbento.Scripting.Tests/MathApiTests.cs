using Ferbento.Scripting.Interpreter;
using Ferbento.Scripting.Runtime;

namespace Ferbento.Scripting.Tests;

public class MathApiTests
{
    private static ScriptValue Eval(string expr)
    {
        var rt = new FerbentoRuntime(source: $"func test():\n    return {expr}\n");
        rt.Loop.Boot();
        return rt.Sandbox.Call("test");
    }

    [Fact]
    public void Abs_ReturnsPositive()
    {
        Assert.Equal(5, Eval("abs(-5)").AsInt());
    }

    [Fact]
    public void Sgn_Zero_IsOne()
    {
        Assert.Equal(1, Eval("sgn(0)").AsInt());
    }

    [Fact]
    public void Sgn_Negative_IsMinusOne()
    {
        Assert.Equal(-1, Eval("sgn(-3)").AsInt());
    }

    [Fact]
    public void Sin_Zero_ReturnsInverted()
    {
        Assert.True(Eval("sin(0)").AsNumber().Raw <= 0);
    }

    [Fact]
    public void Cos_Zero_IsInverted()
    {
        var val = Eval("cos(0)").AsNumber().ToDouble();
        Assert.True(val < 0);
    }

    [Fact]
    public void Rnd_ReturnsInRange()
    {
        var val = Eval("rnd(10)").AsNumber().ToDouble();
        Assert.InRange(val, 0, 10);
    }

    [Fact]
    public void Min_MultipleArgs()
    {
        Assert.Equal(3, Eval("min(5, 3, 8)").AsInt());
    }

    [Fact]
    public void Max_MultipleArgs()
    {
        Assert.Equal(8, Eval("max(5, 3, 8)").AsInt());
    }

    [Fact]
    public void Mid_ReturnsMiddle()
    {
        Assert.Equal(5, Eval("mid(5, 9, 1)").AsInt());
    }

    [Fact]
    public void Flr_RoundsDown()
    {
        Assert.Equal(3, Eval("flr(3.9)").AsInt());
    }

    [Fact]
    public void Sqrt_OfFour_IsTwo()
    {
        Assert.Equal(2, Eval("sqrt(4)").AsInt());
    }

    [Fact]
    public void Atan2_ReturnsIn01Range()
    {
        var val = Eval("atan2(0, 1)").AsNumber().ToDouble();
        Assert.InRange(val, 0, 1);
    }

    [Fact]
    public void Band_BitwiseAnd()
    {
        Assert.Equal(2, Eval("band(6, 3)").AsInt());
    }

    [Fact]
    public void Bor_BitwiseOr()
    {
        Assert.Equal(7, Eval("bor(4, 3)").AsInt());
    }
}
