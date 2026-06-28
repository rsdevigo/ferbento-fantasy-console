using System.Collections.Concurrent;
using Ferbento.Core.Memory;
using Ferbento.Profiles.Models;
using NAudio.Wave;

namespace Ferbento.Core.Audio;

public sealed class AudioEngine : IDisposable, ISfxInstrumentSource
{
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly int _samplesPerTick;
    private readonly SfxChannel[] _sfxChannels;
    private readonly WaveOutEvent? _waveOut;
    private readonly BufferedWaveProvider _mixer;
    private readonly object _lock = new();

    private VirtualMemory? _memory;
    private AudioMemoryLayout _layout;
    private readonly bool[] _waveformSlots = new bool[8];

    private readonly int[][]? _legacyMusicPatterns;
    private readonly int _legacyRowsPerPattern = 64;

    private int _currentSongPattern = -1;
    private int _channelMask;
    private bool _musicPlaying;
    private int _totalPatternsPlayed;
    private int _ticksOnPattern;
    private int _musicFadeRemaining;
    private double _musicFadeGain = 1.0;
    private double _musicFadeStep;

    private readonly int[] _channelSfxSlot;
    private readonly bool[] _channelReservedForMusic;
    private readonly bool[] _channelPatternFinished;

    private readonly ConcurrentQueue<Action> _commandQueue = new();
    private Thread? _pumpThread;
    private volatile bool _pumpRunning;

    private const double TargetBufferSeconds = 0.12;
    private const int MaxSamplesPerPump = 4096;

    public IReadOnlyList<SfxChannel> Channels => _sfxChannels;
    public int SampleRate => _sampleRate;
    public AudioMemoryLayout Layout => _layout;
    public VirtualMemory? Memory => _memory;
    public bool IsMusicPlaying => _musicPlaying;
    public int SfxSlotCount => _layout.SfxSlots;
    public int MusicPatternCount => _layout.MusicPatterns;
    public int MusicTrackCount => _channels;

    public int MusicPlayheadRow => _musicPlaying ? _ticksOnPattern : -1;
    public int MusicCurrentPattern => _currentSongPattern;
    public int MusicRowsPerPattern => _legacyRowsPerPattern;

    public AudioEngine(HardwareProfile? profile = null, VirtualMemory? memory = null, bool enablePlayback = true)
    {
        profile ??= HardwareProfile.Pico8();
        _layout = AudioMemoryLayout.FromProfile(profile);
        _memory = memory;

        _sampleRate = profile.Audio.SampleRate;
        _channels = profile.Audio.Channels;
        _samplesPerTick = System.Math.Max(1, _sampleRate / 60);
        _sfxChannels = new SfxChannel[_channels];
        _channelSfxSlot = new int[_channels];
        _channelReservedForMusic = new bool[_channels];
        _channelPatternFinished = new bool[_channels];

        for (int i = 0; i < _channels; i++)
        {
            _sfxChannels[i] = new SfxChannel(i, _sampleRate, _layout.NotesPerSfx);
            _sfxChannels[i].SetInstrumentSource(this);
            _channelSfxSlot[i] = -1;
        }

        _legacyMusicPatterns = new int[_layout.MusicPatterns][];

        _mixer = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, 1))
        {
            BufferDuration = TimeSpan.FromMilliseconds(300),
            DiscardOnBufferOverflow = false
        };

        if (!enablePlayback) return;

        try
        {
            _waveOut = new WaveOutEvent
            {
                DesiredLatency = 100,
                NumberOfBuffers = 3
            };
            _waveOut.Init(_mixer);
            _waveOut.Play();
            StartPumpThread();
        }
        catch
        {
            _waveOut = null;
        }
    }

    public AudioEngine(int sampleRate, int channels, int sfxSlots, bool enablePlayback = true)
        : this(CreateProfile(sampleRate, channels, sfxSlots), null, enablePlayback)
    {
    }

    private static HardwareProfile CreateProfile(int sampleRate, int channels, int sfxSlots)
    {
        var p = HardwareProfile.Pico8();
        p.Audio.SampleRate = sampleRate;
        p.Audio.Channels = channels;
        p.Audio.SfxSlots = sfxSlots;
        return p;
    }

    public void BindMemory(VirtualMemory memory, HardwareProfile profile)
    {
        _memory = memory;
        _layout = AudioMemoryLayout.FromProfile(profile);
    }

    public void SetWaveformSlot(int slotIndex, bool isWaveform)
    {
        if (slotIndex is >= 0 and < 8)
            _waveformSlots[slotIndex] = isWaveform;
    }

    public void QueueCommand(Action cmd) => _commandQueue.Enqueue(cmd);

    public void ProcessCommands()
    {
        while (_commandQueue.TryDequeue(out var cmd))
            cmd();
    }

    public void Update()
    {
        lock (_lock)
        {
            ProcessCommands();
            FlushChannelCommands();
            if (_pumpThread == null)
                FillBufferToTarget();
        }
    }

    private void StartPumpThread()
    {
        _pumpRunning = true;
        _pumpThread = new Thread(PumpLoop)
        {
            IsBackground = true,
            Name = "FerbentoAudioPump"
        };
        _pumpThread.Start();
    }

    private void PumpLoop()
    {
        while (_pumpRunning)
        {
            lock (_lock)
                FillBufferToTarget();
            Thread.Sleep(3);
        }
    }

    private void FillBufferToTarget()
    {
        if (_waveOut == null) return;

        ProcessCommands();
        FlushChannelCommands();

        double bufferedSeconds = _mixer.BufferedBytes / (4.0 * _sampleRate);
        int samplesNeeded = (int)((TargetBufferSeconds - bufferedSeconds) * _sampleRate);
        if (samplesNeeded <= 0) return;

        samplesNeeded = System.Math.Min(samplesNeeded, MaxSamplesPerPump);
        GenerateSamples(samplesNeeded);
    }

    private void FlushChannelCommands()
    {
        for (int ch = 0; ch < _channels; ch++)
            _sfxChannels[ch].FlushCommands();
    }

    private void GenerateSamples(int count)
    {
        var mixed = new float[count];

        for (int i = 0; i < count; i++)
        {
            if (_musicPlaying)
                UpdateMusicTick();

            float sample = 0;
            for (int ch = 0; ch < _channels; ch++)
                sample += _sfxChannels[ch].NextSample();

            if (_musicFadeRemaining > 0)
            {
                _musicFadeGain -= _musicFadeStep;
                _musicFadeRemaining--;
                sample *= (float)System.Math.Max(0, _musicFadeGain);
                if (_musicFadeRemaining == 0 && _musicFadeGain <= 0)
                    StopMusicImmediate();
            }

            mixed[i] = System.Math.Clamp(sample, -1f, 1f);
        }

        var byteBuffer = new byte[mixed.Length * sizeof(float)];
        Buffer.BlockCopy(mixed, 0, byteBuffer, 0, byteBuffer.Length);
        _mixer.AddSamples(byteBuffer, 0, byteBuffer.Length);
    }

    private void UpdateMusicTick()
    {
        _ticksOnPattern++;

        bool anyPlaying = false;
        int leftmostFinished = -1;

        for (int ch = 0; ch < _channels; ch++)
        {
            if (!_channelReservedForMusic[ch]) continue;

            if (_sfxChannels[ch].IsPlaying)
            {
                anyPlaying = true;
            }
            else if (_channelSfxSlot[ch] >= 0 && !_channelPatternFinished[ch])
            {
                _channelPatternFinished[ch] = true;
                if (leftmostFinished < 0)
                    leftmostFinished = ch;
            }
        }

        if (anyPlaying) return;

        if (leftmostFinished >= 0 || AllChannelsIdle())
            OnPatternEnd();
    }

    private bool AllChannelsIdle()
    {
        for (int i = 0; i < _channels; i++)
        {
            if (_channelReservedForMusic[i] && _channelSfxSlot[i] >= 0)
                return false;
        }
        return true;
    }

    private void OnPatternEnd()
    {
        if (_currentSongPattern < 0) return;

        var pattern = ReadSongPattern(_currentSongPattern);

        if ((pattern.Flags & SongPatternFlags.Stop) != 0)
        {
            StopMusic();
            return;
        }

        if ((pattern.Flags & SongPatternFlags.LoopBack) != 0)
        {
            int loopTarget = FindLoopStart(_currentSongPattern);
            if (loopTarget >= 0)
            {
                PlaySongPattern(loopTarget);
                return;
            }
        }

        int next = _currentSongPattern + 1;
        if (next >= _layout.MusicPatterns)
        {
            StopMusic();
            return;
        }

        var nextPattern = ReadSongPattern(next);
        if (!nextPattern.HasEnabledChannels)
        {
            StopMusic();
            return;
        }

        PlaySongPattern(next);
    }

    private int FindLoopStart(int fromPattern)
    {
        for (int i = fromPattern; i >= 0; i--)
        {
            var p = ReadSongPattern(i);
            if ((p.Flags & SongPatternFlags.LoopStart) != 0)
                return i;
        }
        return -1;
    }

    private void PlaySongPattern(int patternIndex)
    {
        _currentSongPattern = patternIndex;
        _totalPatternsPlayed++;
        _ticksOnPattern = 0;

        for (int ch = 0; ch < _channels; ch++)
        {
            _channelPatternFinished[ch] = false;
            _channelSfxSlot[ch] = -1;

            if (!_channelReservedForMusic[ch])
                continue;

            var pattern = ReadSongPattern(patternIndex);
            if (ch >= pattern.Channels.Length) continue;

            var entry = pattern.Channels[ch];
            if (!entry.Enabled || entry.SfxIndex < 0)
            {
                _sfxChannels[ch].StopImmediate();
                continue;
            }

            LoadSlotToChannel(entry.SfxIndex, ch, usePico8Pitch: true);
            _channelSfxSlot[ch] = entry.SfxIndex;
            _sfxChannels[ch].Play(0, entry.SfxIndex);
        }

        FlushChannelCommands();
    }

    public void PlaySfx(int sfxIndex, int channel = -1, int offset = 0, int length = -1)
    {
        if (sfxIndex < 0) return;
        if (sfxIndex >= _layout.SfxSlots) return;

        if (channel >= 0 && channel < _channels)
        {
            if (_channelReservedForMusic[channel] && _musicPlaying) return;
            LoadSlotToChannel(sfxIndex, channel, usePico8Pitch: true);
            _sfxChannels[channel].Play(offset, sfxIndex);
            return;
        }

        for (int i = 0; i < _channels; i++)
        {
            if (_channelReservedForMusic[i] && _musicPlaying) continue;
            if (!_sfxChannels[i].IsPlaying)
            {
                LoadSlotToChannel(sfxIndex, i, usePico8Pitch: true);
                _sfxChannels[i].Play(offset, sfxIndex);
                break;
            }
        }
    }

    public void StopSfxOnChannel(int channel)
    {
        if (channel >= 0 && channel < _channels)
            _sfxChannels[channel].StopImmediate();
    }

    public void StopSfx(int channel) => StopSfxOnChannel(channel);

    public void StopSfxByIndex(int sfxIndex)
    {
        for (int i = 0; i < _channels; i++)
        {
            if (_sfxChannels[i].PlayingSfxIndex == sfxIndex)
                _sfxChannels[i].StopImmediate();
        }
    }

    public void ReleaseLoopOnChannel(int channel)
    {
        if (channel >= 0 && channel < _channels)
            _sfxChannels[channel].ReleaseLoop();
    }

    public void StopAllSfx()
    {
        for (int i = 0; i < _channels; i++)
            _sfxChannels[i].StopImmediate();
    }

    public int FindFreeChannel(bool respectMusicMask = true)
    {
        for (int i = 0; i < _channels; i++)
        {
            if (respectMusicMask && _channelReservedForMusic[i] && _musicPlaying) continue;
            if (!_sfxChannels[i].IsPlaying)
                return i;
        }
        return -1;
    }

    public void LoadSlotToChannel(int slotIndex, int channel, bool usePico8Pitch = false)
    {
        if (slotIndex < 0 || slotIndex >= _layout.SfxSlots) return;
        if (channel < 0 || channel >= _channels) return;

        var notes = ReadSlotNotes(slotIndex);
        var (speed, loopStart, loopEnd, filters) = ReadSlotHeader(slotIndex);

        for (int i = 0; i < _layout.NotesPerSfx; i++)
            _sfxChannels[channel].SetNote(i, i < notes.Length ? notes[i] : new SfxNote());
        _sfxChannels[channel].SetSpeed(speed);
        _sfxChannels[channel].SetLoop(loopStart, loopEnd);
        _sfxChannels[channel].SetFilters(filters);
        _sfxChannels[channel].SetUsePico8Pitch(usePico8Pitch);
    }

    public SfxNote[] GetSlot(int slotIndex) => ReadSlotNotes(slotIndex);

    public void SetSlot(int slotIndex, SfxNote[] notes, int speed = 4, int loopStart = 0, int loopEnd = -1, SfxFilters filters = SfxFilters.None)
    {
        if (slotIndex < 0 || slotIndex >= _layout.SfxSlots) return;
        if (loopEnd < 0) loopEnd = _layout.NotesPerSfx - 1;

        if (_memory != null)
        {
            SfxCodec.WriteSlot(_memory, _layout, slotIndex, notes, speed, loopStart, loopEnd, filters);
            return;
        }

        WriteSlotCache(slotIndex, notes, speed, loopStart, loopEnd, filters);
    }

    private readonly Dictionary<int, CachedSlot> _slotCache = new();

    private sealed class CachedSlot
    {
        public SfxNote[] Notes = [];
        public int Speed = 4;
        public int LoopStart;
        public int LoopEnd = 31;
        public SfxFilters Filters;
    }

    private void WriteSlotCache(int slotIndex, SfxNote[] notes, int speed, int loopStart, int loopEnd, SfxFilters filters)
    {
        if (!_slotCache.TryGetValue(slotIndex, out var cached))
        {
            cached = new CachedSlot { Notes = new SfxNote[_layout.NotesPerSfx], LoopEnd = _layout.NotesPerSfx - 1 };
            _slotCache[slotIndex] = cached;
        }

        for (int i = 0; i < _layout.NotesPerSfx; i++)
            cached.Notes[i] = i < notes.Length ? notes[i] : new SfxNote();
        cached.Speed = speed;
        cached.LoopStart = loopStart;
        cached.LoopEnd = loopEnd;
        cached.Filters = filters;
    }

    private SfxNote[] ReadSlotNotes(int slotIndex)
    {
        if (_memory != null)
            return SfxCodec.ReadNotes(_memory, _layout, slotIndex);

        if (_slotCache.TryGetValue(slotIndex, out var cached))
        {
            var copy = new SfxNote[_layout.NotesPerSfx];
            Array.Copy(cached.Notes, copy, _layout.NotesPerSfx);
            return copy;
        }

        return new SfxNote[_layout.NotesPerSfx];
    }

    private (int speed, int loopStart, int loopEnd, SfxFilters filters) ReadSlotHeader(int slotIndex)
    {
        if (_memory != null)
            return SfxCodec.ReadHeader(_memory, _layout, slotIndex);

        if (_slotCache.TryGetValue(slotIndex, out var cached))
            return (cached.Speed, cached.LoopStart, cached.LoopEnd, cached.Filters);

        return (4, 0, _layout.NotesPerSfx - 1, SfxFilters.None);
    }

    public int GetSlotSpeed(int slotIndex) => ReadSlotHeader(slotIndex).speed;

    public void SetSlotSpeed(int slotIndex, int speed)
    {
        var notes = ReadSlotNotes(slotIndex);
        var (_, ls, le, fl) = ReadSlotHeader(slotIndex);
        SetSlot(slotIndex, notes, speed, ls, le, fl);
    }

    public (int start, int end) GetSlotLoop(int slotIndex)
    {
        var h = ReadSlotHeader(slotIndex);
        return (h.loopStart, h.loopEnd);
    }

    public void SetSlotLoop(int slotIndex, int start, int end)
    {
        var notes = ReadSlotNotes(slotIndex);
        var (sp, _, _, fl) = ReadSlotHeader(slotIndex);
        SetSlot(slotIndex, notes, sp, start, end, fl);
    }

    public SfxFilters GetSlotFilters(int slotIndex) => ReadSlotHeader(slotIndex).filters;

    public void SetSlotFilters(int slotIndex, SfxFilters filters)
    {
        var notes = ReadSlotNotes(slotIndex);
        var (sp, ls, le, _) = ReadSlotHeader(slotIndex);
        SetSlot(slotIndex, notes, sp, ls, le, filters);
    }

    public void SetSfxNote(int channel, int patternIndex, SfxNote note)
    {
        if (channel >= 0 && channel < _channels && patternIndex >= 0 && patternIndex < _layout.NotesPerSfx)
            _sfxChannels[channel].SetNote(patternIndex, note);
    }

    public SfxNote GetSfxNote(int channel, int patternIndex)
    {
        if (channel >= 0 && channel < _channels && patternIndex >= 0 && patternIndex < _layout.NotesPerSfx)
            return _sfxChannels[channel].GetNote(patternIndex);
        return new SfxNote();
    }

    public void SetSfxSpeed(int channel, int speed)
    {
        if (channel >= 0 && channel < _channels)
            _sfxChannels[channel].SetSpeed(speed);
    }

    public void SetSfxLoop(int channel, int start, int end)
    {
        if (channel >= 0 && channel < _channels)
            _sfxChannels[channel].SetLoop(start, end);
    }

    public void PreviewNote(int channel, SfxNote note, int speed = 4)
    {
        if (channel < 0 || channel >= _channels) return;
        _sfxChannels[channel].SetNote(0, note);
        _sfxChannels[channel].SetSpeed(speed);
        _sfxChannels[channel].SetUsePico8Pitch(false);
        _sfxChannels[channel].Play(0);
    }

    public void PlayMusic(int patternIndex, int fadeMs = 0, int channelMask = -1)
    {
        void start()
        {
            if (channelMask < 0)
            {
                channelMask = 0;
                for (int i = 0; i < _channels; i++)
                    channelMask |= 1 << i;
            }

            _channelMask = channelMask;
            for (int i = 0; i < _channels; i++)
                _channelReservedForMusic[i] = (channelMask & (1 << i)) != 0;

            _musicPlaying = true;
            _totalPatternsPlayed = 0;
            _musicFadeRemaining = 0;
            _musicFadeGain = 1.0;

            PlaySongPattern(patternIndex);
        }

        if (_waveOut == null)
            start();
        else
            _commandQueue.Enqueue(start);
    }

    public void StopMusic(int fadeMs = 0)
    {
        if (fadeMs > 0 && _musicPlaying)
        {
            _musicFadeRemaining = fadeMs * _sampleRate / 1000;
            _musicFadeStep = 1.0 / System.Math.Max(1, _musicFadeRemaining);
            return;
        }

        StopMusicImmediate();
    }

    private void StopMusicImmediate()
    {
        lock (_lock)
        {
            _musicPlaying = false;
            _currentSongPattern = -1;
            _ticksOnPattern = 0;
            _musicFadeRemaining = 0;
            _musicFadeGain = 1.0;

            for (int i = 0; i < _channels; i++)
            {
                _channelSfxSlot[i] = -1;
                _channelPatternFinished[i] = false;
                if (_channelReservedForMusic[i])
                    _sfxChannels[i].StopImmediate();
            }

            _mixer.ClearBuffer();
        }
    }

    public SongPattern ReadSongPattern(int patternIndex)
    {
        if (_memory != null)
            return SongCodec.ReadPattern(_memory, _layout, patternIndex);
        return new SongPattern(new SongChannelEntry[_layout.Channels]);
    }

    public void WriteSongPattern(int patternIndex, SongPattern pattern)
    {
        if (_memory != null)
            SongCodec.WritePattern(_memory, _layout, patternIndex, pattern);
    }

    public int PatternCellCount => _legacyRowsPerPattern * _channels;

    public void ConfigureMusic(int patternCount, int rowsPerPattern = 64)
    {
        // Legacy editor API — patterns stored separately from Pico-8 song RAM.
    }

    public void SetMusicPattern(int patternIndex, int[] patternData)
    {
        if (_legacyMusicPatterns != null && patternIndex >= 0 && patternIndex < _legacyMusicPatterns.Length)
            _legacyMusicPatterns[patternIndex] = patternData;
    }

    public int[] GetMusicPattern(int patternIndex)
    {
        if (_legacyMusicPatterns == null || patternIndex < 0 || patternIndex >= _legacyMusicPatterns.Length)
            return [];
        return _legacyMusicPatterns[patternIndex] ?? [];
    }

    public void SetMusicLoop(int start, int end) { }
    public void SetMusicSpeed(int speed) { }
    public void SetMusicRepeat(bool repeat) { }

    public int StatSfxOnChannel(int ch)
    {
        if (ch < 0 || ch >= _channels) return -1;
        int playing = _sfxChannels[ch].PlayingSfxIndex;
        if (playing >= 0) return playing;
        return _channelSfxSlot[ch];
    }

    public int StatNoteOnChannel(int ch) =>
        ch >= 0 && ch < _channels ? _sfxChannels[ch].CurrentNoteIndex : -1;

    public int StatCurrentPattern() => _currentSongPattern;
    public int StatTotalPatterns() => _totalPatternsPlayed;
    public int StatTicksOnPattern() => _ticksOnPattern;
    public bool StatMusicPlaying() => _musicPlaying;

    public bool TryGetInstrumentSfx(int instrumentIndex, out SfxNote[] notes, out int speed, out int loopStart, out int loopEnd, out SfxFilters filters)
    {
        notes = [];
        speed = 4;
        loopStart = 0;
        loopEnd = _layout.NotesPerSfx - 1;
        filters = SfxFilters.None;

        if (instrumentIndex < 0 || instrumentIndex >= 8) return false;

        notes = ReadSlotNotes(instrumentIndex);
        var header = ReadSlotHeader(instrumentIndex);
        speed = header.speed;
        loopStart = header.loopStart;
        loopEnd = header.loopEnd;
        filters = header.filters;
        return true;
    }

    public bool TryGetWaveformTable(int instrumentIndex, out byte[] table)
    {
        table = [];
        if (instrumentIndex < 0 || instrumentIndex >= 8 || !_waveformSlots[instrumentIndex])
            return false;

        var notes = ReadSlotNotes(instrumentIndex);
        table = new byte[64];
        for (int i = 0; i < 64 && i < notes.Length; i++)
            table[i] = (byte)System.Math.Clamp(notes[i].Pitch * 2, 0, 255);
        return true;
    }

    public void Dispose()
    {
        _pumpRunning = false;
        _pumpThread?.Join(500);

        _waveOut?.Stop();
        _waveOut?.Dispose();
        _mixer.ClearBuffer();
    }
}
