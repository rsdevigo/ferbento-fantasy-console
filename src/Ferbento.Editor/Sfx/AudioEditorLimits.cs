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
}
