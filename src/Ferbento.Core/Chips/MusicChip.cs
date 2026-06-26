using Ferbento.Core.Audio;
using Ferbento.Profiles.Models;

namespace Ferbento.Core.Chips;

public sealed class MusicChip : IChip
{
    public string Name => "Music";

    private ChipBus _bus = null!;
    private int _patternCount = 64;

    public void Configure(HardwareProfile profile) => _patternCount = profile.Audio.MusicPatterns;

    public void Initialize(ChipBus bus) => _bus = bus;

    public void Reset() => _bus.Sound.Engine.StopMusic();

    public void Tick() { }

    public void Music(int pattern = -1, int fade = 0, int channelMask = 15)
    {
        if (pattern < 0)
            _bus.Sound.Engine.StopMusic();
        else
            _bus.Sound.Engine.PlayMusic(pattern, fade, channelMask);
    }

    public void SetMusicPattern(int patternIndex, int[] patternData)
    {
        _bus.Sound.Engine.SetMusicPattern(patternIndex, patternData);
    }

    public int[] GetMusicPattern(int patternIndex)
    {
        return _bus.Sound.Engine.GetMusicPattern(patternIndex);
    }

    public void SetMusicLoop(int start, int end)
    {
        _bus.Sound.Engine.SetMusicLoop(start, end);
    }

    public void SetMusicSpeed(int speed)
    {
        _bus.Sound.Engine.SetMusicSpeed(speed);
    }

    public void SetMusicRepeat(bool repeat)
    {
        _bus.Sound.Engine.SetMusicRepeat(repeat);
    }

    public int PatternCount => _patternCount;
    public int RowsPerPattern => _bus.Sound.Engine.MusicRowsPerPattern;
    public int TrackCount => _bus.Sound.Engine.MusicTrackCount;
    public int PlayheadRow => _bus.Sound.Engine.MusicPlayheadRow;
    public bool IsPlaying => _bus.Sound.Engine.IsMusicPlaying;

    public int ChannelMask(int channelCount)
    {
        int mask = 0;
        for (int i = 0; i < channelCount; i++)
            mask |= 1 << i;
        return mask;
    }
}
