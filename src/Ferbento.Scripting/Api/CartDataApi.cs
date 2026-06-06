using Ferbento.Scripting.Interpreter;

namespace Ferbento.Scripting.Api;

public static class CartDataApi
{
    public static void Register(Interpreter.Environment env, Pico8Context ctx)
    {
        env.Define("cartdata", ScriptValue.Native(args =>
        {
            var id = args.Length > 0 ? args[0].AsString() : "";
            ctx.CartData(id);
            return ScriptValue.Nil;
        }));

        env.Define("dget", ScriptValue.Native(args =>
        {
            var idx = args.Length > 0 ? args[0].AsInt() : 0;
            return ScriptValue.Number(ctx.DGet(idx));
        }));

        env.Define("dset", ScriptValue.Native(args =>
        {
            var idx = args.Length > 0 ? args[0].AsInt() : 0;
            var val = args.Length > 1 ? args[1].AsNumber().ToDouble() : 0;
            ctx.DSet(idx, val);
            return ScriptValue.Nil;
        }));
    }
}
