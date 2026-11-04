using System.Collections.Immutable;
using System.Text;
using Ferbento.Scripting.Compiler.Ir;

namespace Ferbento.Scripting.Compiler.Lua;

/// <summary>
/// Prints IR as PICO-8 Lua source. Parentheses are emitted only where precedence or
/// associativity requires them (each pair costs a PICO-8 token).
/// </summary>
public sealed class LuaPrinter
{
    private const string IndentUnit = "  ";

    // PICO-8 follows Lua 5.3 precedence, with ^^ in place of ~ (xor) and \ next to //.
    private const int PrecOr = 1;
    private const int PrecAnd = 2;
    private const int PrecCompare = 3;
    private const int PrecBitOr = 4;
    private const int PrecBitXor = 5;
    private const int PrecBitAnd = 6;
    private const int PrecShift = 7;
    private const int PrecConcat = 8;
    private const int PrecAdditive = 9;
    private const int PrecMultiplicative = 10;
    private const int PrecUnary = 11;
    private const int PrecPower = 12;
    private const int PrecAtom = 100;

    private readonly StringBuilder _out = new();
    private int _indent;

    public static string Print(IrChunk chunk)
    {
        var printer = new LuaPrinter();
        printer.WriteBlock(chunk.Body);
        return printer._out.ToString();
    }

    public static string Print(IrStmt statement) => Print(new IrChunk([statement]));

    public static string Print(IrExpr expression) => new LuaPrinter().Expr(expression).Text;

    // ---- statements ----

    private void WriteBlock(ImmutableArray<IrStmt> block)
    {
        for (var i = 0; i < block.Length; i++)
        {
            var isLast = i == block.Length - 1;
            WriteStatement(block[i], isLast);
        }
    }

    private void WriteIndentedBlock(ImmutableArray<IrStmt> block)
    {
        _indent++;
        WriteBlock(block);
        _indent--;
    }

    private void Line(string text)
    {
        for (var i = 0; i < _indent; i++) _out.Append(IndentUnit);
        _out.Append(text).Append('\n');
    }

    private void WriteStatement(IrStmt stmt, bool isLastInBlock)
    {
        switch (stmt)
        {
            case IrLocal local:
                RequireNames(local.Names, local);
                Line(local.Values.IsDefaultOrEmpty
                    ? $"local {string.Join(", ", local.Names)}"
                    : $"local {string.Join(", ", local.Names)} = {ExprList(local.Values)}");
                break;

            case IrAssign assign:
                if (assign.Targets.IsDefaultOrEmpty || assign.Values.IsDefaultOrEmpty)
                    throw Invalid(assign, "assignment needs at least one target and one value");
                foreach (var target in assign.Targets)
                {
                    if (target is not (IrName or IrIndex))
                        throw Invalid(assign, $"cannot assign to {target.GetType().Name}");
                }
                StatementLine($"{ExprList(assign.Targets)} = {ExprList(assign.Values)}");
                break;

            case IrCallStmt call:
                StatementLine(Expr(call.Call).Text);
                break;

            case IrIf ifStmt:
                WriteIf(ifStmt);
                break;

            case IrWhile whileStmt:
                Line($"while {Expr(whileStmt.Condition).Text} do");
                WriteIndentedBlock(whileStmt.Body);
                Line("end");
                break;

            case IrRepeat repeat:
                Line("repeat");
                WriteIndentedBlock(repeat.Body);
                Line($"until {Expr(repeat.Condition).Text}");
                break;

            case IrNumericFor numericFor:
                RequireName(numericFor.Variable, numericFor);
                var range = $"{Expr(numericFor.Start).Text}, {Expr(numericFor.Limit).Text}";
                if (numericFor.Step is not null) range += $", {Expr(numericFor.Step).Text}";
                Line($"for {numericFor.Variable} = {range} do");
                WriteIndentedBlock(numericFor.Body);
                Line("end");
                break;

            case IrGenericFor genericFor:
                RequireNames(genericFor.Variables, genericFor);
                if (genericFor.Iterators.IsDefaultOrEmpty)
                    throw Invalid(genericFor, "generic for needs an iterator expression");
                Line($"for {string.Join(", ", genericFor.Variables)} in {ExprList(genericFor.Iterators)} do");
                WriteIndentedBlock(genericFor.Body);
                Line("end");
                break;

            case IrDo doStmt:
                Line("do");
                WriteIndentedBlock(doStmt.Body);
                Line("end");
                break;

            case IrBreak:
                Line("break");
                break;

            case IrReturn ret:
                var returnText = ret.Values.IsDefaultOrEmpty ? "return" : $"return {ExprList(ret.Values)}";
                // Lua only allows return as the last statement of a block.
                Line(isLastInBlock ? returnText : $"do {returnText} end");
                break;

            case IrGoto gotoStmt:
                RequireName(gotoStmt.Label, gotoStmt);
                Line($"goto {gotoStmt.Label}");
                break;

            case IrLabel label:
                RequireName(label.Name, label);
                Line($"::{label.Name}::");
                break;

            case IrFunctionStmt function:
                if (function.NamePath.IsDefaultOrEmpty)
                    throw Invalid(function, "function statement needs a name");
                RequireNames(function.NamePath, function);
                if (function.IsMethod)
                {
                    if (function.NamePath.Length < 2)
                        throw Invalid(function, "method function needs a table and a method name");
                    if (function.Function.Parameters.IsDefaultOrEmpty || function.Function.Parameters[0] != "self")
                        throw Invalid(function, "method function must have 'self' as its first parameter");
                    var owner = string.Join(".", function.NamePath.Take(function.NamePath.Length - 1));
                    WriteFunction($"function {owner}:{function.NamePath[^1]}", function.Function, skipSelf: true);
                }
                else
                {
                    WriteFunction($"function {string.Join(".", function.NamePath)}", function.Function);
                }
                break;

            case IrLocalFunction localFunction:
                RequireName(localFunction.Name, localFunction);
                WriteFunction($"local function {localFunction.Name}", localFunction.Function);
                break;

            default:
                throw Invalid(stmt, $"unsupported statement {stmt.GetType().Name}");
        }
    }

    // A statement starting with '(' would be read as a call on the previous line's expression.
    private void StatementLine(string text) => Line(text.StartsWith('(') ? ";" + text : text);

    private void WriteIf(IrIf ifStmt)
    {
        if (ifStmt.Clauses.IsDefaultOrEmpty)
            throw Invalid(ifStmt, "if needs at least one clause");

        for (var i = 0; i < ifStmt.Clauses.Length; i++)
        {
            var clause = ifStmt.Clauses[i];
            Line($"{(i == 0 ? "if" : "elseif")} {Expr(clause.Condition).Text} then");
            WriteIndentedBlock(clause.Body);
        }

        if (ifStmt.Else is { IsDefaultOrEmpty: false } elseBlock)
        {
            Line("else");
            WriteIndentedBlock(elseBlock);
        }

        Line("end");
    }

    private void WriteFunction(string header, IrFunction function, bool skipSelf = false)
    {
        Line($"{header}({ParameterList(function, skipSelf)})");
        WriteIndentedBlock(function.Body);
        Line("end");
    }

    // ---- expressions ----

    private readonly record struct Printed(string Text, int Precedence, bool IsPrefix);

    private string ExprList(ImmutableArray<IrExpr> list) =>
        string.Join(", ", list.Select(e => Expr(e).Text));

    private Printed Expr(IrExpr expr)
    {
        switch (expr)
        {
            case IrNil:
                return new("nil", PrecAtom, false);
            case IrBool b:
                return new(b.Value ? "true" : "false", PrecAtom, false);
            case IrNumber n:
                var number = LuaLiterals.FormatNumber(n.Value);
                // A negative literal behaves like unary minus: -2^2 is -(2^2).
                return new(number, number.StartsWith('-') ? PrecUnary : PrecAtom, false);
            case IrString s:
                return new(LuaLiterals.FormatString(s.Value), PrecAtom, false);
            case IrName name:
                RequireName(name.Name, name);
                return new(name.Name, PrecAtom, true);
            case IrIndex index:
                var target = PrefixText(index.Target);
                if (index.Key is IrString { Value: var key } && LuaLiterals.IsIdentifier(key))
                    return new($"{target}.{key}", PrecAtom, true);
                return new($"{target}[{Expr(index.Key).Text}]", PrecAtom, true);
            case IrCall call:
                return new($"{PrefixText(call.Callee)}({ExprList(call.Arguments)})", PrecAtom, true);
            case IrMethodCall methodCall:
                RequireName(methodCall.Method, methodCall);
                return new($"{PrefixText(methodCall.Receiver)}:{methodCall.Method}({ExprList(methodCall.Arguments)})", PrecAtom, true);
            case IrVararg:
                return new("...", PrecAtom, false);
            case IrParen paren:
                return new($"({Expr(paren.Inner).Text})", PrecAtom, true);
            case IrFunction function:
                return new(FunctionText(function), PrecAtom, false);
            case IrTable table:
                return new(TableText(table), PrecAtom, false);
            case IrBinary binary:
                return Binary(binary);
            case IrUnary unary:
                return Unary(unary);
            default:
                throw Invalid(expr, $"unsupported expression {expr.GetType().Name}");
        }
    }

    // Callees and indexed values must be prefix expressions (name, index, call) or be parenthesized.
    private string PrefixText(IrExpr expr)
    {
        var printed = Expr(expr);
        return printed.IsPrefix ? printed.Text : $"({printed.Text})";
    }

    private static string ParameterList(IrFunction function, bool skipSelf = false)
    {
        RequireNames(function.Parameters, function);
        IEnumerable<string> names = function.Parameters.IsDefault ? [] : function.Parameters.Skip(skipSelf ? 1 : 0);
        if (function.IsVararg) names = names.Append("...");
        return string.Join(", ", names);
    }

    private string FunctionText(IrFunction function)
    {
        var inner = new LuaPrinter { _indent = _indent + 1 };
        inner.WriteBlock(function.Body);
        var sb = new StringBuilder();
        sb.Append($"function({ParameterList(function)})\n");
        sb.Append(inner._out);
        for (var i = 0; i < _indent; i++) sb.Append(IndentUnit);
        sb.Append("end");
        return sb.ToString();
    }

    private string TableText(IrTable table)
    {
        if (table.Fields.IsDefaultOrEmpty) return "{}";
        var fields = table.Fields.Select(field => field.Key switch
        {
            null => Expr(field.Value).Text,
            IrString { Value: var key } when LuaLiterals.IsIdentifier(key) => $"{key} = {Expr(field.Value).Text}",
            _ => $"[{Expr(field.Key).Text}] = {Expr(field.Value).Text}",
        });
        return $"{{{string.Join(", ", fields)}}}";
    }

    private Printed Binary(IrBinary binary)
    {
        var (symbol, precedence, rightAssociative) = BinaryInfo(binary.Operator);
        var left = Expr(binary.Left);
        var right = Expr(binary.Right);

        var leftNeedsParens = left.Precedence < precedence || (rightAssociative && left.Precedence == precedence);
        var rightNeedsParens = right.Precedence < precedence || (!rightAssociative && right.Precedence == precedence);

        // Lua's grammar accepts a unary operand on the right of ^ (2^-1 is 2^(-1)).
        if (binary.Operator == IrBinaryOp.Power && right.Precedence == PrecUnary)
            rightNeedsParens = false;

        var leftText = leftNeedsParens ? $"({left.Text})" : left.Text;
        var rightText = rightNeedsParens ? $"({right.Text})" : right.Text;
        return new($"{leftText} {symbol} {rightText}", precedence, false);
    }

    private Printed Unary(IrUnary unary)
    {
        var operand = Expr(unary.Operand);
        var operandText = operand.Precedence < PrecUnary ? $"({operand.Text})" : operand.Text;

        var text = unary.Operator switch
        {
            IrUnaryOp.Not => $"not {operandText}",
            // "--" would start a comment; "~~" is kept apart for the same kind of safety.
            IrUnaryOp.Negate => operandText.StartsWith('-') ? $"- {operandText}" : $"-{operandText}",
            IrUnaryOp.BitNot => operandText.StartsWith('~') ? $"~ {operandText}" : $"~{operandText}",
            IrUnaryOp.Length => $"#{operandText}",
            _ => throw Invalid(unary, $"unsupported unary operator {unary.Operator}"),
        };
        return new(text, PrecUnary, false);
    }

    private static (string Symbol, int Precedence, bool RightAssociative) BinaryInfo(IrBinaryOp op) => op switch
    {
        IrBinaryOp.Or => ("or", PrecOr, false),
        IrBinaryOp.And => ("and", PrecAnd, false),
        IrBinaryOp.Less => ("<", PrecCompare, false),
        IrBinaryOp.Greater => (">", PrecCompare, false),
        IrBinaryOp.LessEqual => ("<=", PrecCompare, false),
        IrBinaryOp.GreaterEqual => (">=", PrecCompare, false),
        IrBinaryOp.Equal => ("==", PrecCompare, false),
        IrBinaryOp.NotEqual => ("~=", PrecCompare, false),
        IrBinaryOp.BitOr => ("|", PrecBitOr, false),
        IrBinaryOp.BitXor => ("^^", PrecBitXor, false),
        IrBinaryOp.BitAnd => ("&", PrecBitAnd, false),
        IrBinaryOp.ShiftLeft => ("<<", PrecShift, false),
        IrBinaryOp.ShiftRight => (">>", PrecShift, false),
        IrBinaryOp.LogicalShiftRight => (">>>", PrecShift, false),
        IrBinaryOp.RotateLeft => ("<<>", PrecShift, false),
        IrBinaryOp.RotateRight => (">><", PrecShift, false),
        IrBinaryOp.Concat => ("..", PrecConcat, true),
        IrBinaryOp.Add => ("+", PrecAdditive, false),
        IrBinaryOp.Subtract => ("-", PrecAdditive, false),
        IrBinaryOp.Multiply => ("*", PrecMultiplicative, false),
        IrBinaryOp.Divide => ("/", PrecMultiplicative, false),
        IrBinaryOp.IntDivide => ("\\", PrecMultiplicative, false),
        IrBinaryOp.Modulo => ("%", PrecMultiplicative, false),
        IrBinaryOp.Power => ("^", PrecPower, true),
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, "unsupported binary operator"),
    };

    // ---- validation ----

    private static void RequireName(string name, IrNode node)
    {
        if (!LuaLiterals.IsIdentifier(name))
            throw Invalid(node, $"'{name}' is not a valid Lua identifier");
    }

    private static void RequireNames(ImmutableArray<string> names, IrNode node)
    {
        if (names.IsDefault) return;
        foreach (var name in names) RequireName(name, node);
    }

    private static ArgumentException Invalid(IrNode node, string message) =>
        new(node.Span is null ? message : $"{message} (at {node.Span})");
}
