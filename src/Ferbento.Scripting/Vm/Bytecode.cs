using System.Collections.Immutable;
using Ferbento.Scripting.Compiler.Ir;
using Ferbento.Scripting.Interpreter;

namespace Ferbento.Scripting.Vm;

/// <summary>
/// Instructions of the VM. It is a stack machine: expressions push their values on the operand
/// stack of the frame, and locals live in numbered slots below it. "k" is a constant index,
/// "slot" a local slot of the frame and "target" an instruction index.
/// </summary>
public enum OpCode : byte
{
    // ---- constants ----
    LoadNil,
    LoadTrue,
    LoadFalse,
    /// <summary>A = k.</summary>
    LoadConst,

    // ---- variables ----
    /// <summary>A = slot.</summary>
    GetLocal,
    /// <summary>A = slot; pops the value.</summary>
    SetLocal,
    /// <summary>A = slot holding a captured local (an <see cref="UpvalueCell"/>).</summary>
    GetCell,
    /// <summary>A = slot; pops the value into the existing cell.</summary>
    SetCell,
    /// <summary>A = slot; pops the value into a new cell (each execution of <c>local</c> is a new variable).</summary>
    NewCell,
    /// <summary>A = upvalue index of the running closure.</summary>
    GetUpval,
    /// <summary>A = upvalue index; pops the value.</summary>
    SetUpval,
    /// <summary>A = k (the name). Reads the environment, the globals table by default.</summary>
    GetGlobal,
    /// <summary>A = k (the name); pops the value.</summary>
    SetGlobal,
    /// <summary>Pushes the environment itself (the name <c>_ENV</c>).</summary>
    GetEnv,
    /// <summary>Pops a value and makes it the environment (<c>_ENV = t</c>).</summary>
    SetEnv,

    // ---- tables ----
    /// <summary>t k → t[k]</summary>
    GetIndex,
    /// <summary>t k v → (t[k] = v)</summary>
    SetIndex,
    /// <summary>A = k. t → t[k]</summary>
    GetField,
    /// <summary>A = k. t v → (t[k] = v)</summary>
    SetField,
    /// <summary>
    /// Multiple assignment: pops v and does t[k] = v, with t and k at A and A - 1 values below the top
    /// (counted before the pop). The pair stays on the stack.
    /// </summary>
    SetIndexFrom,
    /// <summary>A = k. obj → obj[k] obj (the method and its <c>self</c>).</summary>
    Self,
    /// <summary>Pushes a new table.</summary>
    NewTable,
    /// <summary>
    /// A values (plus the open results when C = 1) → stores them at t[B], t[B+1]…; the table stays,
    /// just below the values.
    /// </summary>
    SetList,
    /// <summary>k v → t[k] = v, with t A values below the key (the positional values not yet stored).</summary>
    SetTableField,

    // ---- operators (all through Pico8Ops) ----
    Add, Sub, Mul, Div, IntDiv, Mod, Pow,
    BAnd, BOr, BXor, Shl, Shr, Lshr, Rotl, Rotr,
    Concat,
    Eq, Ne, Lt, Le, Gt, Ge,
    Neg, Not, Len, BNot,

    // ---- control ----
    /// <summary>A = target.</summary>
    Jump,
    /// <summary>A = target; pops the condition.</summary>
    JumpIfFalse,
    /// <summary><c>and</c>: A = target. A false top stays and jumps; otherwise it is popped.</summary>
    JumpIfFalseKeep,
    /// <summary><c>or</c>: A = target. A true top stays and jumps; otherwise it is popped.</summary>
    JumpIfTrueKeep,
    /// <summary>A = count.</summary>
    Pop,
    /// <summary>Copies the two values on top (t k → t k t k), for <c>t[k] += v</c>.</summary>
    Dup2,

    // ---- functions ----
    /// <summary>
    /// f a1..an → results. A = fixed argument count; C = 1 when the last argument is open (its results
    /// count too); B = results wanted, or -1 to keep them all open.
    /// </summary>
    Call,
    /// <summary>A = fixed value count; C = 1 when the last value is open.</summary>
    Return,
    /// <summary><c>...</c>: A = values wanted, or -1 to keep them all open.</summary>
    Vararg,
    /// <summary>A = index of the nested prototype. Captures its upvalues from the running frame.</summary>
    Closure,

    // ---- loops ----
    /// <summary>
    /// start limit step → hidden slots A, A+1, A+2. Checks they are numbers, sets the index to
    /// start - step and jumps to the <see cref="ForLoop"/> at B.
    /// </summary>
    ForPrep,
    /// <summary>index += step; if still inside the limit, pushes the index and jumps to B.</summary>
    ForLoop,
    /// <summary>
    /// After <c>generator(state, control)</c> left B results: if the first is nil, pops them and jumps
    /// to C; otherwise copies it to the control slot A + 2 and goes on (the results are then stored
    /// in the loop variables).
    /// </summary>
    TForLoop,
}

/// <summary>One instruction; the meaning of A, B and C depends on <see cref="Op"/>.</summary>
public readonly record struct Instruction(OpCode Op, int A = 0, int B = 0, int C = 0)
{
    public override string ToString() => $"{Op} {A} {B} {C}";
}

/// <summary>
/// Where a function finds an upvalue when its closure is created: a captured local slot of the
/// enclosing frame (<see cref="FromParentLocal"/>) or one of the enclosing closure's upvalues.
/// </summary>
public readonly record struct UpvalueDescriptor(string Name, bool FromParentLocal, int Index);

/// <summary>A compiled function: its code and everything the VM needs to run a call to it.</summary>
public sealed class Prototype
{
    public required string Name { get; init; }

    public required ImmutableArray<Instruction> Code { get; init; }

    /// <summary>The source position of each instruction (same length as <see cref="Code"/>).</summary>
    public required ImmutableArray<SourceSpan?> Spans { get; init; }

    public required ImmutableArray<ScriptValue> Constants { get; init; }

    public required ImmutableArray<Prototype> Prototypes { get; init; }

    public required ImmutableArray<UpvalueDescriptor> Upvalues { get; init; }

    public required int ParameterCount { get; init; }

    public required bool IsVararg { get; init; }

    /// <summary>Local slots: one per declared local, plus the hidden slots of the loops.</summary>
    public required int SlotCount { get; init; }

    /// <summary>Largest depth of the operand stack above the slots.</summary>
    public required int MaxStack { get; init; }

    /// <summary>Parameters a nested function captures; they get a cell when the call starts.</summary>
    public required ImmutableArray<int> CapturedParameters { get; init; }

    public SourceSpan? Span { get; init; }

    /// <summary>The source line of instruction <paramref name="pc"/>, or 0 when unknown.</summary>
    public int LineAt(int pc) => pc >= 0 && pc < Spans.Length ? Spans[pc]?.Line ?? 0 : 0;

    public override string ToString() => Name;
}

/// <summary>A captured local variable, shared by every closure that uses it.</summary>
public sealed class UpvalueCell(ScriptValue value)
{
    public ScriptValue Value = value;
}

/// <summary>A Lua function value: a prototype plus the cells of its upvalues.</summary>
public sealed class LuaClosure(Prototype prototype, UpvalueCell[] upvalues)
{
    public Prototype Prototype { get; } = prototype;

    public UpvalueCell[] Upvalues { get; } = upvalues;

    public override string ToString() => $"function: {Prototype.Name}";
}
