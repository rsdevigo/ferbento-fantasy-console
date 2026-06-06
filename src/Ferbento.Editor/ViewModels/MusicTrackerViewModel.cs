using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ferbento.Core.Audio;
using Ferbento.Core.Chips;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.ViewModels;

public sealed class MusicTrackerViewModel : INotifyPropertyChanged
{
    public ChipBus Bus { get; }

    private int _currentPattern;
    public int CurrentPattern
    {
        get => _currentPattern;
        set
        {
            if (value < 0 || value >= 64) return;
            SaveCurrentPattern();
            _currentPattern = value;
            OnPropertyChanged();
            LoadPattern(_currentPattern);
        }
    }

    private int _speed = 8;
    public int Speed
    {
        get => _speed;
        set { _speed = Math.Max(1, value); OnPropertyChanged(); }
    }

    private int _loopStart;
    public int LoopStart
    {
        get => _loopStart;
        set { _loopStart = Math.Clamp(value, 0, 63); OnPropertyChanged(); }
    }

    private int _loopEnd = 63;
    public int LoopEnd
    {
        get => _loopEnd;
        set { _loopEnd = Math.Clamp(value, 0, 63); OnPropertyChanged(); }
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set { _isPlaying = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsStopped)); }
    }

    public bool IsStopped => !_isPlaying;

    public int[] PatternData { get; private set; } = new int[256];

    public MusicTrackerViewModel(ChipBus? bus = null)
    {
        Bus = bus ?? new ChipBus(HardwareProfile.Pico8());
        LoadPattern(0);
    }

    public void SetCell(int row, int channel, int note, int instrument, int volume, SfxEffect effect, int effectParam)
    {
        int index = row * 4 + channel;
        if (index < 0 || index >= 256) return;

        if (note <= 0)
        {
            PatternData[index] = 0;
        }
        else
        {
            PatternData[index] = PackNote(note, instrument, volume, effect, effectParam);
        }
        SyncToEngine();
        OnPropertyChanged(nameof(PatternData));
    }

    public void ToggleCell(int row, int channel)
    {
        int index = row * 4 + channel;
        if (index < 0 || index >= 256) return;

        if (PatternData[index] == 0)
        {
            PatternData[index] = PackNote(48, 0, 7, SfxEffect.None, 0);
        }
        else
        {
            PatternData[index] = 0;
        }
        SyncToEngine();
        OnPropertyChanged(nameof(PatternData));
    }

    public (int note, int instrument, int volume, SfxEffect effect, int effectParam) GetCell(int row, int channel)
    {
        int index = row * 4 + channel;
        if (index < 0 || index >= 256) return (0, 0, 0, SfxEffect.None, 0);

        int data = PatternData[index];
        if (data == 0) return (0, 0, 0, SfxEffect.None, 0);

        return UnpackNote(data);
    }

    public string GetCellDisplay(int row, int channel)
    {
        int index = row * 4 + channel;
        if (index < 0 || index >= 256) return "---";

        int data = PatternData[index];
        if (data == 0) return "---";

        var (note, inst, vol, effect, _) = UnpackNote(data);
        string pitchName = NoteToPitchName(note);
        return $"{pitchName} v{vol}";
    }

    public int GetCellRaw(int row, int channel)
    {
        int index = row * 4 + channel;
        if (index < 0 || index >= 256) return 0;
        return PatternData[index];
    }

    public bool HasNoteAt(int row, int channel)
    {
        int index = row * 4 + channel;
        return index >= 0 && index < 256 && PatternData[index] != 0;
    }

    public void Play()
    {
        SaveCurrentPattern();
        Bus.Music.SetMusicSpeed(_speed);
        Bus.Music.SetMusicLoop(_loopStart, _loopEnd);
        Bus.Music.Music(_currentPattern, 0, 15);
        IsPlaying = true;
    }

    public void Stop()
    {
        Bus.Music.Music(-1);
        IsPlaying = false;
    }

    public void TickPlayback()
    {
        if (!IsPlaying) return;
        if (!Bus.Sound.Engine.IsMusicPlaying)
            IsPlaying = false;
    }

    public void ClearPattern()
    {
        Array.Clear(PatternData);
        SyncToEngine();
        OnPropertyChanged(nameof(PatternData));
    }

    private void SaveCurrentPattern()
    {
        Bus.Music.SetMusicPattern(_currentPattern, PatternData);
    }

    private void LoadPattern(int patternIndex)
    {
        var data = Bus.Music.GetMusicPattern(patternIndex);
        PatternData = data.Length == 256 ? data : new int[256];
        OnPropertyChanged(nameof(PatternData));
    }

    private void SyncToEngine()
    {
        Bus.Music.SetMusicPattern(_currentPattern, PatternData);
    }

    private static int PackNote(int note, int instrument, int volume, SfxEffect effect, int effectParam)
    {
        return (note & 0xFF) |
               ((instrument & 0xF) << 8) |
               ((volume & 0x7) << 12) |
               (((int)effect & 0x7) << 15) |
               ((effectParam & 0x3FF) << 18);
    }

    private static (int note, int instrument, int volume, SfxEffect effect, int effectParam) UnpackNote(int data)
    {
        int note = data & 0xFF;
        int instrument = (data >> 8) & 0xF;
        int volume = (data >> 12) & 0x7;
        SfxEffect effect = (SfxEffect)((data >> 15) & 0x7);
        int effectParam = (data >> 18) & 0x3FF;
        return (note, instrument, volume, effect, effectParam);
    }

    private static string NoteToPitchName(int midiNote)
    {
        string[] notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int octave = (midiNote / 12) - 1;
        int n = midiNote % 12;
        return $"{notes[n]}{octave}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
