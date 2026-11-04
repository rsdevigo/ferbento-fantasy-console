using Ferbento.Core.Math;
using Ferbento.Scripting.Compiler.Diagnostics;
using Ferbento.Scripting.Compiler.Parsing;

namespace Ferbento.Scripting.Tests.Compiler;

public class LuaLexerTests
{
    private static (List<Token> Tokens, List<Diagnostic> Diagnostics) Lex(string source)
    {
        var diagnostics = new List<Diagnostic>();
        var tokens = LuaLexer.Tokenize(source, null, diagnostics).ToList();
        Assert.Equal(TokenKind.EndOfFile, tokens[^1].Kind);
        tokens.RemoveAt(tokens.Count - 1);
        return (tokens, diagnostics);
    }

    private static Token Single(string source)
    {
        var (tokens, diagnostics) = Lex(source);
        Assert.Empty(diagnostics);
        return Assert.Single(tokens);
    }

    private static int Raw(string number) => Single(number).NumberValue.Raw;

    private static string Str(string literal) => Single(literal).StringValue!;

    private static Diagnostic SingleError(string source)
    {
        var (_, diagnostics) = Lex(source);
        return Assert.Single(diagnostics);
    }

    // ---- names, keywords and symbols ----

    [Fact]
    public void Names_And_Keywords()
    {
        var (tokens, diagnostics) = Lex("local _x1 = endx end");
        Assert.Empty(diagnostics);
        Assert.Equal(
            [TokenKind.Local, TokenKind.Name, TokenKind.Assign, TokenKind.Name, TokenKind.End],
            tokens.Select(t => t.Kind));
        Assert.Equal("_x1", tokens[1].StringValue);
        Assert.Equal("endx", tokens[3].StringValue);
    }

    [Theory]
    [InlineData("...", TokenKind.Ellipsis)]
    [InlineData("..", TokenKind.DotDot)]
    [InlineData(">>>", TokenKind.ShiftRightLogical)]
    [InlineData("<<>", TokenKind.RotateLeft)]
    [InlineData(">><", TokenKind.RotateRight)]
    [InlineData(">>", TokenKind.ShiftRight)]
    [InlineData("<<", TokenKind.ShiftLeft)]
    [InlineData("^^", TokenKind.CaretCaret)]
    [InlineData("~=", TokenKind.TildeEqual)]
    [InlineData("::", TokenKind.DoubleColon)]
    [InlineData("\\", TokenKind.Backslash)]
    [InlineData("<=", TokenKind.LessEqual)]
    public void Symbols_LongestMatchWins(string text, TokenKind kind) => Assert.Equal(kind, Single(text).Kind);

    [Fact]
    public void Symbols_AdjacentWithoutSpaces()
    {
        var (tokens, _) = Lex("a.b:c(...)..[[s]]");
        Assert.Equal(
            [TokenKind.Name, TokenKind.Dot, TokenKind.Name, TokenKind.Colon, TokenKind.Name, TokenKind.LeftParen,
             TokenKind.Ellipsis, TokenKind.RightParen, TokenKind.DotDot, TokenKind.String],
            tokens.Select(t => t.Kind));
    }

    // ---- numbers ----

    [Theory]
    [InlineData("0", 0)]
    [InlineData("42", 42 << 16)]
    [InlineData("1.5", 0x18000)]
    [InlineData(".5", 0x8000)]
    [InlineData("5.", 5 << 16)]
    [InlineData("32767", 32767 << 16)]
    [InlineData("1e2", 100 << 16)]
    [InlineData("25e-1", 0x28000)]
    public void Numbers_Decimal(string text, int raw) => Assert.Equal(raw, Raw(text));

    [Fact]
    public void Numbers_Decimal_WrapAroundLikePico8()
    {
        Assert.Equal(int.MinValue, Raw("32768"));
        Assert.Equal(-1 << 16, Raw("65535"));
        Assert.Equal(0, Raw("65536"));
    }

    [Fact]
    public void Numbers_Decimal_RoundToNearest()
    {
        // 0.1 * 65536 = 6553.6. TODO conferir no z8lua (arredonda ou trunca?).
        Assert.Equal(0x199a, Raw("0.1"));
        Assert.Equal(1, Raw("0.00001"));
    }

    [Theory]
    [InlineData("0x10", 0x10 << 16)]
    [InlineData("0XfF", 0xff << 16)]
    [InlineData("0x0.8", 0x8000)]
    [InlineData("0x.1", 0x1000)]
    [InlineData("0x1.2345", 0x12345)]
    [InlineData("0x7fff.ffff", 0x7fffffff)]
    [InlineData("0xffff", -1 << 16)]
    [InlineData("0xffff.ffff", -1)]
    [InlineData("0x8000", int.MinValue)]
    [InlineData("0x1p4", 16 << 16)]
    public void Numbers_Hex(string text, int raw) => Assert.Equal(raw, Raw(text));

    [Fact]
    public void Numbers_Hex_ExtraFractionDigitsAreDropped()
    {
        // TODO conferir no z8lua.
        Assert.Equal(0x12345, Raw("0x1.23456"));
    }

    [Theory]
    [InlineData("1..2")]
    [InlineData("3e")]
    [InlineData("0x")]
    [InlineData("1.2.3")]
    public void Numbers_Malformed(string text)
    {
        var error = SingleError(text);
        Assert.Equal("FB0004", error.Code);
        Assert.Equal(1, error.Span.Column);
    }

    [Fact]
    public void Numbers_HexEDigitIsNotAnExponent()
    {
        var (tokens, diagnostics) = Lex("0x1e+2");
        Assert.Empty(diagnostics);
        Assert.Equal([TokenKind.Number, TokenKind.Plus, TokenKind.Number], tokens.Select(t => t.Kind));
        Assert.Equal(0x1e << 16, tokens[0].NumberValue.Raw);
    }

    [Fact]
    public void Numbers_FollowedByName()
    {
        var (tokens, diagnostics) = Lex("3x");
        Assert.Empty(diagnostics);
        Assert.Equal([TokenKind.Number, TokenKind.Name], tokens.Select(t => t.Kind));
    }

    // ---- strings ----

    [Fact]
    public void Strings_Quotes()
    {
        Assert.Equal("abc", Str("\"abc\""));
        Assert.Equal("a\"b", Str("'a\"b'"));
        Assert.Equal("", Str("''"));
    }

    [Theory]
    [InlineData(@"""\n\t\r\\\""\'""", "\n\t\r\\\"'")]
    [InlineData(@"""\a\b\f\v""", "\a\b\f\v")]
    [InlineData(@"""\x41\x7e""", "A~")]
    [InlineData(@"""\65\066\0""", "AB\0")]
    [InlineData(@"""\1234""", "{4")]
    [InlineData("\"a\\z  \n  b\"", "ab")]
    [InlineData("\"a\\\nb\"", "a\nb")]
    public void Strings_LuaEscapes(string literal, string value) => Assert.Equal(value, Str(literal));

    [Fact]
    public void Strings_P8sciiControlEscapes()
    {
        Assert.Equal("\x01\x02\x03\x04\x05\x06", Str(@"""\*\#\-\|\+\^"""));
    }

    [Fact]
    public void Strings_Long()
    {
        Assert.Equal("a\nb", Str("[[a\nb]]"));
        Assert.Equal("first newline skipped", Str("[[\nfirst newline skipped]]"));
        Assert.Equal("has ]] inside", Str("[==[has ]] inside]==]"));
        Assert.Equal("no \\n escapes", Str(@"[[no \n escapes]]"));
        Assert.Equal("crlf\nline", Str("[[crlf\r\nline]]"));
    }

    [Fact]
    public void Strings_Errors()
    {
        Assert.Equal("FB0002", SingleError("\"abc").Code);
        Assert.Equal("FB0002", SingleError("'abc\nx").Code);
        Assert.Equal("FB0005", SingleError("[[abc").Code);
        Assert.Equal("FB0007", SingleError(@"""\300""").Code);

        var invalid = SingleError(@"x = ""ab\q""");
        Assert.Equal("FB0003", invalid.Code);
        Assert.Equal(8, invalid.Span.Column);
    }

    [Fact]
    public void Strings_InvalidEscape_StillClosesTheString()
    {
        var (tokens, diagnostics) = Lex(@"""a\qb"" x");
        Assert.Single(diagnostics);
        Assert.Equal([TokenKind.String, TokenKind.Name], tokens.Select(t => t.Kind));
        Assert.Equal("aqb", tokens[0].StringValue);
    }

    // ---- comments and whitespace ----

    [Fact]
    public void Comments_LineAndLong()
    {
        var (tokens, diagnostics) = Lex("a -- comment\nb --[[ long\ncomment ]] c --[==[ x ]==] d");
        Assert.Empty(diagnostics);
        Assert.Equal(["a", "b", "c", "d"], tokens.Select(t => t.Text));
    }

    [Fact]
    public void Comments_DashDashBracketWithoutLongBracket_IsALineComment()
    {
        var (tokens, _) = Lex("--[ not long\nx");
        Assert.Equal("x", Assert.Single(tokens).Text);
    }

    [Fact]
    public void Comments_UnterminatedLong()
    {
        Assert.Equal("FB0006", SingleError("--[[ never closed").Code);
    }

    [Fact]
    public void UnexpectedCharacter_IsReportedAndSkipped()
    {
        var (tokens, diagnostics) = Lex("a $ b");
        Assert.Equal("FB0001", Assert.Single(diagnostics).Code);
        Assert.Equal(["a", "b"], tokens.Select(t => t.Text));
    }

    // ---- positions ----

    [Fact]
    public void Positions_LineAndColumn_WithAllNewlineStyles()
    {
        var (tokens, _) = Lex("a\n  b\r\nc\rd [[x\ny]] e");
        Assert.Equal(
            [(1, 1), (2, 3), (3, 1), (4, 1), (4, 3), (5, 5)],
            tokens.Select(t => (t.Span.Line, t.Span.Column)));
    }

    [Fact]
    public void Positions_OffsetsCoverTheSourceText()
    {
        const string source = "local s = 'hi' -- x";
        var (tokens, _) = Lex(source);
        foreach (var token in tokens) Assert.Equal(token.Text, source.Substring(token.Start, token.Length));
    }

    [Fact]
    public void Number_ValueIsFixedPoint()
    {
        Assert.Equal(FixedPoint.FromInt(7), Single("7").NumberValue);
    }
}
