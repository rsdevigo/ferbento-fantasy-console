using Ferbento.Core.Math;
using Ferbento.Scripting.Interpreter;
using Ferbento.Scripting.Vm;

namespace Ferbento.Scripting.Tests.Vm;

/// <summary>Lua/PICO-8 table key semantics (story 008).</summary>
public class ScriptTableTests
{
    private static ScriptValue N(double v) => ScriptValue.Number(v);
    private static ScriptValue S(string s) => ScriptValue.Str(s);

    [Fact]
    public void FractionalNumberKeys_AreDistinct()
    {
        var t = new ScriptTable();
        t.Set(N(1), S("one"));
        t.Set(N(1.5), S("one and a half"));
        Assert.Equal("one", t.Get(N(1)).AsString());
        Assert.Equal("one and a half", t.Get(N(1.5)).AsString());
    }

    // ref: Lua 5.2 manual §2.1: the number 1 and the string "1" are different keys.
    [Fact]
    public void NumberAndStringKeys_AreDistinct()
    {
        var t = new ScriptTable();
        t.Set(N(1), S("number"));
        t.Set(S("1"), S("string"));
        Assert.Equal("number", t.Get(N(1)).AsString());
        Assert.Equal("string", t.Get(S("1")).AsString());
        Assert.Equal(2, t.Count);
    }

    [Fact]
    public void BoolAndTableKeys_Work()
    {
        var t = new ScriptTable();
        var keyTable = ScriptValue.Table(new ScriptTable());
        t.Set(ScriptValue.True, S("yes"));
        t.Set(keyTable, S("table key"));
        Assert.Equal("yes", t.Get(ScriptValue.True).AsString());
        Assert.Equal("table key", t.Get(keyTable).AsString());
        Assert.Equal(ScriptValueType.Nil, t.Get(ScriptValue.False).Type);
        Assert.Equal(ScriptValueType.Nil, t.Get(ScriptValue.Table(new ScriptTable())).Type);
    }

    [Fact]
    public void AssigningNil_RemovesTheKey()
    {
        var t = new ScriptTable();
        t.Set(S("a"), N(1));
        t.Set(S("a"), ScriptValue.Nil);
        Assert.Equal(0, t.Count);
        Assert.Empty(t.NamedKeys);
    }

    // ref: Lua 5.2: "table index is nil".
    [Fact]
    public void NilKey_Throws()
    {
        var ex = Assert.Throws<Pico8RuntimeException>(() => new ScriptTable().Set(ScriptValue.Nil, N(1)));
        Assert.Equal("table index is nil", ex.Message);
    }

    [Fact]
    public void GetWithNilKey_ReturnsNil()
    {
        Assert.Equal(ScriptValueType.Nil, new ScriptTable().Get(ScriptValue.Nil).Type);
    }

    [Fact]
    public void Length_GrowsAndShrinksWithTheSequence()
    {
        var t = new ScriptTable();
        t.Set(2, N(20));
        Assert.Equal(0, t.Length);
        t.Set(1, N(10));
        Assert.Equal(2, t.Length);
        t.Set(3, N(30));
        Assert.Equal(3, t.Length);
        t.Set(3, ScriptValue.Nil);
        Assert.Equal(2, t.Length);
    }

    [Fact]
    public void Length_WithHole_IsFirstBorder()
    {
        // TODO conferir no PICO-8/z8lua: real Lua may return any border (here 1 or 3).
        var t = new ScriptTable();
        t.Set(1, N(1));
        t.Set(2, N(2));
        t.Set(3, N(3));
        t.Set(2, ScriptValue.Nil);
        Assert.Equal(1, t.Length);
    }

    [Fact]
    public void Length_IgnoresNonIntegerAndNonPositiveKeys()
    {
        var t = new ScriptTable();
        t.Set(N(0), N(1));
        t.Set(N(-1), N(1));
        t.Set(N(1.5), N(1));
        Assert.Equal(0, t.Length);
    }

    [Fact]
    public void LegacyIntAndStringOverloads_ShareKeysWithValueOverloads()
    {
        var t = new ScriptTable();
        t.Set(1, S("a"));
        t.Set("k", S("b"));
        Assert.Equal("a", t.Get(N(1)).AsString());
        Assert.Equal("b", t.Get(S("k")).AsString());
        Assert.Equal("a", t.Get(1).AsString());
        Assert.Equal("b", t.Get("k").AsString());
    }

    [Fact]
    public void ArrayKeys_AreSortedIntegerKeysOnly()
    {
        var t = new ScriptTable();
        t.Set(3, N(3));
        t.Set(1, N(1));
        t.Set(N(2.5), N(2.5));
        t.Set("x", N(0));
        Assert.Equal(new[] { 1, 3 }, t.ArrayKeys);
        Assert.Equal(new[] { 1.0, 3.0 }, t.ArrayValues.Select(v => v.AsNumber().ToDouble()));
        Assert.Equal(new[] { "x" }, t.NamedKeys);
    }

    [Fact]
    public void Entries_ExposeAllKeysInInsertionOrder()
    {
        var t = new ScriptTable();
        t.Set(S("b"), N(1));
        t.Set(N(1), N(2));
        t.Set(ScriptValue.True, N(3));
        Assert.Equal(new[] { "b", "1", "true" }, t.Entries.Select(e => e.Key.ToString()));
    }

    [Fact]
    public void NumberKeys_CompareByRawBits()
    {
        var t = new ScriptTable();
        t.Set(ScriptValue.Number(new FixedPoint(0x10000)), S("one"));
        Assert.Equal("one", t.Get(1).AsString());
    }
}
