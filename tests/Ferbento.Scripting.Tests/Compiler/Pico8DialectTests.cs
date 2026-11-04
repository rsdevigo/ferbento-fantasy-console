using Ferbento.Scripting.Compiler.Diagnostics;
using Ferbento.Scripting.Compiler.Ir;
using Ferbento.Scripting.Compiler.Lua;
using Ferbento.Scripting.Compiler.Parsing;

namespace Ferbento.Scripting.Tests.Compiler;

/// <summary>PICO-8 syntax on top of Lua 5.2 (story 004). The reference is z8lua's llex.c and lparser.c.</summary>
public class Pico8DialectTests
{
    private static IrChunk ParseOk(string source)
    {
        var result = LuaParser.Parse(source);
        Assert.True(result.Diagnostics.IsEmpty, string.Join("\n", result.Diagnostics));
        return result.Chunk;
    }

    private static string Reprint(string source) => LuaPrinter.Print(ParseOk(source));

    private static IrExpr Expr(string expression) =>
        Assert.Single(Assert.IsType<IrAssign>(Assert.Single(ParseOk($"x = {expression}").Body)).Values);

    private static Diagnostic FirstError(string source)
    {
        var result = LuaParser.Parse(source);
        Assert.True(result.HasErrors, "expected a syntax error");
        return result.Diagnostics[0];
    }

    private static List<Token> Tokens(string source)
    {
        var diagnostics = new List<Diagnostic>();
        var tokens = LuaLexer.Tokenize(source, null, diagnostics).ToList();
        Assert.Empty(diagnostics);
        tokens.RemoveAt(tokens.Count - 1);
        return tokens;
    }

    private static IrName N(string name) => new(name);

    // ---- lexer ----

    [Fact]
    public void Lexer_BangEqual_IsNotEqual()
    {
        var token = Assert.Single(Tokens("!="));
        Assert.Equal(TokenKind.TildeEqual, token.Kind);
        Assert.Equal("!=", token.Text);
    }

    [Fact]
    public void Lexer_DoubleSlash_IsALineComment()
    {
        Assert.Equal(["a", "b"], Tokens("a // comment / here\nb").Select(t => t.Text));
        Assert.Equal(TokenKind.SlashAssign, Assert.Single(Tokens("/=")).Kind);
    }

    [Theory]
    [InlineData("+=", TokenKind.PlusAssign)]
    [InlineData("-=", TokenKind.MinusAssign)]
    [InlineData("*=", TokenKind.StarAssign)]
    [InlineData("/=", TokenKind.SlashAssign)]
    [InlineData("\\=", TokenKind.BackslashAssign)]
    [InlineData("%=", TokenKind.PercentAssign)]
    [InlineData("^=", TokenKind.CaretAssign)]
    [InlineData("..=", TokenKind.ConcatAssign)]
    [InlineData("|=", TokenKind.PipeAssign)]
    [InlineData("&=", TokenKind.AmpersandAssign)]
    [InlineData("^^=", TokenKind.BitXorAssign)]
    [InlineData("<<=", TokenKind.ShiftLeftAssign)]
    [InlineData(">>=", TokenKind.ShiftRightAssign)]
    [InlineData(">>>=", TokenKind.ShiftRightLogicalAssign)]
    [InlineData("<<>=", TokenKind.RotateLeftAssign)]
    [InlineData(">><=", TokenKind.RotateRightAssign)]
    [InlineData("@", TokenKind.At)]
    [InlineData("$", TokenKind.Dollar)]
    [InlineData("?", TokenKind.Question)]
    public void Lexer_Pico8Symbols(string text, TokenKind kind) => Assert.Equal(kind, Assert.Single(Tokens(text)).Kind);

    [Fact]
    public void Lexer_ComparisonsAreNotCompoundAssignments()
    {
        Assert.Equal([TokenKind.Name, TokenKind.LessEqual, TokenKind.Name], Tokens("a<=b").Select(t => t.Kind));
        Assert.Equal([TokenKind.Name, TokenKind.EqualEqual, TokenKind.Minus, TokenKind.Number],
            Tokens("a==-1").Select(t => t.Kind));
        Assert.Equal([TokenKind.Name, TokenKind.Assign, TokenKind.Minus, TokenKind.Number],
            Tokens("a=-1").Select(t => t.Kind));
    }

    [Fact]
    public void Lexer_NewlineBefore_IgnoresNewlinesInsideLongCommentsAndStrings()
    {
        var tokens = Tokens("a --[[\n]] b [[\n]] c\nd -- x\ne");
        Assert.Equal([false, false, false, false, true, true], tokens.Select(t => t.NewlineBefore));
    }

    // ---- != ----

    [Fact]
    public void BangEqual_ParsesAsNotEqual()
    {
        IrAssert.Equal(new IrBinary(IrBinaryOp.NotEqual, N("a"), N("b")), Expr("a != b"));
    }

    // ---- compound assignment ----

    [Theory]
    [InlineData("x += 1", "x += 1")]
    [InlineData("x -= 1", "x -= 1")]
    [InlineData("x *= 2", "x *= 2")]
    [InlineData("x /= 2", "x /= 2")]
    [InlineData("x \\= 2", "x \\= 2")]
    [InlineData("x %= 2", "x %= 2")]
    [InlineData("x ^= 2", "x ^= 2")]
    [InlineData("s ..= \"!\"", "s ..= \"!\"")]
    [InlineData("x |= 1", "x |= 1")]
    [InlineData("x &= 1", "x &= 1")]
    [InlineData("x ^^= 1", "x ^^= 1")]
    [InlineData("x <<= 1", "x <<= 1")]
    [InlineData("x >>= 1", "x >>= 1")]
    [InlineData("x >>>= 1", "x >>>= 1")]
    [InlineData("x <<>= 1", "x <<>= 1")]
    [InlineData("x >><= 1", "x >><= 1")]
    [InlineData("p.x+=p.dx", "p.x += p.dx")]
    [InlineData("t[i]-=1", "t[i] -= 1")]
    public void Compound_AllOperators(string source, string expected) => Assert.Equal(expected + "\n", Reprint(source));

    [Fact]
    public void Compound_TargetIsKeptOnce_AndValueIsAWholeExpression()
    {
        var statement = Assert.IsType<IrCompoundAssign>(Assert.Single(ParseOk("t[f()] *= a + b").Body));
        Assert.Equal(IrBinaryOp.Multiply, statement.Operator);
        IrAssert.Equal(new IrIndex(N("t"), new IrCall(N("f"), [])), statement.Target);
        // a *= a + b is a = a * (a + b), not a = a * a + b.
        IrAssert.Equal(new IrBinary(IrBinaryOp.Add, N("a"), N("b")), statement.Value);
    }

    [Fact]
    public void Compound_LowPrecedenceValue_IsStillWhole()
    {
        var statement = Assert.IsType<IrCompoundAssign>(Assert.Single(ParseOk("x += a or b").Body));
        Assert.IsType<IrBinary>(statement.Value);
        Assert.Equal(IrBinaryOp.Or, ((IrBinary)statement.Value).Operator);
        Assert.Equal("x += a or b\n", LuaPrinter.Print(statement));
    }

    [Theory]
    [InlineData("f() += 1", "FB0104")]
    [InlineData("(x) += 1", "FB0104")]
    [InlineData("a, b += 1", "FB0101")]
    [InlineData("x +=", "FB0103")]
    public void Compound_Errors(string source, string code) => Assert.Equal(code, FirstError(source).Code);

    // ---- peek operators ----

    [Fact]
    public void Peek_Operators()
    {
        IrAssert.Equal(new IrUnary(IrUnaryOp.Peek, N("a")), Expr("@a"));
        IrAssert.Equal(new IrUnary(IrUnaryOp.Peek2, N("a")), Expr("%a"));
        IrAssert.Equal(new IrUnary(IrUnaryOp.Peek4, N("a")), Expr("$a"));
    }

    [Theory]
    [InlineData("@0x6000", "@24576")]
    [InlineData("@a + 1", "@a + 1")]
    [InlineData("@(a + 1)", "@(a + 1)")]
    [InlineData("a % %b", "a % %b")]
    [InlineData("-@a", "-@a")]
    [InlineData("$a ^ 2", "$a ^ 2")]
    [InlineData("@@a", "@@a")]
    public void Peek_PrecedenceAndPrinting(string source, string expected)
    {
        var assign = Assert.IsType<IrAssign>(Assert.Single(ParseOk($"x = {source}").Body));
        Assert.Equal(expected, LuaPrinter.Print(Assert.Single(assign.Values)));
    }

    [Fact]
    public void Peek_UnaryBindsTighterThanBinary()
    {
        // @a + 1 is (@a) + 1, like any other unary operator.
        IrAssert.Equal(new IrBinary(IrBinaryOp.Add, new IrUnary(IrUnaryOp.Peek, N("a")), new IrNumber(Core.Math.FixedPoint.FromInt(1))),
            Expr("@a + 1"));
    }

    // ---- short if ----

    [Fact]
    public void ShortIf_Basic()
    {
        var chunk = ParseOk("if (a) b()\nc()");
        Assert.Equal(2, chunk.Body.Length);
        var ifStmt = Assert.IsType<IrIf>(chunk.Body[0]);
        Assert.Single(Assert.Single(ifStmt.Clauses).Body);
        Assert.Null(ifStmt.Else);
        Assert.Equal("if a then\n  b()\nend\nc()\n", LuaPrinter.Print(chunk));
    }

    [Fact]
    public void ShortIf_WithElse_AndSeveralStatements()
    {
        Assert.Equal("if a then\n  b()\n  c = 1\nelse\n  d()\nend\ne()\n", Reprint("if (a) b() c = 1 else d()\ne()"));
    }

    [Fact]
    public void ShortIf_ElseifNeedsThen()
    {
        Assert.Equal("if a then\n  b()\nelseif c then\n  d()\nend\n", Reprint("if (a) b() elseif c then d()"));
        Assert.Equal("FB0101", FirstError("if (a) b() elseif (c) d()").Code);
    }

    [Fact]
    public void ShortIf_AtEndOfFile_WithoutNewline()
    {
        Assert.Equal("if a then\n  b()\nend\n", Reprint("if (a) b()"));
    }

    [Fact]
    public void ShortIf_ClosedByTheEndOfAnOuterBlock()
    {
        Assert.Equal("function f()\n  if a then\n    b()\n  end\nend\ng()\n", Reprint("function f() if (a) b() end\ng()"));
    }

    [Fact]
    public void ShortIf_Nested_ShareTheEndOfLine()
    {
        var chunk = ParseOk("if (a) if (b) c()\nd()");
        Assert.Equal(2, chunk.Body.Length);
        Assert.Equal("if a then\n  if b then\n    c()\n  end\nend\nd()\n", LuaPrinter.Print(chunk));
    }

    [Fact]
    public void ShortIf_WithGotoAndBreak()
    {
        Assert.Equal("for i = 1, 3 do\n  if i == 2 then\n    goto skip\n  end\n  ::skip::\nend\n",
            Reprint("for i = 1, 3 do\n  if (i == 2) goto skip\n  ::skip::\nend"));
        Assert.Equal("while true do\n  if done then\n    break\n  end\nend\n", Reprint("while true do\n if (done) break\nend"));
    }

    [Fact]
    public void ShortIf_ConditionCanContinueAfterTheParentheses()
    {
        // z8lua only checks that the parentheses are balanced at the token after the condition.
        Assert.Equal("if a and b then\n  c()\nend\n", Reprint("if (a) and b c()"));
    }

    [Fact]
    public void ShortIf_LongCommentWithNewline_DoesNotEndTheLine()
    {
        var ifStmt = Assert.IsType<IrIf>(Assert.Single(ParseOk("if (a) b() --[[\n]] c()").Body));
        Assert.Equal(2, ifStmt.Clauses[0].Body.Length);
    }

    [Fact]
    public void NormalIf_WithParenthesizedCondition_StillWorks()
    {
        Assert.Equal("if a then\n  b()\nend\n", Reprint("if (a) then b() end"));
        Assert.Equal("if a then\n  b()\nend\n", Reprint("if (a) then\n  b()\nend"));
        Assert.Equal("if a and b then\nend\n", Reprint("if (a) and (b) then end"));
    }

    [Theory]
    [InlineData("if (a)\nb()", "FB0101")]
    [InlineData("if a b()", "FB0101")]
    [InlineData("if (a) and (b) c()", "FB0101")]
    [InlineData("if (a) x = 1 +\n2", "FB0103")]
    [InlineData("if (a) for i = 1, 2 do\nx()\nend", "FB0101")]
    public void ShortIf_Errors(string source, string code) => Assert.Equal(code, FirstError(source).Code);

    [Fact]
    public void ShortIf_BrokenAcrossLines_SaysWhatIsMissing()
    {
        Assert.Equal("esperava 'then', mas encontrou o nome 'b'", FirstError("if (a)\nb()").Message);
        Assert.Equal("esperava 'end', mas encontrou o fim da linha", FirstError("if (a) for i = 1, 2 do\nx()\nend").Message);
    }

    // ---- short while ----

    [Fact]
    public void ShortWhile()
    {
        Assert.Equal("while i < 10 do\n  i += 1\nend\nf()\n", Reprint("while (i < 10) i += 1\nf()"));
        Assert.Equal("while a do\nend\n", Reprint("while (a) do end"));
        Assert.Equal("FB0101", FirstError("while (a)\nx()").Code);
    }

    // ---- ? (print) ----

    [Fact]
    public void ShortPrint()
    {
        Assert.Equal("print(\"hi\")\n", Reprint("?\"hi\""));
        Assert.Equal("print(x, 1, 2)\nf()\n", Reprint("?x, 1, 2\nf()"));
        Assert.Equal("print()\n", Reprint("?"));
        Assert.Equal("print()\nf()\n", Reprint("?\nf()"));
    }

    [Fact]
    public void ShortPrint_IsACallToPrint()
    {
        var call = Assert.IsType<IrCall>(Assert.IsType<IrCallStmt>(Assert.Single(ParseOk("?a").Body)).Call);
        IrAssert.Equal(N("print"), call.Callee);
        IrAssert.Equal(N("a"), Assert.Single(call.Arguments));
    }

    [Fact]
    public void ShortPrint_InsideShortIf()
    {
        Assert.Equal("if a then\n  print(\"x\")\nend\nf()\n", Reprint("if (a) ?\"x\"\nf()"));
    }

    [Theory]
    [InlineData("?a b")]
    [InlineData("?a end")]
    public void ShortPrint_MustEndTheLine(string source)
    {
        var error = FirstError(source);
        Assert.Equal("FB0101", error.Code);
        Assert.Contains("o fim da linha", error.Message);
    }

    // ---- a cartridge using everything ----

    private const string Cart = """
        -- dialect sampler
        function _init()
          score, lives = 0, 3
          enemies = {}
          for i = 1, 8 do add(enemies, {x = i * 16, y = 0, hp = 2}) end
        end

        function _update60()
          for e in all(enemies) do
            e.y += 0.5 // fall
            if (e.y > 128) e.y = -8 e.hp -= 1
            if (e.hp <= 0) del(enemies, e) score += 10
          end
          if (btnp(4)) lives -= 1 else score ..= ""
          while (#enemies < 8) add(enemies, {x = rnd(128), y = -8, hp = 2})
          local flags = @0x5f2c | %0x5f80 & $0x5f00
          flags ^^= 0b1010.1
          if (lives != 3) flags <<= 1
        end

        function _draw()
          cls()
          for e in all(enemies) do circfill(e.x, e.y, 3, 8) end
          ?"score: "..score, 1, 1, 7
          if (lives == 0) ?"game over", 40, 60, 8
        end
        """;

    [Fact]
    public void Cart_UsingEveryExtension_ParsesAndRoundTrips()
    {
        var first = ParseOk(Cart);
        var printed = LuaPrinter.Print(first);
        var second = ParseOk(printed);
        IrAssert.Equal(first, second);
        Assert.Equal(printed, LuaPrinter.Print(second));

        Assert.Contains("e.y += 0.5\n", printed);
        Assert.Contains("    if e.y > 128 then\n      e.y = -8\n      e.hp -= 1\n    end\n", printed);
        Assert.Contains("local flags = @24364 | %24448 & $24320\n", printed);
        Assert.Contains("flags ^^= 10.5\n", printed);
        Assert.Contains("if (btnp(4)) then\n", printed);
        Assert.Contains("print(\"score: \" .. score, 1, 1, 7)\n", printed);
        Assert.Contains("if lives ~= 3 then\n", printed);
    }
}
