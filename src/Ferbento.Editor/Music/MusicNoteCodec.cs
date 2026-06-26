using Ferbento.Core.Audio;

namespace Ferbento.Editor.Music;

public static class MusicNoteCodec
{
    public static int Pack(int note, int instrument, int volume, SfxEffect effect, int effectParam) =>
        (note & 0xFF) |
        ((instrument & 0xF) << 8) |
        ((volume & 0x7) << 12) |
        (((int)effect & 0x7) << 15) |
        ((effectParam & 0x3FF) << 18);

    public static (int note, int instrument, int volume, SfxEffect effect, int effectParam) Unpack(int data)
    {
        int note = data & 0xFF;
        int instrument = (data >> 8) & 0xF;
        int volume = (data >> 12) & 0x7;
        var effect = (SfxEffect)((data >> 15) & 0x7);
        int effectParam = (data >> 18) & 0x3FF;
        return (note, instrument, volume, effect, effectParam);
    }

    public static string NoteToPitchName(int midiNote)
    {
        if (midiNote <= 0) return "---";
        string[] notes = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        int octave = midiNote / 12 - 1;
        int n = midiNote % 12;
        return $"{notes[n]}{octave}";
    }

    public static string NoteLetter(int midiNote)
    {
        if (midiNote <= 0) return "C";
        string[] notes = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        return notes[midiNote % 12];
    }

    public static int OctaveFromNote(int midiNote) =>
        midiNote <= 0 ? 1 : midiNote / 12 - 1;

    public static int CombineNoteOctave(string letter, int octave)
    {
        string[] notes = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        int idx = Array.IndexOf(notes, letter);
        if (idx < 0) idx = 0;
        return (octave + 1) * 12 + idx;
    }
}
