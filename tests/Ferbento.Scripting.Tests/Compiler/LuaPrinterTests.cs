using Ferbento.Core.Math;
using Ferbento.Scripting.Compiler.Ir;
using Ferbento.Scripting.Compiler.Lua;

namespace Ferbento.Scripting.Tests.Compiler;

public class LuaPrinterTests
{
    private static IrName N(string name) => new(name);
    private static IrNumber Num(int value) => new(FixedPoint.FromInt(value));
    private static IrString S(string value) => new(value);
    private static IrBinary B(IrBinaryOp op, IrExpr left, IrExpr right) => new(op, left, right);
    private static IrUnary U(IrUnaryOp op, IrExpr operand) => new(op, operand);
    private static IrCall Call(string name, params IrExpr[] args) => new(N(name), [.. args]);
    private static string P(IrExpr expr) => LuaPrinter.Print(expr);
    private static string P(params IrStmt[] body) => LuaPrinter.Print(new IrChunk([.. body]));

    // ---- literals and atoms ----

    [Fact]
    public void Literals_Nil_Bool_String()
    {
        Assert.Equal("nil", P(new IrNil()));
        Assert.Equal("true", P(new IrBool(true)));
        Assert.Equal("false", P(new IrBool(false)));
        Assert.Equal("\"hi\"", P(S("hi")));
    }

    [Fact]
    public void Name_And_Index()
    {
        Assert.Equal("x", P(N("x")));
        Assert.Equal("t.pos", P(new IrIndex(N("t"), S("pos"))));
        Assert.Equal("t[1]", P(new IrIndex(N("t"), Num(1))));
        Assert.Equal("t[\"a b\"]", P(new IrIndex(N("t"), S("a b"))));
        Assert.Equal("t[\"end\"]", P(new IrIndex(N("t"), S("end"))));
        Assert.Equal("a.b[i].c", P(new IrIndex(new IrIndex(new IrIndex(N("a"), S("b")), N("i")), S("c"))));
    }

    [Fact]
    public void Call_And_NonPrefixCallee_IsParenthesized()
    {
        Assert.Equal("spr(1, x, y)", P(Call("spr", Num(1), N("x"), N("y"))));
        Assert.Equal("f()", P(Call("f")));
        Assert.Equal("f(1)(2)", P(new IrCall(Call("f", Num(1)), [Num(2)])));
        Assert.Equal("(\"abc\")[1]", P(new IrIndex(S("abc"), Num(1))));
        Assert.Equal("({1})[1]", P(new IrIndex(new IrTable([new IrTableField(null, Num(1))]), Num(1))));
    }

    [Fact]
    public void Table_PositionalAndKeyedFields()
    {
        Assert.Equal("{}", P(new IrTable([])));
        var table = new IrTable([
            new IrTableField(null, Num(1)),
            new IrTableField(S("x"), Num(2)),
            new IrTableField(S("not id"), Num(3)),
            new IrTableField(Num(5), S("five")),
        ]);
        Assert.Equal("{1, x = 2, [\"not id\"] = 3, [5] = \"five\"}", P(table));
    }

    [Fact]
    public void AnonymousFunction_IsMultiLineAndIndented()
    {
        var fn = new IrFunction(["x"], [new IrReturn([B(IrBinaryOp.Multiply, N("x"), Num(2))])]);
        var stmt = new IrLocal(["double"], [fn]);
        Assert.Equal("local double = function(x)\n  return x * 2\nend\n", P(stmt));
    }

    [Fact]
    public void AnonymousFunction_NestedInsideBlock_KeepsIndentation()
    {
        var fn = new IrFunction([], [new IrCallStmt(Call("yield"))]);
        var body = new IrWhile(new IrBool(true), [new IrAssign([N("c")], [Call("cocreate", fn)])]);
        Assert.Equal(
            "while true do\n  c = cocreate(function()\n    yield()\n  end)\nend\n",
            P(body));
    }

    // ---- numbers ----

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(-5, "-5")]
    [InlineData(32767, "32767")]
    public void Number_Integers(int value, string expected) =>
        Assert.Equal(expected, LuaLiterals.FormatNumber(FixedPoint.FromInt(value)));

    [Theory]
    [InlineData(0x8000, "0.5")]
    [InlineData(0x18000, "1.5")]
    [InlineData(0x4000, "0.25")]
    [InlineData(-0x8000, "-0.5")]
    [InlineData(0x0800, "0.03125")] // 5 decimal digits: still decimal
    [InlineData(0x0400, "0x0.04")] // 0.015625 has 6 digits: hex
    [InlineData(0x1999, "0x0.1999")] // 0.1 is not exact in 16.16
    [InlineData(0x0001, "0x0.0001")]
    [InlineData(0x7FFFFFFF, "0x7fff.ffff")]
    [InlineData(-0x7FFFFFFF, "-0x7fff.ffff")]
    [InlineData(int.MinValue, "0x8000")]
    public void Number_Fractions_AreExact(int raw, string expected) =>
        Assert.Equal(expected, LuaLiterals.FormatNumber(new FixedPoint(raw)));

    [Fact]
    public void Number_Decimal_RoundTripsExactly()
    {
        for (var raw = -0x30000; raw <= 0x30000; raw += 0x0800)
        {
            var text = LuaLiterals.FormatNumber(new FixedPoint(raw));
            if (text.Contains('x')) continue;
            Assert.Equal(raw, (int)System.Math.Round(double.Parse(text, System.Globalization.CultureInfo.InvariantCulture) * 65536));
        }
    }

    // ---- strings ----

    [Fact]
    public void String_Escapes()
    {
        Assert.Equal("\"a\\\"b\"", LuaLiterals.FormatString("a\"b"));
        Assert.Equal("\"a\\\\b\"", LuaLiterals.FormatString("a\\b"));
        Assert.Equal("\"l1\\nl2\\tx\\r\"", LuaLiterals.FormatString("l1\nl2\tx\r"));
        Assert.Equal("\"\\x00\\x1f\\x7f\\x80\\xff\"", LuaLiterals.FormatString("\0\u001f\u007f\u0080ÿ"));
        Assert.Equal("\"'ok' ?\"", LuaLiterals.FormatString("'ok' ?"));
    }

    [Fact]
    public void String_NonByteCharacter_Throws() =>
        Assert.Throws<ArgumentException>(() => LuaLiterals.FormatString("éĀ"));

    // ---- operators ----

    [Theory]
    [InlineData(IrBinaryOp.Or, "a or b")]
    [InlineData(IrBinaryOp.And, "a and b")]
    [InlineData(IrBinaryOp.Less, "a < b")]
    [InlineData(IrBinaryOp.Greater, "a > b")]
    [InlineData(IrBinaryOp.LessEqual, "a <= b")]
    [InlineData(IrBinaryOp.GreaterEqual, "a >= b")]
    [InlineData(IrBinaryOp.Equal, "a == b")]
    [InlineData(IrBinaryOp.NotEqual, "a ~= b")]
    [InlineData(IrBinaryOp.BitOr, "a | b")]
    [InlineData(IrBinaryOp.BitXor, "a ^^ b")]
    [InlineData(IrBinaryOp.BitAnd, "a & b")]
    [InlineData(IrBinaryOp.ShiftLeft, "a << b")]
    [InlineData(IrBinaryOp.ShiftRight, "a >> b")]
    [InlineData(IrBinaryOp.LogicalShiftRight, "a >>> b")]
    [InlineData(IrBinaryOp.RotateLeft, "a <<> b")]
    [InlineData(IrBinaryOp.RotateRight, "a >>< b")]
    [InlineData(IrBinaryOp.Concat, "a .. b")]
    [InlineData(IrBinaryOp.Add, "a + b")]
    [InlineData(IrBinaryOp.Subtract, "a - b")]
    [InlineData(IrBinaryOp.Multiply, "a * b")]
    [InlineData(IrBinaryOp.Divide, "a / b")]
    [InlineData(IrBinaryOp.IntDivide, "a \\ b")]
    [InlineData(IrBinaryOp.Modulo, "a % b")]
    [InlineData(IrBinaryOp.Power, "a ^ b")]
    public void Binary_Symbols(IrBinaryOp op, string expected) =>
        Assert.Equal(expected, P(B(op, N("a"), N("b"))));

    [Fact]
    public void Unary_Symbols()
    {
        Assert.Equal("-x", P(U(IrUnaryOp.Negate, N("x"))));
        Assert.Equal("not x", P(U(IrUnaryOp.Not, N("x"))));
        Assert.Equal("#t", P(U(IrUnaryOp.Length, N("t"))));
        Assert.Equal("~x", P(U(IrUnaryOp.BitNot, N("x"))));
    }

    [Fact]
    public void Unary_DoubleNegation_DoesNotBecomeComment()
    {
        Assert.Equal("- -x", P(U(IrUnaryOp.Negate, U(IrUnaryOp.Negate, N("x")))));
        Assert.Equal("- -1", P(U(IrUnaryOp.Negate, Num(-1))));
        Assert.Equal("~ ~x", P(U(IrUnaryOp.BitNot, U(IrUnaryOp.BitNot, N("x")))));
        Assert.Equal("a - -1", P(B(IrBinaryOp.Subtract, N("a"), Num(-1))));
    }

    // ---- precedence and associativity ----

    [Fact]
    public void Precedence_NoParensWhenNotNeeded()
    {
        // a + b * c
        Assert.Equal("a + b * c", P(B(IrBinaryOp.Add, N("a"), B(IrBinaryOp.Multiply, N("b"), N("c")))));
        // a * b + c
        Assert.Equal("a * b + c", P(B(IrBinaryOp.Add, B(IrBinaryOp.Multiply, N("a"), N("b")), N("c"))));
        // a or b and c
        Assert.Equal("a or b and c", P(B(IrBinaryOp.Or, N("a"), B(IrBinaryOp.And, N("b"), N("c")))));
        // x < y and y < z
        Assert.Equal("x < y and y < z",
            P(B(IrBinaryOp.And, B(IrBinaryOp.Less, N("x"), N("y")), B(IrBinaryOp.Less, N("y"), N("z")))));
        // a | b & c   (& binds tighter than ^^, which binds tighter than |)
        Assert.Equal("a | b ^^ c & d",
            P(B(IrBinaryOp.BitOr, N("a"), B(IrBinaryOp.BitXor, N("b"), B(IrBinaryOp.BitAnd, N("c"), N("d"))))));
        // a & b << 2
        Assert.Equal("a & b << 2", P(B(IrBinaryOp.BitAnd, N("a"), B(IrBinaryOp.ShiftLeft, N("b"), Num(2)))));
        // "s" .. a + 1
        Assert.Equal("\"s\" .. a + 1", P(B(IrBinaryOp.Concat, S("s"), B(IrBinaryOp.Add, N("a"), Num(1)))));
        // a == b | c
        Assert.Equal("a == b | c", P(B(IrBinaryOp.Equal, N("a"), B(IrBinaryOp.BitOr, N("b"), N("c")))));
    }

    [Fact]
    public void Precedence_ParensWhenNeeded()
    {
        Assert.Equal("(a + b) * c", P(B(IrBinaryOp.Multiply, B(IrBinaryOp.Add, N("a"), N("b")), N("c"))));
        Assert.Equal("(a or b) and c", P(B(IrBinaryOp.And, B(IrBinaryOp.Or, N("a"), N("b")), N("c"))));
        Assert.Equal("(a | b) & c", P(B(IrBinaryOp.BitAnd, B(IrBinaryOp.BitOr, N("a"), N("b")), N("c"))));
        Assert.Equal("(a .. b) + 1", P(B(IrBinaryOp.Add, B(IrBinaryOp.Concat, N("a"), N("b")), Num(1))));
        Assert.Equal("a == (b < c)", P(B(IrBinaryOp.Equal, N("a"), B(IrBinaryOp.Less, N("b"), N("c")))));
        // Comparisons are left-associative: (a < b) == c needs no parens.
        Assert.Equal("a < b == c", P(B(IrBinaryOp.Equal, B(IrBinaryOp.Less, N("a"), N("b")), N("c"))));
    }

    [Fact]
    public void LeftAssociative_RightOperandOfSamePrecedenceIsParenthesized()
    {
        // a - (b - c) must keep its parens; (a - b) - c must not get any.
        Assert.Equal("a - (b - c)", P(B(IrBinaryOp.Subtract, N("a"), B(IrBinaryOp.Subtract, N("b"), N("c")))));
        Assert.Equal("a - b - c", P(B(IrBinaryOp.Subtract, B(IrBinaryOp.Subtract, N("a"), N("b")), N("c"))));
        Assert.Equal("a / (b * c)", P(B(IrBinaryOp.Divide, N("a"), B(IrBinaryOp.Multiply, N("b"), N("c")))));
        Assert.Equal("a \\ b % c", P(B(IrBinaryOp.Modulo, B(IrBinaryOp.IntDivide, N("a"), N("b")), N("c"))));
    }

    [Fact]
    public void RightAssociative_PowerAndConcat()
    {
        Assert.Equal("a ^ b ^ c", P(B(IrBinaryOp.Power, N("a"), B(IrBinaryOp.Power, N("b"), N("c")))));
        Assert.Equal("(a ^ b) ^ c", P(B(IrBinaryOp.Power, B(IrBinaryOp.Power, N("a"), N("b")), N("c"))));
        Assert.Equal("a .. b .. c", P(B(IrBinaryOp.Concat, N("a"), B(IrBinaryOp.Concat, N("b"), N("c")))));
        Assert.Equal("(a .. b) .. c", P(B(IrBinaryOp.Concat, B(IrBinaryOp.Concat, N("a"), N("b")), N("c"))));
    }

    [Fact]
    public void Unary_VersusPower()
    {
        // -x^2 is -(x^2) in Lua: no parens needed.
        Assert.Equal("-x ^ 2", P(U(IrUnaryOp.Negate, B(IrBinaryOp.Power, N("x"), Num(2)))));
        // (-x)^2 needs them.
        Assert.Equal("(-x) ^ 2", P(B(IrBinaryOp.Power, U(IrUnaryOp.Negate, N("x")), Num(2))));
        // A negative literal behaves like unary minus.
        Assert.Equal("(-2) ^ 2", P(B(IrBinaryOp.Power, Num(-2), Num(2))));
        // 2^-1 is valid Lua and means 2^(-1).
        Assert.Equal("2 ^ -1", P(B(IrBinaryOp.Power, Num(2), Num(-1))));
        Assert.Equal("2 ^ -x", P(B(IrBinaryOp.Power, Num(2), U(IrUnaryOp.Negate, N("x")))));
    }

    [Fact]
    public void Unary_OperandOfLowerPrecedence_IsParenthesized()
    {
        Assert.Equal("-(a + b)", P(U(IrUnaryOp.Negate, B(IrBinaryOp.Add, N("a"), N("b")))));
        Assert.Equal("not (a and b)", P(U(IrUnaryOp.Not, B(IrBinaryOp.And, N("a"), N("b")))));
        Assert.Equal("#t + 1", P(B(IrBinaryOp.Add, U(IrUnaryOp.Length, N("t")), Num(1))));
        Assert.Equal("not a == b", P(B(IrBinaryOp.Equal, U(IrUnaryOp.Not, N("a")), N("b"))));
        Assert.Equal("#t.items", P(U(IrUnaryOp.Length, new IrIndex(N("t"), S("items")))));
    }

    // ---- statements ----

    [Fact]
    public void Local_WithAndWithoutValues()
    {
        Assert.Equal("local a\n", P(new IrLocal(["a"], [])));
        Assert.Equal("local a, b = 1, 2\n", P(new IrLocal(["a", "b"], [Num(1), Num(2)])));
    }

    [Fact]
    public void Assign_SingleAndMultiple()
    {
        Assert.Equal("x = 1\n", P(new IrAssign([N("x")], [Num(1)])));
        Assert.Equal("a, t.k = b, 2\n", P(new IrAssign([N("a"), new IrIndex(N("t"), S("k"))], [N("b"), Num(2)])));
    }

    [Fact]
    public void Assign_ToNonTarget_Throws() =>
        Assert.Throws<ArgumentException>(() => P(new IrAssign([Num(1)], [Num(2)])));

    [Fact]
    public void CallStatement_StartingWithParen_GetsSemicolon()
    {
        Assert.Equal("cls(0)\n", P(new IrCallStmt(Call("cls", Num(0)))));
        var callOnFunction = new IrCall(new IrFunction([], []), []);
        Assert.Equal(";(function()\nend)()\n", P(new IrCallStmt(callOnFunction)));
    }

    [Fact]
    public void If_ElseIf_Else()
    {
        var ifStmt = new IrIf(
            [
                new IrIfClause(B(IrBinaryOp.Greater, N("x"), Num(0)), [new IrCallStmt(Call("a"))]),
                new IrIfClause(B(IrBinaryOp.Less, N("x"), Num(0)), [new IrCallStmt(Call("b"))]),
            ],
            [new IrCallStmt(Call("c"))]);
        Assert.Equal(
            "if x > 0 then\n  a()\nelseif x < 0 then\n  b()\nelse\n  c()\nend\n",
            P(ifStmt));
    }

    [Fact]
    public void If_WithoutElse()
    {
        var ifStmt = new IrIf([new IrIfClause(N("ok"), [new IrBreak()])], null);
        Assert.Equal("if ok then\n  break\nend\n", P(ifStmt));
    }

    [Fact]
    public void While_And_Do()
    {
        Assert.Equal("while i < 10 do\n  i = i + 1\nend\n",
            P(new IrWhile(B(IrBinaryOp.Less, N("i"), Num(10)),
                [new IrAssign([N("i")], [B(IrBinaryOp.Add, N("i"), Num(1))])])));
        Assert.Equal("do\n  local t = 1\nend\n", P(new IrDo([new IrLocal(["t"], [Num(1)])])));
    }

    [Fact]
    public void NumericFor_WithAndWithoutStep()
    {
        Assert.Equal("for i = 0, n - 1 do\n  f(i)\nend\n",
            P(new IrNumericFor("i", Num(0), B(IrBinaryOp.Subtract, N("n"), Num(1)), null, [new IrCallStmt(Call("f", N("i")))])));
        Assert.Equal("for i = 10, 1, -1 do\nend\n",
            P(new IrNumericFor("i", Num(10), Num(1), Num(-1), [])));
    }

    [Fact]
    public void GenericFor()
    {
        Assert.Equal("for e in all(enemies) do\n  e.x = 0\nend\n",
            P(new IrGenericFor(["e"], [Call("all", N("enemies"))],
                [new IrAssign([new IrIndex(N("e"), S("x"))], [Num(0)])])));
        Assert.Equal("for k, v in pairs(t) do\nend\n",
            P(new IrGenericFor(["k", "v"], [Call("pairs", N("t"))], [])));
    }

    [Fact]
    public void Break_Goto_Label()
    {
        var loop = new IrWhile(new IrBool(true), [
            new IrIf([new IrIfClause(N("skip"), [new IrGoto("continue_1")])], null),
            new IrCallStmt(Call("work")),
            new IrLabel("continue_1"),
        ]);
        Assert.Equal(
            "while true do\n  if skip then\n    goto continue_1\n  end\n  work()\n  ::continue_1::\nend\n",
            P(loop));
    }

    [Fact]
    public void Return_LastAndNotLast()
    {
        Assert.Equal("return\n", P(new IrReturn([])));
        Assert.Equal("return a, b\n", P(new IrReturn([N("a"), N("b")])));
        // Lua only allows return at the end of a block.
        Assert.Equal("do return 1 end\nf()\n", P(new IrReturn([Num(1)]), new IrCallStmt(Call("f"))));
    }

    [Fact]
    public void FunctionStatements()
    {
        var body = new IrFunction(["a", "b"], [new IrReturn([B(IrBinaryOp.Add, N("a"), N("b"))])]);
        Assert.Equal("function add(a, b)\n  return a + b\nend\n", P(new IrFunctionStmt(["add"], body)));
        Assert.Equal("function m.add(a, b)\n  return a + b\nend\n", P(new IrFunctionStmt(["m", "add"], body)));
        Assert.Equal("local function add(a, b)\n  return a + b\nend\n", P(new IrLocalFunction("add", body)));
        Assert.Equal("function _init()\nend\n", P(new IrFunctionStmt(["_init"], new IrFunction([], []))));
    }

    [Fact]
    public void InvalidIdentifier_Throws_WithSpan()
    {
        var bad = new IrName("end") { Span = new SourceSpan("main.gd", 3, 5) };
        var ex = Assert.Throws<ArgumentException>(() => P(bad));
        Assert.Contains("main.gd:3:5", ex.Message);
        Assert.Throws<ArgumentException>(() => P(new IrLocal(["1x"], [])));
        Assert.Throws<ArgumentException>(() => P(new IrLabel("a-b")));
    }

    [Fact]
    public void Spans_AreCarriedByNodes()
    {
        var span = new SourceSpan("main.gd", 10, 2);
        var stmt = new IrBreak { Span = span };
        Assert.Equal(span, stmt.Span);
        Assert.Null(new IrBreak().Span);
    }

    [Fact]
    public void SmallProgram()
    {
        var chunk = new IrChunk([
            new IrAssign([N("x")], [Num(64)]),
            new IrFunctionStmt(["_update"], new IrFunction([], [
                new IrIf([new IrIfClause(Call("btn", Num(0)), [
                    new IrAssign([N("x")], [B(IrBinaryOp.Subtract, N("x"), Num(1))]),
                ])], null),
            ])),
            new IrFunctionStmt(["_draw"], new IrFunction([], [
                new IrCallStmt(Call("cls")),
                new IrCallStmt(Call("print", B(IrBinaryOp.Concat, S("x="), N("x")), Num(0), Num(0), Num(7))),
            ])),
        ]);
        Assert.Equal(
            "x = 64\n" +
            "function _update()\n  if btn(0) then\n    x = x - 1\n  end\nend\n" +
            "function _draw()\n  cls()\n  print(\"x=\" .. x, 0, 0, 7)\nend\n",
            LuaPrinter.Print(chunk));
    }
}
