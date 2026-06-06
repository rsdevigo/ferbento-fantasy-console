using Ferbento.Core.Math;
using Ferbento.Scripting.Interpreter;

namespace Ferbento.Scripting.Api;

public static class MemoryApi
{
    public static void Register(Interpreter.Environment env, Pico8Context ctx)
    {
        env.Define("peek", ScriptValue.Native(args =>
        {
            var addr = args.Length > 0 ? args[0].AsInt() : 0;
            ctx.TickCpu();
            return ScriptValue.Number(ctx.Bus.Memory.Memory.Peek(addr));
        }));

        env.Define("poke", ScriptValue.Native(args =>
        {
            var addr = args.Length > 0 ? args[0].AsInt() : 0;
            var val = args.Length > 1 ? args[1].AsInt() : 0;
            ctx.TickCpu();
            ctx.Bus.Memory.Memory.Poke(addr, (byte)(val & 0xFF));
            return ScriptValue.Nil;
        }));

        env.Define("peek2", ScriptValue.Native(args =>
        {
            var addr = args.Length > 0 ? args[0].AsInt() : 0;
            ctx.TickCpu();
            return ScriptValue.Number(ctx.Bus.Memory.Memory.Peek2(addr));
        }));

        env.Define("poke2", ScriptValue.Native(args =>
        {
            var addr = args.Length > 0 ? args[0].AsInt() : 0;
            var val = args.Length > 1 ? args[0].AsInt() : 0;
            ctx.TickCpu();
            ctx.Bus.Memory.Memory.Poke2(addr, (ushort)(args.Length > 1 ? args[1].AsInt() : 0));
            return ScriptValue.Nil;
        }));

        env.Define("peek4", ScriptValue.Native(args =>
        {
            var addr = args.Length > 0 ? args[0].AsInt() : 0;
            ctx.TickCpu();
            return ScriptValue.Number(FixedPoint.FromInt((int)ctx.Bus.Memory.Memory.Peek4(addr)));
        }));

        env.Define("poke4", ScriptValue.Native(args =>
        {
            var addr = args.Length > 0 ? args[0].AsInt() : 0;
            var val = args.Length > 1 ? (uint)args[1].AsNumber().Raw : 0;
            ctx.TickCpu();
            ctx.Bus.Memory.Memory.Poke4(addr, val);
            return ScriptValue.Nil;
        }));

        env.Define("memcpy", ScriptValue.Native(args =>
        {
            var dst = args.Length > 0 ? args[0].AsInt() : 0;
            var src = args.Length > 1 ? args[1].AsInt() : 0;
            var len = args.Length > 2 ? args[2].AsInt() : 0;
            ctx.TickCpu();
            ctx.Bus.Memory.Memory.Memcpy(dst, src, len);
            return ScriptValue.Nil;
        }));

        env.Define("memset", ScriptValue.Native(args =>
        {
            var dst = args.Length > 0 ? args[0].AsInt() : 0;
            var val = args.Length > 1 ? args[1].AsInt() : 0;
            var len = args.Length > 2 ? args[2].AsInt() : 0;
            ctx.TickCpu();
            ctx.Bus.Memory.Memory.Memset(dst, (byte)(val & 0xFF), len);
            return ScriptValue.Nil;
        }));
    }
}
