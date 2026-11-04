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

    // Longest first, so that ">>>=" wins over ">>>", ">>" and ">".
    private static readonly (string Text, TokenKind Kind)[] Symbols =
    [
        (">>>=", TokenKind.ShiftRightLogicalAssign), ("<<>=", TokenKind.RotateLeftAssign),
        (">><=", TokenKind.RotateRightAssign),
        ("...", TokenKind.Ellipsis), ("..=", TokenKind.ConcatAssign), (">>>", TokenKind.ShiftRightLogical),
        ("<<>", TokenKind.RotateLeft), (">><", TokenKind.RotateRight), ("^^=", TokenKind.BitXorAssign),
        ("<<=", TokenKind.ShiftLeftAssign), (">>=", TokenKind.ShiftRightAssign),
        ("..", TokenKind.DotDot), ("::", TokenKind.DoubleColon), ("==", TokenKind.EqualEqual),
        ("~=", TokenKind.TildeEqual), ("!=", TokenKind.TildeEqual), ("<=", TokenKind.LessEqual),
        (">=", TokenKind.GreaterEqual), ("<<", TokenKind.ShiftLeft), (">>", TokenKind.ShiftRight),
        ("^^", TokenKind.CaretCaret),
        ("+=", TokenKind.PlusAssign), ("-=", TokenKind.MinusAssign), ("*=", TokenKind.StarAssign),
        ("/=", TokenKind.SlashAssign), ("\\=", TokenKind.BackslashAssign), ("%=", TokenKind.PercentAssign),
        ("^=", TokenKind.CaretAssign), ("&=", TokenKind.AmpersandAssign), ("|=", TokenKind.PipeAssign),
        ("@", TokenKind.At), ("$", TokenKind.Dollar), ("?", TokenKind.Question),
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
    private bool _newlineSinceLastToken;

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

    private void Emit(Token token)
    {
        _tokens.Add(token with { NewlineBefore = _newlineSinceLastToken });
        _newlineSinceLastToken = false;
    }

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

        Emit(new Token(TokenKind.EndOfFile, "", CurrentSpan, _pos, 0));
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
            if (SkipNewline())
            {
                // Newlines inside long comments and strings do not count: they never end a short 'if'.
                _newlineSinceLastToken = true;
                continue;
            }
            if (c is ' ' or '\t' or '\f' or '\v')
            {
                _pos++;
            }
            else if (c == '/' && Peek(1) == '/')
            {
                // PICO-8 line comment.
                while (!AtEnd && Current is not ('\n' or '\r')) _pos++;
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
        Emit(new Token(kind, text, span, start, _pos - start, kind == TokenKind.Name ? text : null));
    }

    private bool LexSymbol(int start, SourceSpan span)
    {
        foreach (var (text, kind) in Symbols)
        {
            if (string.CompareOrdinal(_source, _pos, text, 0, text.Length) != 0) continue;
            _pos += text.Length;
            Emit(new Token(kind, text, span, start, text.Length));
            return true;
        }
        return false;
    }

    // ---- numbers ----
    // Same rules as z8lua (llex.c read_numeral, lobject.c luaO_str2d): no exponents,
    // hex (0x) and binary (0b) are exact, decimals are truncated to 16.16.

    [GeneratedRegex(@"^(?:[0-9]+\.?[0-9]*|\.[0-9]+)$")]
    private static partial Regex DecimalNumber();

    [GeneratedRegex(@"^0[xX](?<int>[0-9a-fA-F]*)(?:\.(?<frac>[0-9a-fA-F]*))?$")]
    private static partial Regex HexNumber();

    [GeneratedRegex(@"^0[bB](?<int>[01]*)(?:\.(?<frac>[01]*))?$")]
    private static partial Regex BinaryNumber();

    private void LexNumber(int start, SourceSpan span)
    {
        var hex = false;
        if (Current == '0' && Peek(1) is 'x' or 'X')
        {
            hex = true;
            _pos += 2;
        }
        else if (Current == '0' && Peek(1) is 'b' or 'B')
        {
            _pos += 2;
        }

        while ((hex ? char.IsAsciiHexDigit(Current) : char.IsAsciiDigit(Current)) || Current == '.') _pos++;

        var text = _source[start.._pos];
        var value = ParseNumber(text, span);
        Emit(new Token(TokenKind.Number, text, span, start, _pos - start, NumberValue: value ?? default));
    }

    private FixedPoint? ParseNumber(string text, SourceSpan span)
    {
        if (HexNumber().Match(text) is { Success: true } hex && HasDigits(hex))
            return ParseRadix(hex, bitsPerDigit: 4, maxFractionDigits: 4);

        if (BinaryNumber().Match(text) is { Success: true } binary && HasDigits(binary))
            return ParseRadix(binary, bitsPerDigit: 1, maxFractionDigits: 16);

        if (DecimalNumber().IsMatch(text))
            return ParseDecimal(text, span);

        Report(DiagnosticCatalog.MalformedNumber, span, text);
        return null;
    }

    private static bool HasDigits(Match match) => match.Groups["int"].Length > 0 || match.Groups["frac"].Length > 0;

    /// <summary>
    /// Hex and binary are exact: the integer part wraps to 16 bits (<c>0xffff</c> is -1), and only the
    /// first 4 hex (or 16 binary) fraction digits count; the rest are ignored, as in z8lua.
    /// </summary>
    private static FixedPoint ParseRadix(Match match, int bitsPerDigit, int maxFractionDigits)
    {
        uint integer = 0;
        foreach (var digit in match.Groups["int"].Value)
            integer = ((integer << bitsPerDigit) | (uint)HexValue(digit)) & 0xFFFF;

        var fractionDigits = match.Groups["frac"].Value;
        if (fractionDigits.Length > maxFractionDigits) fractionDigits = fractionDigits[..maxFractionDigits];
        uint fraction = 0;
        foreach (var digit in fractionDigits) fraction = (fraction << bitsPerDigit) | (uint)HexValue(digit);
        fraction <<= 16 - fractionDigits.Length * bitsPerDigit;

        return new FixedPoint(unchecked((int)((integer << 16) | fraction)));
    }

    /// <summary>
    /// Decimal goes through a double and is truncated toward zero, like z8lua's
    /// <c>fix32(double)</c>: <c>0.1</c> is <c>0x0.1999</c>. It wraps around (<c>32768</c> is -32768).
    /// </summary>
    private FixedPoint? ParseDecimal(string text, SourceSpan span)
    {
        var scaled = double.Parse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture) * FixedPoint.RawOne;
        if (scaled >= 9.2e18)
        {
            Report(DiagnosticCatalog.NumberOutOfRange, span, text);
            return null;
        }
        return new FixedPoint(unchecked((int)(long)scaled));
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
        Emit(new Token(TokenKind.String, _source[start.._pos], span, start, _pos - start, content));
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
        Emit(new Token(TokenKind.String, _source[start.._pos], span, start, _pos - start, sb.ToString()));
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
            // P8SCII control codes, same table as z8lua.
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
