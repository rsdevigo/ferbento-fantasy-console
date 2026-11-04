using System.Collections.Immutable;

namespace Ferbento.Scripting.Compiler.Ir;

public abstract record IrStmt : IrNode;

/// <summary>A whole program: the top-level block of the generated Lua.</summary>
public sealed record IrChunk(ImmutableArray<IrStmt> Body) : IrNode;

/// <summary><c>local a, b = x, y</c>. <see cref="Values"/> may be empty.</summary>
public sealed record IrLocal(ImmutableArray<string> Names, ImmutableArray<IrExpr> Values) : IrStmt;

/// <summary><c>a, t[k] = x, y</c>. Targets must be <see cref="IrName"/> or <see cref="IrIndex"/>.</summary>
public sealed record IrAssign(ImmutableArray<IrExpr> Targets, ImmutableArray<IrExpr> Values) : IrStmt;

/// <summary>A call used as a statement.</summary>
public sealed record IrCallStmt(IrCallExpr Call) : IrStmt;

public sealed record IrIfClause(IrExpr Condition, ImmutableArray<IrStmt> Body) : IrNode;

/// <summary><c>if … elseif … else … end</c>. <see cref="Else"/> is null when absent.</summary>
public sealed record IrIf(ImmutableArray<IrIfClause> Clauses, ImmutableArray<IrStmt>? Else) : IrStmt;

public sealed record IrWhile(IrExpr Condition, ImmutableArray<IrStmt> Body) : IrStmt;

/// <summary><c>repeat … until cond</c>. The condition sees the body's locals.</summary>
public sealed record IrRepeat(ImmutableArray<IrStmt> Body, IrExpr Condition) : IrStmt;

/// <summary><c>for v = start, limit[, step] do … end</c> (limit inclusive).</summary>
public sealed record IrNumericFor(
    string Variable, IrExpr Start, IrExpr Limit, IrExpr? Step, ImmutableArray<IrStmt> Body) : IrStmt;

/// <summary><c>for a, b in exprs do … end</c>.</summary>
public sealed record IrGenericFor(
    ImmutableArray<string> Variables, ImmutableArray<IrExpr> Iterators, ImmutableArray<IrStmt> Body) : IrStmt;

/// <summary><c>do … end</c>, used to scope locals (e.g. around <c>continue</c> labels).</summary>
public sealed record IrDo(ImmutableArray<IrStmt> Body) : IrStmt;

public sealed record IrBreak : IrStmt;

public sealed record IrReturn(ImmutableArray<IrExpr> Values) : IrStmt;

public sealed record IrGoto(string Label) : IrStmt;

public sealed record IrLabel(string Name) : IrStmt;

/// <summary>
/// <c>function a.b.c(params) … end</c>: assigns to a global (or a field path).
/// With <see cref="IsMethod"/>, it is <c>function a.b:c(params)</c>, and the first parameter of
/// <see cref="Function"/> is the implicit <c>self</c>.
/// </summary>
public sealed record IrFunctionStmt(ImmutableArray<string> NamePath, IrFunction Function) : IrStmt
{
    public bool IsMethod { get; init; }
}

/// <summary><c>local function f(params) … end</c>.</summary>
public sealed record IrLocalFunction(string Name, IrFunction Function) : IrStmt;
