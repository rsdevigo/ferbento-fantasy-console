using Ferbento.Scripting.Interpreter;

namespace Ferbento.Scripting.Api;

public static class AudioApi
{
    public static void Register(Interpreter.Environment env, Pico8Context ctx)
    {
        env.Define("sfx", ScriptValue.Native(args =>
        {
            var n = args.Length > 0 ? args[0].AsInt() : -1;
            var ch = args.Length > 1 ? args[1].AsInt() : -1;
            var offset = args.Length > 2 ? args[2].AsInt() : 0;
            ctx.Bus.Sound.Sfx(n, ch, offset);
            return ScriptValue.Nil;
        }));

        env.Define("music", ScriptValue.Native(args =>
        {
            var n = args.Length > 0 ? args[0].AsInt() : -1;
            var fade = args.Length > 1 ? args[1].AsInt() : 0;
            var mask = args.Length > 2 ? args[2].AsInt() : 0;
            ctx.Bus.Music.Music(n, fade, mask);
            return ScriptValue.Nil;
        }));
    }
}
