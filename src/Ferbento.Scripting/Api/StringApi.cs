using Ferbento.Scripting.Interpreter;

namespace Ferbento.Scripting.Api;

public static class StringApi
{
    public static void Register(Interpreter.Environment env, Pico8Context ctx)
    {
        env.Define("sub", ScriptValue.Native(args =>
        {
            var str = args.Length > 0 ? args[0].AsString() : "";
            var from = args.Length > 1 ? args[1].AsInt() : 1;
            var to = args.Length > 2 ? args[2].AsInt() : Math.Min(str.Length, ctx.MaxStringLength);
            if (from < 1) from = 1;
            if (to < from) return ScriptValue.Str("");
            var start = from - 1;
            var maxLen = Math.Min(str.Length - start, ctx.MaxStringLength);
            var len = Math.Min(to - from + 1, maxLen);
            if (start >= str.Length || len <= 0) return ScriptValue.Str("");
            return ScriptValue.Str(str.Substring(start, len));
        }));

        env.Define("tostr", ScriptValue.Native(args =>
        {
            if (args.Length == 0) return ScriptValue.Str("");
            if (args.Length > 1 && args[1].IsTruthy())
                return ScriptValue.Str($"0x{args[0].AsInt():X}");
            return ScriptValue.Str(args[0].ToString());
        }));

        env.Define("tonum", ScriptValue.Native(args =>
        {
            var str = args.Length > 0 ? args[0].AsString() : "0";
            if (double.TryParse(str, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var d))
                return ScriptValue.Number(d);
            return ScriptValue.Number(0);
        }));
    }
}
