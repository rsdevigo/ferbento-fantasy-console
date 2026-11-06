using System.Collections.Immutable;
using Ferbento.Scripting.Compiler.Analysis;
using Ferbento.Scripting.Compiler.Diagnostics;
using Ferbento.Scripting.Compiler.Parsing;
using Ferbento.Scripting.Vm;

namespace Ferbento.Scripting.Compiler.Bytecode;

/// <summary>Result of <see cref="LuaCompiler.Compile"/>: the main prototype, or the errors that prevented it.</summary>
public sealed record CompilationResult(
    Prototype? Main,
    ImmutableArray<Diagnostic> Diagnostics,
    ParseResult Parse,
    AnalysisResult? Analysis)
{
    public bool Success => Main is not null;

    public IEnumerable<Diagnostic> Errors => Diagnostics.Where(d => d.IsError);

    public IEnumerable<Diagnostic> Warnings => Diagnostics.Where(d => !d.IsError);
}

/// <summary>The whole pipeline: Lua source → parser → static analysis → bytecode.</summary>
public static class LuaCompiler
{
    public static CompilationResult Compile(string source, string? file = null, AnalysisOptions? options = null)
    {
        var parse = LuaParser.Parse(source, file);
        if (parse.HasErrors) return new CompilationResult(null, parse.Diagnostics, parse, null);

        var analysis = StaticAnalyzer.Analyze(parse, options);
        var diagnostics = parse.Diagnostics.AddRange(analysis.Diagnostics);
        if (analysis.HasErrors) return new CompilationResult(null, diagnostics, parse, analysis);

        var main = BytecodeCompiler.Compile(parse.Chunk, analysis, file ?? "main chunk");
        return new CompilationResult(main, diagnostics, parse, analysis);
    }
}
