using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ferbento.Core.Audio;
using Ferbento.Core.Chips;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.ViewModels;

public sealed class SfxEditorViewModel : INotifyPropertyChanged
{
    public SoundChip SoundChip { get; }

    private int _selectedSlot;
    public int SelectedSlot
    {
        get => _selectedSlot;
        set
        {
            if (value < 0 || value >= SoundChip.SfxSlotCount) return;
            SaveCurrentSlot();
            _selectedSlot = value;
            OnPropertyChanged();
            LoadSlot(_selectedSlot);
        }
    }

    private int _selectedChannel;
    public int SelectedChannel
    {
        get => _selectedChannel;
        set
        {
            if (value < 0 || value > 3) return;
            _selectedChannel = value;
            OnPropertyChanged();
        }
    }

    private int _defaultWaveform;
    public int DefaultWaveform
    {
        get => _defaultWaveform;
        set { _defaultWaveform = value & 7; OnPropertyChanged(); }
    }

    private int _defaultVolume = 7;
    public int DefaultVolume
    {
        get => _defaultVolume;
        set { _defaultVolume = Math.Clamp(value, 0, 7); OnPropertyChanged(); }
    }

    private SfxEffect _defaultEffect;
    public SfxEffect DefaultEffect
    {
        get => _defaultEffect;
        set { _defaultEffect = value; OnPropertyChanged(); }
    }

    private int _defaultEffectParam;
    public int DefaultEffectParam
    {
        get => _defaultEffectParam;
        set { _defaultEffectParam = Math.Clamp(value, 0, 255); OnPropertyChanged(); }
    }

    private int _speed = 4;
    public int Speed
    {
        get => _speed;
        set { _speed = Math.Max(1, value); OnPropertyChanged(); }
    }

    private int _loopStart;
    public int LoopStart
    {
        get => _loopStart;
        set { _loopStart = Math.Clamp(value, 0, 31); OnPropertyChanged(); }
    }

    private int _loopEnd = 31;
    public int LoopEnd
    {
        get => _loopEnd;
        set { _loopEnd = Math.Clamp(value, 0, 31); OnPropertyChanged(); }
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set { _isPlaying = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsStopped)); }
    }

    public bool IsStopped => !_isPlaying;

    private int _selectedStep = -1;
    public int SelectedStep
    {
        get => _selectedStep;
        set
        {
            _selectedStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedNote));
            OnPropertyChanged(nameof(SelectedNotePitch));
            OnPropertyChanged(nameof(SelectedNoteWaveform));
            OnPropertyChanged(nameof(SelectedNoteVolume));
            OnPropertyChanged(nameof(SelectedNoteEffect));
            OnPropertyChanged(nameof(SelectedNoteEffectParam));
        }
    }

    public bool HasSelectedNote => _selectedStep >= 0 && _selectedStep < 32;

    public string SelectedNotePitch
    {
        get
        {
            if (!HasSelectedNote) return "--";
            var n = ActiveSlot[_selectedStep];
            if (n.IsEmpty) return "--";
            return NoteToPitchName(n.Note);
        }
    }

    public int SelectedNoteWaveform
    {
        get
        {
            if (!HasSelectedNote) return 0;
            return ActiveSlot[_selectedStep].Instrument & 7;
        }
        set
        {
            if (!HasSelectedNote) return;
            var n = ActiveSlot[_selectedStep];
            ActiveSlot[_selectedStep] = new SfxNote(n.Note, value & 7, n.Volume, n.Effect, n.EffectParam);
            SyncCurrentSlotToEngine();
            OnPropertyChanged();
            InvalidateView();
        }
    }

    public int SelectedNoteVolume
    {
        get
        {
            if (!HasSelectedNote) return 0;
            return ActiveSlot[_selectedStep].Volume;
        }
        set
        {
            if (!HasSelectedNote) return;
            var n = ActiveSlot[_selectedStep];
            ActiveSlot[_selectedStep] = new SfxNote(n.Note, n.Instrument, Math.Clamp(value, 0, 7), n.Effect, n.EffectParam);
            SyncCurrentSlotToEngine();
            OnPropertyChanged();
            InvalidateView();
        }
    }

    public SfxEffect SelectedNoteEffect
    {
        get
        {
            if (!HasSelectedNote) return SfxEffect.None;
            return ActiveSlot[_selectedStep].Effect;
        }
        set
        {
            if (!HasSelectedNote) return;
            var n = ActiveSlot[_selectedStep];
            ActiveSlot[_selectedStep] = new SfxNote(n.Note, n.Instrument, n.Volume, value, n.EffectParam);
            SyncCurrentSlotToEngine();
            OnPropertyChanged();
            InvalidateView();
        }
    }

    public int SelectedNoteEffectParam
    {
        get
        {
            if (!HasSelectedNote) return 0;
            return ActiveSlot[_selectedStep].EffectParam;
        }
        set
        {
            if (!HasSelectedNote) return;
            var n = ActiveSlot[_selectedStep];
            ActiveSlot[_selectedStep] = new SfxNote(n.Note, n.Instrument, n.Volume, n.Effect, Math.Clamp(value, 0, 255));
            SyncCurrentSlotToEngine();
            OnPropertyChanged();
            InvalidateView();
        }
    }

    public SfxNote[] ActiveSlot { get; private set; } = new SfxNote[32];

    public static IReadOnlyList<string> PitchNames { get; } = GeneratePitchNames();
    public static IReadOnlyList<string> WaveformNames { get; } = new[] { "triangle", "saw", "square", "noise", "pulse25", "pulse12", "tri2", "noise2" };
    public static IReadOnlyList<string> EffectNames { get; } = new[] { "none", "slide", "vibrato", "drop", "fadein", "fadeout", "arpeg", "arpeg2" };

    public SfxEditorViewModel(ChipBus? bus = null)
    {
        var chipBus = bus ?? new ChipBus(HardwareProfile.Pico8());
        SoundChip = chipBus.Sound;
        LoadSlot(0);
    }

    public void SetNote(int step, int pitch)
    {
        if (step < 0 || step >= 32) return;
        var current = ActiveSlot[step];

        if (current.Note == pitch && !current.IsEmpty)
        {
            ActiveSlot[step] = new SfxNote();
        }
        else
        {
            ActiveSlot[step] = new SfxNote(
                note: pitch,
                instrument: _defaultWaveform,
                volume: _defaultVolume,
                effect: _defaultEffect,
                effectParam: _defaultEffectParam
            );
        }

        SyncCurrentSlotToEngine();
        InvalidateView();
    }

    public void ToggleNote(int step)
    {
        if (step < 0 || step >= 32) return;
        var current = ActiveSlot[step];
        if (current.IsEmpty)
        {
            ActiveSlot[step] = new SfxNote(
                note: 48,
                instrument: _defaultWaveform,
                volume: _defaultVolume,
                effect: _defaultEffect,
                effectParam: _defaultEffectParam
            );
        }
        else
        {
            ActiveSlot[step] = new SfxNote();
        }
        SyncCurrentSlotToEngine();
        InvalidateView();
    }

    public void ClearPatternAll()
    {
        for (int i = 0; i < 32; i++)
            ActiveSlot[i] = new SfxNote();
        SyncCurrentSlotToEngine();
        InvalidateView();
    }

    public bool HasNoteAt(int step, int pitch)
    {
        if (step < 0 || step >= 32) return false;
        var n = ActiveSlot[step];
        return !n.IsEmpty && n.Note == pitch;
    }

    public bool IsStepEmpty(int step)
    {
        return step < 0 || step >= 32 || ActiveSlot[step].IsEmpty;
    }

    public SfxNote GetNoteAt(int step)
    {
        return step >= 0 && step < 32 ? ActiveSlot[step] : new SfxNote();
    }

    public void Play()
    {
        SaveCurrentSlot();
        SoundChip.LoadSlotToChannel(_selectedSlot, _selectedChannel);
        SoundChip.Engine.SetSfxLoop(_selectedChannel, _loopStart, _loopEnd);
        SoundChip.Sfx(_selectedSlot, _selectedChannel, _loopStart);
        IsPlaying = true;
    }

    public void Stop()
    {
        SoundChip.Engine.StopSfx(_selectedChannel);
        IsPlaying = false;
    }

    public void TickPlayback()
    {
        if (!IsPlaying) return;
        var ch = SoundChip.Engine.Channels[_selectedChannel];
        if (!ch.IsPlaying)
            IsPlaying = false;
    }

    public int GetPlayheadColumn()
    {
        if (!IsPlaying) return -1;
        var ch = SoundChip.Engine.Channels[_selectedChannel];
        return ch.IsPlaying ? ch.CurrentNoteIndex : -1;
    }

    private void SaveCurrentSlot()
    {
        SoundChip.SetSlot(_selectedSlot, ActiveSlot);
        SoundChip.SetSlotSpeed(_selectedSlot, _speed);
        SoundChip.SetSlotLoop(_selectedSlot, _loopStart, _loopEnd);
    }

    private void LoadSlot(int slotIndex)
    {
        ActiveSlot = SoundChip.GetSlot(slotIndex);
        _speed = SoundChip.GetSlotSpeed(slotIndex);
        _loopStart = SoundChip.GetSlotLoop(slotIndex).start;
        _loopEnd = SoundChip.GetSlotLoop(slotIndex).end;
        OnPropertyChanged(nameof(Speed));
        OnPropertyChanged(nameof(LoopStart));
        OnPropertyChanged(nameof(LoopEnd));
        InvalidateView();
    }

    private void SyncCurrentSlotToEngine()
    {
        SoundChip.SetSlot(_selectedSlot, ActiveSlot);
        SoundChip.SetSlotSpeed(_selectedSlot, _speed);
        SoundChip.SetSlotLoop(_selectedSlot, _loopStart, _loopEnd);
    }

    private void InvalidateView()
    {
        OnPropertyChanged(nameof(ActiveSlot));
        OnPropertyChanged(nameof(SelectedNotePitch));
        OnPropertyChanged(nameof(SelectedNoteWaveform));
        OnPropertyChanged(nameof(SelectedNoteVolume));
        OnPropertyChanged(nameof(SelectedNoteEffect));
        OnPropertyChanged(nameof(SelectedNoteEffectParam));
    }

    private static string NoteToPitchName(int midiNote)
    {
        string[] notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int octave = (midiNote / 12) - 1;
        int n = midiNote % 12;
        return $"{notes[n]}{octave}";
    }

    private static List<string> GeneratePitchNames()
    {
        var names = new List<string>();
        string[] notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        for (int octave = 4; octave <= 8; octave++)
        {
            for (int n = 0; n < 12; n++)
            {
                int midiNote = (octave + 1) * 12 + n;
                if (midiNote < 48 || midiNote > 108) continue;
                names.Add($"{notes[n]}{octave}");
            }
        }
        return names;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
