using Ferbento.Core.Memory;
using Ferbento.Profiles.Models;

namespace Ferbento.Core.Chips;

public sealed class MemoryChip : IChip
{
    public string Name => "Memory";
    public VirtualMemory Memory { get; private set; } = null!;

    private HardwareProfile _profile = HardwareProfile.Pico8();

    public void Configure(HardwareProfile profile) => _profile = profile;

    public void Initialize(ChipBus bus)
    {
        Memory = new VirtualMemory(_profile.Memory.Ram);
    }

    public void Reset() => Memory.Clear();

    /// <summary>
    /// Clears volatile runtime regions (draw state, screen buffer in RAM) but preserves
    /// cart asset data (GFX, map, SFX, palette user data).
    /// </summary>
    public void ResetRuntimeOnly()
    {
        var mem = Memory;
        if (MemoryMap.DrawStateStart < mem.Size)
            mem.Memset(MemoryMap.DrawStateStart, 0, mem.Size - MemoryMap.DrawStateStart);
    }

    public void Tick() { }
}
