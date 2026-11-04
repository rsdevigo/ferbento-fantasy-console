using System.Collections.Immutable;
using Ferbento.Scripting.Compiler.Diagnostics;
using Ferbento.Scripting.Compiler.Ir;

namespace Ferbento.Scripting.Compiler.Parsing;

/// <summary>Result of parsing: the IR (partial when there are errors), the diagnostics and the tokens.</summary>
public sealed record ParseResult(IrChunk Chunk, ImmutableArray<Diagnostic> Diagnostics, ImmutableArray<Token> Tokens)
{
    public bool HasErrors => Diagnostics.Any(d => d.IsError);
}

/// <summary>
/// Recursive-descent parser for the Lua 5.2 grammar that PICO-8 accepts, producing IR directly.
/// Operator precedence follows Lua 5.3 with the PICO-8 operators (same table as <c>LuaPrinter</c>).
/// After a syntax error it skips to the next statement and goes on, so several errors can be
/// reported at once; the returned IR is then incomplete and must not be compiled.
/// </summary>
public sealed class LuaParser
{
    // Lua's subexpr priorities (left, right). Right < left means right associative.
    private const int UnaryPriority = 12;

    private readonly ImmutableArray<Token> _tokens;
    private readonly List<Diagnostic> _diagnostics;
    private int _pos;
    private int _lastErrorPos = -1;

    private sealed class SyntaxError : Exception;

    private LuaParser(ImmutableArray<Token> tokens, List<Diagnostic> diagnostics)
    {
        _tokens = tokens;
        _diagnostics = diagnostics;
    }

    public static ParseResult Parse(string source, string? file = null)
    {
        var diagnostics = new List<Diagnostic>();
        var tokens = LuaLexer.Tokenize(source, file, diagnostics);
        var parser = new LuaParser(tokens, diagnostics);
        var chunk = parser.ParseChunk();
        return new ParseResult(chunk, [.. diagnostics.OrderBy(d => d.Span.Line).ThenBy(d => d.Span.Column)], tokens);
    }

    // ---- token helpers ----

    private Token Current => _tokens[_pos];

    private Token PeekToken(int offset) => _tokens[Math.Min(_pos + offset, _tokens.Length - 1)];

    private bool Check(TokenKind kind) => Current.Kind == kind;

    private Token Advance()
    {
        var token = Current;
        if (_pos < _tokens.Length - 1) _pos++;
        return token;
    }

    private bool Accept(TokenKind kind)
    {
        if (!Check(kind)) return false;
        Advance();
        return true;
    }

    private Token Expect(TokenKind kind, string? what = null)
    {
        if (Check(kind)) return Advance();
        throw Error(DiagnosticCatalog.Expected, Current, what ?? Token.Describe(kind), Current.Describe());
    }

    /// <summary>Expects the token that closes <paramref name="opener"/> (e.g. the <c>end</c> of a <c>function</c>).</summary>
    private Token ExpectClosing(TokenKind kind, Token opener)
    {
        if (Check(kind)) return Advance();
        if (opener.Span.Line == Current.Span.Line)
            throw Error(DiagnosticCatalog.Expected, Current, Token.Describe(kind), Current.Describe());
        throw Error(DiagnosticCatalog.ExpectedClosing, Current,
            Token.Describe(kind), $"'{opener.Text}'", opener.Span.Line, Current.Describe());
    }

    private string ExpectName() => Expect(TokenKind.Name).StringValue!;

    private SyntaxError Error(DiagnosticDescriptor descriptor, Token at, params object[] args)
    {
        Report(descriptor, at, args);
        return new SyntaxError();
    }

    private void Report(DiagnosticDescriptor descriptor, Token at, params object[] args)
    {
        // One error per position: a failed statement often trips again on the same token.
        if (_pos == _lastErrorPos) return;
        _lastErrorPos = _pos;
        _diagnostics.Add(descriptor.Create(at.Span, args));
    }

    private static T At<T>(T node, Token token) where T : IrNode => node with { Span = token.Span };

    // ---- blocks and recovery ----

    private IrChunk ParseChunk()
    {
        var start = Current;
        var body = ParseBlock();
        while (!Check(TokenKind.EndOfFile))
        {
            // A stray closer at top level ('end', 'until'…): report it and keep parsing after it.
            Report(DiagnosticCatalog.Expected, Current, "um statement", Current.Describe());
            Advance();
            body = body.AddRange(ParseBlock());
        }
        return At(new IrChunk(body), start);
    }

    private bool IsBlockEnd() => Current.Kind is TokenKind.EndOfFile or TokenKind.End or TokenKind.Else
        or TokenKind.Elseif or TokenKind.Until;

    private ImmutableArray<IrStmt> ParseBlock()
    {
        var statements = ImmutableArray.CreateBuilder<IrStmt>();
        while (!IsBlockEnd())
        {
            var start = _pos;
            try
            {
                if (Check(TokenKind.Return))
                {
                    var returnToken = Current;
                    statements.Add(ParseReturn());
                    if (!IsBlockEnd())
                    {
                        Report(DiagnosticCatalog.ReturnNotLast, returnToken);
                        continue;
                    }
                    break;
                }

                if (ParseStatement() is { } statement) statements.Add(statement);
            }
            catch (SyntaxError)
            {
                Synchronize(start);
            }
        }
        return statements.ToImmutable();
    }

    /// <summary>Skips tokens up to something that looks like the start of the next statement.</summary>
    private void Synchronize(int failedStatementStart)
    {
        if (_pos == failedStatementStart) Advance();
        var errorLine = _tokens[Math.Max(_pos - 1, 0)].Span.Line;
        while (!Check(TokenKind.EndOfFile))
        {
            switch (Current.Kind)
            {
                case TokenKind.Local or TokenKind.Function or TokenKind.If or TokenKind.While or TokenKind.For
                    or TokenKind.Repeat or TokenKind.Return or TokenKind.Do or TokenKind.Break or TokenKind.Goto
                    or TokenKind.DoubleColon or TokenKind.Semicolon:
                case TokenKind.End or TokenKind.Else or TokenKind.Elseif or TokenKind.Until:
                    return;
                case TokenKind.Name when Current.Span.Line > errorLine:
                    return;
            }
            Advance();
        }
    }

    // ---- statements ----

    private IrStmt? ParseStatement()
    {
        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.Semicolon:
                Advance();
                return null;

            case TokenKind.DoubleColon:
                Advance();
                var label = ExpectName();
                Expect(TokenKind.DoubleColon);
                return At(new IrLabel(label), token);

            case TokenKind.Break:
                Advance();
                return At(new IrBreak(), token);

            case TokenKind.Goto:
                Advance();
                return At(new IrGoto(ExpectName()), token);

            case TokenKind.Do:
            {
                Advance();
                var body = ParseBlock();
                ExpectClosing(TokenKind.End, token);
                return At(new IrDo(body), token);
            }

            case TokenKind.While:
            {
                Advance();
                var condition = ParseExpression();
                Expect(TokenKind.Do);
                var body = ParseBlock();
                ExpectClosing(TokenKind.End, token);
                return At(new IrWhile(condition, body), token);
            }

            case TokenKind.Repeat:
            {
                Advance();
                var body = ParseBlock();
                ExpectClosing(TokenKind.Until, token);
                return At(new IrRepeat(body, ParseExpression()), token);
            }

            case TokenKind.If:
                return ParseIf();

            case TokenKind.For:
                return ParseFor();

            case TokenKind.Function:
                return ParseFunctionStatement();

            case TokenKind.Local:
                Advance();
                if (Check(TokenKind.Function))
                {
                    var functionToken = Advance();
                    var name = ExpectName();
                    return At(new IrLocalFunction(name, ParseFunctionBody(functionToken)), token);
                }
                return ParseLocal(token);

            default:
                return ParseExpressionStatement();
        }
    }

    private IrIf ParseIf()
    {
        var ifToken = Advance();
        var clauses = ImmutableArray.CreateBuilder<IrIfClause>();
        ImmutableArray<IrStmt>? elseBlock = null;

        var clauseToken = ifToken;
        while (true)
        {
            var condition = ParseExpression();
            Expect(TokenKind.Then);
            clauses.Add(At(new IrIfClause(condition, ParseBlock()), clauseToken));

            if (Check(TokenKind.Elseif))
            {
                clauseToken = Advance();
                continue;
            }
            // An empty 'else' means nothing; dropping it keeps the IR canonical.
            if (Accept(TokenKind.Else) && ParseBlock() is { IsEmpty: false } block) elseBlock = block;
            break;
        }

        ExpectClosing(TokenKind.End, ifToken);
        return At(new IrIf(clauses.ToImmutable(), elseBlock), ifToken);
    }

    private IrStmt ParseFor()
    {
        var forToken = Advance();
        var first = ExpectName();

        if (Accept(TokenKind.Assign))
        {
            var start = ParseExpression();
            Expect(TokenKind.Comma);
            var limit = ParseExpression();
            var step = Accept(TokenKind.Comma) ? ParseExpression() : null;
            Expect(TokenKind.Do);
            var body = ParseBlock();
            ExpectClosing(TokenKind.End, forToken);
            return At(new IrNumericFor(first, start, limit, step, body), forToken);
        }

        var names = ImmutableArray.CreateBuilder<string>();
        names.Add(first);
        while (Accept(TokenKind.Comma)) names.Add(ExpectName());
        Expect(TokenKind.In, names.Count == 1 ? "'=' ou 'in'" : null);
        var iterators = ParseExpressionList();
        Expect(TokenKind.Do);
        var loopBody = ParseBlock();
        ExpectClosing(TokenKind.End, forToken);
        return At(new IrGenericFor(names.ToImmutable(), iterators, loopBody), forToken);
    }

    private IrFunctionStmt ParseFunctionStatement()
    {
        var functionToken = Advance();
        var path = ImmutableArray.CreateBuilder<string>();
        path.Add(ExpectName());
        while (Accept(TokenKind.Dot)) path.Add(ExpectName());
        var isMethod = Accept(TokenKind.Colon);
        if (isMethod) path.Add(ExpectName());

        var function = ParseFunctionBody(functionToken, isMethod);
        return At(new IrFunctionStmt(path.ToImmutable(), function) { IsMethod = isMethod }, functionToken);
    }

    /// <summary>Parses <c>(params) block end</c>; <paramref name="functionToken"/> is the <c>function</c> keyword.</summary>
    private IrFunction ParseFunctionBody(Token functionToken, bool isMethod = false)
    {
        var parameters = ImmutableArray.CreateBuilder<string>();
        if (isMethod) parameters.Add("self");
        var isVararg = false;

        Expect(TokenKind.LeftParen);
        if (!Check(TokenKind.RightParen))
        {
            do
            {
                if (Accept(TokenKind.Ellipsis))
                {
                    isVararg = true;
                    break;
                }
                parameters.Add(Expect(TokenKind.Name, "um nome de parâmetro ou '...'").StringValue!);
            } while (Accept(TokenKind.Comma));
        }
        Expect(TokenKind.RightParen);

        var body = ParseBlock();
        ExpectClosing(TokenKind.End, functionToken);
        return At(new IrFunction(parameters.ToImmutable(), body) { IsVararg = isVararg }, functionToken);
    }

    private IrLocal ParseLocal(Token localToken)
    {
        var names = ImmutableArray.CreateBuilder<string>();
        do names.Add(ExpectName());
        while (Accept(TokenKind.Comma));

        var values = Accept(TokenKind.Assign) ? ParseExpressionList() : [];
        return At(new IrLocal(names.ToImmutable(), values), localToken);
    }

    private IrReturn ParseReturn()
    {
        var returnToken = Advance();
        var values = IsBlockEnd() || Check(TokenKind.Semicolon) ? [] : ParseExpressionList();
        Accept(TokenKind.Semicolon);
        return At(new IrReturn(values), returnToken);
    }

    private IrStmt ParseExpressionStatement()
    {
        var startToken = Current;
        var (first, firstAssignable) = ParseSuffixedExpression();

        if (Check(TokenKind.Assign) || Check(TokenKind.Comma))
        {
            var targets = ImmutableArray.CreateBuilder<IrExpr>();
            AddTarget(first, firstAssignable, startToken);
            while (Accept(TokenKind.Comma))
            {
                var targetToken = Current;
                var (target, assignable) = ParseSuffixedExpression();
                AddTarget(target, assignable, targetToken);
            }
            Expect(TokenKind.Assign);
            return At(new IrAssign(targets.ToImmutable(), ParseExpressionList()), startToken);

            void AddTarget(IrExpr target, bool assignable, Token token)
            {
                if (!assignable) throw Error(DiagnosticCatalog.InvalidAssignmentTarget, token);
                targets.Add(target);
            }
        }

        if (first is IrCallExpr call) return At(new IrCallStmt(call), startToken);
        throw Error(DiagnosticCatalog.NotAStatement, startToken);
    }

    // ---- expressions ----

    private ImmutableArray<IrExpr> ParseExpressionList()
    {
        var list = ImmutableArray.CreateBuilder<IrExpr>();
        do list.Add(ParseExpression());
        while (Accept(TokenKind.Comma));
        return list.ToImmutable();
    }

    private IrExpr ParseExpression() => ParseSubExpression(0);

    /// <summary>Lua's subexpr: parses operators whose left priority is above <paramref name="limit"/>.</summary>
    private IrExpr ParseSubExpression(int limit)
    {
        IrExpr left;
        var startToken = Current;
        if (UnaryOperator(Current.Kind) is { } unary)
        {
            Advance();
            left = At(new IrUnary(unary, ParseSubExpression(UnaryPriority)), startToken);
        }
        else
        {
            left = ParseSimpleExpression();
        }

        while (BinaryOperator(Current.Kind) is { } binary && binary.Left > limit)
        {
            var operatorToken = Advance();
            var right = ParseSubExpression(binary.Right);
            left = At(new IrBinary(binary.Op, left, right), operatorToken);
        }
        return left;
    }

    private static IrUnaryOp? UnaryOperator(TokenKind kind) => kind switch
    {
        TokenKind.Minus => IrUnaryOp.Negate,
        TokenKind.Not => IrUnaryOp.Not,
        TokenKind.Hash => IrUnaryOp.Length,
        TokenKind.Tilde => IrUnaryOp.BitNot,
        _ => null,
    };

    private static (IrBinaryOp Op, int Left, int Right)? BinaryOperator(TokenKind kind) => kind switch
    {
        TokenKind.Or => (IrBinaryOp.Or, 1, 1),
        TokenKind.And => (IrBinaryOp.And, 2, 2),
        TokenKind.Less => (IrBinaryOp.Less, 3, 3),
        TokenKind.Greater => (IrBinaryOp.Greater, 3, 3),
        TokenKind.LessEqual => (IrBinaryOp.LessEqual, 3, 3),
        TokenKind.GreaterEqual => (IrBinaryOp.GreaterEqual, 3, 3),
        TokenKind.EqualEqual => (IrBinaryOp.Equal, 3, 3),
        TokenKind.TildeEqual => (IrBinaryOp.NotEqual, 3, 3),
        TokenKind.Pipe => (IrBinaryOp.BitOr, 4, 4),
        TokenKind.CaretCaret => (IrBinaryOp.BitXor, 5, 5),
        TokenKind.Ampersand => (IrBinaryOp.BitAnd, 6, 6),
        TokenKind.ShiftLeft => (IrBinaryOp.ShiftLeft, 7, 7),
        TokenKind.ShiftRight => (IrBinaryOp.ShiftRight, 7, 7),
        TokenKind.ShiftRightLogical => (IrBinaryOp.LogicalShiftRight, 7, 7),
        TokenKind.RotateLeft => (IrBinaryOp.RotateLeft, 7, 7),
        TokenKind.RotateRight => (IrBinaryOp.RotateRight, 7, 7),
        TokenKind.DotDot => (IrBinaryOp.Concat, 9, 8),
        TokenKind.Plus => (IrBinaryOp.Add, 10, 10),
        TokenKind.Minus => (IrBinaryOp.Subtract, 10, 10),
        TokenKind.Star => (IrBinaryOp.Multiply, 11, 11),
        TokenKind.Slash => (IrBinaryOp.Divide, 11, 11),
        TokenKind.Backslash => (IrBinaryOp.IntDivide, 11, 11),
        TokenKind.Percent => (IrBinaryOp.Modulo, 11, 11),
        TokenKind.Caret => (IrBinaryOp.Power, 14, 13),
        _ => null,
    };

    private IrExpr ParseSimpleExpression()
    {
        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.Number:
                Advance();
                return At(new IrNumber(token.NumberValue), token);
            case TokenKind.String:
                Advance();
                return At(new IrString(token.StringValue!), token);
            case TokenKind.Nil:
                Advance();
                return At(new IrNil(), token);
            case TokenKind.True:
                Advance();
                return At(new IrBool(true), token);
            case TokenKind.False:
                Advance();
                return At(new IrBool(false), token);
            case TokenKind.Ellipsis:
                Advance();
                return At(new IrVararg(), token);
            case TokenKind.LeftBrace:
                return ParseTable();
            case TokenKind.Function:
                Advance();
                return ParseFunctionBody(token);
            default:
                return ParseSuffixedExpression().Expr;
        }
    }

    /// <summary>
    /// <c>primary { .name | [exp] | :name args | args }</c>. <c>Assignable</c> tells whether the result
    /// can be the target of an assignment (a name or an index, but not a parenthesized name).
    /// </summary>
    private (IrExpr Expr, bool Assignable) ParseSuffixedExpression()
    {
        var (expr, assignable) = ParsePrimaryExpression();
        while (true)
        {
            var token = Current;
            switch (token.Kind)
            {
                case TokenKind.Dot:
                {
                    Advance();
                    var nameToken = Expect(TokenKind.Name);
                    expr = At(new IrIndex(expr, At(new IrString(nameToken.StringValue!), nameToken)), token);
                    assignable = true;
                    break;
                }
                case TokenKind.LeftBracket:
                {
                    Advance();
                    var key = ParseExpression();
                    ExpectClosing(TokenKind.RightBracket, token);
                    expr = At(new IrIndex(expr, key), token);
                    assignable = true;
                    break;
                }
                case TokenKind.Colon:
                {
                    Advance();
                    var method = ExpectName();
                    expr = At(new IrMethodCall(expr, method, ParseCallArguments()), token);
                    assignable = false;
                    break;
                }
                case TokenKind.LeftParen or TokenKind.String or TokenKind.LeftBrace:
                    expr = At(new IrCall(expr, ParseCallArguments()), token);
                    assignable = false;
                    break;
                default:
                    return (expr, assignable);
            }
        }
    }

    private (IrExpr Expr, bool Assignable) ParsePrimaryExpression()
    {
        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.Name:
                Advance();
                return (At(new IrName(token.StringValue!), token), true);
            case TokenKind.LeftParen:
            {
                Advance();
                var inner = ParseExpression();
                ExpectClosing(TokenKind.RightParen, token);
                // Only calls and '...' change meaning inside parentheses (they truncate to one value).
                var expr = inner is IrCallExpr or IrVararg ? At(new IrParen(inner), token) : inner;
                return (expr, false);
            }
            default:
                throw Error(DiagnosticCatalog.ExpectedExpression, token, token.Describe());
        }
    }

    private ImmutableArray<IrExpr> ParseCallArguments()
    {
        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.String:
                Advance();
                return [At(new IrString(token.StringValue!), token)];
            case TokenKind.LeftBrace:
                return [ParseTable()];
            case TokenKind.LeftParen:
            {
                Advance();
                var arguments = Check(TokenKind.RightParen) ? [] : ParseExpressionList();
                ExpectClosing(TokenKind.RightParen, token);
                return arguments;
            }
            default:
                throw Error(DiagnosticCatalog.Expected, token, "os argumentos da chamada", token.Describe());
        }
    }

    private IrTable ParseTable()
    {
        var openToken = Expect(TokenKind.LeftBrace);
        var fields = ImmutableArray.CreateBuilder<IrTableField>();

        while (!Check(TokenKind.RightBrace))
        {
            var fieldToken = Current;
            if (Check(TokenKind.LeftBracket))
            {
                Advance();
                var key = ParseExpression();
                ExpectClosing(TokenKind.RightBracket, fieldToken);
                Expect(TokenKind.Assign);
                fields.Add(At(new IrTableField(key, ParseExpression()), fieldToken));
            }
            else if (Check(TokenKind.Name) && PeekToken(1).Kind == TokenKind.Assign)
            {
                Advance();
                Advance();
                var key = At(new IrString(fieldToken.StringValue!), fieldToken);
                fields.Add(At(new IrTableField(key, ParseExpression()), fieldToken));
            }
            else
            {
                fields.Add(At(new IrTableField(null, ParseExpression()), fieldToken));
            }

            if (!Accept(TokenKind.Comma) && !Accept(TokenKind.Semicolon)) break;
        }

        ExpectClosing(TokenKind.RightBrace, openToken);
        return At(new IrTable(fields.ToImmutable()), openToken);
    }
}
