using System.Collections.Immutable;
using Ferbento.Core.Math;

namespace Ferbento.Scripting.Compiler.Ir;

public abstract record IrExpr : IrNode;

public sealed record IrNil : IrExpr;

public sealed record IrBool(bool Value) : IrExpr;

/// <summary>Number literal, already in PICO-8 16.16 fixed point.</summary>
public sealed record IrNumber(FixedPoint Value) : IrExpr;

/// <summary>String literal. Characters are P8SCII bytes (0..255).</summary>
public sealed record IrString(string Value) : IrExpr;

/// <summary>Reference to a local, upvalue or global name.</summary>
public sealed record IrName(string Name) : IrExpr;

/// <summary><c>target[key]</c>; printed as <c>target.key</c> when the key is an identifier string.</summary>
public sealed record IrIndex(IrExpr Target, IrExpr Key) : IrExpr;

/// <summary>A call expression: <see cref="IrCall"/> or <see cref="IrMethodCall"/>.</summary>
public abstract record IrCallExpr : IrExpr;

public sealed record IrCall(IrExpr Callee, ImmutableArray<IrExpr> Arguments) : IrCallExpr;

/// <summary><c>receiver:method(args)</c>: <see cref="Receiver"/> is evaluated once and passed as <c>self</c>.</summary>
public sealed record IrMethodCall(IrExpr Receiver, string Method, ImmutableArray<IrExpr> Arguments) : IrCallExpr;

/// <summary><c>...</c>: the extra arguments of a vararg function.</summary>
public sealed record IrVararg : IrExpr;

/// <summary>
/// <c>(expr)</c> around a call or <c>...</c>: truncates the results to exactly one value.
/// Parentheses around any other expression only group, so they are not kept in the IR.
/// </summary>
public sealed record IrParen(IrExpr Inner) : IrExpr;

/// <summary>
/// Anonymous function: <c>function(params) body end</c>. <see cref="IsVararg"/> adds a trailing <c>...</c>.
/// </summary>
public sealed record IrFunction(ImmutableArray<string> Parameters, ImmutableArray<IrStmt> Body) : IrExpr
{
    public bool IsVararg { get; init; }
}

/// <summary>Table field: positional when <see cref="Key"/> is null.</summary>
public sealed record IrTableField(IrExpr? Key, IrExpr Value) : IrNode;

public sealed record IrTable(ImmutableArray<IrTableField> Fields) : IrExpr;

public sealed record IrBinary(IrBinaryOp Operator, IrExpr Left, IrExpr Right) : IrExpr;

public sealed record IrUnary(IrUnaryOp Operator, IrExpr Operand) : IrExpr;
