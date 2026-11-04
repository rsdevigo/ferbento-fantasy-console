using System.Globalization;
using Ferbento.Scripting.Interpreter;
using Ferbento.Scripting.Runtime;

namespace Ferbento.Scripting.Tests;

// Regression tests for BUG-002: number <-> text conversion must not depend on the machine culture.
public class CultureInvarianceTests
{
    private static T WithCulture<T>(string name, Func<T> action)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);
        try { return action(); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static ScriptValue Eval(string expr)
    {
        var rt = new FerbentoRuntime(source: $"func test():\n    return {expr}\n");
        rt.Loop.Boot();
        return rt.Sandbox.Call("test");
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("de-DE")]
    public void Tostr_UsesDecimalPoint(string culture)
    {
        Assert.Equal("0.5", WithCulture(culture, () => Eval("tostr(0.5)").AsString()));
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("de-DE")]
    public void ScriptValueToString_UsesDecimalPoint(string culture)
    {
        Assert.Equal("-1.25", WithCulture(culture, () => ScriptValue.Number(-1.25).ToString()));
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("de-DE")]
    public void Tonum_ParsesDecimalPoint(string culture)
    {
        Assert.Equal(0.5, WithCulture(culture, () => Eval("tonum(\"0.5\")").AsNumber().ToDouble()));
    }
}
