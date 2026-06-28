using Ferbento.Core.Memory;

namespace Ferbento.Core.Audio;

public enum SfxWaveform : byte
{
    Triangle = 0,
    Sawtooth = 1,
    Square = 2,
    Noise = 3,
    Pulse25 = 4,
    Pulse12 = 5,
    Triangle2 = 6,
    Noise2 = 7,
}

public enum SfxEffect : byte
{
    None = 0,
    Slide = 1,
    Vibrato = 2,
    Drop = 3,
    FadeIn = 4,
    FadeOut = 5,
    Arpeggio = 6,
    Arpeggio2 = 7,
}

[Flags]
public enum SfxFilters : byte
{
    None = 0,
    Noiz = 1 << 0,
    Buzz = 1 << 1,
    Detune1 = 1 << 2,
    Detune2 = 1 << 3,
    Reverb = 1 << 4,
    Dampen = 1 << 5,
}

public readonly struct SfxNote
{
    public int Pitch { get; }
    public int Note => Pitch;
    public int Instrument { get; }
    public int Volume { get; }
    public SfxEffect Effect { get; }
    public bool CustomInst { get; }
    public int EffectParam { get; }

    public SfxNote(
        int note = 0,
        int instrument = 0,
        int volume = 0,
        SfxEffect effect = SfxEffect.None,
        int effectParam = 0,
        bool customInst = false)
    {
        Pitch = note;
        Instrument = instrument;
        Volume = volume;
        Effect = effect;
        EffectParam = effectParam;
        CustomInst = customInst;
    }

    public bool IsEmpty =>
        Pitch == 0 && Instrument == 0 && Volume == 0 && Effect == SfxEffect.None && !CustomInst;

    public static SfxNote FromRaw(ushort raw)
    {
        int pitch = raw & 0x3F;
        int instrument = (raw >> 6) & 0x7;
        int volume = (raw >> 9) & 0x7;
        var effect = (SfxEffect)((raw >> 12) & 0x7);
        bool customInst = (raw & 0x8000) != 0;
        return new SfxNote(pitch, instrument, volume, effect, customInst: customInst);
    }

    public ushort ToRaw()
    {
        int raw = Pitch & 0x3F;
        raw |= (Instrument & 0x7) << 6;
        raw |= (Volume & 0x7) << 9;
        raw |= ((int)Effect & 0x7) << 12;
        if (CustomInst) raw |= 0x8000;
        return (ushort)raw;
    }
}

public readonly struct SongChannelEntry
{
    public int SfxIndex { get; }
    public bool Enabled { get; }

    public SongChannelEntry(int sfxIndex, bool enabled)
    {
        SfxIndex = sfxIndex;
        Enabled = enabled;
    }

    public static SongChannelEntry FromByte(byte value)
    {
        int sfx = value & 0x3F;
        bool enabled = (value & 0x40) != 0;
        return new SongChannelEntry(sfx, enabled);
    }

    public byte ToByte()
    {
        int value = SfxIndex & 0x3F;
        if (Enabled) value |= 0x40;
        return (byte)value;
    }
}

[Flags]
public enum SongPatternFlags : byte
{
    None = 0,
    LoopStart = 1 << 0,
    LoopBack = 1 << 1,
    Stop = 1 << 2,
}

public readonly struct SongPattern
{
    public SongChannelEntry[] Channels { get; }
    public SongPatternFlags Flags { get; }

    public SongPattern(SongChannelEntry[] channels, SongPatternFlags flags = SongPatternFlags.None)
    {
        Channels = channels;
        Flags = flags;
    }

    public bool HasEnabledChannels
    {
        get
        {
            foreach (var ch in Channels)
            {
                if (ch.Enabled && ch.SfxIndex >= 0)
                    return true;
            }
            return false;
        }
    }
}

public static class SfxCodec
{
    public static void WriteSlot(VirtualMemory mem, AudioMemoryLayout layout, int slotIndex,
        SfxNote[] notes, int speed, int loopStart, int loopEnd, SfxFilters filters)
    {
        if (slotIndex < 0 || slotIndex >= layout.SfxSlots) return;

        int addr = layout.SfxSlotAddress(slotIndex);
        int noteCount = System.Math.Min(notes.Length, layout.NotesPerSfx);

        for (int i = 0; i < layout.NotesPerSfx; i++)
        {
            ushort raw = i < noteCount ? notes[i].ToRaw() : (ushort)0;
            mem.Poke2(addr + i * 2, raw);
        }

        int header = addr + layout.NotesPerSfx * 2;
        mem.Poke(header, (byte)System.Math.Clamp(speed, 1, 255));
        mem.Poke(header + 1, (byte)System.Math.Clamp(loopStart, 0, layout.NotesPerSfx - 1));
        mem.Poke(header + 2, (byte)System.Math.Clamp(loopEnd, 0, layout.NotesPerSfx - 1));
        mem.Poke(header + 3, (byte)filters);
    }

    public static SfxNote[] ReadNotes(VirtualMemory mem, AudioMemoryLayout layout, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= layout.SfxSlots)
            return new SfxNote[layout.NotesPerSfx];

        int addr = layout.SfxSlotAddress(slotIndex);
        var notes = new SfxNote[layout.NotesPerSfx];
        for (int i = 0; i < layout.NotesPerSfx; i++)
            notes[i] = SfxNote.FromRaw(mem.Peek2(addr + i * 2));
        return notes;
    }

    public static (int speed, int loopStart, int loopEnd, SfxFilters filters) ReadHeader(
        VirtualMemory mem, AudioMemoryLayout layout, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= layout.SfxSlots)
            return (4, 0, layout.NotesPerSfx - 1, SfxFilters.None);

        int header = layout.SfxSlotAddress(slotIndex) + layout.NotesPerSfx * 2;
        return (
            mem.Peek(header),
            mem.Peek(header + 1),
            mem.Peek(header + 2),
            (SfxFilters)mem.Peek(header + 3));
    }
}

public static class SongCodec
{
    public static void WritePattern(VirtualMemory mem, AudioMemoryLayout layout, int patternIndex, SongPattern pattern)
    {
        if (patternIndex < 0 || patternIndex >= layout.MusicPatterns) return;

        int addr = layout.SongPatternAddress(patternIndex);
        int channelCount = System.Math.Min(pattern.Channels.Length, layout.Channels);

        for (int ch = 0; ch < channelCount; ch++)
        {
            byte value = pattern.Channels[ch].ToByte();
            if (!pattern.Channels[ch].Enabled)
            {
                if (ch == 0 && (pattern.Flags & SongPatternFlags.LoopStart) != 0) value |= 0x80;
                if (ch == 1 && (pattern.Flags & SongPatternFlags.LoopBack) != 0) value |= 0x80;
                if (ch == 2 && (pattern.Flags & SongPatternFlags.Stop) != 0) value |= 0x80;
            }
            mem.Poke(addr + ch, value);
        }

        for (int ch = channelCount; ch < layout.SongBytesPerPattern - 1; ch++)
            mem.Poke(addr + ch, 0);

        if (layout.SongBytesPerPattern > layout.Channels)
            mem.Poke(addr + layout.SongBytesPerPattern - 1, (byte)pattern.Flags);
    }

    public static SongPattern ReadPattern(VirtualMemory mem, AudioMemoryLayout layout, int patternIndex)
    {
        if (patternIndex < 0 || patternIndex >= layout.MusicPatterns)
            return new SongPattern(new SongChannelEntry[layout.Channels]);

        int addr = layout.SongPatternAddress(patternIndex);
        var channels = new SongChannelEntry[layout.Channels];
        var flags = SongPatternFlags.None;

        for (int ch = 0; ch < layout.Channels; ch++)
        {
            byte value = ch < layout.SongBytesPerPattern ? mem.Peek(addr + ch) : (byte)0;
            channels[ch] = SongChannelEntry.FromByte(value);

            if ((value & 0xC0) == 0x80)
            {
                if (ch == 0) flags |= SongPatternFlags.LoopStart;
                else if (ch == 1) flags |= SongPatternFlags.LoopBack;
                else if (ch == 2) flags |= SongPatternFlags.Stop;
            }
        }

        if (layout.SongBytesPerPattern > layout.Channels)
            flags |= (SongPatternFlags)mem.Peek(addr + layout.SongBytesPerPattern - 1);

        return new SongPattern(channels, flags);
    }
}
