using System.Collections.Concurrent;
using NAudio.Wave;

namespace Ferbento.Core.Audio;

public interface ISfxInstrumentSource
{
    bool TryGetInstrumentSfx(int instrumentIndex, out SfxNote[] notes, out int speed, out int loopStart, out int loopEnd, out SfxFilters filters);
    bool TryGetWaveformTable(int instrumentIndex, out byte[] table);
}

public sealed class SfxChannel : ISampleProvider
{
    private readonly int _channelIndex;
    private readonly int _sampleRate;
    private readonly int _notesPerSfx;
    private readonly WaveFormat _waveFormat;

    private SfxNote[] _pattern;
    private int _patternIndex;
    private int _speed = 4;
    private int _frameCounter;
    private readonly int _samplesPerTick;
    private int _tickSampleCounter;

    private int _loopStart;
    private int _loopEnd;

    private double _phase;
    private double _detunePhase;
    private int _currentNote;
    private int _currentInstrument;
    private int _currentVolume;
    private SfxEffect _currentEffect = SfxEffect.None;
    private int _currentEffectParam;
    private bool _currentCustomInst;
    private bool _usePico8Pitch;
    private bool _releaseLoop;

    private int _slideTargetNote;
    private double _slideSpeed;
    private double _vibratoPhase;
    private double _vibratoDepth;
    private double _vibratoSpeed;
    private int _fadeVolume;
    private int _fadeTargetVolume;
    private double _fadeSpeed;
    private int _arpeggioNotes;
    private int _arpeggioIndex;
    private double _arpeggioTimer;

    private double _noiseSample;
    private double _lastNoiseAdvance;
    private uint _noiseRng = 0xACE1u;

    private SfxFilters _filters = SfxFilters.None;
    private ISfxInstrumentSource? _instrumentSource;

    private readonly double[] _reverbBuffer = new double[512];
    private int _reverbWritePos;
    private double _dampenState;

    private int _customInstNoteIndex;
    private SfxNote[]? _customInstPattern;
    private int _customInstSpeed;
    private int _customInstFrameCounter;
    private int _customInstLoopStart;
    private int _customInstLoopEnd;

    private byte[]? _waveformTable;
    private double _waveformPhase;

    private bool _playing;
    private int _playingSfxIndex = -1;

    private readonly ConcurrentQueue<Action> _commandQueue = new();

    public WaveFormat WaveFormat => _waveFormat;
    public int ChannelIndex => _channelIndex;
    public bool IsPlaying => _playing;
    public int CurrentNoteIndex => _patternIndex;
    public int PlayingSfxIndex => _playingSfxIndex;
    public int LoopStart { get => _loopStart; set => _loopStart = System.Math.Clamp(value, 0, _notesPerSfx - 1); }
    public int LoopEnd { get => _loopEnd; set => _loopEnd = System.Math.Clamp(value, _loopStart, _notesPerSfx - 1); }

    public SfxChannel(int channelIndex, int sampleRate = 22050, int notesPerSfx = 32)
    {
        _channelIndex = channelIndex;
        _sampleRate = sampleRate;
        _notesPerSfx = System.Math.Max(1, notesPerSfx);
        _samplesPerTick = System.Math.Max(1, sampleRate / 60);
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _pattern = new SfxNote[_notesPerSfx];
        _loopEnd = _notesPerSfx - 1;
        ClearPattern();
    }

    public void SetInstrumentSource(ISfxInstrumentSource? source) => _instrumentSource = source;

    public void SetFilters(SfxFilters filters) => _filters = filters;

    public void SetUsePico8Pitch(bool usePico8Pitch) => _usePico8Pitch = usePico8Pitch;

    public void SetLoop(int start, int end)
    {
        _loopStart = System.Math.Clamp(start, 0, _notesPerSfx - 1);
        _loopEnd = System.Math.Clamp(end, _loopStart, _notesPerSfx - 1);
    }

    public void ClearPattern()
    {
        for (int i = 0; i < _notesPerSfx; i++)
            _pattern[i] = new SfxNote();
        _patternIndex = 0;
        _speed = 4;
        _frameCounter = 0;
    }

    public void SetNote(int index, SfxNote note)
    {
        if (index >= 0 && index < _notesPerSfx)
            _pattern[index] = note;
    }

    public SfxNote GetNote(int index) =>
        index >= 0 && index < _notesPerSfx ? _pattern[index] : new SfxNote();

    public void SetSpeed(int speed) => _speed = System.Math.Max(1, speed);

    public void Play(int startIndex = 0, int sfxIndex = -1)
    {
        _commandQueue.Enqueue(() =>
        {
            _patternIndex = System.Math.Clamp(startIndex, 0, _notesPerSfx - 1);
            _frameCounter = 0;
            _releaseLoop = false;
            _playingSfxIndex = sfxIndex;
            _playing = true;
            AdvanceNote();
        });
    }

    public void Stop() => _commandQueue.Enqueue(Silence);

    public void ReleaseLoop() => _commandQueue.Enqueue(() => _releaseLoop = true);

    public void StopImmediate() => Silence();

    private void Silence()
    {
        _playing = false;
        _playingSfxIndex = -1;
        _currentNote = 0;
        _currentVolume = 0;
        _currentInstrument = 0;
        _currentEffect = SfxEffect.None;
        _currentEffectParam = 0;
        _currentCustomInst = false;
        _phase = 0;
        _detunePhase = 0;
        _patternIndex = 0;
        _frameCounter = 0;
        _tickSampleCounter = 0;
        _noiseSample = 0;
        _lastNoiseAdvance = 0;
        _customInstPattern = null;
        _waveformTable = null;
        _dampenState = 0;
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
            if (!_releaseLoop && _loopEnd > _loopStart)
                _patternIndex = _loopStart;
            else
            {
                Silence();
                return;
            }
        }

        if (_patternIndex < 0 || _patternIndex >= _notesPerSfx)
        {
            Silence();
            return;
        }

        var note = _pattern[_patternIndex];
        if (note.IsEmpty)
        {
            _currentNote = 0;
            _currentInstrument = 0;
            _currentVolume = 0;
            _currentEffect = SfxEffect.None;
            _currentEffectParam = 0;
            _currentCustomInst = false;
            _customInstPattern = null;
            _waveformTable = null;
            _noiseSample = 0;
            _lastNoiseAdvance = _phase / (2 * System.Math.PI);
        }
        else
        {
            _currentNote = note.Pitch;
            _currentInstrument = note.Instrument;
            _currentVolume = note.Volume;
            _currentEffect = note.Effect;
            _currentEffectParam = note.EffectParam;
            _currentCustomInst = note.CustomInst;
            _noiseSample = 0;
            _lastNoiseAdvance = _phase / (2 * System.Math.PI);
            InitializeEffect();
            SetupCustomInstrument();
        }

        _patternIndex++;
        _frameCounter = 0;
    }

    private void SetupCustomInstrument()
    {
        _customInstPattern = null;
        _waveformTable = null;

        if (_instrumentSource == null) return;

        if (_currentCustomInst && _currentInstrument is >= 0 and <= 7)
        {
            if (_instrumentSource.TryGetInstrumentSfx(
                    _currentInstrument, out var notes, out var speed, out var loopStart, out var loopEnd, out _))
            {
                _customInstPattern = notes;
                _customInstSpeed = speed;
                _customInstLoopStart = loopStart;
                _customInstLoopEnd = loopEnd;
                _customInstNoteIndex = 0;
                _customInstFrameCounter = 0;
            }
            return;
        }

        if (_currentInstrument is >= 0 and <= 7 &&
            _instrumentSource.TryGetWaveformTable(_currentInstrument, out var table))
        {
            _waveformTable = table;
            _waveformPhase = 0;
        }
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
            case SfxEffect.Arpeggio2:
                _arpeggioNotes = _currentEffectParam;
                break;
        }
    }

    private double GetNextSample()
    {
        if (!_playing || _currentVolume <= 0 || _currentNote <= 0)
            return 0;

        if (_customInstPattern != null)
            return GetCustomInstrumentSample();

        if (_waveformTable != null)
            return GetWaveformInstrumentSample();

        double sample = GenerateMelodicSample(_currentNote, _currentInstrument, _currentVolume, _phase, ref _phase);

        if ((_filters & SfxFilters.Detune1) != 0)
        {
            double detuned = GenerateMelodicSample(_currentNote, _currentInstrument, _currentVolume, _detunePhase, ref _detunePhase, pitchScale: 1.001);
            sample = sample * 0.5 + detuned * 0.5;
        }
        else if ((_filters & SfxFilters.Detune2) != 0)
        {
            double detuned = GenerateMelodicSample(_currentNote + 12, _currentInstrument, _currentVolume, _detunePhase, ref _detunePhase);
            sample = sample * 0.5 + detuned * 0.5;
        }

        sample = ApplyBuzz(sample);
        sample = ApplyReverb(sample);
        sample = ApplyDampen(sample);

        return sample;
    }

    private double GetCustomInstrumentSample()
    {
        if (_customInstPattern == null || _customInstPattern.Length == 0)
            return 0;

        if (_customInstNoteIndex > _customInstLoopEnd)
        {
            if (_customInstLoopEnd > _customInstLoopStart)
                _customInstNoteIndex = _customInstLoopStart;
            else
                return 0;
        }

        var note = _customInstPattern[_customInstNoteIndex];
        if (note.IsEmpty)
            return 0;

        int relPitch = _currentNote + (note.Pitch - 32);
        double sample = GenerateMelodicSample(relPitch, note.Instrument, System.Math.Min(7, _currentVolume * note.Volume / 7),
            _phase, ref _phase, note.CustomInst);

        _customInstFrameCounter++;
        if (_customInstFrameCounter >= _customInstSpeed)
        {
            _customInstFrameCounter = 0;
            _customInstNoteIndex++;
        }

        return sample;
    }

    private double GetWaveformInstrumentSample()
    {
        if (_waveformTable == null || _waveformTable.Length == 0)
            return 0;

        double freq = NoteToFrequency(_currentNote, _usePico8Pitch);
        _waveformPhase += freq / _sampleRate;
        if (_waveformPhase >= 1.0) _waveformPhase -= 1.0;

        double pos = _waveformPhase * _waveformTable.Length;
        int idx = (int)pos % _waveformTable.Length;
        int next = (idx + 1) % _waveformTable.Length;
        double frac = pos - System.Math.Floor(pos);
        double value = (_waveformTable[idx] + frac * (_waveformTable[next] - _waveformTable[idx])) / 127.5 - 1.0;
        return value * (_currentVolume / 7.0) * 0.5;
    }

    private double GenerateMelodicSample(int note, int instrument, int volume, double phase, ref double phaseRef, bool customInst = false, double pitchScale = 1.0)
    {
        double freq = NoteToFrequency(note, _usePico8Pitch) * pitchScale;

        switch (_currentEffect)
        {
            case SfxEffect.Slide:
            case SfxEffect.Drop:
                freq = NoteToFrequency((int)(note + _slideSpeed * _frameCounter), _usePico8Pitch) * pitchScale;
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
                freq = NoteToFrequency(arpNotes[_arpeggioIndex], _usePico8Pitch) * pitchScale;
        }

        int vol = volume;
        if (_currentEffect == SfxEffect.FadeIn || _currentEffect == SfxEffect.FadeOut)
        {
            _fadeVolume += (int)(_fadeSpeed * (_fadeTargetVolume - _fadeVolume));
            vol = System.Math.Clamp(_fadeVolume, 0, 7);
        }

        phaseRef += freq * 2 * System.Math.PI / _sampleRate;
        if (phaseRef > 2 * System.Math.PI) phaseRef -= 2 * System.Math.PI;

        var waveform = ResolveWaveform(instrument);
        double advance = phaseRef / (2 * System.Math.PI);
        double volNorm = vol / 7.0;

        if ((_filters & SfxFilters.Noiz) != 0 && instrument == 6)
            return NextNoiseRandom() * volNorm;

        return GenerateWaveform(waveform, phaseRef, advance, volNorm, note);
    }

    private static SfxWaveform ResolveWaveform(int instrument)
    {
        if (instrument >= 0 && instrument < 8)
            return (SfxWaveform)instrument;
        return (SfxWaveform)(instrument % 3);
    }

    private static double NoteToFrequency(int note, bool pico8Pitch)
    {
        if (note <= 0) return 0;
        if (pico8Pitch)
            return 440.0 * System.Math.Pow(2.0, (note - 57) / 12.0);
        if (note <= 63)
            return 440.0 * System.Math.Pow(2.0, (note - 57) / 12.0);
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

    private double GenerateFilteredNoise(double advance, int note, double volume)
    {
        const double tscale = 8.858923;
        double scale = (advance - _lastNoiseAdvance) * tscale;
        if (scale < 0) scale = 0;

        double random = NextNoiseRandom() * 2 - 1;
        _noiseSample = (_noiseSample + scale * random) / (1.0 + scale);

        double factor = 1.0 - System.Math.Clamp(note / 63.0, 0, 1);
        double sample = _noiseSample * (0.75 + factor * 0.5) * volume;
        _lastNoiseAdvance = advance;
        return sample;
    }

    private double ApplyBuzz(double sample)
    {
        if ((_filters & SfxFilters.Buzz) == 0) return sample;
        double crushed = System.Math.Round(sample * 16.0) / 16.0;
        return System.Math.Sign(sample) * System.Math.Min(1.0, System.Math.Abs(crushed) * 1.25);
    }

    private double ApplyReverb(double sample)
    {
        if ((_filters & SfxFilters.Reverb) == 0) return sample;

        int delay = _sampleRate / 22050 * 120;
        int readPos = (_reverbWritePos - delay + _reverbBuffer.Length) % _reverbBuffer.Length;
        double echo = _reverbBuffer[readPos];
        _reverbBuffer[_reverbWritePos] = sample + echo * 0.3;
        _reverbWritePos = (_reverbWritePos + 1) % _reverbBuffer.Length;
        return sample + echo * 0.3;
    }

    private double ApplyDampen(double sample)
    {
        if ((_filters & SfxFilters.Dampen) == 0) return sample;
        double coef = 0.6;
        _dampenState = _dampenState * coef + sample * (1.0 - coef);
        return _dampenState;
    }

    private double NextNoiseRandom()
    {
        _noiseRng = unchecked(_noiseRng * 1664525u + 1013904223u);
        return (_noiseRng >> 16) / 65536.0;
    }

    private static double TriangleWave(double phase) =>
        2 * System.Math.Abs(2 * (phase / (2 * System.Math.PI) - System.Math.Floor(phase / (2 * System.Math.PI) + 0.5))) - 1;

    private static double SawtoothWave(double phase) =>
        2 * (phase / (2 * System.Math.PI) - System.Math.Floor(phase / (2 * System.Math.PI) + 0.5));

    private static double SquareWave(double phase) => System.Math.Sin(phase) >= 0 ? 1 : -1;

    private static double PulseWave(double phase, double duty) =>
        (phase / (2 * System.Math.PI) % 1) < duty ? 1 : -1;

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
