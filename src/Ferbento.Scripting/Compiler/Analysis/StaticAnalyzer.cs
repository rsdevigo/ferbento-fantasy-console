using System.Collections.Immutable;
using System.Globalization;
using Ferbento.Scripting.Api;
using Ferbento.Scripting.Compiler.Diagnostics;
using Ferbento.Scripting.Compiler.Ir;
using Ferbento.Scripting.Compiler.Lua;
using Ferbento.Scripting.Compiler.Parsing;

namespace Ferbento.Scripting.Compiler.Analysis;

public sealed record AnalysisOptions
{
    /// <summary>Which API functions exist; defaults to the PICO-8 profile.</summary>
    public ApiAvailability? Api { get; init; }
}

/// <summary>
/// Static analysis of the IR: resolves every name to a local, upvalue or global (with Lua 5.2
/// scoping), checks <c>break</c>/<c>goto</c>/labels/<c>...</c> and the local and upvalue limits,
/// and warns about likely mistakes. Warnings can be silenced with <c>-- fb-ignore: FB0250</c>.
/// </summary>
public static class StaticAnalyzer
{
    /// <summary>Active locals per function (z8lua MAXVARS).</summary>
    public const int MaxLocals = 200;

    /// <summary>Upvalues per function, counting the implicit <c>_ENV</c> (z8lua MAXUPVAL = 255).</summary>
    public const int MaxUpvalues = 255;

    /// <summary>Analyzes a parsed program; tokens and comments enable the number and <c>fb-ignore</c> checks.</summary>
    public static AnalysisResult Analyze(ParseResult parse, AnalysisOptions? options = null) =>
        new Walker(options ?? new AnalysisOptions()).Run(parse.Chunk, parse.Tokens, parse.Comments);

    /// <summary>Analyzes IR built by hand (no tokens or comments).</summary>
    public static AnalysisResult Analyze(IrChunk chunk, AnalysisOptions? options = null) =>
        new Walker(options ?? new AnalysisOptions()).Run(chunk, [], []);

    private sealed class Block
    {
        public int EntryActive;
        public int FirstLabel;
        public int FirstGoto;
        public bool IsLoop;
        public bool IsRepeatBody;
    }

    private sealed record LabelEntry(string Name, SourceSpan? Span, int Active);

    private sealed class GotoEntry(string name, SourceSpan? span, int active)
    {
        public string Name { get; } = name;
        public SourceSpan? Span { get; } = span;
        public int Active { get; set; } = active;
    }

    private readonly record struct ActiveSlot(string Name, LocalVariable? Variable);

    private sealed class FunctionState(FunctionInfo info, FunctionState? parent)
    {
        public FunctionInfo Info { get; } = info;
        public FunctionState? Parent { get; } = parent;
        public List<ActiveSlot> Active { get; } = [];
        public List<Block> Blocks { get; } = [];
        public List<LabelEntry> Labels { get; } = [];
        public List<GotoEntry> Gotos { get; } = [];
        public bool ReportedTooManyLocals;
    }

    private sealed class Walker(AnalysisOptions options)
    {
        private static readonly SourceSpan NoSpan = new(null, 0, 0);

        private readonly ApiAvailability _api = options.Api ?? ApiAvailability.Pico8;
        private readonly List<Diagnostic> _diagnostics = [];
        private readonly Dictionary<IrName, NameResolution> _names = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<IrFunctionStmt, NameResolution> _functionNames = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<IrFunction, FunctionInfo> _functions = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<IrNode, ImmutableArray<LocalVariable>> _declarations = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<string> _assignedGlobals = new(StringComparer.Ordinal);
        private readonly List<(string Name, SourceSpan? Span)> _globalReads = [];
        private FunctionState _fs = null!;

        public AnalysisResult Run(IrChunk chunk, ImmutableArray<Token> tokens, ImmutableArray<LuaComment> comments)
        {
            var main = new FunctionInfo(null, null, isVararg: true);
            _fs = new FunctionState(main, null);
            VisitBlock(chunk.Body, isLoop: false);
            CloseFunction();

            ReportGlobalReads();
            ReportWrappingNumbers(tokens);

            var suppressions = Suppressions.FromComments(comments);
            var diagnostics = _diagnostics
                .Where(d => d.IsError || !suppressions.Silences(d))
                .OrderBy(d => d.Span.Line).ThenBy(d => d.Span.Column)
                .ToImmutableArray();

            return new AnalysisResult(diagnostics, main, _assignedGlobals, _names, _functionNames, _functions, _declarations);
        }

        private void Report(DiagnosticDescriptor descriptor, SourceSpan? span, params object[] args) =>
            _diagnostics.Add(descriptor.Create(span ?? NoSpan, args));

        // ---- blocks, locals, labels ----

        private Block EnterBlock(bool isLoop, bool isRepeatBody = false)
        {
            var block = new Block
            {
                EntryActive = _fs.Active.Count,
                FirstLabel = _fs.Labels.Count,
                FirstGoto = _fs.Gotos.Count,
                IsLoop = isLoop,
                IsRepeatBody = isRepeatBody,
            };
            _fs.Blocks.Add(block);
            return block;
        }

        private void LeaveBlock()
        {
            var block = _fs.Blocks[^1];
            _fs.Blocks.RemoveAt(_fs.Blocks.Count - 1);
            _fs.Labels.RemoveRange(block.FirstLabel, _fs.Labels.Count - block.FirstLabel);
            // Pending gotos move to the outer block; the locals of this block are no longer in their way.
            for (var i = block.FirstGoto; i < _fs.Gotos.Count; i++)
            {
                if (_fs.Gotos[i].Active > block.EntryActive) _fs.Gotos[i].Active = block.EntryActive;
            }
            _fs.Active.RemoveRange(block.EntryActive, _fs.Active.Count - block.EntryActive);
        }

        private void VisitBlock(ImmutableArray<IrStmt> body, bool isLoop, bool isRepeatBody = false, IrExpr? untilCondition = null)
        {
            var block = EnterBlock(isLoop, isRepeatBody);
            VisitStatements(body, block);
            // 'until' sees the body's locals.
            if (untilCondition is not null) VisitExpr(untilCondition);
            LeaveBlock();
        }

        private void VisitStatements(ImmutableArray<IrStmt> body, Block block)
        {
            if (body.IsDefault) return;
            for (var i = 0; i < body.Length; i++) VisitStatement(body, i, block);
        }

        private LocalVariable DeclareLocal(string name, SourceSpan? span)
        {
            var variable = _fs.Info.AddLocal(name, span);
            AddActive(new ActiveSlot(name, variable), span);
            return variable;
        }

        /// <summary>The 3 hidden control slots of a 'for' loop, as in Lua.</summary>
        private void DeclareHiddenForSlots(string kind, SourceSpan? span)
        {
            foreach (var slot in kind == "numeric"
                         ? new[] { "(for index)", "(for limit)", "(for step)" }
                         : ["(for generator)", "(for state)", "(for control)"])
            {
                AddActive(new ActiveSlot(slot, null), span);
            }
        }

        private void AddActive(ActiveSlot slot, SourceSpan? span)
        {
            _fs.Active.Add(slot);
            if (_fs.Active.Count > _fs.Info.MaxActiveLocals) _fs.Info.MaxActiveLocals = _fs.Active.Count;
            if (_fs.Active.Count > MaxLocals && !_fs.ReportedTooManyLocals)
            {
                _fs.ReportedTooManyLocals = true;
                Report(DiagnosticCatalog.TooManyLocals, span, MaxLocals);
            }
        }

        private void VisitLabel(IrLabel label, ImmutableArray<IrStmt> body, int index, Block block)
        {
            for (var i = block.FirstLabel; i < _fs.Labels.Count; i++)
            {
                if (_fs.Labels[i].Name == label.Name)
                    Report(DiagnosticCatalog.DuplicateLabel, label.Span, label.Name, _fs.Labels[i].Span?.Line ?? 0);
            }

            // A label followed only by other labels at the end of a block is outside the scope of the
            // block's locals (Lua 5.2), which is what makes 'goto continue' work. Not in 'repeat': 'until'
            // still sees the locals.
            var atEnd = !block.IsRepeatBody;
            for (var i = index + 1; i < body.Length && atEnd; i++) atEnd = body[i] is IrLabel;

            var entry = new LabelEntry(label.Name, label.Span, atEnd ? block.EntryActive : _fs.Active.Count);
            _fs.Labels.Add(entry);

            for (var i = _fs.Gotos.Count - 1; i >= block.FirstGoto; i--)
            {
                var pending = _fs.Gotos[i];
                if (pending.Name != label.Name) continue;
                CheckGotoScope(pending, entry);
                _fs.Gotos.RemoveAt(i);
            }
        }

        private void VisitGoto(IrGoto statement)
        {
            var pending = new GotoEntry(statement.Label, statement.Span, _fs.Active.Count);
            for (var i = _fs.Labels.Count - 1; i >= 0; i--)
            {
                if (_fs.Labels[i].Name != statement.Label) continue;
                CheckGotoScope(pending, _fs.Labels[i]);
                return;
            }
            _fs.Gotos.Add(pending);
        }

        private void CheckGotoScope(GotoEntry pending, LabelEntry label)
        {
            if (pending.Active < label.Active)
                Report(DiagnosticCatalog.GotoIntoLocalScope, pending.Span, pending.Name, _fs.Active[pending.Active].Name);
        }

        private void CloseFunction()
        {
            foreach (var pending in _fs.Gotos) Report(DiagnosticCatalog.GotoWithoutLabel, pending.Span, pending.Name);
            _fs.Gotos.Clear();
        }

        // ---- statements ----

        private void VisitStatement(ImmutableArray<IrStmt> body, int index, Block block)
        {
            switch (body[index])
            {
                case IrLocal local:
                    VisitExprs(local.Values);
                    _declarations[local] = [.. local.Names.Select(name => DeclareLocal(name, local.Span))];
                    break;

                case IrAssign assign:
                    VisitExprs(assign.Values);
                    foreach (var target in assign.Targets) VisitTarget(target);
                    break;

                case IrCompoundAssign compound:
                    VisitTarget(compound.Target);
                    VisitExpr(compound.Value);
                    break;

                case IrCallStmt call:
                    VisitExpr(call.Call);
                    break;

                case IrIf ifStatement:
                    foreach (var clause in ifStatement.Clauses)
                    {
                        VisitExpr(clause.Condition);
                        VisitBlock(clause.Body, isLoop: false);
                    }
                    if (ifStatement.Else is { } elseBlock) VisitBlock(elseBlock, isLoop: false);
                    break;

                case IrWhile whileLoop:
                    VisitExpr(whileLoop.Condition);
                    VisitBlock(whileLoop.Body, isLoop: true);
                    break;

                case IrRepeat repeat:
                    VisitBlock(repeat.Body, isLoop: true, isRepeatBody: true, untilCondition: repeat.Condition);
                    break;

                case IrNumericFor numericFor:
                    VisitExpr(numericFor.Start);
                    VisitExpr(numericFor.Limit);
                    if (numericFor.Step is not null) VisitExpr(numericFor.Step);
                    VisitForBody(numericFor, "numeric", [numericFor.Variable], numericFor.Body);
                    break;

                case IrGenericFor genericFor:
                    VisitExprs(genericFor.Iterators);
                    VisitForBody(genericFor, "generic", genericFor.Variables, genericFor.Body);
                    break;

                case IrDo doBlock:
                    VisitBlock(doBlock.Body, isLoop: false);
                    break;

                case IrBreak breakStatement:
                    if (!_fs.Blocks.Any(b => b.IsLoop)) Report(DiagnosticCatalog.BreakOutsideLoop, breakStatement.Span);
                    break;

                case IrReturn ret:
                    VisitExprs(ret.Values);
                    break;

                case IrGoto gotoStatement:
                    VisitGoto(gotoStatement);
                    break;

                case IrLabel label:
                    VisitLabel(label, body, index, block);
                    break;

                case IrFunctionStmt function:
                    var name = function.NamePath[0];
                    var resolution = Resolve(name);
                    _functionNames[function] = resolution;
                    if (function.NamePath.Length == 1)
                        MarkAssigned(resolution);
                    else
                        RecordRead(resolution, function.Span);
                    VisitFunction(function.Function);
                    break;

                case IrLocalFunction localFunction:
                    // The name is in scope inside the body, so the function can call itself.
                    _declarations[localFunction] = [DeclareLocal(localFunction.Name, localFunction.Span)];
                    VisitFunction(localFunction.Function);
                    break;
            }
        }

        private void VisitForBody(IrStmt loop, string kind, ImmutableArray<string> variables, ImmutableArray<IrStmt> body)
        {
            // Like Lua: an outer loop block with the hidden slots, an inner block with the loop variables.
            EnterBlock(isLoop: true);
            DeclareHiddenForSlots(kind, loop.Span);
            var inner = EnterBlock(isLoop: false);
            _declarations[loop] = [.. variables.Select(variable => DeclareLocal(variable, loop.Span))];
            VisitStatements(body, inner);
            LeaveBlock();
            LeaveBlock();
        }

        private void VisitTarget(IrExpr target)
        {
            switch (target)
            {
                case IrName name:
                    var resolution = Resolve(name.Name);
                    _names[name] = resolution;
                    MarkAssigned(resolution);
                    break;
                default:
                    VisitExpr(target);
                    break;
            }
        }

        private void MarkAssigned(NameResolution resolution)
        {
            if (resolution is { Kind: NameKind.Global, Environment: null }) _assignedGlobals.Add(resolution.Name);
        }

        private void RecordRead(NameResolution resolution, SourceSpan? span)
        {
            if (resolution is { Kind: NameKind.Global, Environment: null }) _globalReads.Add((resolution.Name, span));
        }

        // ---- functions ----

        private void VisitFunction(IrFunction function)
        {
            var info = new FunctionInfo(function, _fs.Info, function.IsVararg);
            _functions[function] = info;

            var outer = _fs;
            _fs = new FunctionState(info, outer);
            var block = EnterBlock(isLoop: false);
            _declarations[function] = function.Parameters.IsDefault
                ? []
                : [.. function.Parameters.Select(parameter => DeclareLocal(parameter, function.Span))];
            VisitStatements(function.Body, block);
            LeaveBlock();
            CloseFunction();
            _fs = outer;

            var upvalues = info.Upvalues.Count + (info.UsesGlobals ? 1 : 0);
            if (upvalues > MaxUpvalues) Report(DiagnosticCatalog.TooManyUpvalues, function.Span, MaxUpvalues);
        }

        // ---- expressions ----

        private void VisitExprs(ImmutableArray<IrExpr> expressions)
        {
            if (expressions.IsDefault) return;
            foreach (var expression in expressions) VisitExpr(expression);
        }

        private void VisitExpr(IrExpr expression)
        {
            switch (expression)
            {
                case IrName name:
                    var resolution = Resolve(name.Name);
                    _names[name] = resolution;
                    RecordRead(resolution, name.Span);
                    break;
                case IrIndex index:
                    VisitExpr(index.Target);
                    VisitExpr(index.Key);
                    break;
                case IrCall call:
                    VisitExpr(call.Callee);
                    VisitExprs(call.Arguments);
                    break;
                case IrMethodCall methodCall:
                    VisitExpr(methodCall.Receiver);
                    VisitExprs(methodCall.Arguments);
                    break;
                case IrVararg vararg:
                    if (!_fs.Info.IsVararg) Report(DiagnosticCatalog.VarargOutsideVarargFunction, vararg.Span);
                    break;
                case IrParen paren:
                    VisitExpr(paren.Inner);
                    break;
                case IrFunction function:
                    VisitFunction(function);
                    break;
                case IrTable table:
                    if (table.Fields.IsDefault) break;
                    foreach (var field in table.Fields)
                    {
                        if (field.Key is not null) VisitExpr(field.Key);
                        VisitExpr(field.Value);
                    }
                    break;
                case IrBinary binary:
                    VisitExpr(binary.Left);
                    VisitExpr(binary.Right);
                    break;
                case IrUnary unary:
                    VisitExpr(unary.Operand);
                    break;
            }
        }

        // ---- name resolution ----

        private NameResolution Resolve(string name) => ResolveIn(_fs, name);

        private static NameResolution ResolveIn(FunctionState fs, string name)
        {
            if (FindActive(fs, name) is { } local)
                return new NameResolution(NameKind.Local, name) { Variable = local };

            var upvalue = FindUpvalue(fs, name);
            if (upvalue >= 0)
                return new NameResolution(NameKind.Upvalue, name) { Variable = fs.Info.Upvalues[upvalue].Variable, UpvalueIndex = upvalue };

            if (name == "_ENV") return new NameResolution(NameKind.Environment, name);

            // A free name is _ENV.name: a field of the user's _ENV if one is in scope, else a global.
            var environment = ResolveIn(fs, "_ENV");
            if (environment.Kind != NameKind.Environment)
                return new NameResolution(NameKind.Global, name) { Environment = environment };

            for (var f = fs; f is not null; f = f.Parent) f.Info.UsesGlobals = true;
            return new NameResolution(NameKind.Global, name);
        }

        private static LocalVariable? FindActive(FunctionState fs, string name)
        {
            for (var i = fs.Active.Count - 1; i >= 0; i--)
            {
                if (fs.Active[i].Variable is { } variable && variable.Name == name) return variable;
            }
            return null;
        }

        private static int FindUpvalue(FunctionState fs, string name)
        {
            if (fs.Parent is null) return -1;

            if (FindActive(fs.Parent, name) is { } parentLocal)
            {
                parentLocal.IsCaptured = true;
                return fs.Info.AddUpvalue(new UpvalueInfo(name, parentLocal, FromParentLocal: true, ParentUpvalueIndex: -1));
            }

            var parentUpvalue = FindUpvalue(fs.Parent, name);
            if (parentUpvalue < 0) return -1;
            var variable = fs.Parent.Info.Upvalues[parentUpvalue].Variable;
            return fs.Info.AddUpvalue(new UpvalueInfo(name, variable, FromParentLocal: false, ParentUpvalueIndex: parentUpvalue));
        }

        // ---- warnings ----

        private void ReportGlobalReads()
        {
            foreach (var (name, span) in _globalReads)
            {
                if (_assignedGlobals.Contains(name)) continue;
                if (_api.Known.Contains(name))
                {
                    if (!_api.Implemented.Contains(name)) Report(DiagnosticCatalog.UnimplementedApiFunction, span, name);
                    continue;
                }
                var suggestion = Suggest(name);
                Report(DiagnosticCatalog.UndefinedGlobal, span, name, suggestion is null ? "" : $"; você quis dizer '{suggestion}'?");
            }
        }

        private string? Suggest(string name)
        {
            var limit = name.Length <= 4 ? 1 : 2;
            string? best = null;
            var bestDistance = int.MaxValue;
            foreach (var candidate in _assignedGlobals.Concat(_api.Known).Distinct().Order(StringComparer.Ordinal))
            {
                if (candidate == name) continue;
                var distance = Levenshtein(name, candidate);
                if (distance <= limit && distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static int Levenshtein(string a, string b)
        {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (var j = 0; j <= b.Length; j++) previous[j] = j;
            for (var i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }
                (previous, current) = (current, previous);
            }
            return previous[b.Length];
        }

        /// <summary>
        /// Decimal literals of 32768 or more wrap around silently. Hex and binary are left alone: 0xffff
        /// as -1 is a common idiom. '-32768' is fine, because the negation wraps back.
        /// </summary>
        private void ReportWrappingNumbers(ImmutableArray<Token> tokens)
        {
            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                if (token.Kind != TokenKind.Number || token.Text.Length > 1 && token.Text[1] is 'x' or 'X' or 'b' or 'B')
                    continue;
                if (!double.TryParse(token.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
                    || value < 32768)
                    continue;
                if (value == 32768 && i > 0 && tokens[i - 1].Kind == TokenKind.Minus) continue;
                Report(DiagnosticCatalog.NumberWrapsAround, token.Span, token.Text, LuaLiterals.FormatNumber(token.NumberValue));
            }
        }
    }
}
