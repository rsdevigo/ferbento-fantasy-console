using System.Collections.Concurrent;
using NAudio.Wave;

namespace Ferbento.Core.Audio;

public sealed class AudioEngine : IDisposable
{
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly SfxChannel[] _sfxChannels;
    private readonly WaveOutEvent? _waveOut;
    private readonly BufferedWaveProvider _mixer;
    private readonly object _lock = new();
    
    private readonly SfxNote[][] _sfxSlots;
    private readonly int[] _slotSpeed = new int[64];
    private readonly int[] _slotLoopStart = new int[64];
    private readonly int[] _slotLoopEnd = new int[64];
    
    private int[][] _musicPatterns = new int[64][];
    private int _currentMusicPattern = -1;
    private int _musicPatternIndex = 0;
    private int _musicSpeed = 4;
    private int _musicSpeedCounter = 0;
    private int _musicLoopStart = 0;
    private int _musicLoopEnd = 63;
    private bool _musicPlaying = false;
    private readonly int[] _musicChannelMap = new int[4];
    
    private readonly ConcurrentQueue<Action> _commandQueue = new();

    public IReadOnlyList<SfxChannel> Channels => _sfxChannels;
    public int SampleRate => _sampleRate;
    public bool IsMusicPlaying => _musicPlaying;
    public int SfxSlotCount => _sfxSlots.Length;

    public AudioEngine(int sampleRate = 22050, int channels = 4, int sfxSlots = 64)
    {
        _sampleRate = sampleRate;
        _channels = channels;
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

        _mixer = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1))
        {
            BufferDuration = TimeSpan.FromMilliseconds(200),
            DiscardOnBufferOverflow = true
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
        ProcessCommands();

        if (_waveOut == null) return;

        var buffer = new float[_sampleRate / 60];
        var mixed = new float[buffer.Length];
        
        for (int ch = 0; ch < _channels; ch++)
        {
            var channelBuffer = new float[buffer.Length];
            int read = _sfxChannels[ch].Read(channelBuffer, 0, buffer.Length);
            for (int i = 0; i < read; i++)
                mixed[i] += channelBuffer[i];
        }

        if (_musicPlaying)
        {
            UpdateMusic();
            var musicBuffer = new float[buffer.Length];
            RenderMusicPattern(musicBuffer);
            for (int i = 0; i < buffer.Length; i++)
                mixed[i] += musicBuffer[i];
        }

        for (int i = 0; i < mixed.Length; i++)
            mixed[i] = System.Math.Clamp(mixed[i], -1f, 1f);

        byte[] byteBuffer = new byte[mixed.Length * 4];
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

    public void SetMusicPattern(int patternIndex, int[] patternData)
    {
        if (patternIndex >= 0 && patternIndex < 64)
            _musicPatterns[patternIndex] = patternData;
    }

    public int[] GetMusicPattern(int patternIndex)
    {
        return patternIndex >= 0 && patternIndex < 64 ? _musicPatterns[patternIndex] ?? new int[0] : new int[0];
    }

    public void PlayMusic(int patternIndex, int fade = 0, int channelMask = 15)
    {
        _commandQueue.Enqueue(() =>
        {
            _currentMusicPattern = patternIndex;
            _musicPatternIndex = _musicLoopStart;
            _musicSpeedCounter = 0;
            _musicPlaying = true;
            
            for (int i = 0; i < 4; i++)
                _musicChannelMap[i] = (channelMask & (1 << i)) != 0 ? i : -1;
        });
    }

    public void StopMusic()
    {
        _commandQueue.Enqueue(() => _musicPlaying = false);
    }

    public void SetMusicLoop(int start, int end)
    {
        _musicLoopStart = System.Math.Clamp(start, 0, 63);
        _musicLoopEnd = System.Math.Clamp(end, _musicLoopStart, 63);
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

    private void RenderMusicPattern(float[] buffer)
    {
        if (_currentMusicPattern < 0 || _currentMusicPattern >= 64)
            return;

        var pattern = _musicPatterns[_currentMusicPattern];
        if (pattern == null || pattern.Length == 0)
            return;

        int row = _musicPatternIndex * 4;
        for (int ch = 0; ch < 4; ch++)
        {
            int mappedChannel = _musicChannelMap[ch];
            if (mappedChannel < 0 || mappedChannel >= _channels) continue;
            
            int noteIndex = row + ch;
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
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _mixer.ClearBuffer();
    }
}
