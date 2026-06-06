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

    public void Tick() { }
}
