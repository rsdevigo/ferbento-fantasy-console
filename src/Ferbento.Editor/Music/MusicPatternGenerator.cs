using Ferbento.Core.Audio;

namespace Ferbento.Editor.Music;

/// <summary>
/// Generates structured tracker patterns (drums, bass, melody) on a minor pentatonic scale.
/// </summary>
public static class MusicPatternGenerator
{
    // C minor pentatonic around octave 3-4: C, Eb, F, G, Bb
    private static readonly int[] Pentatonic = [48, 51, 53, 55, 58, 60, 63, 65, 67, 70];

    public static int[] Generate(int length, int channelCount, int defaultInstrument, int defaultVolume, Random? rng = null)
    {
        rng ??= Random.Shared;
        length = Math.Clamp(length, 1, 64);
        channelCount = Math.Max(1, channelCount);
        var data = new int[length * channelCount];

        int drumCh = 0;
        int bassCh = channelCount >= 2 ? 1 : 0;
        int melodyCh = channelCount >= 3 ? 2 : Math.Min(1, channelCount - 1);

        int root = Pentatonic[rng.Next(Pentatonic.Length / 2)];
        int bassNote = root - 12;
        if (bassNote < 36) bassNote = root;

        int step = Math.Max(1, length / 8);
        int vol = Math.Clamp(defaultVolume, 4, 7);

        for (int row = 0; row < length; row++)
        {
            bool beat = row % step == 0;
            bool offbeat = row % step == step / 2;

            // drums
            if (beat && rng.Next(3) != 0)
            {
                int inst = 3; // noise
                int note = 48 + rng.Next(4);
                SetCell(data, row, drumCh, channelCount, note, inst, vol, rng);
            }
            else if (offbeat && channelCount > 1 && rng.Next(4) == 0)
            {
                SetCell(data, row, drumCh, channelCount, 50, 2, vol - 1, rng);
            }

            // bass, sometimes a fifth up
            if (beat && bassCh != drumCh && rng.Next(3) != 0)
            {
                int note = rng.Next(5) == 0 ? bassNote + 7 : bassNote;
                SetCell(data, row, bassCh, channelCount, note, 2, vol, rng);
            }

            // melody
            if (melodyCh != drumCh && melodyCh != bassCh && rng.Next(4) == 0)
            {
                int note = Pentatonic[rng.Next(Pentatonic.Length)];
                int inst = defaultInstrument <= 7 ? defaultInstrument : rng.Next(0, 4);
                SetCell(data, row, melodyCh, channelCount, note, inst, vol, rng);
            }

            // remaining channels
            for (int ch = 3; ch < channelCount; ch++)
            {
                if (rng.Next(6) != 0) continue;
                int note = Pentatonic[rng.Next(Pentatonic.Length)];
                SetCell(data, row, ch, channelCount, note, rng.Next(0, 4), vol - 1, rng);
            }
        }

        return data;
    }

    private static void SetCell(int[] data, int row, int ch, int channelCount, int note, int inst, int volume, Random rng)
    {
        int idx = row * channelCount + ch;
        if (idx < 0 || idx >= data.Length) return;
        volume = Math.Clamp(volume + rng.Next(-1, 1), 1, 7);
        inst = Math.Clamp(inst, 0, 7);
        data[idx] = MusicNoteCodec.Pack(note, inst, volume, SfxEffect.None, 0);
    }
}
