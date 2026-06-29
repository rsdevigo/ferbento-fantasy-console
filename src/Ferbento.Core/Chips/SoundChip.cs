using Ferbento.Core.Audio;
using Ferbento.Core.Memory;
using Ferbento.Profiles.Models;

namespace Ferbento.Core.Chips;

public sealed class SoundChip : IChip
{
    public string Name => "Sound";
    public AudioEngine Engine { get; private set; } = null!;

    private ChipBus _bus = null!;
    private HardwareProfile _profile = HardwareProfile.Pico8();
    private AudioMemoryLayout _layout = AudioMemoryLayout.FromProfile(HardwareProfile.Pico8());

    public void Configure(HardwareProfile profile)
    {
        _profile = profile;
        _layout = AudioMemoryLayout.FromProfile(profile);
        Engine = new AudioEngine(profile);
    }

    public void Initialize(ChipBus bus)
    {
        _bus = bus;
        Engine ??= new AudioEngine(_profile);
        Engine.BindMemory(bus.Memory.Memory, _profile);
    }

    public void Reset()
    {
        for (int i = 0; i < Engine.Channels.Count; i++)
            Engine.StopSfxOnChannel(i);
        Engine.StopMusic();
    }

    public void Tick() => Engine.Update();

    public void Sfx(int n, int channel = -1, int offset = 0, int length = -1)
    {
        if (n == -1)
        {
            if (channel >= 0)
                Engine.StopSfxOnChannel(channel);
            else
                Engine.StopAllSfx();
            return;
        }

        if (n == -2)
        {
            if (channel >= 0)
                Engine.ReleaseLoopOnChannel(channel);
            return;
        }

        if (channel == -2)
        {
            Engine.StopSfxByIndex(n);
            return;
        }

        Engine.PlaySfx(n, channel, offset, length);
    }

    public void SetSfxNote(int channel, int patternIndex, SfxNote note) =>
        Engine.SetSfxNote(channel, patternIndex, note);

    public SfxNote GetSfxNote(int channel, int patternIndex) =>
        Engine.GetSfxNote(channel, patternIndex);

    public void SetSfxSpeed(int channel, int speed) =>
        Engine.SetSfxSpeed(channel, speed);

    public SfxNote[] GetSlot(int slotIndex) => Engine.GetSlot(slotIndex);

    public void SetSlot(int slotIndex, SfxNote[] notes) =>
        SetSlot(slotIndex, notes, Engine.GetSlotSpeed(slotIndex),
            Engine.GetSlotLoop(slotIndex).start, Engine.GetSlotLoop(slotIndex).end,
            Engine.GetSlotFilters(slotIndex));

    public void SetSlot(int slotIndex, SfxNote[] notes, int speed, int loopStart, int loopEnd, SfxFilters filters = SfxFilters.None) =>
        Engine.SetSlot(slotIndex, notes, speed, loopStart, loopEnd, filters);

    public int GetSlotSpeed(int slotIndex) => Engine.GetSlotSpeed(slotIndex);
    public void SetSlotSpeed(int slotIndex, int speed) => Engine.SetSlotSpeed(slotIndex, speed);
    public (int start, int end) GetSlotLoop(int slotIndex) => Engine.GetSlotLoop(slotIndex);
    public void SetSlotLoop(int slotIndex, int start, int end) => Engine.SetSlotLoop(slotIndex, start, end);
    public SfxFilters GetSlotFilters(int slotIndex) => Engine.GetSlotFilters(slotIndex);
    public void SetSlotFilters(int slotIndex, SfxFilters filters) => Engine.SetSlotFilters(slotIndex, filters);
    public void LoadSlotToChannel(int slotIndex, int channel) => Engine.LoadSlotToChannel(slotIndex, channel);
    public void PreviewNote(int channel, SfxNote note, int speed = 4) => Engine.PreviewNote(channel, note, speed);
    public void SetWaveformSlot(int slotIndex, bool isWaveform) => Engine.SetWaveformSlot(slotIndex, isWaveform);
    public bool GetWaveformSlot(int slotIndex) => Engine.GetWaveformSlot(slotIndex);
    public int SfxSlotCount => _layout.SfxSlots;
    public AudioMemoryLayout Layout => _layout;
}
