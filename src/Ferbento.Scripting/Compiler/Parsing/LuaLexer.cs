using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ferbento.Core.Math;
using Ferbento.Scripting.Compiler.Diagnostics;
using Ferbento.Scripting.Compiler.Ir;

namespace Ferbento.Scripting.Compiler.Parsing;

/// <summary>
/// Turns PICO-8 Lua source into tokens. Errors become diagnostics and lexing goes on, so one
/// bad character does not hide the rest of the file. The token list always ends with
/// <see cref="TokenKind.EndOfFile"/>.
/// </summary>
public sealed partial class LuaLexer
{
    private static readonly Dictionary<string, TokenKind> Keywords = new(StringComparer.Ordinal)
    {
        ["and"] = TokenKind.And, ["break"] = TokenKind.Break, ["do"] = TokenKind.Do,
        ["else"] = TokenKind.Else, ["elseif"] = TokenKind.Elseif, ["end"] = TokenKind.End,
        ["false"] = TokenKind.False, ["for"] = TokenKind.For, ["function"] = TokenKind.Function,
        ["goto"] = TokenKind.Goto, ["if"] = TokenKind.If, ["in"] = TokenKind.In,
        ["local"] = TokenKind.Local, ["nil"] = TokenKind.Nil, ["not"] = TokenKind.Not,
        ["or"] = TokenKind.Or, ["repeat"] = TokenKind.Repeat, ["return"] = TokenKind.Return,
        ["then"] = TokenKind.Then, ["true"] = TokenKind.True, ["until"] = TokenKind.Until,
        ["while"] = TokenKind.While,
    };

    // Longest first, so that ">>>" wins over ">>" and ">".
    private static readonly (string Text, TokenKind Kind)[] Symbols =
    [
        ("...", TokenKind.Ellipsis), (">>>", TokenKind.ShiftRightLogical), ("<<>", TokenKind.RotateLeft),
        (">><", TokenKind.RotateRight),
        ("..", TokenKind.DotDot), ("::", TokenKind.DoubleColon), ("==", TokenKind.EqualEqual),
        ("~=", TokenKind.TildeEqual), ("<=", TokenKind.LessEqual), (">=", TokenKind.GreaterEqual),
        ("<<", TokenKind.ShiftLeft), (">>", TokenKind.ShiftRight), ("^^", TokenKind.CaretCaret),
        ("+", TokenKind.Plus), ("-", TokenKind.Minus), ("*", TokenKind.Star), ("/", TokenKind.Slash),
        ("\\", TokenKind.Backslash), ("%", TokenKind.Percent), ("^", TokenKind.Caret), ("#", TokenKind.Hash),
        ("&", TokenKind.Ampersand), ("|", TokenKind.Pipe), ("~", TokenKind.Tilde), ("<", TokenKind.Less),
        (">", TokenKind.Greater), ("=", TokenKind.Assign), ("(", TokenKind.LeftParen),
        (")", TokenKind.RightParen), ("{", TokenKind.LeftBrace), ("}", TokenKind.RightBrace),
        ("[", TokenKind.LeftBracket), ("]", TokenKind.RightBracket), (";", TokenKind.Semicolon),
        (":", TokenKind.Colon), (",", TokenKind.Comma), (".", TokenKind.Dot),
    ];

    private readonly string _source;
    private readonly string? _file;
    private readonly ImmutableArray<Token>.Builder _tokens = ImmutableArray.CreateBuilder<Token>();
    private readonly List<Diagnostic> _diagnostics;
    private int _pos;
    private int _line = 1;
    private int _lineStart;

    private LuaLexer(string source, string? file, List<Diagnostic> diagnostics)
    {
        _source = source;
        _file = file;
        _diagnostics = diagnostics;
    }

    /// <summary>Tokenizes <paramref name="source"/>, adding any errors to <paramref name="diagnostics"/>.</summary>
    public static ImmutableArray<Token> Tokenize(string source, string? file, List<Diagnostic> diagnostics)
    {
        var lexer = new LuaLexer(source, file, diagnostics);
        lexer.Run();
        return lexer._tokens.ToImmutable();
    }

    private char Current => _pos < _source.Length ? _source[_pos] : '\0';

    private char Peek(int offset) => _pos + offset < _source.Length ? _source[_pos + offset] : '\0';

    private bool AtEnd => _pos >= _source.Length;

    private SourceSpan SpanAt(int line, int lineStart, int offset) => new(_file, line, offset - lineStart + 1);

    private SourceSpan CurrentSpan => SpanAt(_line, _lineStart, _pos);

    private void Report(DiagnosticDescriptor descriptor, SourceSpan span, params object[] args) =>
        _diagnostics.Add(descriptor.Create(span, args));

    private void Run()
    {
        while (true)
        {
            SkipWhitespaceAndComments();
            if (AtEnd) break;

            var start = _pos;
            var span = CurrentSpan;
            var c = Current;

            if (char.IsAsciiLetter(c) || c == '_')
                LexName(start, span);
            else if (char.IsAsciiDigit(c) || (c == '.' && char.IsAsciiDigit(Peek(1))))
                LexNumber(start, span);
            else if (c is '"' or '\'')
                LexShortString(start, span, c);
            else if (c == '[' && LongBracketLevel() is { } level)
                LexLongString(start, span, level);
            else if (!LexSymbol(start, span))
            {
                Report(DiagnosticCatalog.UnexpectedCharacter, span, DescribeChar(c));
                _pos++;
            }
        }

        _tokens.Add(new Token(TokenKind.EndOfFile, "", CurrentSpan, _pos, 0));
    }

    private static string DescribeChar(char c) =>
        c < 0x20 || c == 0x7F ? $"U+{(int)c:X4}" : $"'{c}'";

    // ---- whitespace, newlines and comments ----

    /// <summary>Consumes one newline (\n, \r, \r\n or \n\r) if there is one at the current position.</summary>
    private bool SkipNewline()
    {
        var c = Current;
        if (c is not ('\n' or '\r')) return false;
        _pos++;
        var next = Current;
        if (next is '\n' or '\r' && next != c) _pos++;
        _line++;
        _lineStart = _pos;
        return true;
    }

    private void SkipWhitespaceAndComments()
    {
        while (!AtEnd)
        {
            var c = Current;
            if (SkipNewline()) continue;
            if (c is ' ' or '\t' or '\f' or '\v')
            {
                _pos++;
            }
            else if (c == '-' && Peek(1) == '-')
            {
                var span = CurrentSpan;
                _pos += 2;
                if (Current == '[' && LongBracketLevel() is { } level)
                {
                    if (!SkipLongBracket(level, out _))
                        Report(DiagnosticCatalog.UnterminatedLongComment, span, ClosingBracket(level));
                }
                else
                {
                    while (!AtEnd && Current is not ('\n' or '\r')) _pos++;
                }
            }
            else
            {
                return;
            }
        }
    }

    // ---- long brackets: [[ ]], [==[ ]==] ----

    /// <summary>At a '[': the level of a long bracket opening here (number of '='), or null.</summary>
    private int? LongBracketLevel()
    {
        var i = 1;
        while (Peek(i) == '=') i++;
        return Peek(i) == '[' ? i - 1 : null;
    }

    private static string ClosingBracket(int level) => "]" + new string('=', level) + "]";

    /// <summary>
    /// Consumes a long bracket whose opening starts at the current '['. Returns false (and consumes
    /// the rest of the source) if it is not closed. A newline right after the opening is skipped.
    /// </summary>
    private bool SkipLongBracket(int level, out string content)
    {
        _pos += level + 2;
        SkipNewline();
        var sb = new StringBuilder();
        var closing = ClosingBracket(level);
        while (!AtEnd)
        {
            if (Current == ']' && string.CompareOrdinal(_source, _pos, closing, 0, closing.Length) == 0)
            {
                _pos += closing.Length;
                content = sb.ToString();
                return true;
            }
            if (Current is '\n' or '\r')
            {
                SkipNewline();
                sb.Append('\n');
            }
            else
            {
                sb.Append(Current);
                _pos++;
            }
        }
        content = sb.ToString();
        return false;
    }

    // ---- names and symbols ----

    private void LexName(int start, SourceSpan span)
    {
        while (char.IsAsciiLetterOrDigit(Current) || Current == '_') _pos++;
        var text = _source[start.._pos];
        var kind = Keywords.GetValueOrDefault(text, TokenKind.Name);
        _tokens.Add(new Token(kind, text, span, start, _pos - start, kind == TokenKind.Name ? text : null));
    }

    private bool LexSymbol(int start, SourceSpan span)
    {
        foreach (var (text, kind) in Symbols)
        {
            if (string.CompareOrdinal(_source, _pos, text, 0, text.Length) != 0) continue;
            _pos += text.Length;
            _tokens.Add(new Token(kind, text, span, start, text.Length));
            return true;
        }
        return false;
    }

    // ---- numbers ----

    [GeneratedRegex(@"^(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$")]
    private static partial Regex DecimalNumber();

    [GeneratedRegex(@"^0[xX](?<int>[0-9a-fA-F]*)(?:\.(?<frac>[0-9a-fA-F]*))?(?:[pP](?<exp>[+-]?[0-9]+))?$")]
    private static partial Regex HexNumber();

    private void LexNumber(int start, SourceSpan span)
    {
        // Same scan as Lua's read_numeral: hex digits, dots and a signed exponent.
        var exponentChars = Current == '0' && Peek(1) is 'x' or 'X' ? "pP" : "eE";
        if (exponentChars == "pP") _pos += 2;
        while (true)
        {
            if (exponentChars.Contains(Current) && !AtEnd)
                _pos += Peek(1) is '+' or '-' ? 2 : 1;
            else if (char.IsAsciiHexDigit(Current) || Current == '.')
                _pos++;
            else
                break;
        }

        var text = _source[start.._pos];
        var value = ParseNumber(text, span);
        _tokens.Add(new Token(TokenKind.Number, text, span, start, _pos - start, NumberValue: value ?? default));
    }

    private FixedPoint? ParseNumber(string text, SourceSpan span)
    {
        var hex = HexNumber().Match(text);
        if (hex.Success && (hex.Groups["int"].Length > 0 || hex.Groups["frac"].Length > 0))
            return ParseHex(hex, text, span);

        if (DecimalNumber().IsMatch(text))
            return ParseDecimal(text, span);

        Report(DiagnosticCatalog.MalformedNumber, span, text);
        return null;
    }

    /// <summary>
    /// Hex is exact: the integer part wraps to 16 bits (<c>0xffff</c> is -1) and the first four
    /// fraction digits are the fraction bits. Further fraction digits are dropped.
    /// </summary>
    private FixedPoint? ParseHex(Match match, string text, SourceSpan span)
    {
        // TODO conferir no z8lua: se os dígitos de fração além do 4º são truncados ou arredondados.
        if (match.Groups["exp"].Success)
        {
            var mantissa = 0.0;
            foreach (var digit in match.Groups["int"].Value) mantissa = mantissa * 16 + HexValue(digit);
            var scale = 1.0 / 16;
            foreach (var digit in match.Groups["frac"].Value)
            {
                mantissa += HexValue(digit) * scale;
                scale /= 16;
            }
            var exponent = int.Parse(match.Groups["exp"].Value, CultureInfo.InvariantCulture);
            return WrapToFixed((decimal)Math.ScaleB(mantissa, exponent), text, span);
        }

        uint integer = 0;
        foreach (var digit in match.Groups["int"].Value) integer = ((integer << 4) | (uint)HexValue(digit)) & 0xFFFF;

        var fractionDigits = match.Groups["frac"].Value.PadRight(4, '0')[..4];
        uint fraction = 0;
        foreach (var digit in fractionDigits) fraction = (fraction << 4) | (uint)HexValue(digit);

        return new FixedPoint(unchecked((int)((integer << 16) | fraction)));
    }

    /// <summary>
    /// Decimal is rounded to the nearest 1/65536 and wraps around like PICO-8 (<c>32768</c> is -32768).
    /// </summary>
    private FixedPoint? ParseDecimal(string text, SourceSpan span)
    {
        // TODO conferir no z8lua: arredondamento (0.1 → 0x0.199a ou 0x0.1999) e o estouro de decimais grandes.
        if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            Report(DiagnosticCatalog.NumberOutOfRange, span, text);
            return null;
        }
        return WrapToFixed(value, text, span);
    }

    private FixedPoint? WrapToFixed(decimal value, string text, SourceSpan span)
    {
        try
        {
            var raw = decimal.Round(value * FixedPoint.RawOne, MidpointRounding.AwayFromZero);
            var wrapped = (long)(raw % 4_294_967_296m);
            return new FixedPoint(unchecked((int)wrapped));
        }
        catch (OverflowException)
        {
            Report(DiagnosticCatalog.NumberOutOfRange, span, text);
            return null;
        }
    }

    private static int HexValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        _ => c - 'A' + 10,
    };

    // ---- strings ----

    private void LexLongString(int start, SourceSpan span, int level)
    {
        if (!SkipLongBracket(level, out var content))
            Report(DiagnosticCatalog.UnterminatedLongString, span, ClosingBracket(level));
        _tokens.Add(new Token(TokenKind.String, _source[start.._pos], span, start, _pos - start, content));
    }

    private void LexShortString(int start, SourceSpan span, char quote)
    {
        _pos++;
        var sb = new StringBuilder();
        var closed = false;
        while (!AtEnd)
        {
            var c = Current;
            if (c == quote)
            {
                _pos++;
                closed = true;
                break;
            }
            if (c is '\n' or '\r') break;
            if (c == '\\')
            {
                LexEscape(sb);
                continue;
            }
            sb.Append(c);
            _pos++;
        }

        if (!closed) Report(DiagnosticCatalog.UnterminatedString, span, quote == '"' ? "\"" : "'");
        _tokens.Add(new Token(TokenKind.String, _source[start.._pos], span, start, _pos - start, sb.ToString()));
    }

    /// <summary>
    /// Escapes of Lua 5.2 plus the P8SCII control codes of PICO-8 (<c>\*</c> = 1 … <c>\^</c> = 6).
    /// </summary>
    private void LexEscape(StringBuilder sb)
    {
        var span = CurrentSpan;
        _pos++; // backslash
        var c = Current;
        switch (c)
        {
            case 'a': sb.Append('\a'); _pos++; return;
            case 'b': sb.Append('\b'); _pos++; return;
            case 'f': sb.Append('\f'); _pos++; return;
            case 'n': sb.Append('\n'); _pos++; return;
            case 'r': sb.Append('\r'); _pos++; return;
            case 't': sb.Append('\t'); _pos++; return;
            case 'v': sb.Append('\v'); _pos++; return;
            case '\\': sb.Append('\\'); _pos++; return;
            case '"': sb.Append('"'); _pos++; return;
            case '\'': sb.Append('\''); _pos++; return;
            // P8SCII control codes. TODO conferir no z8lua.
            case '*': sb.Append('\x01'); _pos++; return;
            case '#': sb.Append('\x02'); _pos++; return;
            case '-': sb.Append('\x03'); _pos++; return;
            case '|': sb.Append('\x04'); _pos++; return;
            case '+': sb.Append('\x05'); _pos++; return;
            case '^': sb.Append('\x06'); _pos++; return;
            case '\n' or '\r':
                SkipNewline();
                sb.Append('\n');
                return;
            case 'z':
                _pos++;
                while (!AtEnd && (char.IsWhiteSpace(Current)))
                {
                    if (!SkipNewline()) _pos++;
                }
                return;
            case 'x':
                if (char.IsAsciiHexDigit(Peek(1)) && char.IsAsciiHexDigit(Peek(2)))
                {
                    sb.Append((char)(HexValue(Peek(1)) * 16 + HexValue(Peek(2))));
                    _pos += 3;
                    return;
                }
                break;
            default:
                if (char.IsAsciiDigit(c))
                {
                    var digits = 0;
                    var value = 0;
                    while (digits < 3 && char.IsAsciiDigit(Current))
                    {
                        value = value * 10 + (Current - '0');
                        _pos++;
                        digits++;
                    }
                    if (value > 255)
                        Report(DiagnosticCatalog.DecimalEscapeTooLarge, span, value.ToString(CultureInfo.InvariantCulture));
                    else
                        sb.Append((char)value);
                    return;
                }
                break;
        }

        // Invalid escape: report it and keep the character, so the string still closes normally.
        if (AtEnd || c is '\n' or '\r') return;
        Report(DiagnosticCatalog.InvalidEscape, span, c);
        sb.Append(c);
        _pos++;
    }
}
