namespace Ferbento.Scripting.Vm;

/// <summary>
/// A runtime error with PICO-8/Lua semantics (for example "attempt to compare number with string").
/// Messages follow the Lua wording so they match what the same code reports in PICO-8.
/// </summary>
public sealed class Pico8RuntimeException : Exception
{
    public Pico8RuntimeException(string message) : base(message) { }
}
