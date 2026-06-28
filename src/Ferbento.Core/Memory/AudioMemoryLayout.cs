using Ferbento.Core.Exceptions;
using Ferbento.Profiles.Models;

namespace Ferbento.Core.Memory;

/// <summary>
/// Computes virtual RAM regions for SFX and song data from a hardware profile.
/// </summary>
public sealed class AudioMemoryLayout
{
    public int SfxStart { get; }
    public int SfxEnd { get; }
    public int SfxSlotBytes { get; }
    public int SfxRegionSize { get; }

    public int SongStart { get; }
    public int SongEnd { get; }
    public int SongBytesPerPattern { get; }
    public int SongRegionSize { get; }

    public int NotesPerSfx { get; }
    public int SfxSlots { get; }
    public int MusicPatterns { get; }
    public int Channels { get; }

    public static AudioMemoryLayout FromProfile(HardwareProfile profile)
    {
        var audio = profile.Audio;
        int notesPerSfx = audio.NotesPerSfx;
        int sfxSlotBytes = notesPerSfx * 2 + MemoryMap.SfxHeaderBytes;
        int sfxRegionSize = sfxSlotBytes * audio.SfxSlots;

        int songBytesPerPattern = audio.Channels <= 4 ? 4 : audio.Channels + 1;
        int songRegionSize = songBytesPerPattern * audio.MusicPatterns;

        int songStart = MemoryMap.SongStart;
        int songEnd = songStart + songRegionSize - 1;

        bool pico8Canonical = audio.Channels == 4
                              && audio.SfxSlots == 64
                              && audio.MusicPatterns == 64
                              && notesPerSfx == MemoryMap.DefaultNotesPerSfx;

        int sfxStart = pico8Canonical
            ? MemoryMap.SfxStart
            : Align256(songStart + songRegionSize);

        int sfxEnd = sfxStart + sfxRegionSize - 1;

        if (!pico8Canonical && songEnd >= sfxStart)
        {
            throw new HardwareLimitException(
                $"Profile '{profile.Name}': song region ({songRegionSize} bytes) " +
                $"overlaps SFX region at 0x{sfxStart:X4}.");
        }

        if (pico8Canonical && songRegionSize > MemoryMap.SfxStart - MemoryMap.SongStart)
        {
            throw new HardwareLimitException(
                $"Profile '{profile.Name}': song region ({songRegionSize} bytes) " +
                $"exceeds Pico-8 song table size.");
        }

        if (sfxEnd >= MemoryMap.UserDataStart && profile.Memory.Ram <= MemoryMap.TotalSize)
        {
            throw new HardwareLimitException(
                $"Profile '{profile.Name}': audio SFX region ({sfxRegionSize} bytes) " +
                $"exceeds available RAM space before 0x{MemoryMap.UserDataStart:X4}.");
        }

        return new AudioMemoryLayout(
            sfxStart, sfxEnd, sfxSlotBytes, sfxRegionSize,
            songStart, songEnd, songBytesPerPattern, songRegionSize,
            notesPerSfx, audio.SfxSlots, audio.MusicPatterns, audio.Channels);
    }

    private static int Align256(int address) => (address + 0xFF) & ~0xFF;

    private AudioMemoryLayout(
        int sfxStart, int sfxEnd, int sfxSlotBytes, int sfxRegionSize,
        int songStart, int songEnd, int songBytesPerPattern, int songRegionSize,
        int notesPerSfx, int sfxSlots, int musicPatterns, int channels)
    {
        SfxStart = sfxStart;
        SfxEnd = sfxEnd;
        SfxSlotBytes = sfxSlotBytes;
        SfxRegionSize = sfxRegionSize;
        SongStart = songStart;
        SongEnd = songEnd;
        SongBytesPerPattern = songBytesPerPattern;
        SongRegionSize = songRegionSize;
        NotesPerSfx = notesPerSfx;
        SfxSlots = sfxSlots;
        MusicPatterns = musicPatterns;
        Channels = channels;
    }

    public int SfxSlotAddress(int slotIndex) => SfxStart + slotIndex * SfxSlotBytes;

    public int SongPatternAddress(int patternIndex) => SongStart + patternIndex * SongBytesPerPattern;

    public static int Pico8SfxSlotBytes(int notesPerSfx = MemoryMap.DefaultNotesPerSfx) =>
        notesPerSfx * 2 + MemoryMap.SfxHeaderBytes;
}
