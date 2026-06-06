namespace Ferbento.Scripting.Interpreter;

public sealed class Environment
{
    private readonly Dictionary<string, ScriptValue> _values = new();
    public Environment? Parent { get; }

    public Environment(Environment? parent = null) => Parent = parent;

    public void Define(string name, ScriptValue value) => _values[name] = value;

    public ScriptValue Get(string name)
    {
        if (_values.TryGetValue(name, out var value))
            return value;
        if (Parent != null)
            return Parent.Get(name);
        return ScriptValue.Nil;
    }

    public void Set(string name, ScriptValue value)
    {
        if (_values.ContainsKey(name))
            _values[name] = value;
        else if (Parent != null)
            Parent.Set(name, value);
        else
            _values[name] = value;
    }

    public bool HasLocal(string name) => _values.ContainsKey(name);

    public Environment Child() => new(this);
}
