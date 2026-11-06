using System.Runtime.InteropServices;
using Ferbento.Scripting.Interpreter;

namespace Ferbento.Scripting.Vm;

/// <summary>
/// Runs the bytecode of <see cref="Compiler.Bytecode.BytecodeCompiler"/>. Calls between Lua
/// functions use the VM's own frame stack, never the C# stack, so a coroutine can later keep its
/// frames and resume them (story 010). Native functions are plain C# calls; a native that calls
/// back into Lua (through <see cref="Call"/>) runs a nested dispatch loop.
/// </summary>
public sealed class LuaVm
{
    /// <summary>Value stack size limit, as Lua's LUAI_MAXSTACK.</summary>
    public const int MaxStackSize = 1_000_000;

    private static readonly ScriptValue[] NoValues = [];

    private ScriptValue[] _stack = new ScriptValue[256];
    private UpvalueCell?[] _cells = new UpvalueCell?[256];
    private int _top;
    private CallFrame[] _frames = new CallFrame[16];
    private int _frameCount;

    // How many values the last open call or '...' left on the stack (read by the next instruction).
    private int _openCount;

    private ScriptValue _environment = ScriptValue.Nil;
    private ScriptTable? _environmentTable;

    public LuaVm()
    {
        Globals = new ScriptTable();
        SetEnvironment(ScriptValue.Table(Globals));
    }

    /// <summary>The globals table: the environment of every compiled chunk.</summary>
    public ScriptTable Globals { get; }

    /// <summary>Instructions executed so far (all dispatches go through one place).</summary>
    public long InstructionCount { get; private set; }

    public void SetGlobal(string name, ScriptValue value) => Globals.Set(name, value);

    public ScriptValue GetGlobal(string name) => Globals.Get(name);

    public void Register(string name, Func<ScriptValue[], ScriptValue> function) =>
        SetGlobal(name, ScriptValue.Native(function));

    public void Register(string name, Func<ScriptValue[], ScriptValue[]> function) =>
        SetGlobal(name, ScriptValue.NativeMulti(function));

    /// <summary>Runs a compiled chunk and returns what it returns.</summary>
    public ScriptValue[] Execute(Prototype chunk, params ScriptValue[] arguments) =>
        Call(ScriptValue.Func(new LuaClosure(chunk, [])), arguments);

    /// <summary>Calls a Lua or native function and returns all its results.</summary>
    /// <exception cref="Pico8RuntimeException">A runtime error, with the position in the Lua source.</exception>
    public ScriptValue[] Call(ScriptValue function, params ScriptValue[] arguments)
    {
        switch (function.Data)
        {
            case Func<ScriptValue[], ScriptValue> native:
                return [native(arguments) ?? ScriptValue.Nil];
            case Func<ScriptValue[], ScriptValue[]> nativeMulti:
                return nativeMulti(arguments) ?? NoValues;
            case not LuaClosure:
                throw new Pico8RuntimeException($"attempt to call a {Pico8Ops.TypeName(function)} value");
        }

        var closure = (LuaClosure)function.Data;
        var savedTop = _top;
        var depth = _frameCount;
        try
        {
            EnsureStack(_top + arguments.Length + 1);
            _stack[_top++] = function;
            Array.Copy(arguments, 0, _stack, _top, arguments.Length);
            _top += arguments.Length;
            PushFrame(closure, savedTop, arguments.Length, wanted: -1);
            Run(depth);

            var results = new ScriptValue[_top - savedTop];
            Array.Copy(_stack, savedTop, results, 0, results.Length);
            return results;
        }
        finally
        {
            // On errors too: the frames of the failed call are dropped and the VM stays usable.
            _frameCount = depth;
            _top = savedTop;
        }
    }

    private void SetEnvironment(ScriptValue environment)
    {
        _environment = environment;
        _environmentTable = environment.Type == ScriptValueType.Table ? environment.AsTable() : null;
    }

    private ScriptValue GetFromEnvironment(ScriptValue name) =>
        _environmentTable?.Get(name) ?? Pico8Ops.Index(_environment, name);

    private void SetInEnvironment(ScriptValue name, ScriptValue value)
    {
        if (_environmentTable is not null) _environmentTable.Set(name, value);
        else Pico8Ops.SetIndex(_environment, name, value);
    }

    // ---- stack and frames ----

    private void EnsureStack(int size)
    {
        if (size <= _stack.Length) return;
        if (size > MaxStackSize) throw new Pico8RuntimeException("stack overflow");
        var newSize = Math.Min(MaxStackSize, Math.Max(size, _stack.Length * 2));
        Array.Resize(ref _stack, newSize);
        Array.Resize(ref _cells, newSize);
    }

    /// <summary>
    /// Starts a call to <paramref name="closure"/>: the function is at <paramref name="functionIndex"/> and its
    /// <paramref name="argumentCount"/> arguments right above it; they become the first slots of the new frame.
    /// </summary>
    private void PushFrame(LuaClosure closure, int functionIndex, int argumentCount, int wanted)
    {
        var proto = closure.Prototype;
        var frameBase = functionIndex + 1;
        EnsureStack(frameBase + Math.Max(argumentCount, proto.SlotCount) + proto.MaxStack + 1);
        var stack = _stack;

        var varargs = NoValues;
        if (proto.IsVararg && argumentCount > proto.ParameterCount)
        {
            varargs = new ScriptValue[argumentCount - proto.ParameterCount];
            Array.Copy(stack, frameBase + proto.ParameterCount, varargs, 0, varargs.Length);
        }

        Array.Fill(stack, ScriptValue.Nil, frameBase + Math.Min(argumentCount, proto.ParameterCount),
            proto.SlotCount - Math.Min(argumentCount, proto.ParameterCount));
        foreach (var parameter in proto.CapturedParameters)
            _cells[frameBase + parameter] = new UpvalueCell(stack[frameBase + parameter]);
        _top = frameBase + proto.SlotCount;

        if (_frameCount == _frames.Length) Array.Resize(ref _frames, _frames.Length * 2);
        var frame = _frames[_frameCount] ??= new CallFrame();
        frame.Closure = closure;
        frame.Code = ImmutableCollectionsMarshal.AsArray(proto.Code)!;
        frame.Constants = ImmutableCollectionsMarshal.AsArray(proto.Constants)!;
        frame.Base = frameBase;
        frame.Pc = 0;
        frame.Varargs = varargs;
        frame.ReturnBase = functionIndex;
        frame.Wanted = wanted;
        _frameCount++;
    }

    /// <summary>Calls a native function whose arguments are on the stack and pushes its results.</summary>
    private void CallNative(ScriptValue function, int functionIndex, int argumentCount, int wanted)
    {
        var arguments = argumentCount == 0 ? NoValues : new ScriptValue[argumentCount];
        Array.Copy(_stack, functionIndex + 1, arguments, 0, argumentCount);
        _top = functionIndex;

        if (function.Data is Func<ScriptValue[], ScriptValue> native)
        {
            var result = native(arguments) ?? ScriptValue.Nil;
            if (wanted < 0)
            {
                _stack[_top++] = result;
                _openCount = 1;
                return;
            }
            if (wanted == 0) return;
            _stack[_top++] = result;
            for (var i = 1; i < wanted; i++) _stack[_top++] = ScriptValue.Nil;
            return;
        }

        var results = ((Func<ScriptValue[], ScriptValue[]>)function.Data!)(arguments) ?? NoValues;
        PushResults(results, wanted);
    }

    private void PushResults(ScriptValue[] values, int wanted)
    {
        var count = wanted < 0 ? values.Length : wanted;
        EnsureStack(_top + count);
        for (var i = 0; i < count; i++) _stack[_top++] = i < values.Length ? values[i] : ScriptValue.Nil;
        if (wanted < 0) _openCount = values.Length;
    }

    // ---- dispatch ----

    private void Run(int stopDepth)
    {
        CallFrame frame = null!;
        var pc = 0;
        try
        {
            while (true)
            {
                frame = _frames[_frameCount - 1];
                var code = frame.Code;
                var k = frame.Constants;
                var frameBase = frame.Base;
                var stack = _stack;
                var cells = _cells;
                pc = frame.Pc;

                var switchFrame = false;
                while (!switchFrame)
                {
                    // The single dispatch point: the CPU cost per instruction (story 011) goes here.
                    var instruction = code[pc++];
                    InstructionCount++;

                    switch (instruction.Op)
                    {
                        case OpCode.LoadNil:
                            stack[_top++] = ScriptValue.Nil;
                            break;
                        case OpCode.LoadTrue:
                            stack[_top++] = ScriptValue.True;
                            break;
                        case OpCode.LoadFalse:
                            stack[_top++] = ScriptValue.False;
                            break;
                        case OpCode.LoadConst:
                            stack[_top++] = k[instruction.A];
                            break;

                        case OpCode.GetLocal:
                            stack[_top++] = stack[frameBase + instruction.A];
                            break;
                        case OpCode.SetLocal:
                            stack[frameBase + instruction.A] = stack[--_top];
                            break;
                        case OpCode.GetCell:
                            stack[_top++] = cells[frameBase + instruction.A]?.Value ?? ScriptValue.Nil;
                            break;
                        case OpCode.SetCell:
                            (cells[frameBase + instruction.A] ??= new UpvalueCell(ScriptValue.Nil)).Value = stack[--_top];
                            break;
                        case OpCode.NewCell:
                            cells[frameBase + instruction.A] = new UpvalueCell(stack[--_top]);
                            break;
                        case OpCode.GetUpval:
                            stack[_top++] = frame.Closure.Upvalues[instruction.A].Value;
                            break;
                        case OpCode.SetUpval:
                            frame.Closure.Upvalues[instruction.A].Value = stack[--_top];
                            break;
                        case OpCode.GetGlobal:
                            stack[_top++] = GetFromEnvironment(k[instruction.A]);
                            break;
                        case OpCode.SetGlobal:
                            SetInEnvironment(k[instruction.A], stack[--_top]);
                            break;
                        case OpCode.GetEnv:
                            stack[_top++] = _environment;
                            break;
                        case OpCode.SetEnv:
                            SetEnvironment(stack[--_top]);
                            break;

                        case OpCode.GetIndex:
                        {
                            var key = stack[--_top];
                            stack[_top - 1] = Pico8Ops.Index(stack[_top - 1], key);
                            break;
                        }
                        case OpCode.SetIndex:
                            Pico8Ops.SetIndex(stack[_top - 3], stack[_top - 2], stack[_top - 1]);
                            _top -= 3;
                            break;
                        case OpCode.GetField:
                            stack[_top - 1] = Pico8Ops.Index(stack[_top - 1], k[instruction.A]);
                            break;
                        case OpCode.SetField:
                            Pico8Ops.SetIndex(stack[_top - 2], k[instruction.A], stack[_top - 1]);
                            _top -= 2;
                            break;
                        case OpCode.SetIndexFrom:
                        {
                            var valueAt = _top - 1;
                            Pico8Ops.SetIndex(stack[valueAt - instruction.A], stack[valueAt - instruction.A + 1], stack[valueAt]);
                            _top--;
                            break;
                        }
                        case OpCode.Self:
                        {
                            var receiver = stack[_top - 1];
                            stack[_top - 1] = Pico8Ops.Index(receiver, k[instruction.A]);
                            stack[_top++] = receiver;
                            break;
                        }
                        case OpCode.NewTable:
                            stack[_top++] = ScriptValue.Table(new ScriptTable());
                            break;
                        case OpCode.SetList:
                        {
                            var count = instruction.A + (instruction.C == 1 ? _openCount : 0);
                            var first = _top - count;
                            var table = stack[first - 1].AsTable();
                            for (var i = 0; i < count; i++) table.Set(instruction.B + i, stack[first + i]);
                            _top = first;
                            break;
                        }
                        case OpCode.SetTableField:
                            stack[_top - 3 - instruction.A].AsTable().Set(stack[_top - 2], stack[_top - 1]);
                            _top -= 2;
                            break;

                        case OpCode.Add: Binary(stack, Pico8Ops.Add); break;
                        case OpCode.Sub: Binary(stack, Pico8Ops.Sub); break;
                        case OpCode.Mul: Binary(stack, Pico8Ops.Mul); break;
                        case OpCode.Div: Binary(stack, Pico8Ops.Div); break;
                        case OpCode.IntDiv: Binary(stack, Pico8Ops.IntDiv); break;
                        case OpCode.Mod: Binary(stack, Pico8Ops.Mod); break;
                        case OpCode.Pow: Binary(stack, Pico8Ops.Pow); break;
                        case OpCode.BAnd: Binary(stack, Pico8Ops.BAnd); break;
                        case OpCode.BOr: Binary(stack, Pico8Ops.BOr); break;
                        case OpCode.BXor: Binary(stack, Pico8Ops.BXor); break;
                        case OpCode.Shl: Binary(stack, Pico8Ops.Shl); break;
                        case OpCode.Shr: Binary(stack, Pico8Ops.Shr); break;
                        case OpCode.Lshr: Binary(stack, Pico8Ops.Lshr); break;
                        case OpCode.Rotl: Binary(stack, Pico8Ops.Rotl); break;
                        case OpCode.Rotr: Binary(stack, Pico8Ops.Rotr); break;
                        case OpCode.Concat: Binary(stack, Pico8Ops.Concat); break;
                        case OpCode.Eq:
                            _top--;
                            stack[_top - 1] = Bool(Pico8Ops.RawEquals(stack[_top - 1], stack[_top]));
                            break;
                        case OpCode.Ne:
                            _top--;
                            stack[_top - 1] = Bool(!Pico8Ops.RawEquals(stack[_top - 1], stack[_top]));
                            break;
                        case OpCode.Lt:
                            _top--;
                            stack[_top - 1] = Bool(Pico8Ops.LessThan(stack[_top - 1], stack[_top]));
                            break;
                        case OpCode.Le:
                            _top--;
                            stack[_top - 1] = Bool(Pico8Ops.LessEqual(stack[_top - 1], stack[_top]));
                            break;
                        // a > b is b < a, as Lua compiles it (it shows in the error message).
                        case OpCode.Gt:
                            _top--;
                            stack[_top - 1] = Bool(Pico8Ops.LessThan(stack[_top], stack[_top - 1]));
                            break;
                        case OpCode.Ge:
                            _top--;
                            stack[_top - 1] = Bool(Pico8Ops.LessEqual(stack[_top], stack[_top - 1]));
                            break;
                        case OpCode.Neg:
                            stack[_top - 1] = Pico8Ops.Neg(stack[_top - 1]);
                            break;
                        case OpCode.Not:
                            stack[_top - 1] = Bool(!Pico8Ops.IsTruthy(stack[_top - 1]));
                            break;
                        case OpCode.Len:
                            stack[_top - 1] = Pico8Ops.Length(stack[_top - 1]);
                            break;
                        case OpCode.BNot:
                            stack[_top - 1] = Pico8Ops.BNot(stack[_top - 1]);
                            break;

                        case OpCode.Jump:
                            pc = instruction.A;
                            break;
                        case OpCode.JumpIfFalse:
                            if (!Pico8Ops.IsTruthy(stack[--_top])) pc = instruction.A;
                            break;
                        case OpCode.JumpIfFalseKeep:
                            if (!Pico8Ops.IsTruthy(stack[_top - 1])) pc = instruction.A;
                            else _top--;
                            break;
                        case OpCode.JumpIfTrueKeep:
                            if (Pico8Ops.IsTruthy(stack[_top - 1])) pc = instruction.A;
                            else _top--;
                            break;
                        case OpCode.Pop:
                            _top -= instruction.A;
                            break;
                        case OpCode.Dup2:
                            stack[_top] = stack[_top - 2];
                            stack[_top + 1] = stack[_top - 1];
                            _top += 2;
                            break;

                        case OpCode.Call:
                        {
                            var argumentCount = instruction.A + (instruction.C == 1 ? _openCount : 0);
                            var functionIndex = _top - argumentCount - 1;
                            var callee = stack[functionIndex];
                            frame.Pc = pc;
                            if (callee.Data is LuaClosure closure)
                            {
                                PushFrame(closure, functionIndex, argumentCount, instruction.B);
                                switchFrame = true;
                            }
                            else if (callee.Type == ScriptValueType.NativeFunction)
                            {
                                CallNative(callee, functionIndex, argumentCount, instruction.B);
                                // A native may call back into Lua, which can grow the stack.
                                stack = _stack;
                                cells = _cells;
                            }
                            else
                            {
                                throw new Pico8RuntimeException($"attempt to call a {Pico8Ops.TypeName(callee)} value");
                            }
                            break;
                        }
                        case OpCode.Return:
                        {
                            var count = instruction.A + (instruction.C == 1 ? _openCount : 0);
                            var source = _top - count;
                            var destination = frame.ReturnBase;
                            var wanted = frame.Wanted;
                            var kept = wanted < 0 ? count : Math.Min(count, wanted);
                            Array.Copy(stack, source, stack, destination, kept);
                            if (wanted < 0)
                            {
                                _openCount = count;
                                _top = destination + count;
                            }
                            else
                            {
                                for (var i = kept; i < wanted; i++) stack[destination + i] = ScriptValue.Nil;
                                _top = destination + wanted;
                            }

                            frame.Closure = null!;
                            frame.Varargs = NoValues;
                            _frameCount--;
                            if (_frameCount == stopDepth) return;
                            switchFrame = true;
                            break;
                        }
                        case OpCode.Vararg:
                        {
                            var varargs = frame.Varargs;
                            if (instruction.A < 0)
                            {
                                EnsureStack(_top + varargs.Length);
                                stack = _stack;
                                cells = _cells;
                                Array.Copy(varargs, 0, stack, _top, varargs.Length);
                                _top += varargs.Length;
                                _openCount = varargs.Length;
                            }
                            else
                            {
                                for (var i = 0; i < instruction.A; i++)
                                    stack[_top++] = i < varargs.Length ? varargs[i] : ScriptValue.Nil;
                            }
                            break;
                        }
                        case OpCode.Closure:
                            stack[_top++] = ScriptValue.Func(MakeClosure(frame, instruction.A));
                            break;

                        case OpCode.ForPrep:
                        {
                            var slot = frameBase + instruction.A;
                            var start = ForNumber(stack[_top - 3], "initial value");
                            var limit = ForNumber(stack[_top - 2], "limit");
                            var step = ForNumber(stack[_top - 1], "step");
                            _top -= 3;
                            stack[slot] = Pico8Ops.Sub(start, step);
                            stack[slot + 1] = limit;
                            stack[slot + 2] = step;
                            pc = instruction.B;
                            break;
                        }
                        case OpCode.ForLoop:
                        {
                            var slot = frameBase + instruction.A;
                            var step = stack[slot + 2];
                            var index = Pico8Ops.Add(stack[slot], step);
                            var inside = step.AsNumber().Raw > 0
                                ? Pico8Ops.LessEqual(index, stack[slot + 1])
                                : Pico8Ops.LessEqual(stack[slot + 1], index);
                            if (inside)
                            {
                                stack[slot] = index;
                                stack[_top++] = index;
                                pc = instruction.B;
                            }
                            break;
                        }
                        case OpCode.TForLoop:
                        {
                            var first = stack[_top - instruction.B];
                            if (first.Type == ScriptValueType.Nil)
                            {
                                _top -= instruction.B;
                                pc = instruction.C;
                            }
                            else
                            {
                                stack[frameBase + instruction.A + 2] = first;
                            }
                            break;
                        }

                        default:
                            throw new InvalidOperationException($"Unknown instruction {instruction.Op}.");
                    }
                }
            }
        }
        catch (Pico8RuntimeException e) when (e.Span is null && frame?.Closure is not null)
        {
            var spans = frame.Closure.Prototype.Spans;
            var at = pc - 1;
            throw new Pico8RuntimeException(e.Message, at >= 0 && at < spans.Length ? spans[at] : null, e);
        }
    }

    private void Binary(ScriptValue[] stack, Func<ScriptValue, ScriptValue, ScriptValue> op)
    {
        _top--;
        stack[_top - 1] = op(stack[_top - 1], stack[_top]);
    }

    private static ScriptValue Bool(bool value) => value ? ScriptValue.True : ScriptValue.False;

    private static ScriptValue ForNumber(ScriptValue value, string what) =>
        Pico8Ops.TryToNumber(value, out var number)
            ? ScriptValue.Number(number)
            : throw new Pico8RuntimeException($"'for' {what} must be a number");

    private LuaClosure MakeClosure(CallFrame frame, int index)
    {
        var proto = frame.Closure.Prototype.Prototypes[index];
        var upvalues = new UpvalueCell[proto.Upvalues.Length];
        for (var i = 0; i < upvalues.Length; i++)
        {
            var descriptor = proto.Upvalues[i];
            upvalues[i] = descriptor.FromParentLocal
                ? _cells[frame.Base + descriptor.Index] ??= new UpvalueCell(ScriptValue.Nil)
                : frame.Closure.Upvalues[descriptor.Index];
        }
        return new LuaClosure(proto, upvalues);
    }

    private sealed class CallFrame
    {
        public LuaClosure Closure = null!;
        public Instruction[] Code = [];
        public ScriptValue[] Constants = [];
        public int Base;
        public int Pc;
        public ScriptValue[] Varargs = [];

        /// <summary>Where the results go: the stack position of the called function.</summary>
        public int ReturnBase;

        /// <summary>Results the caller wants, or -1 for all of them.</summary>
        public int Wanted;
    }
}
