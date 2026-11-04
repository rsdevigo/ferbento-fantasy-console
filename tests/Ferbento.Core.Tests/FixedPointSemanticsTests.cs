using Ferbento.Core.Math;

namespace Ferbento.Core.Tests;

/// <summary>
/// Characterizes <see cref="FixedPoint"/> against PICO-8 16.16 semantics.
/// Raw values are written as hex (0x7fffffff == 0x7fff.ffff in PICO-8 notation).
/// </summary>
public class FixedPointSemanticsTests
{
    private static FixedPoint Raw(int raw) => new(raw);
    private static FixedPoint N(double v) => FixedPoint.FromDouble(v);

    // ref: PICO-8 manual, "Numbers": addition wraps (a counter overflows after ~18 minutes).
    [Fact]
    public void Add_Overflow_Wraps()
    {
        var result = Raw(0x7fffffff) + Raw(1);
        Assert.Equal(int.MinValue, result.Raw);
    }

    [Fact]
    public void Mul_Overflow_Wraps()
    {
        // 256 * 256 = 65536, which is outside 16.16 and wraps to 0.
        var result = FixedPoint.FromInt(256) * FixedPoint.FromInt(256);
        Assert.Equal(0, result.Raw);
    }

    [Fact]
    public void Mul_Negative_FloorsTheLowBits()
    {
        // ref: z8lua fix32 multiplication is (int64(a) * b) >> 16 (arithmetic shift, floor).
        var result = Raw(-1) * Raw(0x8000); // -0x0.0001 * 0.5
        Assert.Equal(-1, result.Raw);
    }

    // ref: PICO-8 manual: division by zero gives 0x7fff.ffff if positive, -0x7fff.ffff if negative.
    [Fact]
    public void Div_PositiveByZero_IsMax()
    {
        Assert.Equal(0x7fffffff, (FixedPoint.FromInt(5) / FixedPoint.FromInt(0)).Raw);
    }

    [Fact]
    public void Div_NegativeByZero_IsMinusMax()
    {
        Assert.Equal(-0x7fffffff, (FixedPoint.FromInt(-5) / FixedPoint.FromInt(0)).Raw);
    }

    [Fact]
    public void Div_ZeroByZero_IsMax()
    {
        // TODO conferir no PICO-8/z8lua: assumed 0 counts as "positive" (manual does not say).
        Assert.Equal(0x7fffffff, (FixedPoint.FromInt(0) / FixedPoint.FromInt(0)).Raw);
    }

    [Fact]
    public void Div_Fraction_IsExact()
    {
        Assert.Equal(0x8000, (FixedPoint.FromInt(1) / FixedPoint.FromInt(2)).Raw);
    }

    // ref: Lua 5.2 modulo is a - floor(a/b)*b, so the result has the sign of the divisor.
    [Fact]
    public void Mod_NegativeDividend_IsPositive()
    {
        Assert.Equal(FixedPoint.FromInt(1), FixedPoint.FromInt(-5) % FixedPoint.FromInt(3));
    }

    [Fact]
    public void Mod_NegativeDivisor_HasDivisorSign()
    {
        // TODO conferir no PICO-8/z8lua: Lua gives -1; some sources say PICO-8 always returns positive.
        Assert.Equal(FixedPoint.FromInt(-1), FixedPoint.FromInt(5) % FixedPoint.FromInt(-3));
    }

    [Fact]
    public void Mod_Fraction_Works()
    {
        Assert.Equal(N(0.5), N(5.5) % FixedPoint.FromInt(1));
    }

    [Fact]
    public void Mod_ByZero_IsZero()
    {
        // TODO conferir no PICO-8/z8lua: behavior of x % 0 is not in the manual.
        Assert.Equal(0, (FixedPoint.FromInt(5) % FixedPoint.FromInt(0)).Raw);
    }

    [Fact]
    public void Mod_MinValueByMinusEpsilon_DoesNotThrow()
    {
        Assert.Equal(0, (Raw(int.MinValue) % Raw(-1)).Raw);
    }

    [Fact]
    public void IntDivide_FloorsTowardNegativeInfinity()
    {
        // ref: PICO-8 manual: 9\2 == flr(9/2) == 4.
        Assert.Equal(FixedPoint.FromInt(4), FixedPoint.IntDivide(FixedPoint.FromInt(9), FixedPoint.FromInt(2)));
        Assert.Equal(FixedPoint.FromInt(-5), FixedPoint.IntDivide(FixedPoint.FromInt(-9), FixedPoint.FromInt(2)));
    }

    [Fact]
    public void Floor_Negative_RoundsDown()
    {
        Assert.Equal(FixedPoint.FromInt(-2), FixedPoint.Floor(N(-1.5)));
    }

    [Fact]
    public void Negate_MinValue_Wraps()
    {
        Assert.Equal(int.MinValue, (-Raw(int.MinValue)).Raw);
    }

    // Shifts operate on the raw 32 bits (PICO-8 shl/shr/lshr/rotl/rotr).
    [Fact]
    public void ShiftLeft_UsesRawBits()
    {
        Assert.Equal(FixedPoint.FromInt(16), FixedPoint.ShiftLeft(FixedPoint.FromInt(1), 4));
    }

    [Fact]
    public void ShiftRight_ProducesFractions()
    {
        // ref: PICO-8 manual: shr(1,1) == 0.5.
        Assert.Equal(N(0.5), FixedPoint.ShiftRightArithmetic(FixedPoint.FromInt(1), 1));
    }

    [Fact]
    public void ShiftRight_IsArithmetic()
    {
        Assert.Equal(FixedPoint.FromInt(-1), FixedPoint.ShiftRightArithmetic(FixedPoint.FromInt(-2), 1));
    }

    [Fact]
    public void ShiftRightLogical_FillsWithZeros()
    {
        Assert.Equal(0x7fff8000, FixedPoint.ShiftRightLogical(FixedPoint.FromInt(-1), 1).Raw);
    }

    [Fact]
    public void Shift_ByThirtyTwoOrMore_ClearsOrSignFills()
    {
        // TODO conferir no PICO-8/z8lua: shift amounts >= 32.
        Assert.Equal(0, FixedPoint.ShiftLeft(FixedPoint.FromInt(1), 32).Raw);
        Assert.Equal(-1, FixedPoint.ShiftRightArithmetic(FixedPoint.FromInt(-1), 40).Raw);
        Assert.Equal(0, FixedPoint.ShiftRightLogical(FixedPoint.FromInt(-1), 32).Raw);
    }

    [Fact]
    public void Shift_NegativeAmount_ShiftsTheOtherWay()
    {
        // TODO conferir no PICO-8/z8lua: negative shift amounts.
        Assert.Equal(FixedPoint.FromInt(2), FixedPoint.ShiftLeft(FixedPoint.FromInt(4), -1));
        Assert.Equal(FixedPoint.FromInt(8), FixedPoint.ShiftRightArithmetic(FixedPoint.FromInt(4), -1));
    }

    [Fact]
    public void Rotate_WrapsBitsAround()
    {
        Assert.Equal(int.MinValue, FixedPoint.RotateRight(Raw(1), 1).Raw);
        Assert.Equal(1, FixedPoint.RotateLeft(Raw(int.MinValue), 1).Raw);
    }

    [Fact]
    public void Pow_IntegerExponent()
    {
        Assert.Equal(FixedPoint.FromInt(8), FixedPoint.Pow(FixedPoint.FromInt(2), FixedPoint.FromInt(3)));
    }

    [Fact]
    public void Pow_Overflow_DoesNotThrow()
    {
        // TODO conferir no PICO-8/z8lua: z8lua computes ^ in floating point; overflow result is assumed to wrap.
        var ex = Record.Exception(() => FixedPoint.Pow(FixedPoint.FromInt(1000), FixedPoint.FromInt(10)));
        Assert.Null(ex);
    }
}

// Regression test for BUG-002.
public class FixedPointCultureTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("de-DE")]
    public void ToString_UsesDecimalPoint(string culture)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(culture);
        try
        {
            Assert.Equal("0.5", Ferbento.Core.Math.FixedPoint.FromDouble(0.5).ToString());
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
