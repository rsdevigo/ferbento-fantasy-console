using System.Collections.Concurrent;
using NAudio.Wave;

namespace Ferbento.Core.Audio;

public sealed class AudioEngine : IDisposable
{
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly int _samplesPerTick;
    private readonly SfxChannel[] _sfxChannels;
    private readonly WaveOutEvent? _waveOut;
    private readonly BufferedWaveProvider _mixer;
    private readonly object _lock = new();

    private readonly SfxNote[][] _sfxSlots;
    private readonly int[] _slotSpeed = new int[64];
    private readonly int[] _slotLoopStart = new int[64];
    private readonly int[] _slotLoopEnd = new int[64];

    private int[][] _musicPatterns = [];
    private int _musicPatternCount = 64;
    private int _musicRowsPerPattern = 64;
    private int _currentMusicPattern = -1;
    private int _musicPatternIndex = 0;
    private int _musicSpeed = 4;
    private int _musicSpeedCounter = 0;
    private int _musicTickSampleCounter;
    private int _musicLoopStart = 0;
    private int _musicLoopEnd = 63;
    private bool _musicPlaying = false;
    private int _lastRenderedMusicRow = -1;
    private int[] _musicChannelMap = [];

    private readonly ConcurrentQueue<Action> _commandQueue = new();
    private Thread? _pumpThread;
    private volatile bool _pumpRunning;

    private const double TargetBufferSeconds = 0.12;
    private const int MaxSamplesPerPump = 4096;

    public IReadOnlyList<SfxChannel> Channels => _sfxChannels;
    public int SampleRate => _sampleRate;
    public bool IsMusicPlaying => _musicPlaying;
    public int MusicPlayheadRow => _musicPatternIndex;
    public int MusicCurrentPattern => _currentMusicPattern;
    public int MusicPatternCount => _musicPatternCount;
    public int MusicRowsPerPattern => _musicRowsPerPattern;
    public int MusicTrackCount => _channels;
    public int SfxSlotCount => _sfxSlots.Length;

    public AudioEngine(int sampleRate = 22050, int channels = 4, int sfxSlots = 64)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _samplesPerTick = System.Math.Max(1, sampleRate / 60);
        _sfxChannels = new SfxChannel[channels];

        for (int i = 0; i < channels; i++)
            _sfxChannels[i] = new SfxChannel(i, sampleRate);

        _sfxSlots = new SfxNote[sfxSlots][];
        for (int i = 0; i < sfxSlots; i++)
        {
            _sfxSlots[i] = new SfxNote[32];
            _slotSpeed[i] = 4;
            _slotLoopEnd[i] = 31;
        }

        ConfigureMusic(64, 64);
        _musicChannelMap = new int[_channels];

        _mixer = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1))
        {
            BufferDuration = TimeSpan.FromMilliseconds(300),
            DiscardOnBufferOverflow = false
        };

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
            {
                _musicTickSampleCounter++;
                if (_musicTickSampleCounter >= _samplesPerTick)
                {
                    _musicTickSampleCounter = 0;
                    UpdateMusic();
                    if (_musicPatternIndex != _lastRenderedMusicRow)
                    {
                        _lastRenderedMusicRow = _musicPatternIndex;
                        RenderMusicPattern();
                        FlushChannelCommands();
                    }
                }
            }

            float sample = 0;
            for (int ch = 0; ch < _channels; ch++)
                sample += _sfxChannels[ch].NextSample();
            mixed[i] = System.Math.Clamp(sample, -1f, 1f);
        }

        var byteBuffer = new byte[mixed.Length * sizeof(float)];
        Buffer.BlockCopy(mixed, 0, byteBuffer, 0, byteBuffer.Length);
        _mixer.AddSamples(byteBuffer, 0, byteBuffer.Length);
    }

    public void PlaySfx(int sfxIndex, int channel = -1, int offset = 0)
    {
        if (sfxIndex < 0 || sfxIndex >= _sfxSlots.Length) return;

        if (channel >= 0 && channel < _channels)
        {
            LoadSlotToChannel(sfxIndex, channel);
            _sfxChannels[channel].Play(offset);
        }
        else
        {
            for (int i = 0; i < _channels; i++)
            {
                if (!_sfxChannels[i].IsPlaying)
                {
                    LoadSlotToChannel(sfxIndex, i);
                    _sfxChannels[i].Play(offset);
                    break;
                }
            }
        }
    }

    public int FindFreeChannel()
    {
        for (int i = 0; i < _channels; i++)
        {
            if (!_sfxChannels[i].IsPlaying)
                return i;
        }
        return -1;
    }

    public void LoadSlotToChannel(int slotIndex, int channel)
    {
        if (slotIndex < 0 || slotIndex >= _sfxSlots.Length) return;
        if (channel < 0 || channel >= _channels) return;

        var slot = _sfxSlots[slotIndex];
        for (int i = 0; i < 32; i++)
            _sfxChannels[channel].SetNote(i, slot[i]);
        _sfxChannels[channel].SetSpeed(_slotSpeed[slotIndex]);
        _sfxChannels[channel].SetLoop(_slotLoopStart[slotIndex], _slotLoopEnd[slotIndex]);
    }

    public SfxNote[] GetSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _sfxSlots.Length)
            return new SfxNote[32];
        var result = new SfxNote[32];
        Array.Copy(_sfxSlots[slotIndex], result, 32);
        return result;
    }

    public void SetSlot(int slotIndex, SfxNote[] notes)
    {
        if (slotIndex < 0 || slotIndex >= _sfxSlots.Length) return;
        for (int i = 0; i < 32 && i < notes.Length; i++)
            _sfxSlots[slotIndex][i] = notes[i];
    }

    public int GetSlotSpeed(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < _slotSpeed.Length ? _slotSpeed[slotIndex] : 4;
    }

    public void SetSlotSpeed(int slotIndex, int speed)
    {
        if (slotIndex >= 0 && slotIndex < _slotSpeed.Length)
            _slotSpeed[slotIndex] = System.Math.Max(1, speed);
    }

    public (int start, int end) GetSlotLoop(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < _slotLoopStart.Length)
            return (_slotLoopStart[slotIndex], _slotLoopEnd[slotIndex]);
        return (0, 31);
    }

    public void SetSlotLoop(int slotIndex, int start, int end)
    {
        if (slotIndex >= 0 && slotIndex < _slotLoopStart.Length)
        {
            _slotLoopStart[slotIndex] = System.Math.Clamp(start, 0, 31);
            _slotLoopEnd[slotIndex] = System.Math.Clamp(end, _slotLoopStart[slotIndex], 31);
        }
    }

    public void StopSfx(int channel)
    {
        if (channel >= 0 && channel < _channels)
            _sfxChannels[channel].Stop();
    }

    public void SetSfxNote(int channel, int patternIndex, SfxNote note)
    {
        if (channel >= 0 && channel < _channels && patternIndex >= 0 && patternIndex < 32)
            _sfxChannels[channel].SetNote(patternIndex, note);
    }

    public SfxNote GetSfxNote(int channel, int patternIndex)
    {
        if (channel >= 0 && channel < _channels && patternIndex >= 0 && patternIndex < 32)
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
        _sfxChannels[channel].Play(0);
    }

    public void ConfigureMusic(int patternCount, int rowsPerPattern = 64)
    {
        _musicPatternCount = System.Math.Max(1, patternCount);
        _musicRowsPerPattern = System.Math.Max(1, rowsPerPattern);
        _musicPatterns = new int[_musicPatternCount][];
        _musicLoopEnd = System.Math.Min(_musicLoopEnd, _musicRowsPerPattern - 1);
    }

    public int PatternCellCount => _musicRowsPerPattern * _channels;

    public void SetMusicPattern(int patternIndex, int[] patternData)
    {
        if (patternIndex >= 0 && patternIndex < _musicPatternCount)
            _musicPatterns[patternIndex] = patternData;
    }

    public int[] GetMusicPattern(int patternIndex)
    {
        if (patternIndex < 0 || patternIndex >= _musicPatternCount)
            return [];
        return _musicPatterns[patternIndex] ?? [];
    }

    public void PlayMusic(int patternIndex, int fade = 0, int channelMask = 15)
    {
        _commandQueue.Enqueue(() =>
        {
            _currentMusicPattern = patternIndex;
            _musicPatternIndex = _musicLoopStart;
            _musicSpeedCounter = 0;
            _musicTickSampleCounter = 0;
            _lastRenderedMusicRow = -1;
            _musicPlaying = true;

            if (_musicChannelMap.Length != _channels)
                _musicChannelMap = new int[_channels];

            for (int i = 0; i < _channels; i++)
                _musicChannelMap[i] = (channelMask & (1 << i)) != 0 ? i : -1;

            RenderMusicPattern();
            _lastRenderedMusicRow = _musicPatternIndex;
        });
    }

    public void StopMusic()
    {
        _commandQueue.Enqueue(() =>
        {
            _musicPlaying = false;
            _lastRenderedMusicRow = -1;
            _musicTickSampleCounter = 0;
        });
    }

    public void SetMusicLoop(int start, int end)
    {
        int maxRow = _musicRowsPerPattern - 1;
        _musicLoopStart = System.Math.Clamp(start, 0, maxRow);
        _musicLoopEnd = System.Math.Clamp(end, _musicLoopStart, maxRow);
    }

    public void SetMusicSpeed(int speed)
    {
        _musicSpeed = System.Math.Max(1, speed);
    }

    private void UpdateMusic()
    {
        _musicSpeedCounter++;
        if (_musicSpeedCounter >= _musicSpeed)
        {
            _musicSpeedCounter = 0;
            _musicPatternIndex++;
            if (_musicPatternIndex > _musicLoopEnd)
                _musicPatternIndex = _musicLoopStart;
        }
    }

    private void RenderMusicPattern()
    {
        if (_currentMusicPattern < 0 || _currentMusicPattern >= _musicPatternCount)
            return;

        var pattern = _musicPatterns[_currentMusicPattern];
        if (pattern == null || pattern.Length == 0)
            return;

        int rowBase = _musicPatternIndex * _channels;
        for (int ch = 0; ch < _channels; ch++)
        {
            int mappedChannel = ch < _musicChannelMap.Length ? _musicChannelMap[ch] : ch;
            if (mappedChannel < 0 || mappedChannel >= _channels) continue;

            int noteIndex = rowBase + ch;
            if (noteIndex < pattern.Length)
            {
                int noteData = pattern[noteIndex];
                if (noteData > 0)
                {
                    int note = noteData & 0xFF;
                    int instrument = (noteData >> 8) & 0xF;
                    int volume = (noteData >> 12) & 0x7;
                    int effect = (noteData >> 15) & 0x7;
                    int effectParam = (noteData >> 18) & 0x3FF;

                    var sfxNote = new SfxNote(note, instrument, volume, (SfxEffect)effect, effectParam);
                    _sfxChannels[mappedChannel].SetNote(0, sfxNote);
                    _sfxChannels[mappedChannel].SetSpeed(_musicSpeed);
                    _sfxChannels[mappedChannel].Play(0);
                }
            }
        }
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
