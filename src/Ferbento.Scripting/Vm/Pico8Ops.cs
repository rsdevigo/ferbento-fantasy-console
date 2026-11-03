using System.Globalization;
using Ferbento.Core.Math;
using Ferbento.Scripting.Interpreter;

namespace Ferbento.Scripting.Vm;

/// <summary>
/// The value semantics of PICO-8 Lua, in one place: truthiness, 16.16 arithmetic, bitwise
/// operators, comparison, equality, concatenation, length and indexing. The VM only calls
/// these operations; their tests are the executable specification of the behavior
/// (ADR-0001). Errors are <see cref="Pico8RuntimeException"/> with Lua's wording.
/// </summary>
public static class Pico8Ops
{
    // ---- Truthiness -------------------------------------------------------------------------

    /// <summary>Only <c>nil</c> and <c>false</c> are false; <c>0</c> and <c>""</c> are true.</summary>
    public static bool IsTruthy(ScriptValue v) => v.Type switch
    {
        ScriptValueType.Nil => false,
        ScriptValueType.Bool => v.AsBool(),
        _ => true
    };

    // ---- Arithmetic -------------------------------------------------------------------------

    public static ScriptValue Add(ScriptValue a, ScriptValue b) => Arith(a, b, (x, y) => x + y);
    public static ScriptValue Sub(ScriptValue a, ScriptValue b) => Arith(a, b, (x, y) => x - y);
    public static ScriptValue Mul(ScriptValue a, ScriptValue b) => Arith(a, b, (x, y) => x * y);

    /// <summary><c>/</c>: division by zero gives ±0x7fff.ffff (see <see cref="FixedPoint"/>).</summary>
    public static ScriptValue Div(ScriptValue a, ScriptValue b) => Arith(a, b, (x, y) => x / y);

    /// <summary><c>%</c>: floor modulo, the result takes the divisor's sign.</summary>
    public static ScriptValue Mod(ScriptValue a, ScriptValue b) => Arith(a, b, (x, y) => x % y);

    /// <summary><c>\</c>: <c>flr(a / b)</c>.</summary>
    public static ScriptValue IntDiv(ScriptValue a, ScriptValue b) => Arith(a, b, FixedPoint.IntDivide);

    /// <summary><c>^</c>: power (computed in floating point, like z8lua).</summary>
    public static ScriptValue Pow(ScriptValue a, ScriptValue b) => Arith(a, b, FixedPoint.Pow);

    /// <summary>Unary minus.</summary>
    public static ScriptValue Neg(ScriptValue a) =>
        ScriptValue.Number(-ToArithNumber(a));

    // ---- Bitwise (on the 32 raw bits of 16.16) ------------------------------------------------

    public static ScriptValue BAnd(ScriptValue a, ScriptValue b) => Bits(a, b, (x, y) => x & y);
    public static ScriptValue BOr(ScriptValue a, ScriptValue b) => Bits(a, b, (x, y) => x | y);

    /// <summary><c>^^</c> (xor).</summary>
    public static ScriptValue BXor(ScriptValue a, ScriptValue b) => Bits(a, b, (x, y) => x ^ y);

    /// <summary>Unary <c>~</c>.</summary>
    public static ScriptValue BNot(ScriptValue a) =>
        ScriptValue.Number(new FixedPoint(~ToBitsNumber(a).Raw));

    /// <summary><c>&lt;&lt;</c></summary>
    public static ScriptValue Shl(ScriptValue a, ScriptValue b) => Shift(a, b, FixedPoint.ShiftLeft);

    /// <summary><c>&gt;&gt;</c> (arithmetic).</summary>
    public static ScriptValue Shr(ScriptValue a, ScriptValue b) => Shift(a, b, FixedPoint.ShiftRightArithmetic);

    /// <summary><c>&gt;&gt;&gt;</c> (logical).</summary>
    public static ScriptValue Lshr(ScriptValue a, ScriptValue b) => Shift(a, b, FixedPoint.ShiftRightLogical);

    /// <summary><c>&lt;&lt;&gt;</c></summary>
    public static ScriptValue Rotl(ScriptValue a, ScriptValue b) => Shift(a, b, FixedPoint.RotateLeft);

    /// <summary><c>&gt;&gt;&lt;</c></summary>
    public static ScriptValue Rotr(ScriptValue a, ScriptValue b) => Shift(a, b, FixedPoint.RotateRight);

    // ---- Comparison and equality ----------------------------------------------------------

    /// <summary>
    /// <c>==</c>: values of different types are never equal; numbers compare by value,
    /// strings by content, and tables, functions and coroutines by reference.
    /// </summary>
    public static bool RawEquals(ScriptValue a, ScriptValue b)
    {
        if (a.Type != b.Type) return false;
        return a.Type switch
        {
            ScriptValueType.Nil => true,
            ScriptValueType.Number => a.AsNumber().Raw == b.AsNumber().Raw,
            ScriptValueType.String => string.Equals(a.AsString(), b.AsString(), StringComparison.Ordinal),
            ScriptValueType.Bool => a.AsBool() == b.AsBool(),
            _ => ReferenceEquals(a.Data, b.Data)
        };
    }

    /// <summary><c>&lt;</c>: numbers with numbers, strings with strings (byte order); else an error.</summary>
    public static bool LessThan(ScriptValue a, ScriptValue b) => Compare(a, b) < 0;

    /// <summary><c>&lt;=</c></summary>
    public static bool LessEqual(ScriptValue a, ScriptValue b) => Compare(a, b) <= 0;

    // ---- Strings ----------------------------------------------------------------------------

    /// <summary><c>..</c>: strings and numbers only; numbers use the PICO-8 <c>tostr</c> format.</summary>
    public static ScriptValue Concat(ScriptValue a, ScriptValue b) =>
        ScriptValue.Str(ToConcatString(a) + ToConcatString(b));

    /// <summary>
    /// Formats a number like PICO-8's <c>tostr</c>: integers without decimals, otherwise up to
    /// four decimal places with trailing zeros removed (invariant culture).
    /// </summary>
    // TODO conferir no PICO-8/z8lua: rounding at the 4th decimal and values that round to an
    // integer (e.g. 32767.99999) or to zero (0x0.0001).
    public static string NumberToString(FixedPoint n)
    {
        if ((n.Raw & (FixedPoint.RawOne - 1)) == 0)
            return (n.Raw >> FixedPoint.FractionBits).ToString(CultureInfo.InvariantCulture);

        var s = n.ToDouble().ToString("0.0000", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
        return s == "-0" ? "0" : s;
    }

    /// <summary><c>#</c>: the border of a table's array part, or the length of a string.</summary>
    public static ScriptValue Length(ScriptValue a) => a.Type switch
    {
        ScriptValueType.String => ScriptValue.Number(a.AsString().Length),
        ScriptValueType.Table => ScriptValue.Number(a.AsTable().Length),
        _ => throw new Pico8RuntimeException($"attempt to get length of a {TypeName(a)} value")
    };

    // ---- Tables -----------------------------------------------------------------------------

    /// <summary><c>t[k]</c>: indexing a value that is not a table is an error.</summary>
    public static ScriptValue Index(ScriptValue t, ScriptValue key) =>
        t.Type == ScriptValueType.Table
            ? t.AsTable().Get(key)
            : throw new Pico8RuntimeException($"attempt to index a {TypeName(t)} value");

    /// <summary><c>t[k] = v</c>: a nil value removes the key; a nil key is an error.</summary>
    public static void SetIndex(ScriptValue t, ScriptValue key, ScriptValue value)
    {
        if (t.Type != ScriptValueType.Table)
            throw new Pico8RuntimeException($"attempt to index a {TypeName(t)} value");
        t.AsTable().Set(key, value);
    }

    // ---- Types ------------------------------------------------------------------------------

    /// <summary>The name <c>type()</c> returns: nil, number, string, boolean, table, function, thread.</summary>
    public static string TypeName(ScriptValue v) => v.Type switch
    {
        ScriptValueType.Nil => "nil",
        ScriptValueType.Number => "number",
        ScriptValueType.String => "string",
        ScriptValueType.Bool => "boolean",
        ScriptValueType.Table => "table",
        ScriptValueType.Function or ScriptValueType.NativeFunction => "function",
        ScriptValueType.Coroutine => "thread",
        _ => "userdata"
    };

    /// <summary>
    /// Lua's string-to-number coercion used by arithmetic: decimal, <c>0x</c> hex (with an
    /// optional fractional part) and <c>0b</c> binary, with optional sign and surrounding spaces.
    /// </summary>
    // TODO conferir no PICO-8/z8lua: exact set of accepted formats (exponents, "1." , ".5").
    public static bool TryParseNumber(string s, out FixedPoint result)
    {
        result = default;
        var text = s.Trim();
        if (text.Length == 0) return false;

        var negative = false;
        if (text[0] is '-' or '+')
        {
            negative = text[0] == '-';
            text = text[1..];
        }

        FixedPoint value;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseRadix(text[2..], 16, out value)) return false;
        }
        else if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseRadix(text[2..], 2, out value)) return false;
        }
        else
        {
            if (text.Length == 0 || !(char.IsAsciiDigit(text[0]) || text[0] == '.')) return false;
            if (!double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d))
                return false;
            value = FixedPoint.FromDoubleWrapped(d);
        }

        result = negative ? -value : value;
        return true;
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static ScriptValue Arith(ScriptValue a, ScriptValue b, Func<FixedPoint, FixedPoint, FixedPoint> op)
    {
        var x = ToArithNumber(a);
        var y = ToArithNumber(b);
        return ScriptValue.Number(op(x, y));
    }

    private static ScriptValue Bits(ScriptValue a, ScriptValue b, Func<int, int, int> op) =>
        ScriptValue.Number(new FixedPoint(op(ToBitsNumber(a).Raw, ToBitsNumber(b).Raw)));

    private static ScriptValue Shift(ScriptValue a, ScriptValue b, Func<FixedPoint, int, FixedPoint> op)
    {
        var x = ToBitsNumber(a);
        var n = ToBitsNumber(b).Raw >> FixedPoint.FractionBits; // integer part, floored
        return ScriptValue.Number(op(x, n));
    }

    private static FixedPoint ToArithNumber(ScriptValue v)
    {
        if (TryCoerceNumber(v, out var n)) return n;
        throw new Pico8RuntimeException($"attempt to perform arithmetic on a {TypeName(v)} value");
    }

    private static FixedPoint ToBitsNumber(ScriptValue v)
    {
        if (TryCoerceNumber(v, out var n)) return n;
        // TODO conferir no PICO-8/z8lua: wording of the error for bitwise operators.
        throw new Pico8RuntimeException($"attempt to perform bitwise operation on a {TypeName(v)} value");
    }

    private static bool TryCoerceNumber(ScriptValue v, out FixedPoint n)
    {
        if (v.Type == ScriptValueType.Number)
        {
            n = v.AsNumber();
            return true;
        }
        if (v.Type == ScriptValueType.String) return TryParseNumber(v.AsString(), out n);
        n = default;
        return false;
    }

    private static int Compare(ScriptValue a, ScriptValue b)
    {
        if (a.Type == ScriptValueType.Number && b.Type == ScriptValueType.Number)
            return a.AsNumber().Raw.CompareTo(b.AsNumber().Raw);
        if (a.Type == ScriptValueType.String && b.Type == ScriptValueType.String)
            return Math.Sign(string.CompareOrdinal(a.AsString(), b.AsString()));

        var ta = TypeName(a);
        var tb = TypeName(b);
        throw new Pico8RuntimeException(ta == tb
            ? $"attempt to compare two {ta} values"
            : $"attempt to compare {ta} with {tb}");
    }

    private static string ToConcatString(ScriptValue v) => v.Type switch
    {
        ScriptValueType.String => v.AsString(),
        ScriptValueType.Number => NumberToString(v.AsNumber()),
        _ => throw new Pico8RuntimeException($"attempt to concatenate a {TypeName(v)} value")
    };

    private static bool TryParseRadix(string digits, int radix, out FixedPoint value)
    {
        value = default;
        var parts = digits.Split('.');
        if (parts.Length > 2 || (parts[0].Length == 0 && (parts.Length == 1 || parts[1].Length == 0)))
            return false;

        long intPart = 0;
        foreach (var c in parts[0])
        {
            var d = DigitValue(c);
            if (d < 0 || d >= radix) return false;
            intPart = (intPart * radix + d) & 0xFFFF_FFFF;
        }

        double frac = 0, scale = 1.0 / radix;
        if (parts.Length == 2)
        {
            foreach (var c in parts[1])
            {
                var d = DigitValue(c);
                if (d < 0 || d >= radix) return false;
                frac += d * scale;
                scale /= radix;
            }
        }

        var raw = unchecked((int)((intPart << FixedPoint.FractionBits) + (long)(frac * FixedPoint.RawOne)));
        value = new FixedPoint(raw);
        return true;
    }

    private static int DigitValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1
    };
}
