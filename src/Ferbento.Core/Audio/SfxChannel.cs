using System.Collections.Concurrent;
using NAudio.Wave;

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

public readonly struct SfxNote
{
    public readonly int Note;
    public readonly int Instrument;
    public readonly int Volume;
    public readonly SfxEffect Effect;
    public readonly int EffectParam;

    public SfxNote(int note = 0, int instrument = 0, int volume = 0, SfxEffect effect = SfxEffect.None, int effectParam = 0)
    {
        Note = note;
        Instrument = instrument;
        Volume = volume;
        Effect = effect;
        EffectParam = effectParam;
    }

    public bool IsEmpty => Note == 0 && Instrument == 0 && Volume == 0 && Effect == SfxEffect.None && EffectParam == 0;
}

public sealed class SfxChannel : ISampleProvider
{
    private readonly int _channelIndex;
    private readonly int _sampleRate;
    private readonly WaveFormat _waveFormat;
    
    private SfxNote[] _pattern = new SfxNote[32];
    private int _patternIndex;
    private int _speed = 4;
    private int _frameCounter;
    private readonly int _samplesPerTick;
    private int _tickSampleCounter;
    
    private int _loopStart;
    private int _loopEnd = 31;
    
    private double _phase;
    private int _currentNote = 0;
    private int _currentInstrument = 0;
    private int _currentVolume = 0;
    private SfxEffect _currentEffect = SfxEffect.None;
    private int _currentEffectParam = 0;
    
    private int _slideTargetNote = 0;
    private double _slideSpeed = 0;
    private double _vibratoPhase = 0;
    private double _vibratoDepth = 0;
    private double _vibratoSpeed = 0;
    private int _fadeVolume = 0;
    private int _fadeTargetVolume = 0;
    private double _fadeSpeed = 0;
    private int _arpeggioNotes = 0;
    private int _arpeggioIndex = 0;
    private double _arpeggioTimer = 0;

    private double _noiseSample;
    private double _lastNoiseAdvance;
    private uint _noiseRng = 0xACE1u;

    private bool _playing;

    private readonly ConcurrentQueue<Action> _commandQueue = new();

    public WaveFormat WaveFormat => _waveFormat;
    public int ChannelIndex => _channelIndex;
    public bool IsPlaying => _playing;
    public int CurrentNoteIndex => _patternIndex;
    public int LoopStart { get => _loopStart; set => _loopStart = System.Math.Clamp(value, 0, 31); }
    public int LoopEnd { get => _loopEnd; set => _loopEnd = System.Math.Clamp(value, 0, 31); }

    public SfxChannel(int channelIndex, int sampleRate = 22050)
    {
        _channelIndex = channelIndex;
        _sampleRate = sampleRate;
        _samplesPerTick = System.Math.Max(1, sampleRate / 60);
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        ClearPattern();
    }

    public void SetLoop(int start, int end)
    {
        _loopStart = System.Math.Clamp(start, 0, 31);
        _loopEnd = System.Math.Clamp(end, _loopStart, 31);
    }

    public void ClearPattern()
    {
        for (int i = 0; i < 32; i++)
            _pattern[i] = new SfxNote();
        _patternIndex = 0;
        _speed = 4;
        _frameCounter = 0;
    }

    public void SetNote(int index, SfxNote note)
    {
        if (index >= 0 && index < 32)
            _pattern[index] = note;
    }

    public SfxNote GetNote(int index)
    {
        return index >= 0 && index < 32 ? _pattern[index] : new SfxNote();
    }

        public void SetSpeed(int speed)
        {
            _speed = System.Math.Max(1, speed);
        }

    public void Play(int startIndex = 0)
    {
        _commandQueue.Enqueue(() =>
        {
            _patternIndex = System.Math.Clamp(startIndex, 0, 31);
            _frameCounter = 0;
            _playing = true;
            AdvanceNote();
        });
    }

    public void Stop()
    {
        _commandQueue.Enqueue(Silence);
    }

    public void StopImmediate() => Silence();

    private void Silence()
    {
        _playing = false;
        _currentNote = 0;
        _currentVolume = 0;
        _currentInstrument = 0;
        _currentEffect = SfxEffect.None;
        _currentEffectParam = 0;
        _phase = 0;
        _patternIndex = 0;
        _frameCounter = 0;
        _tickSampleCounter = 0;
        _noiseSample = 0;
        _lastNoiseAdvance = 0;
    }

    public void FlushCommands() => ProcessCommands();

    public float NextSample()
    {
        if (!_playing)
            return 0f;

        float sample = (float)GetNextSample();

        _tickSampleCounter++;
        if (_tickSampleCounter >= _samplesPerTick)
        {
            _tickSampleCounter = 0;
            _frameCounter++;
            if (_frameCounter >= _speed)
                AdvanceNote();
        }

        return sample;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        ProcessCommands();

        if (!_playing)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        for (int i = 0; i < count; i++)
            buffer[offset + i] = NextSample();

        return count;
    }

    private void ProcessCommands()
    {
        while (_commandQueue.TryDequeue(out var cmd))
            cmd();
    }

    private void AdvanceNote()
    {
        if (_patternIndex > _loopEnd)
        {
            if (_loopEnd > _loopStart)
            {
                _patternIndex = _loopStart;
            }
            else
            {
                Silence();
                return;
            }
        }

        if (_patternIndex < 0 || _patternIndex >= 32)
        {
            Silence();
            return;
        }

        var note = _pattern[_patternIndex];
        if (note.IsEmpty)
        {
            // Empty step is an actual rest: do not sustain previous tone.
            _currentNote = 0;
            _currentInstrument = 0;
            _currentVolume = 0;
            _currentEffect = SfxEffect.None;
            _currentEffectParam = 0;
            _noiseSample = 0;
            _lastNoiseAdvance = _phase / (2 * System.Math.PI);
        }
        else
        {
            _currentNote = note.Note;
            _currentInstrument = note.Instrument;
            _currentVolume = note.Volume;
            _currentEffect = note.Effect;
            _currentEffectParam = note.EffectParam;

            _noiseSample = 0;
            _lastNoiseAdvance = _phase / (2 * System.Math.PI);
            InitializeEffect();
        }
        
        _patternIndex++;
        _frameCounter = 0;
    }

    private void InitializeEffect()
    {
        _slideTargetNote = 0;
        _slideSpeed = 0;
        _vibratoPhase = 0;
        _vibratoDepth = 0;
        _vibratoSpeed = 0;
        _fadeVolume = _currentVolume;
        _fadeTargetVolume = _currentVolume;
        _fadeSpeed = 0;
        _arpeggioNotes = 0;
        _arpeggioIndex = 0;
        _arpeggioTimer = 0;

        switch (_currentEffect)
        {
            case SfxEffect.Slide:
                _slideTargetNote = _currentEffectParam & 0xFF;
                _slideSpeed = ((_currentEffectParam >> 8) & 0xFF) / 100.0;
                break;
            case SfxEffect.Vibrato:
                _vibratoDepth = (_currentEffectParam & 0xFF) / 100.0;
                _vibratoSpeed = ((_currentEffectParam >> 8) & 0xFF) / 100.0;
                break;
            case SfxEffect.Drop:
                _slideTargetNote = _currentNote - (_currentEffectParam & 0xFF);
                _slideSpeed = ((_currentEffectParam >> 8) & 0xFF) / 100.0;
                break;
            case SfxEffect.FadeIn:
                _fadeTargetVolume = 7;
                _fadeSpeed = (_currentEffectParam & 0xFF) / 100.0;
                _currentVolume = 0;
                break;
            case SfxEffect.FadeOut:
                _fadeTargetVolume = 0;
                _fadeSpeed = (_currentEffectParam & 0xFF) / 100.0;
                break;
            case SfxEffect.Arpeggio:
                _arpeggioNotes = _currentEffectParam;
                break;
            case SfxEffect.Arpeggio2:
                _arpeggioNotes = _currentEffectParam;
                break;
        }
    }

    private double GetNextSample()
    {
        if (!_playing || _currentVolume <= 0 || _currentNote <= 0)
            return 0;

        double freq = NoteToFrequency(_currentNote);
        
        switch (_currentEffect)
        {
            case SfxEffect.Slide:
            case SfxEffect.Drop:
                freq = NoteToFrequency((int)(_currentNote + _slideSpeed * _frameCounter));
                break;
            case SfxEffect.Vibrato:
                _vibratoPhase += _vibratoSpeed;
                freq *= 1 + _vibratoDepth * System.Math.Sin(_vibratoPhase);
                break;
        }

        if (_currentEffect == SfxEffect.Arpeggio || _currentEffect == SfxEffect.Arpeggio2)
        {
            _arpeggioTimer += 1.0 / _sampleRate * 60;
            if (_arpeggioTimer >= 1.0 / 3.0)
            {
                _arpeggioTimer = 0;
                _arpeggioIndex = (_arpeggioIndex + 1) % 3;
            }
            int[] arpNotes = DecodeArpeggio(_arpeggioNotes);
            if (arpNotes.Length > _arpeggioIndex)
                freq = NoteToFrequency(arpNotes[_arpeggioIndex]);
        }

        if (_currentEffect == SfxEffect.FadeIn || _currentEffect == SfxEffect.FadeOut)
        {
            _fadeVolume += (int)(_fadeSpeed * (_fadeTargetVolume - _fadeVolume));
            _currentVolume = System.Math.Clamp(_fadeVolume, 0, 7);
        }

        _phase += freq * 2 * System.Math.PI / _sampleRate;
        if (_phase > 2 * System.Math.PI) _phase -= 2 * System.Math.PI;

        var waveform = ResolveWaveform(_currentInstrument);
        double advance = _phase / (2 * System.Math.PI);
        double volume = _currentVolume / 7.0;
        double sample = GenerateWaveform(waveform, _phase, advance, volume, _currentNote);
        
        return sample;
    }

    private static SfxWaveform ResolveWaveform(int instrument)
    {
        // Built-in waveforms 0-7. Instruments 8-15 are custom SFX slots in Pico-8 (not yet implemented).
        if (instrument >= 0 && instrument < 8)
            return (SfxWaveform)instrument;

        // Melodic fallback only (triangle/saw/square) — never noise via % 4.
        return (SfxWaveform)(instrument % 3);
    }

    private static double NoteToFrequency(int note)
    {
        if (note <= 0) return 0;
        return 440.0 * System.Math.Pow(2.0, (note - 69) / 12.0);
    }

    private double GenerateWaveform(SfxWaveform waveform, double phase, double advance, double volume, int note)
    {
        return waveform switch
        {
            SfxWaveform.Triangle => TriangleWave(phase) * 0.5 * volume,
            SfxWaveform.Sawtooth => SawtoothWave(phase) * 0.65 * volume,
            SfxWaveform.Square => SquareWave(phase) * 0.25 * volume,
            SfxWaveform.Noise => GenerateFilteredNoise(advance, note, volume),
            SfxWaveform.Pulse25 => PulseWave(phase, 0.25) * 0.25 * volume,
            SfxWaveform.Pulse12 => PulseWave(phase, 0.125) * 0.25 * volume,
            SfxWaveform.Triangle2 => TriangleWave(phase * 2) * 0.5 * volume,
            SfxWaveform.Noise2 => GenerateFilteredNoise(advance, note, volume * 0.5),
            _ => 0
        };
    }

    // Pico-8-style filtered noise (see zepto8 synth.cpp); avoids per-sample white noise hiss.
    private double GenerateFilteredNoise(double advance, int note, double volume)
    {
        const double tscale = 8.858923;
        double scale = (advance - _lastNoiseAdvance) * tscale;
        if (scale < 0)
            scale = 0;

        double random = NextNoiseRandom() * 2 - 1;
        _noiseSample = (_noiseSample + scale * random) / (1.0 + scale);

        double factor = 1.0 - System.Math.Clamp(note / 63.0, 0, 1);
        double sample = _noiseSample * 1.0 * (0.75 + factor * 0.5) * volume;
        _lastNoiseAdvance = advance;
        return sample;
    }

    private double NextNoiseRandom()
    {
        _noiseRng = unchecked(_noiseRng * 1664525u + 1013904223u);
        return (_noiseRng >> 16) / 65536.0;
    }

    private static double TriangleWave(double phase) => 2 * System.Math.Abs(2 * (phase / (2 * System.Math.PI) - System.Math.Floor(phase / (2 * System.Math.PI) + 0.5))) - 1;
    private static double SawtoothWave(double phase) => 2 * (phase / (2 * System.Math.PI) - System.Math.Floor(phase / (2 * System.Math.PI) + 0.5));
    private static double SquareWave(double phase) => System.Math.Sin(phase) >= 0 ? 1 : -1;
    private static double PulseWave(double phase, double duty) => (phase / (2 * System.Math.PI) % 1) < duty ? 1 : -1;

    private static int[] DecodeArpeggio(int param)
    {
        var notes = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            int n = (param >> (i * 4)) & 0xF;
            if (n > 0) notes.Add(n);
        }
        return notes.Count > 0 ? notes.ToArray() : new[] { 0 };
    }
}
