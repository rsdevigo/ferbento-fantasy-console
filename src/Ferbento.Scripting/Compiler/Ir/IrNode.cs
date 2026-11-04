namespace Ferbento.Scripting.Compiler.Ir;

/// <summary>
/// Position in the Lua source that produced an IR node. Line and column are 1-based.
/// </summary>
public sealed record SourceSpan(string? File, int Line, int Column)
{
    public override string ToString() =>
        File is null ? $"{Line}:{Column}" : $"{File}:{Line}:{Column}";
}

/// <summary>
/// Base of every IR node. The IR is an AST of PICO-8 Lua: it is the single source of
/// semantics, consumed by the bytecode compiler (VM) and by the Lua printer (export).
/// <see cref="Span"/> is null only for synthetic nodes (built by hand or by later passes).
/// </summary>
public abstract record IrNode
{
    public SourceSpan? Span { get; init; }
}
