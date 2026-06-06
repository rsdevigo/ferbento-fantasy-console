using Ferbento.Core.Math;
using Ferbento.Scripting.Interpreter;

namespace Ferbento.Scripting.Api;

public static class MathApi
{
    public static void Register(Interpreter.Environment env, Pico8Context ctx)
    {
        RegisterUnary(env, "abs", x => ScriptValue.Number(FixedPoint.Abs(x)));
        RegisterUnary(env, "flr", x => ScriptValue.Number(FixedPoint.Floor(x)));
        RegisterUnary(env, "sgn", x => ScriptValue.Number(FixedPoint.FromInt(FixedPoint.Sgn(x))));
        RegisterUnary(env, "sqrt", x => ScriptValue.Number(FixedPoint.Sqrt(x)));
        RegisterUnary(env, "sin", x => ScriptValue.Number(FixedPoint.Sin(x)));
        RegisterUnary(env, "cos", x => ScriptValue.Number(FixedPoint.Cos(x)));

        env.Define("min", ScriptValue.Native(args =>
        {
            if (args.Length == 0) return ScriptValue.Number(FixedPoint.FromInt(0));
            var result = args[0].AsNumber();
            for (var i = 1; i < args.Length; i++)
                result = FixedPoint.Min(result, args[i].AsNumber());
            return ScriptValue.Number(result);
        }));

        env.Define("max", ScriptValue.Native(args =>
        {
            if (args.Length == 0) return ScriptValue.Number(FixedPoint.FromInt(0));
            var result = args[0].AsNumber();
            for (var i = 1; i < args.Length; i++)
                result = FixedPoint.Max(result, args[i].AsNumber());
            return ScriptValue.Number(result);
        }));

        env.Define("mid", ScriptValue.Native(args =>
        {
            var a = args.Length > 0 ? args[0].AsNumber() : FixedPoint.FromInt(0);
            var b = args.Length > 1 ? args[1].AsNumber() : a;
            var c = args.Length > 2 ? args[2].AsNumber() : b;
            return ScriptValue.Number(FixedPoint.Mid(a, b, c));
        }));

        env.Define("atan2", ScriptValue.Native(args =>
        {
            var dy = args.Length > 0 ? args[0].AsNumber() : FixedPoint.FromInt(0);
            var dx = args.Length > 1 ? args[1].AsNumber() : FixedPoint.FromInt(0);
            return ScriptValue.Number(FixedPoint.Atan2(dy, dx));
        }));

        env.Define("rnd", ScriptValue.Native(args =>
        {
            var max = args.Length > 0 ? args[0].AsNumber() : FixedPoint.FromInt(1);
            return ScriptValue.Number(ctx.Random.Next(max));
        }));

        env.Define("srand", ScriptValue.Native(args =>
        {
            var seed = args.Length > 0 ? args[0].AsInt() : 0;
            ctx.Random.Seed(seed);
            return ScriptValue.Nil;
        }));

        RegisterBitwise(env, "band", FixedPoint.Band);
        RegisterBitwise(env, "bor", FixedPoint.Bor);
        RegisterBitwise(env, "bxor", FixedPoint.Bxor);
        RegisterBitwise(env, "bnot", a => FixedPoint.Bnot(a));
        RegisterBitwise2(env, "shl", FixedPoint.Shl);
        RegisterBitwise2(env, "shr", FixedPoint.Shr);
    }

    private static void RegisterUnary(Interpreter.Environment env, string name, Func<FixedPoint, ScriptValue> fn) =>
        env.Define(name, ScriptValue.Native(args => fn(args.Length > 0 ? args[0].AsNumber() : FixedPoint.FromInt(0))));

    private static void RegisterBitwise(Interpreter.Environment env, string name, Func<int, int, int> fn) =>
        env.Define(name, ScriptValue.Native(args =>
        {
            var a = args.Length > 0 ? args[0].AsInt() : 0;
            var b = args.Length > 1 ? args[1].AsInt() : 0;
            return ScriptValue.Number(fn(a, b));
        }));

    private static void RegisterBitwise(Interpreter.Environment env, string name, Func<int, int> fn) =>
        env.Define(name, ScriptValue.Native(args =>
            ScriptValue.Number(fn(args.Length > 0 ? args[0].AsInt() : 0))));

    private static void RegisterBitwise2(Interpreter.Environment env, string name, Func<int, int, int> fn) =>
        RegisterBitwise(env, name, fn);
}
