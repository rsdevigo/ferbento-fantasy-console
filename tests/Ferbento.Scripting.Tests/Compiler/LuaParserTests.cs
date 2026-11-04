using Ferbento.Core.Math;
using Ferbento.Scripting.Compiler.Diagnostics;
using Ferbento.Scripting.Compiler.Ir;
using Ferbento.Scripting.Compiler.Lua;
using Ferbento.Scripting.Compiler.Parsing;

namespace Ferbento.Scripting.Tests.Compiler;

/// <summary>
/// Most tests compare the parsed program re-printed by <see cref="LuaPrinter"/>: the printer puts
/// parentheses exactly where the tree needs them, so the text shows the structure the parser built.
/// </summary>
public class LuaParserTests
{
    private static IrChunk ParseOk(string source)
    {
        var result = LuaParser.Parse(source);
        Assert.True(result.Diagnostics.IsEmpty, string.Join("\n", result.Diagnostics));
        return result.Chunk;
    }

    /// <summary>Parses and re-prints a program.</summary>
    private static string Reprint(string source) => LuaPrinter.Print(ParseOk(source));

    /// <summary>Parses <c>x = expr</c> and re-prints only the expression.</summary>
    private static string ReprintExpr(string expression)
    {
        var assign = Assert.IsType<IrAssign>(Assert.Single(ParseOk($"x = {expression}").Body));
        return LuaPrinter.Print(Assert.Single(assign.Values));
    }

    private static IrExpr Expr(string expression) =>
        Assert.Single(Assert.IsType<IrAssign>(Assert.Single(ParseOk($"x = {expression}").Body)).Values);

    private static Diagnostic FirstError(string source)
    {
        var result = LuaParser.Parse(source);
        Assert.True(result.HasErrors, "expected a syntax error");
        return result.Diagnostics[0];
    }

    private static IrName N(string name) => new(name);
    private static IrNumber Num(int value) => new(FixedPoint.FromInt(value));

    // ---- statements ----

    [Fact]
    public void Local_WithAndWithoutValues()
    {
        Assert.Equal("local a\n", Reprint("local a"));
        Assert.Equal("local a, b = 1, 2\n", Reprint("local a,b=1,2"));
        Assert.Equal("local a, b = f()\n", Reprint("local a, b = f()"));
    }

    [Fact]
    public void Assignment_SingleAndMultiple()
    {
        Assert.Equal("x = 1\n", Reprint("x = 1"));
        Assert.Equal("a, t.b, t[i] = 1, 2, 3\n", Reprint("a, t.b, t[i] = 1, 2, 3"));
        Assert.Equal(";(f()).x = 1\n", Reprint("(f()).x = 1"));
    }

    [Fact]
    public void CallStatements_AllArgumentForms()
    {
        Assert.Equal("cls(0)\n", Reprint("cls(0)"));
        Assert.Equal("f()\n", Reprint("f()"));
        Assert.Equal("print(\"hi\")\n", Reprint("print \"hi\""));
        Assert.Equal("f({1, 2})\n", Reprint("f{1, 2}"));
        Assert.Equal("f(1)(2)\n", Reprint("f(1)(2)"));
        Assert.Equal("a.b.c(1)\n", Reprint("a.b.c(1)"));
    }

    [Fact]
    public void MethodCall()
    {
        Assert.Equal("obj:update(dt)\n", Reprint("obj:update(dt)"));
        Assert.Equal("a.b:m(1):n()\n", Reprint("a.b:m(1):n()"));
        Assert.Equal("s:sub(\"x\")\n", Reprint("s:sub'x'"));

        var call = Assert.IsType<IrMethodCall>(Expr("p:len()"));
        Assert.Equal("len", call.Method);
        IrAssert.Equal(N("p"), call.Receiver);
    }

    [Fact]
    public void If_ElseIf_Else()
    {
        const string source = "if a then x() elseif b then y() elseif c then else z() end";
        Assert.Equal(
            "if a then\n  x()\nelseif b then\n  y()\nelseif c then\nelse\n  z()\nend\n",
            Reprint(source));
    }

    [Fact]
    public void Loops()
    {
        Assert.Equal("while x < 10 do\n  x = x + 1\nend\n", Reprint("while x < 10 do x = x + 1 end"));
        Assert.Equal("repeat\n  x = x - 1\nuntil x == 0\n", Reprint("repeat x = x - 1 until x == 0"));
        Assert.Equal("for i = 1, 10 do\nend\n", Reprint("for i=1,10 do end"));
        Assert.Equal("for i = 10, 1, -1 do\nend\n", Reprint("for i = 10, 1, -1 do end"));
        Assert.Equal("for k, v in pairs(t) do\nend\n", Reprint("for k, v in pairs(t) do end"));
        Assert.Equal("for x in all(t) do\n  break\nend\n", Reprint("for x in all(t) do break end"));
    }

    [Fact]
    public void Repeat_ConditionIsParsedAfterTheBody()
    {
        var repeat = Assert.IsType<IrRepeat>(Assert.Single(ParseOk("repeat local done = f() until done").Body));
        Assert.IsType<IrLocal>(Assert.Single(repeat.Body));
        IrAssert.Equal(N("done"), repeat.Condition);
    }

    [Fact]
    public void Do_Goto_Label_Semicolons()
    {
        Assert.Equal("do\n  local x = 1\nend\n", Reprint("do local x = 1 end"));
        Assert.Equal("goto continue\n::continue::\n", Reprint("goto continue ::continue::"));
        Assert.Equal("a()\nb()\n", Reprint(";;a();b();"));
    }

    [Fact]
    public void Return_WithValues_AndOptionalSemicolon()
    {
        Assert.Equal("function f()\n  return\nend\n", Reprint("function f() return end"));
        Assert.Equal("function f()\n  return 1, 2\nend\n", Reprint("function f() return 1, 2; end"));
        Assert.Equal("do\n  return\nend\nx()\n", Reprint("do return end x()"));
    }

    [Fact]
    public void Functions_GlobalLocalAnonymousAndFieldPath()
    {
        Assert.Equal("function f(a, b)\n  return a + b\nend\n", Reprint("function f(a, b) return a + b end"));
        Assert.Equal("local function g()\nend\n", Reprint("local function g() end"));
        Assert.Equal("function a.b.c()\nend\n", Reprint("function a.b.c() end"));
        Assert.Equal("f = function(x)\n  return x\nend\n", Reprint("f = function(x) return x end"));
    }

    [Fact]
    public void Functions_Method_ReceivesSelf()
    {
        var statement = Assert.IsType<IrFunctionStmt>(Assert.Single(ParseOk("function enemy:hit(dmg) end").Body));
        Assert.True(statement.IsMethod);
        Assert.Equal(["enemy", "hit"], statement.NamePath.ToArray());
        Assert.Equal(["self", "dmg"], statement.Function.Parameters.ToArray());
        Assert.Equal("function enemy:hit(dmg)\nend\n", LuaPrinter.Print(statement));
        Assert.Equal("function a.b:c()\nend\n", Reprint("function a.b:c() end"));
    }

    [Fact]
    public void Functions_Varargs()
    {
        Assert.Equal("function f(...)\n  return ...\nend\n", Reprint("function f(...) return ... end"));
        Assert.Equal("function f(a, ...)\n  local t = {...}\nend\n", Reprint("function f(a, ...) local t = {...} end"));
        var function = Assert.IsType<IrFunction>(Expr("function(...) end"));
        Assert.True(function.IsVararg);
        Assert.Empty(function.Parameters);
    }

    // ---- expressions ----

    [Fact]
    public void Literals()
    {
        IrAssert.Equal(new IrNil(), Expr("nil"));
        IrAssert.Equal(new IrBool(true), Expr("true"));
        IrAssert.Equal(new IrBool(false), Expr("false"));
        IrAssert.Equal(Num(3), Expr("3"));
        IrAssert.Equal(new IrNumber(new FixedPoint(0x8000)), Expr("0x0.8"));
        IrAssert.Equal(new IrString("a\nb"), Expr("\"a\\nb\""));
    }

    [Fact]
    public void Tables_AllFieldForms()
    {
        Assert.Equal("{}", ReprintExpr("{}"));
        Assert.Equal("{1, 2, 3}", ReprintExpr("{1, 2, 3,}"));
        Assert.Equal("{x = 1, y = 2}", ReprintExpr("{x=1; y=2}"));
        Assert.Equal("{[1] = \"a\", [k] = v, n = {}}", ReprintExpr("{[1]='a', [k]=v, n={}}"));
        Assert.Equal("{a == b}", ReprintExpr("{a == b}"));
    }

    [Fact]
    public void Index_DotAndBracket()
    {
        Assert.Equal("t.pos.x", ReprintExpr("t.pos.x"));
        Assert.Equal("t[i + 1]", ReprintExpr("t[i + 1]"));
        Assert.Equal("t.key", ReprintExpr("t[\"key\"]"));
        Assert.Equal("(\"abc\"):len()", ReprintExpr("(\"abc\"):len()"));
    }

    [Theory]
    [InlineData("a + b * c", "a + b * c")]
    [InlineData("(a + b) * c", "(a + b) * c")]
    [InlineData("a - b - c", "a - b - c")]
    [InlineData("a - (b - c)", "a - (b - c)")]
    [InlineData("a .. b .. c", "a .. b .. c")]
    [InlineData("(a .. b) .. c", "(a .. b) .. c")]
    [InlineData("2 ^ 3 ^ 2", "2 ^ 3 ^ 2")]
    [InlineData("(2 ^ 3) ^ 2", "(2 ^ 3) ^ 2")]
    [InlineData("-x ^ 2", "-x ^ 2")]
    [InlineData("(-x) ^ 2", "(-x) ^ 2")]
    [InlineData("2 ^ -x", "2 ^ -x")]
    [InlineData("not a == b", "not a == b")]
    [InlineData("not (a == b)", "not (a == b)")]
    [InlineData("a or b and c", "a or b and c")]
    [InlineData("(a or b) and c", "(a or b) and c")]
    [InlineData("a < b == c", "a < b == c")]
    [InlineData("1 + 2 .. 3", "1 + 2 .. 3")]
    [InlineData("a | b ^^ c & d << 1", "a | b ^^ c & d << 1")]
    [InlineData("a >>> 1 <<> 2 >>< 3 >> 4", "a >>> 1 <<> 2 >>< 3 >> 4")]
    [InlineData("a \\ b % c / d", "a \\ b % c / d")]
    [InlineData("#t + 1", "#t + 1")]
    [InlineData("~a & b", "~a & b")]
    [InlineData("- -x", "- -x")]
    [InlineData("((a))", "a")]
    public void Precedence_And_Associativity(string source, string expected) =>
        Assert.Equal(expected, ReprintExpr(source));

    [Fact]
    public void Precedence_Structure()
    {
        // a + b * c ^ d
        var expected = new IrBinary(IrBinaryOp.Add, N("a"),
            new IrBinary(IrBinaryOp.Multiply, N("b"), new IrBinary(IrBinaryOp.Power, N("c"), N("d"))));
        IrAssert.Equal(expected, Expr("a + b * c ^ d"));
    }

    [Fact]
    public void Parentheses_AroundCallsAndVarargs_AreKept()
    {
        IrAssert.Equal(new IrParen(new IrCall(N("f"), [])), Expr("(f())"));
        Assert.Equal("function g(...)\n  return (...)\nend\n", Reprint("function g(...) return (...) end"));
        Assert.Equal("(f())", ReprintExpr("(f())"));
        Assert.Equal("(o:m())", ReprintExpr("(o:m())"));
        IrAssert.Equal(N("a"), Expr("(a)"));
    }

    [Fact]
    public void CallOnNextLine_IsStillACall()
    {
        // Lua 5.2 reads this as a(b)(c); the printer then needs ';' only before statements that start with '('.
        var call = Assert.IsType<IrCallStmt>(Assert.Single(ParseOk("a(b)\n(c)").Body));
        Assert.IsType<IrCall>(call.Call);
    }

    // ---- positions ----

    [Fact]
    public void Spans_PointToTheSource()
    {
        var chunk = LuaParser.Parse("x = 1\n\nfunction f(a)\n  return a + 1\nend", "game.lua").Chunk;
        Assert.Equal(new SourceSpan("game.lua", 1, 1), chunk.Body[0].Span);

        var function = Assert.IsType<IrFunctionStmt>(chunk.Body[1]);
        Assert.Equal(new SourceSpan("game.lua", 3, 1), function.Span);

        var ret = Assert.IsType<IrReturn>(Assert.Single(function.Function.Body));
        Assert.Equal(new SourceSpan("game.lua", 4, 3), ret.Span);

        var add = Assert.IsType<IrBinary>(Assert.Single(ret.Values));
        Assert.Equal(4, add.Span!.Line);
        Assert.Equal(12, add.Span.Column);
        Assert.Equal(10, add.Left.Span!.Column);
        Assert.Equal(14, add.Right.Span!.Column);
    }

    [Fact]
    public void Spans_EveryParsedNodeHasOne()
    {
        var chunk = ParseOk(RoundTripPrograms.Platformer);
        AssertAllSpans(chunk);

        static void AssertAllSpans(object? value)
        {
            switch (value)
            {
                case IrNode node:
                    Assert.True(node.Span is not null, $"{node.GetType().Name} without span");
                    foreach (var property in node.GetType().GetProperties())
                    {
                        if (property.Name is "EqualityContract" or "Span") continue;
                        AssertAllSpans(property.GetValue(node));
                    }
                    break;
                case System.Collections.IEnumerable items and not string:
                    foreach (var item in items) AssertAllSpans(item);
                    break;
            }
        }
    }

    // ---- syntax errors ----

    [Fact]
    public void Error_MissingEnd_PointsToTheOpener()
    {
        var error = FirstError("function f()\n  x = 1\n\nfunction g() end");
        Assert.Equal("FB0102", error.Code);
        Assert.Contains("'end' para fechar o 'function' da linha 1", error.Message);
    }

    [Fact]
    public void Error_MissingEnd_AtEndOfFile()
    {
        var error = FirstError("if x then\n  y()\n");
        Assert.Equal("FB0102", error.Code);
        Assert.Contains("o fim do arquivo", error.Message);
        Assert.Equal(3, error.Span.Line);
    }

    [Theory]
    [InlineData("if x y() end", "FB0101", 1, 6)]
    [InlineData("while x y() end", "FB0101", 1, 9)]
    [InlineData("for i 1, 2 do end", "FB0101", 1, 7)]
    [InlineData("local = 1", "FB0101", 1, 7)]
    [InlineData("x = ", "FB0103", 1, 5)]
    [InlineData("x = )", "FB0103", 1, 5)]
    [InlineData("f() = 1", "FB0104", 1, 1)]
    [InlineData("(a) = 1", "FB0104", 1, 1)]
    [InlineData("x", "FB0105", 1, 1)]
    [InlineData("a.b", "FB0105", 1, 1)]
    [InlineData("function f() return 1 x = 2 end", "FB0106", 1, 14)]
    [InlineData("t = {1, 2", "FB0101", 1, 10)]
    [InlineData("f(1, 2", "FB0101", 1, 7)]
    [InlineData("x = a.", "FB0101", 1, 7)]
    [InlineData("function f(1) end", "FB0101", 1, 12)]
    [InlineData("end", "FB0101", 1, 1)]
    [InlineData("goto", "FB0101", 1, 5)]
    [InlineData("::lbl", "FB0101", 1, 6)]
    public void Errors_CodeAndPosition(string source, string code, int line, int column)
    {
        var error = FirstError(source);
        Assert.Equal(code, error.Code);
        Assert.Equal((line, column), (error.Span.Line, error.Span.Column));
    }

    [Fact]
    public void Error_MessagesNameTheTokens()
    {
        Assert.Equal("esperava 'then', mas encontrou o nome 'y'", FirstError("if x y() end").Message);
        Assert.Equal("esperava uma expressão, mas encontrou o fim do arquivo", FirstError("x =").Message);
    }

    [Fact]
    public void Errors_Recovery_ReportsSeveralErrors()
    {
        const string source = "x = = 1\ny = 2\nz = )\nfunction ok() end\nw = (";
        var result = LuaParser.Parse(source);
        Assert.Equal([1, 3, 5], result.Diagnostics.Select(d => d.Span.Line));

        // The valid statements around the errors are still parsed.
        Assert.Contains(result.Chunk.Body, s => s is IrAssign { Targets: [IrName { Name: "y" }] });
        Assert.Contains(result.Chunk.Body, s => s is IrFunctionStmt { NamePath: ["ok"] });
    }

    [Fact]
    public void Errors_Recovery_InsideFunctionBody()
    {
        var result = LuaParser.Parse("function f()\n  x = = 1\n  y = 2\nend\ng()");
        Assert.Single(result.Diagnostics);
        Assert.Equal(2, result.Diagnostics[0].Span.Line);
        Assert.Equal(2, result.Chunk.Body.Length);
    }

    [Fact]
    public void Errors_LexerErrorsAreIncluded()
    {
        var result = LuaParser.Parse("x = \"open\ny = 1");
        Assert.Equal("FB0002", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Errors_NeverThrow_OnGarbage()
    {
        foreach (var source in new[] { ")))", "end end end", "((((", "function", "local function", "{", "x = {[", "a:b", "1 + 2", "\"" })
        {
            var result = LuaParser.Parse(source);
            Assert.True(result.HasErrors, source);
        }
    }

    [Fact]
    public void Catalog_CodesAreUnique()
    {
        var codes = DiagnosticCatalog.All.Select(d => d.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches(@"^FB\d{4}$", code));
    }
}
