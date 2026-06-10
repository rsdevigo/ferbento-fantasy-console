using Ferbento.Core.Chips;
using Ferbento.Core.Math;
using Ferbento.Scripting.Debugger;
using Ferbento.Scripting.Interpreter;

namespace Ferbento.Scripting.Api;

public sealed class Pico8Context
{
    public ChipBus Bus { get; }
    public Pico8Random Random { get; } = new();
    public double BootTime { get; set; }
    public double FrameTime { get; set; }
    public int FrameCount { get; set; }
    public int CpuSteps { get; set; }
    public int MaxCpuSteps { get; set; } = 100_000;
    public int MaxTokens { get; set; } = 8192;
    public int TokenCount { get; set; }
    public int CallDepth { get; set; }
    public int MaxCallDepth { get; set; } = 512;
    public int MaxStringLength { get; set; } = 65536;
    public int MaxTableSize { get; set; } = 65536;
    public Action<string>? PrintHandler { get; set; }
    public Action<string>? ExtCommandHandler { get; set; }
    public BreakpointManager Breakpoints { get; } = new();
    public bool RebootRequested { get; set; }
    public bool FlipCalled { get; set; }
    public string? CurrentSource { get; set; }
    public string? WorkingDirectory { get; set; }

    private readonly double[] _cartData = new double[64];
    private string _cartDataId = "";

    public Pico8Context(ChipBus bus)
    {
        Bus = bus;
        MaxTokens = bus.Profile.Code.MaxTokens;
        MaxCpuSteps = MaxTokens * 12;
    }

    public void TickCpu() => CpuSteps++;

    public void CheckCpuBudget()
    {
        if (CpuSteps > MaxCpuSteps)
            throw new InvalidOperationException("CPU budget exceeded.");
    }

    public void ResetCpuBudget() => CpuSteps = 0;

    public void ValidateStringLength(string value)
    {
        if (value.Length > MaxStringLength)
            throw new InvalidOperationException($"String exceeds maximum length of {MaxStringLength}.");
    }

    public string TruncateString(string value)
    {
        if (value.Length <= MaxStringLength) return value;
        return value[..MaxStringLength];
    }

    public double Time() => FrameTime - BootTime;

    public double DGet(int idx) => idx >= 0 && idx < 64 ? _cartData[idx] : 0;

    public void DSet(int idx, double val)
    {
        if (idx >= 0 && idx < 64)
            _cartData[idx] = val;
    }

    public void CartData(string id)
    {
        _cartDataId = id;
    }
}
