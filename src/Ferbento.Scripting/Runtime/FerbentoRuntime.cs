using Ferbento.Core.Chips;
using Ferbento.Profiles.Models;
using Ferbento.Scripting.Sandbox;

namespace Ferbento.Scripting.Runtime;

public sealed class FerbentoRuntime
{
    public ChipBus Bus { get; }
    public ScriptSandbox Sandbox { get; }
    public GameLoop Loop { get; }

    public FerbentoRuntime(HardwareProfile? profile = null, string? source = null)
    {
        Bus = new ChipBus(profile);
        Sandbox = new ScriptSandbox(Bus);
        if (!string.IsNullOrWhiteSpace(source))
            Sandbox.LoadSource(source);
        Loop = new GameLoop(Bus, Sandbox);
    }

    public void LoadScript(string source)
    {
        Sandbox.LoadSource(source);
        Loop.Boot();
    }
}
