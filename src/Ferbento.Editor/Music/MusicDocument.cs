using Ferbento.Core.Audio;
using Ferbento.Core.Chips;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Music;

public sealed class MusicDocument
{
    public const int DefaultRowsPerPattern = 64;
    public const int DefaultSequenceLength = 32;

    private ChipBus? _bus;
    private HardwareProfile _profile = HardwareProfile.Pico8();

    public int PatternCount { get; private set; } = 64;
    public int ChannelCount { get; private set; } = 4;
    public int SfxSlotCount { get; private set; } = 64;
    public int RowsPerPattern { get; private set; } = DefaultRowsPerPattern;
    public int CellCount => RowsPerPattern * ChannelCount;

    public string SongName { get; set; } = "Untitled";
    public int[] Sequence { get; private set; } = new int[DefaultSequenceLength];
    public int SequenceLength { get; set; } = DefaultSequenceLength;

    public void SetProfile(HardwareProfile profile)
    {
        _profile = profile;
        PatternCount = Math.Max(1, profile.Audio.MusicPatterns);
        ChannelCount = Math.Max(1, profile.Audio.Channels);
        SfxSlotCount = Math.Max(1, profile.Audio.SfxSlots);
        RowsPerPattern = DefaultRowsPerPattern;
        if (Sequence.Length != SequenceLength)
            Sequence = ResizeSequence(Sequence, SequenceLength);
    }

    public void SetChipBus(ChipBus bus)
    {
        _bus = bus;
        SetProfile(bus.Profile);
        bus.Sound.Engine.ConfigureMusic(PatternCount, RowsPerPattern);
    }

    public int[] CreateEmptyPattern()
    {
        int size = CellCount;
        return new int[size];
    }

    public int GetCellIndex(int row, int channel) => row * ChannelCount + channel;

    public int GetCell(int patternIndex, int row, int channel)
    {
        var data = GetPattern(patternIndex);
        int idx = GetCellIndex(row, channel);
        return idx >= 0 && idx < data.Length ? data[idx] : 0;
    }

    public void SetCell(int patternIndex, int row, int channel, int packed)
    {
        if (_bus == null) return;
        var data = GetPatternCopy(patternIndex);
        int idx = GetCellIndex(row, channel);
        if (idx < 0 || idx >= data.Length) return;
        data[idx] = packed;
        _bus.Music.SetMusicPattern(patternIndex, data);
    }

    public int[] GetPattern(int patternIndex)
    {
        if (_bus == null) return CreateEmptyPattern();
        var raw = _bus.Music.GetMusicPattern(patternIndex);
        if (raw.Length == CellCount) return raw;
        var normalized = CreateEmptyPattern();
        Array.Copy(raw, normalized, Math.Min(raw.Length, normalized.Length));
        return normalized;
    }

    public int[] GetPatternCopy(int patternIndex)
    {
        var src = GetPattern(patternIndex);
        var copy = new int[src.Length];
        Array.Copy(src, copy, src.Length);
        return copy;
    }

    public void SetPattern(int patternIndex, int[] data)
    {
        if (_bus == null) return;
        var normalized = CreateEmptyPattern();
        Array.Copy(data, normalized, Math.Min(data.Length, normalized.Length));
        _bus.Music.SetMusicPattern(patternIndex, normalized);
    }

    public (int note, int instrument, int volume, SfxEffect effect, int effectParam) GetCellDecoded(
        int patternIndex, int row, int channel) =>
        MusicNoteCodec.Unpack(GetCell(patternIndex, row, channel));

    public void SetCellDecoded(int patternIndex, int row, int channel,
        int note, int instrument, int volume, SfxEffect effect, int effectParam)
    {
        if (note <= 0)
            SetCell(patternIndex, row, channel, 0);
        else
            SetCell(patternIndex, row, channel,
                MusicNoteCodec.Pack(note, ClampInstrument(instrument), volume, effect, effectParam));
    }

    public int ClampInstrument(int instrument) =>
        Math.Clamp(instrument, 0, Math.Max(0, SfxSlotCount - 1));

    public int ClampPattern(int pattern) => Math.Clamp(pattern, 0, PatternCount - 1);
    public int ClampChannel(int channel) => Math.Clamp(channel, 0, ChannelCount - 1);
    public int ClampRow(int row) => Math.Clamp(row, 0, RowsPerPattern - 1);

    public MusicProjectData ExportProjectData(int currentPattern, int length, int tempo,
        int autoIncrement, int loopStart, int loopEnd)
    {
        var patterns = new string[PatternCount];
        for (int i = 0; i < PatternCount; i++)
        {
            var bytes = new byte[CellCount * sizeof(int)];
            Buffer.BlockCopy(GetPattern(i), 0, bytes, 0, bytes.Length);
            patterns[i] = Convert.ToBase64String(bytes);
        }

        return new MusicProjectData
        {
            SongName = SongName,
            CurrentPattern = currentPattern,
            Length = length,
            Tempo = tempo,
            AutoIncrement = autoIncrement,
            LoopStart = loopStart,
            LoopEnd = loopEnd,
            Sequence = (int[])Sequence.Clone(),
            PatternDataBase64 = patterns,
        };
    }

    public bool ImportProjectData(MusicProjectData data, out int currentPattern,
        out int length, out int tempo, out int autoIncrement, out int loopStart, out int loopEnd)
    {
        SongName = data.SongName ?? "Untitled";
        currentPattern = ClampPattern(data.CurrentPattern);
        length = Math.Clamp(data.Length, 1, RowsPerPattern);
        tempo = Math.Max(1, data.Tempo);
        autoIncrement = Math.Max(0, data.AutoIncrement);
        loopStart = ClampRow(data.LoopStart);
        loopEnd = ClampRow(data.LoopEnd);
        if (loopEnd < loopStart) loopEnd = loopStart;

        if (data.Sequence is { Length: > 0 })
        {
            SequenceLength = data.Sequence.Length;
            Sequence = (int[])data.Sequence.Clone();
        }

        if (data.PatternDataBase64 == null || _bus == null)
            return false;

        for (int i = 0; i < Math.Min(data.PatternDataBase64.Length, PatternCount); i++)
        {
            if (string.IsNullOrEmpty(data.PatternDataBase64[i])) continue;
            var bytes = Convert.FromBase64String(data.PatternDataBase64[i]);
            var ints = new int[bytes.Length / sizeof(int)];
            Buffer.BlockCopy(bytes, 0, ints, 0, bytes.Length);
            SetPattern(i, ints);
        }

        return true;
    }

    private static int[] ResizeSequence(int[] current, int length)
    {
        var next = new int[length];
        Array.Copy(current, next, Math.Min(current.Length, length));
        return next;
    }
}
