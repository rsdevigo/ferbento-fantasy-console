using System.Collections.Immutable;
using Ferbento.Scripting.Compiler.Diagnostics;
using Ferbento.Scripting.Compiler.Ir;

namespace Ferbento.Scripting.Compiler.Analysis;

public enum NameKind
{
    /// <summary>A local of the function where the name appears.</summary>
    Local,

    /// <summary>A local of an enclosing function, reached through <see cref="FunctionInfo.Upvalues"/>.</summary>
    Upvalue,

    /// <summary>A field of the environment: the globals, or a user <c>_ENV</c> (see <see cref="NameResolution.Environment"/>).</summary>
    Global,

    /// <summary>The name <c>_ENV</c> itself, when no local <c>_ENV</c> is in scope: the globals table.</summary>
    Environment,
}

/// <summary>
/// A local variable declaration. Each declaration is a distinct object, even when two have the same
/// name, so the compiler can map it to a register or slot.
/// </summary>
public sealed class LocalVariable
{
    internal LocalVariable(string name, SourceSpan? span, FunctionInfo function, int index)
    {
        Name = name;
        Span = span;
        Function = function;
        Index = index;
    }

    public string Name { get; }

    public SourceSpan? Span { get; }

    /// <summary>The function that declares it.</summary>
    public FunctionInfo Function { get; }

    /// <summary>Order of the declaration inside <see cref="Function"/> (0, 1, 2…).</summary>
    public int Index { get; }

    /// <summary>True when a nested function uses it, so it must live in a closable upvalue cell.</summary>
    public bool IsCaptured { get; internal set; }

    public override string ToString() => $"{Name}#{Index}";
}

/// <summary>
/// An upvalue of a function: <see cref="Variable"/> comes either from the enclosing function's locals
/// (<see cref="FromParentLocal"/>) or from its upvalue number <see cref="ParentUpvalueIndex"/>, as in Lua.
/// </summary>
public sealed record UpvalueInfo(string Name, LocalVariable Variable, bool FromParentLocal, int ParentUpvalueIndex);

/// <summary>What the analysis learned about one function (or the main chunk).</summary>
public sealed class FunctionInfo
{
    private readonly List<LocalVariable> _locals = [];
    private readonly List<UpvalueInfo> _upvalues = [];

    internal FunctionInfo(IrFunction? node, FunctionInfo? parent, bool isVararg)
    {
        Node = node;
        Parent = parent;
        IsVararg = isVararg;
    }

    /// <summary>The function node, or null for the main chunk.</summary>
    public IrFunction? Node { get; }

    public FunctionInfo? Parent { get; }

    /// <summary>The main chunk is vararg, like in Lua.</summary>
    public bool IsVararg { get; }

    /// <summary>Every local declared in this function, in declaration order (parameters first).</summary>
    public IReadOnlyList<LocalVariable> Locals => _locals;

    public IReadOnlyList<UpvalueInfo> Upvalues => _upvalues;

    /// <summary>True when this function (or a nested one) reads or writes the default environment.</summary>
    public bool UsesGlobals { get; internal set; }

    /// <summary>
    /// Largest number of locals alive at the same time, counting the 3 hidden control slots of each
    /// <c>for</c> loop, as Lua does.
    /// </summary>
    public int MaxActiveLocals { get; internal set; }

    internal LocalVariable AddLocal(string name, SourceSpan? span)
    {
        var variable = new LocalVariable(name, span, this, _locals.Count);
        _locals.Add(variable);
        return variable;
    }

    internal int AddUpvalue(UpvalueInfo upvalue)
    {
        for (var i = 0; i < _upvalues.Count; i++)
        {
            if (ReferenceEquals(_upvalues[i].Variable, upvalue.Variable)) return i;
        }
        _upvalues.Add(upvalue);
        return _upvalues.Count - 1;
    }
}

/// <summary>Where a name points to.</summary>
public sealed record NameResolution(NameKind Kind, string Name)
{
    /// <summary>The variable, for <see cref="NameKind.Local"/> and <see cref="NameKind.Upvalue"/>.</summary>
    public LocalVariable? Variable { get; init; }

    /// <summary>Index in the current function's <see cref="FunctionInfo.Upvalues"/>, for <see cref="NameKind.Upvalue"/>.</summary>
    public int UpvalueIndex { get; init; } = -1;

    /// <summary>
    /// For <see cref="NameKind.Global"/>: the user <c>_ENV</c> the name is a field of, or null for the
    /// default environment (the globals).
    /// </summary>
    public NameResolution? Environment { get; init; }
}

/// <summary>
/// Result of <see cref="StaticAnalyzer"/>. Lookups are by node identity, not by record equality:
/// two <c>IrName("x")</c> in different places are different occurrences.
/// </summary>
public sealed class AnalysisResult
{
    private readonly Dictionary<IrName, NameResolution> _names;
    private readonly Dictionary<IrFunctionStmt, NameResolution> _functionNames;
    private readonly Dictionary<IrFunction, FunctionInfo> _functions;
    private readonly Dictionary<IrNode, ImmutableArray<LocalVariable>> _declarations;

    internal AnalysisResult(
        ImmutableArray<Diagnostic> diagnostics,
        FunctionInfo mainChunk,
        IReadOnlySet<string> assignedGlobals,
        Dictionary<IrName, NameResolution> names,
        Dictionary<IrFunctionStmt, NameResolution> functionNames,
        Dictionary<IrFunction, FunctionInfo> functions,
        Dictionary<IrNode, ImmutableArray<LocalVariable>> declarations)
    {
        Diagnostics = diagnostics;
        MainChunk = mainChunk;
        AssignedGlobals = assignedGlobals;
        _names = names;
        _functionNames = functionNames;
        _functions = functions;
        _declarations = declarations;
    }

    public ImmutableArray<Diagnostic> Diagnostics { get; }

    public bool HasErrors => Diagnostics.Any(d => d.IsError);

    public FunctionInfo MainChunk { get; }

    /// <summary>Globals that receive a value somewhere in the program (assignment or <c>function name()</c>).</summary>
    public IReadOnlySet<string> AssignedGlobals { get; }

    /// <summary>Resolution of a name occurrence that belongs to the analyzed chunk.</summary>
    public NameResolution Resolve(IrName name) =>
        _names.TryGetValue(name, out var resolution)
            ? resolution
            : throw new KeyNotFoundException($"'{name.Name}' at {name.Span} is not part of the analyzed chunk.");

    /// <summary>Resolution of the first name of <c>function a.b.c()</c> (the variable that is assigned or indexed).</summary>
    public NameResolution ResolveFunctionName(IrFunctionStmt statement) => _functionNames[statement];

    public FunctionInfo GetFunction(IrFunction function) => _functions[function];

    /// <summary>
    /// Locals created by <paramref name="node"/>: <see cref="IrLocal"/>, <see cref="IrLocalFunction"/>,
    /// <see cref="IrNumericFor"/>, <see cref="IrGenericFor"/> or the parameters of an <see cref="IrFunction"/>.
    /// </summary>
    public ImmutableArray<LocalVariable> GetDeclaredLocals(IrNode node) =>
        _declarations.TryGetValue(node, out var locals) ? locals : [];
}
