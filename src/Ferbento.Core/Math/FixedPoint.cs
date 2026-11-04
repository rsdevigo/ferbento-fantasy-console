namespace Ferbento.Core.Math;

/// <summary>
/// Pico-8 16.16 signed fixed-point number.
/// </summary>
public readonly struct FixedPoint : IEquatable<FixedPoint>
{
    public const int FractionBits = 16;
    public const int RawOne = 1 << FractionBits;
    public const int RawMax = int.MaxValue;
    public const int RawMin = int.MinValue;

    public int Raw { get; }

    public FixedPoint(int raw) => Raw = raw;

    public static FixedPoint FromInt(int value) => new(value << FractionBits);

    public static FixedPoint FromDouble(double value) =>
        new((int)System.Math.Round(value * RawOne, MidpointRounding.AwayFromZero));

    public int ToInt() => Raw >> FractionBits;

    public double ToDouble() => Raw / (double)RawOne;

    public static FixedPoint operator +(FixedPoint a, FixedPoint b) => new(a.Raw + b.Raw);
    public static FixedPoint operator -(FixedPoint a, FixedPoint b) => new(a.Raw - b.Raw);
    public static FixedPoint operator *(FixedPoint a, FixedPoint b) => new((int)((long)a.Raw * b.Raw >> FractionBits));
    /// <summary>
    /// PICO-8 division. Division by zero gives 0x7fff.ffff when the dividend is &gt;= 0 and
    /// -0x7fff.ffff when it is negative (PICO-8 manual). Other overflows wrap to 32 bits.
    /// </summary>
    public static FixedPoint operator /(FixedPoint a, FixedPoint b) =>
        b.Raw == 0
            ? new(a.Raw >= 0 ? RawMax : -RawMax)
            : new(unchecked((int)(((long)a.Raw << FractionBits) / b.Raw)));

    /// <summary>
    /// Lua/PICO-8 modulo: <c>a - flr(a/b)*b</c>, so the result takes the divisor's sign.
    /// <c>x % 0</c> gives 0.
    /// </summary>
    public static FixedPoint operator %(FixedPoint a, FixedPoint b)
    {
        if (b.Raw == 0) return new(0);
        var r = (long)a.Raw % b.Raw;
        if (r != 0 && (r < 0) != (b.Raw < 0)) r += b.Raw;
        return new((int)r);
    }

    public static FixedPoint operator -(FixedPoint a) => new(-a.Raw);

    public static bool operator <(FixedPoint a, FixedPoint b) => a.Raw < b.Raw;
    public static bool operator >(FixedPoint a, FixedPoint b) => a.Raw > b.Raw;
    public static bool operator <=(FixedPoint a, FixedPoint b) => a.Raw <= b.Raw;
    public static bool operator >=(FixedPoint a, FixedPoint b) => a.Raw >= b.Raw;

    public bool Equals(FixedPoint other) => Raw == other.Raw;
    public override bool Equals(object? obj) => obj is FixedPoint fp && Equals(fp);
    public override int GetHashCode() => Raw;
    public override string ToString() =>
        ToDouble().ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

    public static FixedPoint Abs(FixedPoint x) => new(System.Math.Abs(x.Raw));
    public static FixedPoint Min(FixedPoint a, FixedPoint b) => a.Raw <= b.Raw ? a : b;
    public static FixedPoint Max(FixedPoint a, FixedPoint b) => a.Raw >= b.Raw ? a : b;
    public static FixedPoint Mid(FixedPoint a, FixedPoint b, FixedPoint c) =>
        Max(Min(a, b), Min(Max(a, b), c));

    public static FixedPoint Floor(FixedPoint x) => new(x.Raw & ~((1 << FractionBits) - 1));

    public static int Sgn(FixedPoint x) => x.Raw >= 0 ? 1 : -1;

    public static FixedPoint Sqrt(FixedPoint x)
    {
        if (x.Raw <= 0) return FromInt(0);
        var result = 0L;
        var num = (long)x.Raw << FractionBits;
        var bit = 1L << 62;
        while ((bit & num) == 0 && bit > 1) bit >>= 2;
        while (bit != 0)
        {
            if (num >= result + bit)
            {
                num -= result + bit;
                result = (result >> 1) + bit;
            }
            else
            {
                result >>= 1;
            }
            bit >>= 2;
        }
        return new((int)result);
    }

    // Pico-8 sin: input [0..1], output [0..1], inverted (y-down)
    public static FixedPoint Sin(FixedPoint x)
    {
        var t = x.Raw & (RawOne - 1);
        var quadrant = (x.Raw >> FractionBits) & 3;
        var angle = quadrant switch
        {
            0 => t,
            1 => RawOne - t,
            2 => -t,
            _ => -(RawOne - t)
        };
        var a = angle / (double)RawOne * System.Math.PI * 2;
        var s = System.Math.Sin(a);
        var raw = (int)System.Math.Round(s * RawOne, MidpointRounding.AwayFromZero);
        return new(-raw);
    }

    public static FixedPoint Cos(FixedPoint x) => Sin(x + FromDouble(0.25));

    public static FixedPoint Atan2(FixedPoint dy, FixedPoint dx)
    {
        if (dx.Raw == 0 && dy.Raw == 0) return FromInt(0);
        var angle = System.Math.Atan2(dy.ToDouble(), dx.ToDouble());
        if (angle < 0) angle += System.Math.PI * 2;
        return FromDouble(angle / (System.Math.PI * 2));
    }

    public static int Band(int a, int b) => a & b;
    public static int Bor(int a, int b) => a | b;
    public static int Bxor(int a, int b) => a ^ b;
    public static int Bnot(int a) => ~a;
    public static int Shl(int a, int b) => a << (b & 31);
    public static int Shr(int a, int b) => a >> (b & 31);

    /// <summary>PICO-8 <c>\</c>: <c>flr(a / b)</c>, including the division-by-zero rule.</summary>
    public static FixedPoint IntDivide(FixedPoint a, FixedPoint b) => Floor(a / b);

    /// <summary>
    /// PICO-8 <c>^</c>. Computed in floating point (as z8lua does) and wrapped to 32 bits.
    /// </summary>
    public static FixedPoint Pow(FixedPoint a, FixedPoint b) =>
        FromDoubleWrapped(System.Math.Pow(a.ToDouble(), b.ToDouble()));

    /// <summary>Converts a double to 16.16, wrapping to 32 bits instead of being undefined on overflow.</summary>
    public static FixedPoint FromDoubleWrapped(double value)
    {
        if (double.IsNaN(value)) return new(0);
        var scaled = System.Math.Round(value * RawOne, MidpointRounding.AwayFromZero);
        if (scaled >= long.MaxValue) return new(RawMax);
        if (scaled <= long.MinValue) return new(RawMin);
        return new(unchecked((int)(long)scaled));
    }

    // Raw 32-bit shifts and rotations (PICO-8 shl/shr/lshr/rotl/rotr and << >> >>> <<> >><).
    // The amount is an integer number of bits; negative amounts shift the other way.

    public static FixedPoint ShiftLeft(FixedPoint x, int n) =>
        n < 0 ? ShiftRightArithmetic(x, -n) : n >= 32 ? new(0) : new(x.Raw << n);

    public static FixedPoint ShiftRightArithmetic(FixedPoint x, int n) =>
        n < 0 ? ShiftLeft(x, -n) : n >= 32 ? new(x.Raw < 0 ? -1 : 0) : new(x.Raw >> n);

    public static FixedPoint ShiftRightLogical(FixedPoint x, int n) =>
        n < 0 ? ShiftLeft(x, -n) : n >= 32 ? new(0) : new((int)((uint)x.Raw >> n));

    public static FixedPoint RotateLeft(FixedPoint x, int n) =>
        new((int)System.Numerics.BitOperations.RotateLeft((uint)x.Raw, n & 31));

    public static FixedPoint RotateRight(FixedPoint x, int n) =>
        new((int)System.Numerics.BitOperations.RotateRight((uint)x.Raw, n & 31));
}
