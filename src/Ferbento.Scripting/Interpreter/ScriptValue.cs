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

    /// <summary>A Lua function compiled for the VM (story 009).</summary>
    public static ScriptValue Func(Vm.LuaClosure c) => new(ScriptValueType.Function, c);

    /// <summary>A native function with any number of results (e.g. <c>coresume</c>). VM only.</summary>
    public static ScriptValue NativeMulti(Func<ScriptValue[], ScriptValue[]> f) => new(ScriptValueType.NativeFunction, f);
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
        ScriptValueType.Number => Vm.Pico8Ops.NumberToString(AsNumber()),
        ScriptValueType.String => (string)Data!,
        ScriptValueType.Bool => (bool)Data! ? "true" : "false",
        ScriptValueType.Table => "[table]",
        ScriptValueType.Function => "[function]",
        ScriptValueType.NativeFunction => "[native]",
        ScriptValueType.Coroutine => "[coroutine]",
        _ => "?"
    };
}

/// <summary>
/// Lua/PICO-8 table. Keys can be any non-nil value: numbers (compared by their 16.16 raw bits,
/// so <c>t[1.5]</c> and <c>t[1]</c> are different keys), strings, booleans and references
/// (tables, functions, coroutines, compared by identity). The number <c>1</c> and the string
/// <c>"1"</c> are distinct keys. Assigning <c>nil</c> removes the key.
/// </summary>
public sealed class ScriptTable
{
    private readonly Dictionary<TableKey, KeyValuePair<ScriptValue, ScriptValue>> _entries = new();

    // Invariant: t[1].._border are all non-nil and t[_border + 1] is nil.
    private int _border;

    public ScriptValue Get(string key) => Get(TableKey.ForString(key));
    public ScriptValue Get(int index) => Get(TableKey.ForNumber(FixedPoint.FromInt(index)));

    public ScriptValue Get(ScriptValue key) =>
        key.Type == ScriptValueType.Nil ? ScriptValue.Nil : Get(TableKey.From(key));

    public void Set(string key, ScriptValue value) => Set(ScriptValue.Str(key), value);
    public void Set(int index, ScriptValue value) => Set(ScriptValue.Number(index), value);

    /// <summary>Sets <paramref name="key"/>; a nil <paramref name="value"/> removes it.</summary>
    /// <exception cref="Vm.Pico8RuntimeException">The key is nil.</exception>
    public void Set(ScriptValue key, ScriptValue value)
    {
        if (key.Type == ScriptValueType.Nil)
            throw new Vm.Pico8RuntimeException("table index is nil");

        var k = TableKey.From(key);
        if (value.Type == ScriptValueType.Nil)
        {
            _entries.Remove(k);
            if (k.TryGetPositiveInt(out var removed) && removed <= _border)
                _border = removed - 1;
            return;
        }

        _entries[k] = new(key, value);
        if (k.TryGetPositiveInt(out var added) && added == _border + 1)
        {
            _border = added;
            while (_entries.ContainsKey(TableKey.ForNumber(FixedPoint.FromInt(_border + 1))))
                _border++;
        }
    }

    /// <summary>
    /// The <c>#</c> length: the first border of the array part, the same as PICO-8 for
    /// sequences without holes.
    /// </summary>
    // TODO conferir no PICO-8/z8lua: with holes, real Lua may return a later border.
    public int Length => _border;

    /// <summary>Number of non-nil entries of any key type.</summary>
    public int Count => _entries.Count;

    /// <summary>All entries in insertion order (removing keys can change it, as in Lua).</summary>
    public IEnumerable<KeyValuePair<ScriptValue, ScriptValue>> Entries => _entries.Values;

    /// <summary>Integer number keys, ascending.</summary>
    public IEnumerable<int> ArrayKeys =>
        _entries.Keys.Where(k => k.IsIntegerNumber).Select(k => k.IntValue).OrderBy(k => k);

    public IEnumerable<string> NamedKeys =>
        _entries.Keys.Where(k => k.IsString).Select(k => k.StringValue);

    /// <summary>Values of the integer number keys, in ascending key order.</summary>
    public IEnumerable<ScriptValue> ArrayValues => ArrayKeys.Select(Get);

    private ScriptValue Get(TableKey key) =>
        _entries.TryGetValue(key, out var entry) ? entry.Value : ScriptValue.Nil;

    private readonly struct TableKey : IEquatable<TableKey>
    {
        private readonly ScriptValueType _type;
        private readonly int _bits;
        private readonly object? _obj;

        private TableKey(ScriptValueType type, int bits, object? obj)
        {
            _type = type;
            _bits = bits;
            _obj = obj;
        }

        public static TableKey ForNumber(FixedPoint n) => new(ScriptValueType.Number, n.Raw, null);
        public static TableKey ForString(string s) => new(ScriptValueType.String, 0, s);

        public static TableKey From(ScriptValue v) => v.Type switch
        {
            ScriptValueType.Number => ForNumber(v.AsNumber()),
            ScriptValueType.String => ForString(v.AsString()),
            ScriptValueType.Bool => new(ScriptValueType.Bool, v.AsBool() ? 1 : 0, null),
            _ => new(v.Type, 0, v.Data)
        };

        public bool IsString => _type == ScriptValueType.String;
        public string StringValue => (string)_obj!;

        public bool IsIntegerNumber =>
            _type == ScriptValueType.Number && (_bits & (FixedPoint.RawOne - 1)) == 0;

        public int IntValue => _bits >> FixedPoint.FractionBits;

        public bool TryGetPositiveInt(out int value)
        {
            value = IsIntegerNumber ? IntValue : 0;
            return value > 0;
        }

        public bool Equals(TableKey other) =>
            _type == other._type && _bits == other._bits &&
            (_type == ScriptValueType.String
                ? string.Equals((string?)_obj, (string?)other._obj, StringComparison.Ordinal)
                : ReferenceEquals(_obj, other._obj));

        public override bool Equals(object? obj) => obj is TableKey k && Equals(k);

        public override int GetHashCode() => HashCode.Combine(
            _type,
            _bits,
            _type == ScriptValueType.String
                ? StringComparer.Ordinal.GetHashCode((string)_obj!)
                : _obj is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_obj));
    }
}

public sealed class UserFunction
{
    public required string Name { get; init; }
    public required IReadOnlyList<string> Parameters { get; init; }
    public required GDShrapt.Reader.GDStatementsList Body { get; init; }
    public Environment? Closure { get; init; }
}
