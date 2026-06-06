using Ferbento.Core.Audio;
using Ferbento.Profiles.Models;

namespace Ferbento.Core.Chips;

public sealed class SoundChip : IChip
{
    public string Name => "Sound";
    public AudioEngine Engine { get; private set; } = null!;

    private int _sfxSlots = 64;

    public void Configure(HardwareProfile profile)
    {
        _sfxSlots = profile.Audio.SfxSlots;
        Engine = new AudioEngine(profile.Audio.SampleRate, profile.Audio.Channels, profile.Audio.SfxSlots);
    }

    public void Initialize(ChipBus bus)
    {
        Engine ??= new AudioEngine(22050, 4, 64);
    }

    public void Reset() 
    {
        for (int i = 0; i < Engine.Channels.Count; i++)
            Engine.StopSfx(i);
        Engine.StopMusic();
    }

    public void Tick() => Engine.Update();

    public void Sfx(int n, int channel = -1, int offset = 0)
    {
        if (n < 0) return;
        Engine.PlaySfx(n, channel, offset);
    }

    public void SetSfxNote(int channel, int patternIndex, SfxNote note)
    {
        Engine.SetSfxNote(channel, patternIndex, note);
    }

    public SfxNote GetSfxNote(int channel, int patternIndex)
    {
        return Engine.GetSfxNote(channel, patternIndex);
    }

    public void SetSfxSpeed(int channel, int speed)
    {
        Engine.SetSfxSpeed(channel, speed);
    }

    public SfxNote[] GetSlot(int slotIndex) => Engine.GetSlot(slotIndex);
    public void SetSlot(int slotIndex, SfxNote[] notes) => Engine.SetSlot(slotIndex, notes);
    public int GetSlotSpeed(int slotIndex) => Engine.GetSlotSpeed(slotIndex);
    public void SetSlotSpeed(int slotIndex, int speed) => Engine.SetSlotSpeed(slotIndex, speed);
    public (int start, int end) GetSlotLoop(int slotIndex) => Engine.GetSlotLoop(slotIndex);
    public void SetSlotLoop(int slotIndex, int start, int end) => Engine.SetSlotLoop(slotIndex, start, end);
    public void LoadSlotToChannel(int slotIndex, int channel) => Engine.LoadSlotToChannel(slotIndex, channel);
    public int SfxSlotCount => _sfxSlots;
}
