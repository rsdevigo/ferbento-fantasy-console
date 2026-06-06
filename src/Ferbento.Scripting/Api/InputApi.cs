using Ferbento.Scripting.Interpreter;

namespace Ferbento.Scripting.Api;

public static class InputApi
{
    public static void Register(Interpreter.Environment env, Pico8Context ctx)
    {
        env.Define("btn", ScriptValue.Native(args =>
        {
            var button = args.Length > 0 ? args[0].AsInt() : -1;
            var player = args.Length > 1 ? args[1].AsInt() : 0;
            if (button < 0)
            {
                var mask = 0;
                for (var i = 0; i < 6; i++)
                    if (ctx.Bus.Input.Btn(i, player)) mask |= 1 << i;
                return ScriptValue.Number(mask);
            }
            return ctx.Bus.Input.Btn(button, player) ? ScriptValue.Number(1) : ScriptValue.Number(0);
        }));

        env.Define("btnp", ScriptValue.Native(args =>
        {
            var button = args.Length > 0 ? args[0].AsInt() : -1;
            var player = args.Length > 1 ? args[1].AsInt() : 0;
            if (button < 0)
            {
                var mask = 0;
                for (var i = 0; i < 6; i++)
                    if (ctx.Bus.Input.BtnP(i, player)) mask |= 1 << i;
                return ScriptValue.Number(mask);
            }
            return ctx.Bus.Input.BtnP(button, player) ? ScriptValue.Number(1) : ScriptValue.Number(0);
        }));
    }
}
