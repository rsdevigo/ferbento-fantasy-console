using Ferbento.Core.Chips;
using Ferbento.Scripting.Api;
using Ferbento.Scripting.Interpreter;
using Ferbento.Scripting.Parser;
using Ferbento.Core.Exceptions;

namespace Ferbento.Scripting.Sandbox;

public sealed class ScriptSandbox
{
    private readonly ChipBus _bus;
    private readonly GDScriptParserService _parser = new();
    private readonly Pico8Context _context;
    private readonly ScriptInterpreter _interpreter;
    private readonly Interpreter.Environment _globals;

    public Pico8Context Context => _context;
    public ScriptInterpreter Interpreter => _interpreter;
    public Interpreter.Environment Globals => _globals;

    public ScriptSandbox(ChipBus bus)
    {
        _bus = bus;
        _context = new Pico8Context(bus);
        _interpreter = new ScriptInterpreter(_context);
        _globals = new Interpreter.Environment();
        _interpreter.SetGlobals(_globals);
        Pico8ApiRegistry.RegisterAll(_globals, _context, _interpreter);
    }

    public void LoadSource(string source)
    {
        var tokenCount = GdScriptTokenCounter.Count(source);
        if (tokenCount > _context.MaxTokens)
            throw new HardwareLimitException($"Script has {tokenCount} tokens, exceeds limit of {_context.MaxTokens}.");
        _context.TokenCount = tokenCount;
        _context.CurrentSource = source;

        var script = _parser.ParseScript(source);
        _interpreter.LoadScript(script);
    }

    public ScriptValue Call(string function, params ScriptValue[] args) =>
        _interpreter.CallFunction(function, args);

    public bool HasFunction(string name) => _interpreter.HasFunction(name);
}
