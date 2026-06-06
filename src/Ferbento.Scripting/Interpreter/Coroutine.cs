using GDShrapt.Reader;

namespace Ferbento.Scripting.Interpreter;

public enum CoroutineStatus
{
    Suspended,
    Running,
    Dead
}

public sealed class Coroutine
{
    public UserFunction Function { get; }
    public Environment Environment { get; }
    public CoroutineStatus Status { get; set; } = CoroutineStatus.Suspended;
    public int StatementIndex { get; set; }
    public GDStatementsList? Body { get; set; }

    public Coroutine(UserFunction function, Environment env)
    {
        Function = function;
        Environment = env;
        Body = function.Body;
    }
}

public sealed class YieldSignal : Exception
{
    public static readonly YieldSignal Instance = new();
    private YieldSignal() { }
}

public sealed class ReturnSignal : Exception
{
    public ScriptValue Value { get; }
    public ReturnSignal(ScriptValue value) => Value = value;
}
