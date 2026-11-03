using Ferbento.Core.Math;
using Ferbento.Scripting.Interpreter;
using Ferbento.Scripting.Vm;

namespace Ferbento.Scripting.Tests.Vm;

/// <summary>
/// Executable specification of PICO-8 value semantics (story 008).
/// "ref:" comments say where the expected value comes from.
/// </summary>
public class Pico8OpsTests
{
    private static ScriptValue N(double v) => ScriptValue.Number(v);
    private static ScriptValue Raw(int raw) => ScriptValue.Number(new FixedPoint(raw));
    private static ScriptValue S(string s) => ScriptValue.Str(s);
    private static ScriptValue T() => ScriptValue.Table(new ScriptTable());
    private static int RawOf(ScriptValue v) => v.AsNumber().Raw;

    // ---- Truthiness ----

    // ref: Lua 5.2 manual §2.1: only nil and false are false.
    [Fact]
    public void Truthiness_OnlyNilAndFalseAreFalse()
    {
        Assert.False(Pico8Ops.IsTruthy(ScriptValue.Nil));
        Assert.False(Pico8Ops.IsTruthy(ScriptValue.False));
        Assert.True(Pico8Ops.IsTruthy(N(0)));
        Assert.True(Pico8Ops.IsTruthy(S("")));
        Assert.True(Pico8Ops.IsTruthy(T()));
    }

    // ---- Arithmetic ----

    // ref: PICO-8 manual, "Numbers": values wrap around at 32767.99999.
    [Fact]
    public void Add_Overflow_Wraps()
    {
        Assert.Equal(-32768, Pico8Ops.Add(N(32767), N(1)).AsNumber().ToInt());
    }

    // ref: PICO-8 manual: division by zero gives 0x7fff.ffff or -0x7fff.ffff.
    [Fact]
    public void Div_ByZero_GivesSignedMax()
    {
        Assert.Equal(0x7fffffff, RawOf(Pico8Ops.Div(N(1), N(0))));
        Assert.Equal(-0x7fffffff, RawOf(Pico8Ops.Div(N(-1), N(0))));
        // TODO conferir no PICO-8/z8lua: 0/0 assumed to be 0x7fff.ffff.
        Assert.Equal(0x7fffffff, RawOf(Pico8Ops.Div(N(0), N(0))));
    }

    // ref: Lua 5.2 manual §3.4.1: a % b == a - floor(a/b)*b.
    [Fact]
    public void Mod_FloorsTowardNegativeInfinity()
    {
        Assert.Equal(1, Pico8Ops.Mod(N(-5), N(3)).AsInt());
        Assert.Equal(2, Pico8Ops.Mod(N(5), N(3)).AsInt());
    }

    // ref: PICO-8 manual: 9\2 == 4 (flr(9/2)).
    [Fact]
    public void IntDiv_IsFloorOfDivision()
    {
        Assert.Equal(4, Pico8Ops.IntDiv(N(9), N(2)).AsInt());
        Assert.Equal(-5, Pico8Ops.IntDiv(N(-9), N(2)).AsInt());
    }

    [Fact]
    public void Pow_RaisesToPower()
    {
        Assert.Equal(8, Pico8Ops.Pow(N(2), N(3)).AsInt());
        Assert.Equal(0x8000, RawOf(Pico8Ops.Pow(N(0.25), N(0.5))));
    }

    [Fact]
    public void Neg_MinValue_Wraps()
    {
        Assert.Equal(int.MinValue, RawOf(Pico8Ops.Neg(Raw(int.MinValue))));
    }

    // ref: Lua 5.2 manual §3.4.2: strings are converted to numbers in arithmetic.
    [Fact]
    public void Arithmetic_CoercesNumericStrings()
    {
        Assert.Equal(3, Pico8Ops.Add(S("1"), N(2)).AsInt());
        Assert.Equal(17.25, Pico8Ops.Add(S("0x11.4"), N(0)).AsNumber().ToDouble());
        Assert.Equal(5, Pico8Ops.Add(S("0b101"), N(0)).AsInt());
    }

    // ref: Lua 5.2 error wording ("attempt to perform arithmetic on a nil value").
    [Fact]
    public void Arithmetic_OnNonNumber_Throws()
    {
        var ex = Assert.Throws<Pico8RuntimeException>(() => Pico8Ops.Add(ScriptValue.Nil, N(1)));
        Assert.Equal("attempt to perform arithmetic on a nil value", ex.Message);
        Assert.Throws<Pico8RuntimeException>(() => Pico8Ops.Mul(S("abc"), N(1)));
        Assert.Throws<Pico8RuntimeException>(() => Pico8Ops.Sub(T(), N(1)));
    }

    // ---- Bitwise ----

    // ref: PICO-8 manual: & | ^^ ~ operate on the 32 bits of the 16.16 value.
    [Fact]
    public void BAndBOrBXor_UseRawBits()
    {
        Assert.Equal(0x8000, RawOf(Pico8Ops.BAnd(N(1.5), N(0.5))));
        Assert.Equal(0x18000, RawOf(Pico8Ops.BOr(N(1), N(0.5))));
        Assert.Equal(0x10000, RawOf(Pico8Ops.BXor(N(1.5), N(0.5))));
    }

    [Fact]
    public void BNot_InvertsAllBits()
    {
        Assert.Equal(~0x10000, RawOf(Pico8Ops.BNot(N(1))));
    }

    // ref: PICO-8 manual: shl(1,4) == 16 and shr(1,1) == 0.5.
    [Fact]
    public void Shl_And_Shr_ShiftRawBits()
    {
        Assert.Equal(16, Pico8Ops.Shl(N(1), N(4)).AsInt());
        Assert.Equal(0.5, Pico8Ops.Shr(N(1), N(1)).AsNumber().ToDouble());
        Assert.Equal(-1, Pico8Ops.Shr(N(-2), N(1)).AsInt());
    }

    [Fact]
    public void Lshr_FillsWithZeros()
    {
        Assert.Equal(0x7fff8000, RawOf(Pico8Ops.Lshr(N(-1), N(1))));
    }

    [Fact]
    public void Rotl_And_Rotr_WrapBits()
    {
        Assert.Equal(int.MinValue, RawOf(Pico8Ops.Rotr(Raw(1), N(1))));
        Assert.Equal(1, RawOf(Pico8Ops.Rotl(Raw(int.MinValue), N(1))));
    }

    [Fact]
    public void Shift_AmountUsesIntegerPart()
    {
        // TODO conferir no PICO-8/z8lua: fractional shift amounts are floored.
        Assert.Equal(16, Pico8Ops.Shl(N(1), N(4.75)).AsInt());
    }

    [Fact]
    public void Bitwise_OnNonNumber_Throws()
    {
        Assert.Throws<Pico8RuntimeException>(() => Pico8Ops.BAnd(ScriptValue.Nil, N(1)));
        Assert.Throws<Pico8RuntimeException>(() => Pico8Ops.BNot(T()));
    }

    // ---- Comparison ----

    [Fact]
    public void LessThan_Numbers()
    {
        Assert.True(Pico8Ops.LessThan(N(-1), N(0.5)));
        Assert.False(Pico8Ops.LessThan(N(2), N(2)));
        Assert.True(Pico8Ops.LessEqual(N(2), N(2)));
    }

    // ref: Lua 5.2 manual §3.4.3: strings compare by their bytes (strcoll in C locale).
    [Fact]
    public void LessThan_Strings_AreLexicographicByByte()
    {
        Assert.True(Pico8Ops.LessThan(S("a"), S("b")));
        Assert.True(Pico8Ops.LessThan(S("Z"), S("a")));
        Assert.True(Pico8Ops.LessThan(S("ab"), S("abc")));
        Assert.True(Pico8Ops.LessThan(S("10"), S("9")));
    }

    // ref: Lua 5.2 error wording.
    [Fact]
    public void LessThan_NumberWithString_Throws()
    {
        var ex = Assert.Throws<Pico8RuntimeException>(() => Pico8Ops.LessThan(N(1), S("2")));
        Assert.Equal("attempt to compare number with string", ex.Message);
        var ex2 = Assert.Throws<Pico8RuntimeException>(() => Pico8Ops.LessEqual(T(), T()));
        Assert.Equal("attempt to compare two table values", ex2.Message);
    }

    // ---- Equality ----

    [Fact]
    public void Equals_DifferentTypes_IsFalse()
    {
        Assert.False(Pico8Ops.RawEquals(N(1), S("1")));
        Assert.False(Pico8Ops.RawEquals(ScriptValue.False, ScriptValue.Nil));
        Assert.False(Pico8Ops.RawEquals(N(0), ScriptValue.False));
    }

    [Fact]
    public void Equals_SameType_ComparesValues()
    {
        Assert.True(Pico8Ops.RawEquals(N(1.5), N(1.5)));
        Assert.True(Pico8Ops.RawEquals(S("abc"), S("abc")));
        Assert.True(Pico8Ops.RawEquals(ScriptValue.Nil, ScriptValue.Nil));
    }

    [Fact]
    public void Equals_Tables_CompareByReference()
    {
        var t = T();
        Assert.True(Pico8Ops.RawEquals(t, t));
        Assert.False(Pico8Ops.RawEquals(T(), T()));
    }

    // ---- Concatenation ----

    [Fact]
    public void Concat_StringsAndNumbers()
    {
        Assert.Equal("ab", Pico8Ops.Concat(S("a"), S("b")).AsString());
        Assert.Equal("x1.5", Pico8Ops.Concat(S("x"), N(1.5)).AsString());
        Assert.Equal("12", Pico8Ops.Concat(N(1), N(2)).AsString());
    }

    [Fact]
    public void Concat_NilOrTable_Throws()
    {
        var ex = Assert.Throws<Pico8RuntimeException>(() => Pico8Ops.Concat(S("a"), ScriptValue.Nil));
        Assert.Equal("attempt to concatenate a nil value", ex.Message);
        Assert.Throws<Pico8RuntimeException>(() => Pico8Ops.Concat(T(), S("a")));
    }

    // ref: PICO-8 tostr: integers without decimals; fractions with up to 4 decimals.
    [Theory]
    [InlineData(0x10000, "1")]
    [InlineData(-0x80000000, "-32768")]
    [InlineData(0x8000, "0.5")]
    [InlineData(-0x18000, "-1.5")]
    [InlineData(0x5555, "0.3333")]   // 1/3
    [InlineData(0xaaab, "0.6667")]   // 2/3, rounds up
    [InlineData(0x199a, "0.1")]      // 0.1 stored as 0.100006
    public void NumberToString_FollowsTostrFormat(int raw, string expected)
    {
        Assert.Equal(expected, Pico8Ops.NumberToString(new FixedPoint(raw)));
    }

    [Fact]
    public void NumberToString_TinyFractions()
    {
        // TODO conferir no PICO-8/z8lua: 0x0.0001 and -0x0.0001 are assumed to print as "0".
        Assert.Equal("0", Pico8Ops.NumberToString(new FixedPoint(1)));
        Assert.Equal("0", Pico8Ops.NumberToString(new FixedPoint(-1)));
    }

    // ---- Length ----

    [Fact]
    public void Length_String_IsByteCount()
    {
        Assert.Equal(5, Pico8Ops.Length(S("hello")).AsInt());
        Assert.Equal(0, Pico8Ops.Length(S("")).AsInt());
    }

    [Fact]
    public void Length_Table_IsArrayBorder()
    {
        var t = new ScriptTable();
        t.Set(1, N(10));
        t.Set(2, N(20));
        t.Set(3, N(30));
        t.Set("name", S("x"));
        Assert.Equal(3, Pico8Ops.Length(ScriptValue.Table(t)).AsInt());
    }

    [Fact]
    public void Length_OfNil_Throws()
    {
        var ex = Assert.Throws<Pico8RuntimeException>(() => Pico8Ops.Length(ScriptValue.Nil));
        Assert.Equal("attempt to get length of a nil value", ex.Message);
    }

    // ---- Indexing ----

    [Fact]
    public void Index_NonTable_Throws()
    {
        var ex = Assert.Throws<Pico8RuntimeException>(() => Pico8Ops.Index(ScriptValue.Nil, N(1)));
        Assert.Equal("attempt to index a nil value", ex.Message);
        Assert.Throws<Pico8RuntimeException>(() => Pico8Ops.SetIndex(N(1), N(1), N(2)));
    }

    [Fact]
    public void Index_And_SetIndex_RoundTrip()
    {
        var t = T();
        Pico8Ops.SetIndex(t, S("hp"), N(3));
        Assert.Equal(3, Pico8Ops.Index(t, S("hp")).AsInt());
        Assert.Equal(ScriptValueType.Nil, Pico8Ops.Index(t, S("missing")).Type);
    }

    // ---- Types ----

    [Fact]
    public void TypeName_MatchesType()
    {
        Assert.Equal("nil", Pico8Ops.TypeName(ScriptValue.Nil));
        Assert.Equal("number", Pico8Ops.TypeName(N(1)));
        Assert.Equal("string", Pico8Ops.TypeName(S("")));
        Assert.Equal("boolean", Pico8Ops.TypeName(ScriptValue.True));
        Assert.Equal("table", Pico8Ops.TypeName(T()));
        Assert.Equal("function", Pico8Ops.TypeName(ScriptValue.Native(_ => ScriptValue.Nil)));
    }
}
