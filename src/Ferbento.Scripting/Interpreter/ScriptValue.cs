using Ferbento.Core.Math;

namespace Ferbento.Scripting.Interpreter;

public enum ScriptValueType
{
    Nil,
    Number,
    String,
    Bool,
    Table,
    Function,
    NativeFunction,
    Coroutine
}

public sealed class ScriptValue
{
    public ScriptValueType Type { get; }
    public object? Data { get; }

    public static readonly ScriptValue Nil = new(ScriptValueType.Nil, null);
    public static readonly ScriptValue True = new(ScriptValueType.Bool, true);
    public static readonly ScriptValue False = new(ScriptValueType.Bool, false);

    private ScriptValue(ScriptValueType type, object? data)
    {
        Type = type;
        Data = data;
    }

    public static ScriptValue Number(FixedPoint n) => new(ScriptValueType.Number, n);
    public static ScriptValue Number(int n) => new(ScriptValueType.Number, FixedPoint.FromInt(n));
    public static ScriptValue Number(double n) => new(ScriptValueType.Number, FixedPoint.FromDouble(n));
    public static ScriptValue Str(string s) => new(ScriptValueType.String, s ?? "");
    public static ScriptValue Table(ScriptTable t) => new(ScriptValueType.Table, t);
    public static ScriptValue Func(UserFunction f) => new(ScriptValueType.Function, f);
    public static ScriptValue Native(Func<ScriptValue[], ScriptValue> f) => new(ScriptValueType.NativeFunction, f);
    public static ScriptValue Co(Coroutine c) => new(ScriptValueType.Coroutine, c);

    public FixedPoint AsNumber() => Type == ScriptValueType.Number ? (FixedPoint)Data! : FixedPoint.FromInt(0);
    public int AsInt() => AsNumber().ToInt();
    public string AsString() => Type == ScriptValueType.String ? (string)Data! : ToString();
    public bool AsBool() => Type == ScriptValueType.Bool && (bool)Data!;
    public ScriptTable AsTable() => (ScriptTable)(Data ?? new ScriptTable());
    public UserFunction AsFunction() => (UserFunction)Data!;
    public Func<ScriptValue[], ScriptValue> AsNative() => (Func<ScriptValue[], ScriptValue>)Data!;
    public Coroutine AsCoroutine() => (Coroutine)Data!;

    public bool IsTruthy() => Type switch
    {
        ScriptValueType.Nil => false,
        ScriptValueType.Bool => (bool)Data!,
        ScriptValueType.Number => ((FixedPoint)Data!).Raw != 0,
        ScriptValueType.String => ((string)Data!).Length > 0,
        ScriptValueType.Table => true,
        _ => true
    };

    public override string ToString() => Type switch
    {
        ScriptValueType.Nil => "nil",
        ScriptValueType.Number => AsNumber().ToString(),
        ScriptValueType.String => (string)Data!,
        ScriptValueType.Bool => (bool)Data! ? "true" : "false",
        ScriptValueType.Table => "[table]",
        ScriptValueType.Function => "[function]",
        ScriptValueType.NativeFunction => "[native]",
        ScriptValueType.Coroutine => "[coroutine]",
        _ => "?"
    };
}

public sealed class ScriptTable
{
    private readonly Dictionary<string, ScriptValue> _named = new();
    private readonly Dictionary<int, ScriptValue> _array = new();
    private int _length;

    public ScriptValue Get(string key) => _named.TryGetValue(key, out var v) ? v : ScriptValue.Nil;
    public ScriptValue Get(int index) => _array.TryGetValue(index, out var v) ? v : ScriptValue.Nil;

    public void Set(string key, ScriptValue value) => _named[key] = value;
    public void Set(int index, ScriptValue value)
    {
        _array[index] = value;
        if (index > _length) _length = index;
    }

    public int Length => _length;
    public IEnumerable<int> ArrayKeys => _array.Keys.OrderBy(k => k);
    public IEnumerable<string> NamedKeys => _named.Keys;
    public IEnumerable<ScriptValue> ArrayValues => _array.OrderBy(kv => kv.Key).Select(kv => kv.Value);
}

public sealed class UserFunction
{
    public required string Name { get; init; }
    public required IReadOnlyList<string> Parameters { get; init; }
    public required GDShrapt.Reader.GDStatementsList Body { get; init; }
    public Environment? Closure { get; init; }
}
