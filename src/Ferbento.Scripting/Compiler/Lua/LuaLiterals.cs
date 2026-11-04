using System.Globalization;
using System.Text;
using Ferbento.Core.Math;

namespace Ferbento.Scripting.Compiler.Lua;

/// <summary>Formatting of literals and identifiers for PICO-8 Lua source.</summary>
public static class LuaLiterals
{
    /// <summary>Lua 5.2 reserved words (PICO-8 uses the same set).</summary>
    public static readonly IReadOnlySet<string> ReservedWords = new HashSet<string>(StringComparer.Ordinal)
    {
        "and", "break", "do", "else", "elseif", "end", "false", "for", "function", "goto", "if",
        "in", "local", "nil", "not", "or", "repeat", "return", "then", "true", "until", "while",
    };

    // 5^16: frac/65536 == frac * 5^16 / 10^16, so this yields the exact decimal digits.
    private const long FivePow16 = 152_587_890_625L;

    /// <summary>Longest fractional part (in decimal digits) printed in decimal; longer ones use hex.</summary>
    public const int MaxDecimalFractionDigits = 5;

    public static bool IsIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name) || ReservedWords.Contains(name)) return false;
        if (!(char.IsAsciiLetter(name[0]) || name[0] == '_')) return false;
        foreach (var c in name)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_')) return false;
        }
        return true;
    }

    /// <summary>
    /// Prints a 16.16 value so that PICO-8 reads back exactly the same bits.
    /// Integers and fractions with an exact decimal of up to <see cref="MaxDecimalFractionDigits"/>
    /// digits are printed in decimal; anything else in hex (<c>0x1.8</c>), which is always exact.
    /// </summary>
    public static string FormatNumber(FixedPoint value)
    {
        var raw = value.Raw;

        // -32768 has no positive counterpart in 16.16; the hex literal 0x8000 wraps to it.
        if (raw == int.MinValue) return "0x8000";

        var negative = raw < 0;
        var magnitude = System.Math.Abs((long)raw);
        var integer = magnitude >> FixedPoint.FractionBits;
        var fraction = magnitude & (FixedPoint.RawOne - 1);
        var sign = negative ? "-" : "";

        if (fraction == 0)
            return sign + integer.ToString(CultureInfo.InvariantCulture);

        var decimalDigits = (fraction * FivePow16).ToString("D16", CultureInfo.InvariantCulture).TrimEnd('0');
        if (decimalDigits.Length <= MaxDecimalFractionDigits)
            return $"{sign}{integer.ToString(CultureInfo.InvariantCulture)}.{decimalDigits}";

        var hexFraction = fraction.ToString("x4", CultureInfo.InvariantCulture).TrimEnd('0');
        return $"{sign}0x{integer.ToString("x", CultureInfo.InvariantCulture)}.{hexFraction}";
    }

    /// <summary>
    /// Prints a double-quoted string. Characters must be P8SCII bytes (0..255); printable ASCII is
    /// kept, everything else is escaped (<c>\n</c>, <c>\t</c>, <c>\r</c>, <c>\"</c>, <c>\\</c>, <c>\xNN</c>).
    /// </summary>
    public static string FormatString(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\r': sb.Append("\\r"); break;
                default:
                    if (c > 0xFF)
                        throw new ArgumentException(
                            $"Character U+{(int)c:X4} is not a P8SCII byte and cannot be written to a PICO-8 string.",
                            nameof(value));
                    if (c < 0x20 || c > 0x7E)
                        sb.Append("\\x").Append(((int)c).ToString("x2", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
