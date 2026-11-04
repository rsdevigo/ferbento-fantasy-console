using Ferbento.Scripting.Interpreter;
using Ferbento.Scripting.Vm;

namespace Ferbento.Scripting.Api;

public static class TableApi
{
    public static void Register(Interpreter.Environment env, Pico8Context ctx)
    {
        // add(t, v, [i]): appends v (or inserts it at i, shifting the following elements up)
        // and returns v, like PICO-8.
        env.Define("add", ScriptValue.Native(args =>
        {
            if (args.Length < 2 || args[0].Type != ScriptValueType.Table) return ScriptValue.Nil;
            var t = args[0].AsTable();
            var value = args[1];
            var length = t.Length;
            if (length + 1 > ctx.MaxTableSize) return ScriptValue.Nil;

            var index = length + 1;
            if (args.Length > 2 && args[2].Type == ScriptValueType.Number)
                index = Math.Clamp(args[2].AsInt(), 1, length + 1);

            for (var i = length; i >= index; i--)
                t.Set(i + 1, t.Get(i));
            t.Set(index, value);
            return value;
        }));

        // del(t, v): removes the first element equal to v (Lua equality), shifts the following
        // elements down to keep the sequence contiguous and returns the removed value, or nil.
        env.Define("del", ScriptValue.Native(args =>
        {
            if (args.Length < 2 || args[0].Type != ScriptValueType.Table) return ScriptValue.Nil;
            var t = args[0].AsTable();
            var length = t.Length;
            for (var i = 1; i <= length; i++)
            {
                var current = t.Get(i);
                if (!Pico8Ops.RawEquals(current, args[1])) continue;

                for (var j = i; j < length; j++)
                    t.Set(j, t.Get(j + 1));
                t.Set(length, ScriptValue.Nil);
                return current;
            }
            return ScriptValue.Nil;
        }));

        env.Define("count", ScriptValue.Native(args =>
        {
            if (args.Length == 0 || args[0].Type != ScriptValueType.Table) return ScriptValue.Number(0);
            return ScriptValue.Number(args[0].AsTable().Length);
        }));
    }
}
