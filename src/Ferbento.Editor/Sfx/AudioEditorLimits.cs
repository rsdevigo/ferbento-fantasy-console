using Ferbento.Core.Memory;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Sfx;

public sealed class AudioEditorLimits
{
    public const int MaxInstrument = 7;
    public const int MaxVolume = 7;

    public int NotesPerSfx { get; }
    public int SfxSlotCount { get; }
    public int ChannelCount { get; }

    public AudioEditorLimits(int notesPerSfx, int sfxSlotCount, int channelCount)
    {
        NotesPerSfx = notesPerSfx;
        SfxSlotCount = sfxSlotCount;
        ChannelCount = channelCount;
    }

    public static AudioEditorLimits FromProfile(HardwareProfile profile)
    {
        var layout = AudioMemoryLayout.FromProfile(profile);
        return new AudioEditorLimits(layout.NotesPerSfx, layout.SfxSlots, layout.Channels);
    }

    public static void ApplySfxPitchRange(HardwareProfile profile)
    {
        int minOct = Math.Clamp(profile.Audio.SfxOctaveMin, 0, 5);
        int maxOct = Math.Clamp(profile.Audio.SfxOctaveMax, 1, 5);
        if (minOct >= maxOct)
            maxOct = Math.Min(5, minOct + 1);
        SfxPitch.SetOctaveRange(minOct, maxOct);
    }
}
