using Ferbento.Scripting.Interpreter;

namespace Ferbento.Scripting.Api;

public static class TableApi
{
    public static void Register(Interpreter.Environment env, Pico8Context ctx)
    {
        env.Define("add", ScriptValue.Native(args =>
        {
            if (args.Length < 2 || args[0].Type != ScriptValueType.Table) return ScriptValue.Nil;
            var t = args[0].AsTable();
            var next = t.Length + 1;
            if (next > ctx.MaxTableSize) return ScriptValue.Nil;
            t.Set(next, args[1]);
            return ScriptValue.Nil;
        }));

        env.Define("del", ScriptValue.Native(args =>
        {
            if (args.Length < 2 || args[0].Type != ScriptValueType.Table) return ScriptValue.False;
            var t = args[0].AsTable();
            foreach (var key in t.ArrayKeys)
            {
                if (t.Get(key).ToString() == args[1].ToString())
                {
                    t.Set(key, ScriptValue.Nil);
                    return ScriptValue.True;
                }
            }
            return ScriptValue.False;
        }));

        env.Define("count", ScriptValue.Native(args =>
        {
            if (args.Length == 0 || args[0].Type != ScriptValueType.Table) return ScriptValue.Number(0);
            return ScriptValue.Number(args[0].AsTable().Length);
        }));
    }
}
