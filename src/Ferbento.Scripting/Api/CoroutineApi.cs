using Ferbento.Scripting.Interpreter;

namespace Ferbento.Scripting.Api;

public static class CoroutineApi
{
    public static void Register(Interpreter.Environment env, Pico8Context ctx, ScriptInterpreter interpreter)
    {
        env.Define("cocreate", ScriptValue.Native(args =>
        {
            if (args.Length == 0 || args[0].Type != ScriptValueType.Function)
                return ScriptValue.Nil;
            var fn = args[0].AsFunction();
            var co = new Coroutine(fn, interpreter.CurrentEnvironment.Child());
            return ScriptValue.Co(co);
        }));

        env.Define("coresume", ScriptValue.Native(args =>
        {
            if (args.Length == 0 || args[0].Type != ScriptValueType.Coroutine)
                return ScriptValue.False;
            var co = args[0].AsCoroutine();
            if (co.Status == CoroutineStatus.Dead) return ScriptValue.False;
            interpreter.ResumeCoroutine(co);
            return ScriptValue.True;
        }));

        env.Define("costatus", ScriptValue.Native(args =>
        {
            if (args.Length == 0 || args[0].Type != ScriptValueType.Coroutine)
                return ScriptValue.Str("dead");
            var co = args[0].AsCoroutine();
            return ScriptValue.Str(co.Status switch
            {
                CoroutineStatus.Suspended => "suspended",
                CoroutineStatus.Running => "running",
                _ => "dead"
            });
        }));

        env.Define("yield", ScriptValue.Native(_ => throw YieldSignal.Instance));
    }
}
