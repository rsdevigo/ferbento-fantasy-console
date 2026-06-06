using Ferbento.Scripting.Interpreter;

namespace Ferbento.Scripting.Api;

public static class Pico8ApiRegistry
{
    public static void RegisterAll(Interpreter.Environment env, Pico8Context ctx, ScriptInterpreter interpreter)
    {
        GraphicsApi.Register(env, ctx);
        AudioApi.Register(env, ctx);
        InputApi.Register(env, ctx);
        MathApi.Register(env, ctx);
        MemoryApi.Register(env, ctx);
        TableApi.Register(env, ctx);
        StringApi.Register(env, ctx);
        CartDataApi.Register(env, ctx);
        SystemApi.Register(env, ctx);
        CoroutineApi.Register(env, ctx, interpreter);
    }
}
