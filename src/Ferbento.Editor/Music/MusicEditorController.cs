using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ferbento.Core.Audio;
using Ferbento.Core.Chips;
using Ferbento.Editor.Music.Input;
using Ferbento.Editor.Music.Undo;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Music;

public sealed class MusicEditorController : INotifyPropertyChanged
{
    public MusicDocument Document { get; } = new();
    public MusicUndoService Undo { get; } = new();

    private ChipBus? _bus;
    private HardwareProfile _profile = HardwareProfile.Pico8();

    private int _currentPattern;
    private int _selectedRow;
    private int _selectedChannel;
    private int _length = 32;
    private int _tempo = 8;
    private int _autoIncrement = 1;
    private int _loopStart;
    private int _loopEnd = 63;
    private int _selectedInstrument;
    private int _selectedNote = 60;
    private int _selectedVolume = 7;
    private int _sequenceSlot;
    private bool _isPlaying;
    private string _statusMessage = "Click a step or use the piano to enter notes.";

    public event Action? Edited;
    public event Action? RefreshRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    public int PatternCount => Document.PatternCount;
    public int ChannelCount => Document.ChannelCount;
    public int RowsPerPattern => Document.RowsPerPattern;
    public int SfxSlotCount => Document.SfxSlotCount;

    public int CurrentPattern
    {
        get => _currentPattern;
        set
        {
            var clamped = Document.ClampPattern(value);
            if (_currentPattern == clamped) return;
            SaveCurrentPattern();
            _currentPattern = clamped;
            Notify();
            RefreshRequested?.Invoke();
        }
    }

    public int SelectedRow
    {
        get => _selectedRow;
        set { _selectedRow = Document.ClampRow(value); Notify(); RefreshRequested?.Invoke(); }
    }

    public int SelectedChannel
    {
        get => _selectedChannel;
        set { _selectedChannel = Document.ClampChannel(value); Notify(); RefreshRequested?.Invoke(); }
    }

    public int Length
    {
        get => _length;
        set { _length = Math.Clamp(value, 1, RowsPerPattern); Notify(); }
    }

    public int Tempo
    {
        get => _tempo;
        set
        {
            _tempo = Math.Max(1, value);
            _bus?.Music.SetMusicSpeed(_tempo);
            Notify();
        }
    }

    public int AutoIncrement
    {
        get => _autoIncrement;
        set { _autoIncrement = Math.Max(0, value); Notify(); }
    }

    public int LoopStart
    {
        get => _loopStart;
        set
        {
            _loopStart = Document.ClampRow(value);
            if (_loopEnd < _loopStart) _loopEnd = _loopStart;
            Notify();
        }
    }

    public int LoopEnd
    {
        get => _loopEnd;
        set
        {
            _loopEnd = Document.ClampRow(value);
            if (_loopEnd < _loopStart) _loopStart = _loopEnd;
            Notify();
        }
    }

    public int SelectedInstrument
    {
        get => _selectedInstrument;
        set { _selectedInstrument = Document.ClampInstrument(value); Notify(); }
    }

    public int SelectedNote
    {
        get => _selectedNote;
        set { _selectedNote = Math.Clamp(value, 0, 127); Notify(); Notify(nameof(SelectedNoteLetter)); Notify(nameof(SelectedOctave)); }
    }

    public int SelectedVolume
    {
        get => _selectedVolume;
        set { _selectedVolume = Math.Clamp(value, 0, 7); Notify(); }
    }

    public string SelectedNoteLetter => MusicNoteCodec.NoteLetter(_selectedNote);
    public int SelectedOctave => MusicNoteCodec.OctaveFromNote(_selectedNote);

    public int SequenceSlot
    {
        get => _sequenceSlot;
        set
        {
            var clamped = Math.Clamp(value, 0, Document.Sequence.Length - 1);
            if (_sequenceSlot == clamped) return;
            _sequenceSlot = clamped;
            CurrentPattern = GetSequencePattern(clamped);
            Notify();
            RefreshRequested?.Invoke();
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set { _isPlaying = value; Notify(); Notify(nameof(IsStopped)); }
    }

    public bool IsStopped => !IsPlaying;

    public string SongName
    {
        get => Document.SongName;
        set { Document.SongName = value; Notify(); MarkEdited(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; Notify(); }
    }

    public string ProfileLabel => _profile.Name;

    public int PlayheadRow => _bus?.Music.PlayheadRow ?? -1;

    public MusicEditorController()
    {
        Undo.StateChanged += () => Notify(nameof(UndoCount));
    }

    public int UndoCount => Undo.UndoCount;

    public void SetProfile(HardwareProfile profile)
    {
        var prevPattern = _currentPattern;
        _profile = profile;
        Document.SetProfile(profile);
        _currentPattern = Document.ClampPattern(prevPattern);
        _selectedChannel = Document.ClampChannel(_selectedChannel);
        _loopEnd = Math.Min(_loopEnd, RowsPerPattern - 1);
        _length = Math.Min(_length, RowsPerPattern);
        NotifyAllLimits();
    }

    public void SetChipBus(ChipBus bus)
    {
        _bus = bus;
        Document.SetChipBus(bus);
        SetProfile(bus.Profile);
        LoadCurrentPattern();
    }

    public void RefreshFromBus()
    {
        if (_bus == null) return;
        Document.SetProfile(_bus.Profile);
        LoadCurrentPattern();
        NotifyAllLimits();
        RefreshRequested?.Invoke();
    }

    public int GetSequencePattern(int slot)
    {
        if (slot < 0 || slot >= Document.Sequence.Length) return 0;
        return Document.ClampPattern(Document.Sequence[slot]);
    }

    public void SetSequencePattern(int slot, int pattern)
    {
        if (slot < 0 || slot >= Document.Sequence.Length) return;
        Document.Sequence[slot] = Document.ClampPattern(pattern);
        MarkEdited();
        RefreshRequested?.Invoke();
    }

    public int[] GetCurrentPatternData() => Document.GetPattern(_currentPattern);

    public bool HasNoteAt(int row, int channel) =>
        Document.GetCell(_currentPattern, row, channel) != 0;

    public string GetCellDisplay(int row, int channel)
    {
        var (note, _, vol, _, _) = Document.GetCellDecoded(_currentPattern, row, channel);
        if (note <= 0) return "";
        return $"{MusicNoteCodec.NoteToPitchName(note)} v{vol}";
    }

    public void SelectCell(int row, int channel)
    {
        SelectedRow = row;
        SelectedChannel = channel;
        var decoded = Document.GetCellDecoded(_currentPattern, row, channel);
        if (decoded.note > 0)
        {
            SelectedNote = decoded.note;
            SelectedInstrument = decoded.instrument;
            SelectedVolume = decoded.volume;
        }
    }

    public void ToggleCell(int row, int channel)
    {
        SelectedRow = row;
        SelectedChannel = channel;

        int before = Document.GetCell(_currentPattern, row, channel);
        int after;
        if (before == 0)
        {
            after = MusicNoteCodec.Pack(_selectedNote, _selectedInstrument, _selectedVolume,
                SfxEffect.None, 0);
        }
        else after = 0;

        Undo.Execute(new MusicCellCommand(Document, _currentPattern, row, channel, before, after));
        MarkEdited();
        RefreshRequested?.Invoke();
    }

    public void PlaceNoteAtSelection(int midiNote)
    {
        SelectedNote = midiNote;
        int before = Document.GetCell(_currentPattern, _selectedRow, _selectedChannel);
        int after = MusicNoteCodec.Pack(midiNote, _selectedInstrument, _selectedVolume,
            SfxEffect.None, 0);
        Undo.Execute(new MusicCellCommand(Document, _currentPattern, _selectedRow, _selectedChannel,
            before, after, "Place note"));
        MarkEdited();
        RefreshRequested?.Invoke();
        AdvanceAfterEdit(_selectedRow);
        PreviewSelectedNote();
    }

    public void ClearSelectedCell()
    {
        int before = Document.GetCell(_currentPattern, _selectedRow, _selectedChannel);
        if (before == 0) return;
        Undo.Execute(new MusicCellCommand(Document, _currentPattern, _selectedRow, _selectedChannel,
            before, 0, "Clear note"));
        MarkEdited();
        RefreshRequested?.Invoke();
    }

    public void ClearPattern()
    {
        Undo.Execute(new MusicPatternClearCommand(Document, _currentPattern));
        MarkEdited();
        RefreshRequested?.Invoke();
    }

    public void SetNoteLetterOctave(string letter, int octave)
    {
        SelectedNote = MusicNoteCodec.CombineNoteOctave(letter, octave);
    }

    public void PlayPattern()
    {
        if (_bus == null) return;
        SaveCurrentPattern();
        _bus.Music.SetMusicSpeed(_tempo);
        _bus.Music.SetMusicRepeat(false);
        _bus.Music.SetMusicLoop(_loopStart, Math.Min(_loopEnd, _length - 1));
        int mask = _bus.Music.ChannelMask(ChannelCount);
        _bus.Music.Music(_currentPattern, 0, mask);
        IsPlaying = true;
        StatusMessage = $"Playing pattern {_currentPattern:D2}...";
    }

    public void PlaySong()
    {
        if (_bus == null) return;
        SaveCurrentPattern();
        _bus.Music.SetMusicSpeed(_tempo);
        _bus.Music.SetMusicRepeat(true);
        _bus.Music.SetMusicLoop(_loopStart, Math.Min(_loopEnd, _length - 1));
        int mask = _bus.Music.ChannelMask(ChannelCount);
        _bus.Music.Music(_currentPattern, 0, mask);
        IsPlaying = true;
        StatusMessage = "Playing song sequence...";
    }

    public void Stop()
    {
        _bus?.Music.Music(-1);
        IsPlaying = false;
        StatusMessage = "Stopped.";
        RefreshRequested?.Invoke();
    }

    public void TogglePlayStop()
    {
        if (IsPlaying) Stop();
        else PlayPattern();
    }

    public void TickPlayback()
    {
        if (!IsPlaying || _bus == null) return;
        if (!_bus.Music.IsPlaying)
        {
            IsPlaying = false;
            StatusMessage = "Playback finished.";
        }

        Notify(nameof(PlayheadRow));
        RefreshRequested?.Invoke();
    }

    public void PreviewSelectedNote()
    {
        if (_bus == null || _selectedNote <= 0) return;
        int ch = Math.Clamp(_selectedChannel, 0, ChannelCount - 1);
        var note = new SfxNote(_selectedNote, _selectedInstrument, _selectedVolume);
        _bus.Sound.PreviewNote(ch, note, _tempo);
    }

    public void RandomGeneratePattern()
    {
        var data = MusicPatternGenerator.Generate(
            _length, ChannelCount, _selectedInstrument, _selectedVolume);

        Undo.Execute(new MusicPatternReplaceCommand(Document, _currentPattern, data, "Generate pattern"));
        MarkEdited();
        StatusMessage = $"Generated pattern {_currentPattern:D2}.";
        RefreshRequested?.Invoke();
    }

    public int ClampedPlayheadRow => ClampPlayheadRow(PlayheadRow, _length);

    public static int ClampPlayheadRow(int playheadRow, int patternLength)
    {
        if (playheadRow < 0) return -1;
        return Math.Clamp(playheadRow, 0, patternLength - 1);
    }

    public bool HandleKey(string key, bool ctrl, bool shift, bool alt)
    {
        if (MusicEditorInputMap.Matches(key, ctrl, shift, alt, MusicEditorAction.Undo))
            return Undo.Undo() && RefreshAfterCommand();
        if (MusicEditorInputMap.Matches(key, ctrl, shift, alt, MusicEditorAction.Redo))
            return Undo.Redo() && RefreshAfterCommand();
        if (MusicEditorInputMap.Matches(key, ctrl, shift, alt, MusicEditorAction.PlayStop))
        {
            TogglePlayStop();
            return true;
        }
        if (MusicEditorInputMap.Matches(key, ctrl, shift, alt, MusicEditorAction.PlayPattern))
        {
            PlayPattern();
            return true;
        }
        if (MusicEditorInputMap.Matches(key, ctrl, shift, alt, MusicEditorAction.Stop))
        {
            Stop();
            return true;
        }
        if (MusicEditorInputMap.Matches(key, ctrl, shift, alt, MusicEditorAction.PrevPattern))
        {
            CurrentPattern--;
            return true;
        }
        if (MusicEditorInputMap.Matches(key, ctrl, shift, alt, MusicEditorAction.NextPattern))
        {
            CurrentPattern++;
            return true;
        }
        if (MusicEditorInputMap.Matches(key, ctrl, shift, alt, MusicEditorAction.TempoDown))
        {
            Tempo--;
            return true;
        }
        if (MusicEditorInputMap.Matches(key, ctrl, shift, alt, MusicEditorAction.TempoUp))
        {
            Tempo++;
            return true;
        }
        if (MusicEditorInputMap.Matches(key, ctrl, shift, alt, MusicEditorAction.MoveLeft))
        {
            SelectedChannel = Math.Max(0, _selectedChannel - 1);
            return true;
        }
        if (MusicEditorInputMap.Matches(key, ctrl, shift, alt, MusicEditorAction.MoveRight))
        {
            SelectedChannel = Math.Min(ChannelCount - 1, _selectedChannel + 1);
            return true;
        }
        if (MusicEditorInputMap.Matches(key, ctrl, shift, alt, MusicEditorAction.MoveUp))
        {
            SelectedRow = Math.Max(0, _selectedRow - 1);
            return true;
        }
        if (MusicEditorInputMap.Matches(key, ctrl, shift, alt, MusicEditorAction.MoveDown))
        {
            SelectedRow = Math.Min(RowsPerPattern - 1, _selectedRow + 1);
            return true;
        }
        if (MusicEditorInputMap.Matches(key, ctrl, shift, alt, MusicEditorAction.DeleteCell))
        {
            ClearSelectedCell();
            return true;
        }
        if (MusicEditorInputMap.Matches(key, ctrl, shift, alt, MusicEditorAction.RandomGenerate))
        {
            RandomGeneratePattern();
            return true;
        }

        return false;
    }

    public bool HandleTextInput(string text)
    {
        if (text is "+" or "=") { Tempo++; return true; }
        if (text is "-") { Tempo--; return true; }
        if (text is "[") { CurrentPattern--; return true; }
        if (text is "]") { CurrentPattern++; return true; }
        return false;
    }

    public MusicProjectData ExportProjectData() =>
        Document.ExportProjectData(_currentPattern, _length, _tempo, _autoIncrement, _loopStart, _loopEnd);

    public void ImportProjectData(MusicProjectData? data)
    {
        if (data == null) return;
        Document.ImportProjectData(data, out var pat, out var len, out var tempo,
            out var autoInc, out var loopStart, out var loopEnd);
        _currentPattern = pat;
        _length = len;
        _tempo = tempo;
        _autoIncrement = autoInc;
        _loopStart = loopStart;
        _loopEnd = loopEnd;
        NotifyAllLimits();
        RefreshRequested?.Invoke();
    }

    private void SaveCurrentPattern() =>
        Document.SetPattern(_currentPattern, Document.GetPatternCopy(_currentPattern));

    private void LoadCurrentPattern() =>
        Document.GetPattern(_currentPattern);

    private void AdvanceAfterEdit(int row)
    {
        if (_autoIncrement <= 0) return;
        SelectedRow = Math.Min(RowsPerPattern - 1, row + _autoIncrement);
    }

    private void MarkEdited() => Edited?.Invoke();

    private bool RefreshAfterCommand()
    {
        RefreshRequested?.Invoke();
        MarkEdited();
        return true;
    }

    private void NotifyAllLimits()
    {
        Notify(nameof(PatternCount));
        Notify(nameof(ChannelCount));
        Notify(nameof(RowsPerPattern));
        Notify(nameof(SfxSlotCount));
        Notify(nameof(ProfileLabel));
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
