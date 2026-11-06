using System.Collections.Immutable;
using Ferbento.Scripting.Compiler.Analysis;
using Ferbento.Scripting.Compiler.Ir;
using Ferbento.Scripting.Interpreter;
using Ferbento.Scripting.Vm;

namespace Ferbento.Scripting.Compiler.Bytecode;

/// <summary>
/// Compiles the IR to bytecode for <see cref="LuaVm"/>. Names are resolved by the static analysis
/// (story 005): every local has a fixed slot (its <see cref="LocalVariable.Index"/>), captured locals
/// live in cells, and free names are fields of the environment. The chunk must have no analysis errors.
/// </summary>
public static class BytecodeCompiler
{
    public static Prototype Compile(IrChunk chunk, AnalysisResult analysis, string name = "main chunk")
    {
        if (analysis.HasErrors)
            throw new ArgumentException("The chunk has analysis errors and cannot be compiled.", nameof(analysis));

        var compiler = new FunctionCompiler(analysis, analysis.MainChunk, name, chunk.Span);
        return compiler.CompileBody([], chunk.Body, isVararg: true);
    }

    private sealed class BlockScope
    {
        public Dictionary<string, int> Labels { get; } = new(StringComparer.Ordinal);
        public List<(string Label, int Instruction)> PendingGotos { get; } = [];
    }

    private enum TargetKind { Simple, Indexed }

    private readonly record struct Target(TargetKind Kind, NameResolution? Name);

    private sealed class FunctionCompiler(AnalysisResult analysis, FunctionInfo info, string name, SourceSpan? span)
    {
        private const int FieldsPerFlush = 50;

        private readonly List<Instruction> _code = [];
        private readonly List<SourceSpan?> _spans = [];
        private readonly List<ScriptValue> _constants = [];
        private readonly Dictionary<(ScriptValueType, object), int> _constantIndex = [];
        private readonly List<Prototype> _prototypes = [];
        private readonly List<BlockScope> _blocks = [];
        private readonly List<List<int>> _loopBreaks = [];
        private readonly SourceSpan? _functionSpan = span;
        private SourceSpan? _span = span;
        private int _depth;
        private int _maxDepth;
        private int _tempTop;
        private int _maxTemps;

        public Prototype CompileBody(ImmutableArray<LocalVariable> parameters, ImmutableArray<IrStmt> body, bool isVararg)
        {
            Block(body);
            Emit(OpCode.Return, 0, 0, 0, 0);

            return new Prototype
            {
                Name = name,
                Span = _functionSpan,
                Code = [.. _code],
                Spans = [.. _spans],
                Constants = [.. _constants],
                Prototypes = [.. _prototypes],
                Upvalues =
                [
                    .. info.Upvalues.Select(u => new UpvalueDescriptor(
                        u.Name, u.FromParentLocal, u.FromParentLocal ? u.Variable.Index : u.ParentUpvalueIndex)),
                ],
                ParameterCount = parameters.Length,
                IsVararg = isVararg,
                SlotCount = info.Locals.Count + _maxTemps,
                MaxStack = _maxDepth,
                CapturedParameters = [.. parameters.Where(p => p.IsCaptured).Select(p => p.Index)],
            };
        }

        // ---- emitting ----

        /// <summary>Appends an instruction; <paramref name="stackEffect"/> is its net effect on the operand stack.</summary>
        private int Emit(OpCode op, int a, int b, int c, int stackEffect)
        {
            _code.Add(new Instruction(op, a, b, c));
            _spans.Add(_span);
            _depth += stackEffect;
            if (_depth > _maxDepth) _maxDepth = _depth;
            return _code.Count - 1;
        }

        private int Emit(OpCode op, int stackEffect) => Emit(op, 0, 0, 0, stackEffect);

        private int Emit(OpCode op, int a, int stackEffect) => Emit(op, a, 0, 0, stackEffect);

        private int Here => _code.Count;

        private void PatchA(int instruction, int value) => _code[instruction] = _code[instruction] with { A = value };

        private int Constant(ScriptValue value)
        {
            object key = value.Type == ScriptValueType.Number ? value.AsNumber().Raw : value.Data!;
            if (_constantIndex.TryGetValue((value.Type, key), out var index)) return index;
            _constants.Add(value);
            _constantIndex[(value.Type, key)] = _constants.Count - 1;
            return _constants.Count - 1;
        }

        private int StringConstant(string s) => Constant(ScriptValue.Str(s));

        private int AllocTemps(int count)
        {
            var first = info.Locals.Count + _tempTop;
            _tempTop += count;
            if (_tempTop > _maxTemps) _maxTemps = _tempTop;
            return first;
        }

        private void FreeTemps(int count) => _tempTop -= count;

        private SourceSpan? Enter(IrNode node)
        {
            var saved = _span;
            if (node.Span is not null) _span = node.Span;
            return saved;
        }

        // ---- blocks and statements ----

        private void Block(ImmutableArray<IrStmt> body)
        {
            var scope = new BlockScope();
            _blocks.Add(scope);
            if (!body.IsDefault)
            {
                foreach (var statement in body) Statement(statement);
            }
            _blocks.RemoveAt(_blocks.Count - 1);

            if (scope.PendingGotos.Count == 0) return;
            if (_blocks.Count == 0)
                throw new InvalidOperationException($"goto '{scope.PendingGotos[0].Label}' has no visible label.");
            _blocks[^1].PendingGotos.AddRange(scope.PendingGotos);
        }

        private void Statement(IrStmt statement)
        {
            var saved = Enter(statement);
            switch (statement)
            {
                case IrLocal local:
                    LocalStatement(local);
                    break;
                case IrAssign assign:
                    Assign(assign);
                    break;
                case IrCompoundAssign compound:
                    CompoundAssign(compound);
                    break;
                case IrCallStmt call:
                    CallExpr(call.Call, 0);
                    break;
                case IrIf ifStatement:
                    If(ifStatement);
                    break;
                case IrWhile whileLoop:
                    While(whileLoop);
                    break;
                case IrRepeat repeat:
                    Repeat(repeat);
                    break;
                case IrNumericFor numericFor:
                    NumericFor(numericFor);
                    break;
                case IrGenericFor genericFor:
                    GenericFor(genericFor);
                    break;
                case IrDo doBlock:
                    Block(doBlock.Body);
                    break;
                case IrBreak:
                    if (_loopBreaks.Count == 0) throw new InvalidOperationException("break outside a loop.");
                    _loopBreaks[^1].Add(Emit(OpCode.Jump, 0, 0));
                    break;
                case IrReturn ret:
                    var (fixedCount, open) = ExprListOpen(ret.Values);
                    Emit(OpCode.Return, fixedCount, 0, open ? 1 : 0, -(fixedCount + (open ? 1 : 0)));
                    break;
                case IrGoto gotoStatement:
                    Goto(gotoStatement.Label);
                    break;
                case IrLabel label:
                    Label(label.Name);
                    break;
                case IrFunctionStmt function:
                    FunctionStatement(function);
                    break;
                case IrLocalFunction localFunction:
                    LocalFunction(localFunction);
                    break;
                default:
                    throw new NotSupportedException($"Statement {statement.GetType().Name} is not supported.");
            }
            _span = saved;
        }

        private void LocalStatement(IrLocal local)
        {
            var variables = analysis.GetDeclaredLocals(local);
            ExprListAdjusted(local.Values, variables.Length);
            for (var i = variables.Length - 1; i >= 0; i--) StoreNewLocal(variables[i]);
        }

        private void StoreNewLocal(LocalVariable variable) =>
            Emit(variable.IsCaptured ? OpCode.NewCell : OpCode.SetLocal, variable.Index, -1);

        private void LocalFunction(IrLocalFunction statement)
        {
            var variable = analysis.GetDeclaredLocals(statement)[0];
            if (variable.IsCaptured)
            {
                // The cell exists before the closure is made, so the function sees itself.
                Emit(OpCode.LoadNil, 1);
                Emit(OpCode.NewCell, variable.Index, -1);
                Function(statement.Function, statement.Name);
                Emit(OpCode.SetCell, variable.Index, -1);
            }
            else
            {
                Function(statement.Function, statement.Name);
                Emit(OpCode.SetLocal, variable.Index, -1);
            }
        }

        private void FunctionStatement(IrFunctionStmt statement)
        {
            var path = statement.NamePath;
            var functionName = path.Length == 1
                ? path[0]
                : string.Join(".", path[..^1]) + (statement.IsMethod ? ":" : ".") + path[^1];
            var resolution = analysis.ResolveFunctionName(statement);

            if (path.Length == 1)
            {
                var target = PrepareTarget(resolution);
                Function(statement.Function, functionName);
                Store(target);
                return;
            }

            Load(resolution);
            for (var i = 1; i < path.Length - 1; i++) Emit(OpCode.GetField, StringConstant(path[i]), 0);
            Function(statement.Function, functionName);
            Emit(OpCode.SetField, StringConstant(path[^1]), -2);
        }

        private void Assign(IrAssign assign)
        {
            var targets = new Target[assign.Targets.Length];
            for (var i = 0; i < targets.Length; i++) targets[i] = PrepareTarget(assign.Targets[i]);
            ExprListAdjusted(assign.Values, targets.Length);

            if (targets.Length == 1)
            {
                Store(targets[0]);
                return;
            }

            // Stack: [t k]… then one value per target. Assign right to left, like Lua.
            var pairs = targets.Count(t => t.Kind == TargetKind.Indexed);
            var pairSlots = 2 * pairs;
            var pairIndex = pairs;
            for (var j = targets.Length - 1; j >= 0; j--)
            {
                if (targets[j].Kind == TargetKind.Simple)
                {
                    Store(targets[j]);
                    continue;
                }
                pairIndex--;
                Emit(OpCode.SetIndexFrom, pairSlots + j - 2 * pairIndex, -1);
            }
            if (pairSlots > 0) Emit(OpCode.Pop, pairSlots, -pairSlots);
        }

        private void CompoundAssign(IrCompoundAssign compound)
        {
            // z8lua: the target's table and key are evaluated once; the value is a whole expression.
            var target = PrepareTarget(compound.Target);
            if (target.Kind == TargetKind.Simple)
            {
                Load(target.Name!);
                Expr(compound.Value);
                BinaryOperator(compound.Operator);
                Store(target);
                return;
            }

            Emit(OpCode.Dup2, 2);
            Emit(OpCode.GetIndex, -1);
            Expr(compound.Value);
            BinaryOperator(compound.Operator);
            Emit(OpCode.SetIndex, -3);
        }

        private Target PrepareTarget(IrExpr target) => target switch
        {
            IrName name => PrepareTarget(analysis.Resolve(name)),
            IrIndex index => PrepareIndexTarget(index),
            _ => throw new NotSupportedException($"Cannot assign to {target.GetType().Name}."),
        };

        private Target PrepareIndexTarget(IrIndex index)
        {
            Expr(index.Target);
            Expr(index.Key);
            return new Target(TargetKind.Indexed, null);
        }

        private Target PrepareTarget(NameResolution resolution)
        {
            if (resolution is { Kind: NameKind.Global, Environment: { } environment })
            {
                Load(environment);
                Emit(OpCode.LoadConst, StringConstant(resolution.Name), 1);
                return new Target(TargetKind.Indexed, null);
            }
            return new Target(TargetKind.Simple, resolution);
        }

        /// <summary>Pops the value on top into the target (and its table and key, if indexed).</summary>
        private void Store(Target target)
        {
            if (target.Kind == TargetKind.Indexed)
            {
                Emit(OpCode.SetIndex, -3);
                return;
            }

            var resolution = target.Name!;
            switch (resolution.Kind)
            {
                case NameKind.Local:
                    var variable = resolution.Variable!;
                    Emit(variable.IsCaptured ? OpCode.SetCell : OpCode.SetLocal, variable.Index, -1);
                    break;
                case NameKind.Upvalue:
                    Emit(OpCode.SetUpval, resolution.UpvalueIndex, -1);
                    break;
                case NameKind.Global:
                    Emit(OpCode.SetGlobal, StringConstant(resolution.Name), -1);
                    break;
                case NameKind.Environment:
                    Emit(OpCode.SetEnv, -1);
                    break;
            }
        }

        private void Load(NameResolution resolution)
        {
            switch (resolution.Kind)
            {
                case NameKind.Local:
                    var variable = resolution.Variable!;
                    Emit(variable.IsCaptured ? OpCode.GetCell : OpCode.GetLocal, variable.Index, 1);
                    break;
                case NameKind.Upvalue:
                    Emit(OpCode.GetUpval, resolution.UpvalueIndex, 1);
                    break;
                case NameKind.Global when resolution.Environment is { } environment:
                    Load(environment);
                    Emit(OpCode.GetField, StringConstant(resolution.Name), 0);
                    break;
                case NameKind.Global:
                    Emit(OpCode.GetGlobal, StringConstant(resolution.Name), 1);
                    break;
                case NameKind.Environment:
                    Emit(OpCode.GetEnv, 1);
                    break;
            }
        }

        private void If(IrIf statement)
        {
            var exits = new List<int>();
            for (var i = 0; i < statement.Clauses.Length; i++)
            {
                var clause = statement.Clauses[i];
                Expr(clause.Condition);
                var skip = Emit(OpCode.JumpIfFalse, 0, -1);
                Block(clause.Body);
                var isLast = i == statement.Clauses.Length - 1 && statement.Else is null;
                if (!isLast) exits.Add(Emit(OpCode.Jump, 0, 0));
                PatchA(skip, Here);
            }
            if (statement.Else is { } elseBody) Block(elseBody);
            foreach (var exit in exits) PatchA(exit, Here);
        }

        private void While(IrWhile loop)
        {
            var start = Here;
            Expr(loop.Condition);
            var exit = Emit(OpCode.JumpIfFalse, 0, -1);
            LoopBody(loop.Body);
            Emit(OpCode.Jump, start, 0);
            PatchA(exit, Here);
            EndLoop();
        }

        private void Repeat(IrRepeat loop)
        {
            var start = Here;
            _loopBreaks.Add([]);
            // The condition sees the body's locals, so it is compiled inside the body's scope.
            var scope = new BlockScope();
            _blocks.Add(scope);
            if (!loop.Body.IsDefault)
            {
                foreach (var statement in loop.Body) Statement(statement);
            }
            Expr(loop.Condition);
            Emit(OpCode.JumpIfFalse, start, -1);
            _blocks.RemoveAt(_blocks.Count - 1);
            if (scope.PendingGotos.Count > 0) _blocks[^1].PendingGotos.AddRange(scope.PendingGotos);
            EndLoop();
        }

        private void NumericFor(IrNumericFor loop)
        {
            Expr(loop.Start);
            Expr(loop.Limit);
            if (loop.Step is not null) Expr(loop.Step);
            else Emit(OpCode.LoadConst, Constant(ScriptValue.Number(1)), 1);

            var hidden = AllocTemps(3);
            var prep = Emit(OpCode.ForPrep, hidden, 0, 0, -3);
            var bodyStart = Here;
            _depth++; // ForLoop pushes the index before jumping here.
            StoreNewLocal(analysis.GetDeclaredLocals(loop)[0]);
            LoopBody(loop.Body);
            var forLoop = Emit(OpCode.ForLoop, hidden, bodyStart, 0, 0);
            _code[prep] = _code[prep] with { B = forLoop };
            EndLoop();
            FreeTemps(3);
        }

        private void GenericFor(IrGenericFor loop)
        {
            var variables = analysis.GetDeclaredLocals(loop);
            ExprListAdjusted(loop.Iterators, 3);
            var hidden = AllocTemps(3);
            for (var i = 2; i >= 0; i--) Emit(OpCode.SetLocal, hidden + i, -1);

            var start = Here;
            for (var i = 0; i < 3; i++) Emit(OpCode.GetLocal, hidden + i, 1);
            Emit(OpCode.Call, 2, variables.Length, 0, variables.Length - 3);
            var check = Emit(OpCode.TForLoop, hidden, variables.Length, 0, 0);
            for (var i = variables.Length - 1; i >= 0; i--) StoreNewLocal(variables[i]);
            LoopBody(loop.Body);
            Emit(OpCode.Jump, start, 0);
            _code[check] = _code[check] with { C = Here };
            EndLoop();
            FreeTemps(3);
        }

        private void LoopBody(ImmutableArray<IrStmt> body)
        {
            _loopBreaks.Add([]);
            Block(body);
        }

        private void EndLoop()
        {
            foreach (var jump in _loopBreaks[^1]) PatchA(jump, Here);
            _loopBreaks.RemoveAt(_loopBreaks.Count - 1);
        }

        private void Goto(string label)
        {
            for (var i = _blocks.Count - 1; i >= 0; i--)
            {
                if (_blocks[i].Labels.TryGetValue(label, out var target))
                {
                    Emit(OpCode.Jump, target, 0);
                    return;
                }
            }
            _blocks[^1].PendingGotos.Add((label, Emit(OpCode.Jump, 0, 0)));
        }

        private void Label(string label)
        {
            var scope = _blocks[^1];
            scope.Labels[label] = Here;
            for (var i = scope.PendingGotos.Count - 1; i >= 0; i--)
            {
                if (scope.PendingGotos[i].Label != label) continue;
                PatchA(scope.PendingGotos[i].Instruction, Here);
                scope.PendingGotos.RemoveAt(i);
            }
        }

        // ---- expressions ----

        private static bool IsMulti(IrExpr expression) => expression is IrCallExpr or IrVararg;

        /// <summary>Pushes exactly one value.</summary>
        private void Expr(IrExpr expression)
        {
            var saved = Enter(expression);
            switch (expression)
            {
                case IrNil:
                    Emit(OpCode.LoadNil, 1);
                    break;
                case IrBool b:
                    Emit(b.Value ? OpCode.LoadTrue : OpCode.LoadFalse, 1);
                    break;
                case IrNumber n:
                    Emit(OpCode.LoadConst, Constant(ScriptValue.Number(n.Value)), 1);
                    break;
                case IrString s:
                    Emit(OpCode.LoadConst, StringConstant(s.Value), 1);
                    break;
                case IrName name:
                    Load(analysis.Resolve(name));
                    break;
                case IrIndex index:
                    Expr(index.Target);
                    if (index.Key is IrString key)
                    {
                        Emit(OpCode.GetField, StringConstant(key.Value), 0);
                    }
                    else
                    {
                        Expr(index.Key);
                        Emit(OpCode.GetIndex, -1);
                    }
                    break;
                case IrCallExpr call:
                    CallExpr(call, 1);
                    break;
                case IrVararg:
                    Emit(OpCode.Vararg, 1, 1);
                    break;
                case IrParen paren:
                    Expr(paren.Inner);
                    break;
                case IrFunction function:
                    Function(function, "anonymous");
                    break;
                case IrTable table:
                    Table(table);
                    break;
                case IrBinary { Operator: IrBinaryOp.And or IrBinaryOp.Or } logical:
                    Expr(logical.Left);
                    var jump = Emit(
                        logical.Operator == IrBinaryOp.And ? OpCode.JumpIfFalseKeep : OpCode.JumpIfTrueKeep, 0, -1);
                    Expr(logical.Right);
                    PatchA(jump, Here);
                    break;
                case IrBinary binary:
                    Expr(binary.Left);
                    Expr(binary.Right);
                    BinaryOperator(binary.Operator);
                    break;
                case IrUnary unary:
                    Unary(unary);
                    break;
                default:
                    throw new NotSupportedException($"Expression {expression.GetType().Name} is not supported.");
            }
            _span = saved;
        }

        /// <summary>
        /// A call or <c>...</c> with <paramref name="wanted"/> results (-1: all of them, left open for
        /// the instruction that follows).
        /// </summary>
        private void MultiExpr(IrExpr expression, int wanted)
        {
            if (expression is IrVararg)
            {
                var saved = Enter(expression);
                Emit(OpCode.Vararg, wanted, wanted < 0 ? 1 : wanted);
                _span = saved;
                return;
            }
            CallExpr((IrCallExpr)expression, wanted);
        }

        private void CallExpr(IrCallExpr call, int wanted)
        {
            var saved = Enter(call);
            int fixedCount;
            bool open;
            switch (call)
            {
                case IrCall plain:
                    Expr(plain.Callee);
                    (fixedCount, open) = ExprListOpen(plain.Arguments);
                    break;
                case IrMethodCall method:
                    Expr(method.Receiver);
                    Emit(OpCode.Self, StringConstant(method.Method), 1);
                    (fixedCount, open) = ExprListOpen(method.Arguments);
                    fixedCount++;
                    break;
                default:
                    throw new NotSupportedException($"Call {call.GetType().Name} is not supported.");
            }

            // Restore the call's own position: the arguments may have moved it.
            if (call.Span is not null) _span = call.Span;
            var consumed = 1 + fixedCount + (open ? 1 : 0);
            Emit(OpCode.Call, fixedCount, wanted, open ? 1 : 0, (wanted < 0 ? 1 : wanted) - consumed);
            _span = saved;
        }

        /// <summary>Pushes the list; the last call or <c>...</c> keeps all its results (open).</summary>
        private (int FixedCount, bool Open) ExprListOpen(ImmutableArray<IrExpr> expressions)
        {
            if (expressions.IsDefault || expressions.Length == 0) return (0, false);
            for (var i = 0; i < expressions.Length - 1; i++) Expr(expressions[i]);
            var last = expressions[^1];
            if (IsMulti(last))
            {
                MultiExpr(last, -1);
                return (expressions.Length - 1, true);
            }
            Expr(last);
            return (expressions.Length, false);
        }

        /// <summary>Pushes exactly <paramref name="count"/> values, like Lua's adjustment of expression lists.</summary>
        private void ExprListAdjusted(ImmutableArray<IrExpr> expressions, int count)
        {
            var length = expressions.IsDefault ? 0 : expressions.Length;
            var pushed = 0;
            for (var i = 0; i < length; i++)
            {
                var expression = expressions[i];
                var isLast = i == length - 1;
                if (i < count)
                {
                    if (isLast && IsMulti(expression) && count - i > 1)
                    {
                        MultiExpr(expression, count - i);
                        pushed = count;
                    }
                    else
                    {
                        Expr(expression);
                        pushed++;
                    }
                }
                else if (IsMulti(expression))
                {
                    // Extra values are still evaluated, then dropped.
                    MultiExpr(expression, 0);
                }
                else
                {
                    Expr(expression);
                    Emit(OpCode.Pop, 1, -1);
                }
            }
            for (; pushed < count; pushed++) Emit(OpCode.LoadNil, 1);
        }

        private void Table(IrTable table)
        {
            Emit(OpCode.NewTable, 1);
            if (table.Fields.IsDefault) return;

            var pending = 0;
            var nextIndex = 1;
            for (var i = 0; i < table.Fields.Length; i++)
            {
                var field = table.Fields[i];
                if (field.Key is not null)
                {
                    Expr(field.Key);
                    Expr(field.Value);
                    Emit(OpCode.SetTableField, pending, -2);
                    continue;
                }

                if (i == table.Fields.Length - 1 && IsMulti(field.Value))
                {
                    MultiExpr(field.Value, -1);
                    Emit(OpCode.SetList, pending, nextIndex, 1, -(pending + 1));
                    return;
                }

                Expr(field.Value);
                pending++;
                if (pending == FieldsPerFlush)
                {
                    Emit(OpCode.SetList, pending, nextIndex, 0, -pending);
                    nextIndex += pending;
                    pending = 0;
                }
            }
            if (pending > 0) Emit(OpCode.SetList, pending, nextIndex, 0, -pending);
        }

        private void BinaryOperator(IrBinaryOp op)
        {
            var code = op switch
            {
                IrBinaryOp.Add => OpCode.Add,
                IrBinaryOp.Subtract => OpCode.Sub,
                IrBinaryOp.Multiply => OpCode.Mul,
                IrBinaryOp.Divide => OpCode.Div,
                IrBinaryOp.IntDivide => OpCode.IntDiv,
                IrBinaryOp.Modulo => OpCode.Mod,
                IrBinaryOp.Power => OpCode.Pow,
                IrBinaryOp.BitAnd => OpCode.BAnd,
                IrBinaryOp.BitOr => OpCode.BOr,
                IrBinaryOp.BitXor => OpCode.BXor,
                IrBinaryOp.ShiftLeft => OpCode.Shl,
                IrBinaryOp.ShiftRight => OpCode.Shr,
                IrBinaryOp.LogicalShiftRight => OpCode.Lshr,
                IrBinaryOp.RotateLeft => OpCode.Rotl,
                IrBinaryOp.RotateRight => OpCode.Rotr,
                IrBinaryOp.Concat => OpCode.Concat,
                IrBinaryOp.Equal => OpCode.Eq,
                IrBinaryOp.NotEqual => OpCode.Ne,
                IrBinaryOp.Less => OpCode.Lt,
                IrBinaryOp.LessEqual => OpCode.Le,
                IrBinaryOp.Greater => OpCode.Gt,
                IrBinaryOp.GreaterEqual => OpCode.Ge,
                _ => throw new NotSupportedException($"Operator {op} has no instruction (and/or short-circuit)."),
            };
            Emit(code, -1);
        }

        private void Unary(IrUnary unary)
        {
            // @x, %x and $x are calls to peek, peek2 and peek4.
            var peek = unary.Operator switch
            {
                IrUnaryOp.Peek => "peek",
                IrUnaryOp.Peek2 => "peek2",
                IrUnaryOp.Peek4 => "peek4",
                _ => null,
            };
            if (peek is not null)
            {
                Emit(OpCode.GetGlobal, StringConstant(peek), 1);
                Expr(unary.Operand);
                Emit(OpCode.Call, 1, 1, 0, -1);
                return;
            }

            Expr(unary.Operand);
            Emit(unary.Operator switch
            {
                IrUnaryOp.Negate => OpCode.Neg,
                IrUnaryOp.Not => OpCode.Not,
                IrUnaryOp.Length => OpCode.Len,
                IrUnaryOp.BitNot => OpCode.BNot,
                _ => throw new NotSupportedException($"Operator {unary.Operator} is not supported."),
            }, 0);
        }

        private void Function(IrFunction function, string functionName)
        {
            var saved = Enter(function);
            var child = new FunctionCompiler(analysis, analysis.GetFunction(function), functionName, function.Span);
            var parameters = analysis.GetDeclaredLocals(function);
            _prototypes.Add(child.CompileBody(parameters, function.Body, function.IsVararg));
            Emit(OpCode.Closure, _prototypes.Count - 1, 1);
            _span = saved;
        }
    }
}
