using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ferbento.Core.Audio;
using Ferbento.Core.Chips;
using Ferbento.Editor.Sfx.Input;
using Ferbento.Editor.Sfx.Undo;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Sfx;

public sealed class SfxEditorController : INotifyPropertyChanged
{
  public SfxEditorController()
  {
    ResizeSlotNames(Document.SfxSlotCount);
  }

  public SfxDocument Document { get; } = new();
  public SfxUndoService Undo { get; } = new(256);
  public SfxClipboard Clipboard { get; } = new();

  private ChipBus? _bus;
  private HardwareProfile _profile = HardwareProfile.Pico8();

  private int _selectedSlot;
  private int _selectedChannel;
  private int _cursorStep;
  private int _cursorRow = 30;
  private SfxEditorTool _tool = SfxEditorTool.Pencil;
  private int _defaultWaveform;
  private int _defaultVolume = 7;
  private SfxEffect _defaultEffect;
  private int _defaultEffectParam;
  private bool _isPlaying;
  private bool _loopPlayback = true;
  private int _zoomLevel = 2;
  private string _statusMessage = "Click or press Enter to place a note.";
  private readonly HashSet<int> _selectedSteps = new();
  private string[] _slotNames = [];

  public event Action? Edited;
  public event Action? RefreshRequested;
  public event PropertyChangedEventHandler? PropertyChanged;

  public int SfxSlotCount => Document.SfxSlotCount;
  public int NotesPerSfx => Document.NotesPerSfx;
  public int ChannelCount => Document.ChannelCount;
  public bool CanUndo => Undo.UndoCount > 0;
  public bool CanRedo => Undo.RedoCount > 0;

  public int SelectedSlot
  {
    get => _selectedSlot;
    set
    {
      var clamped = Math.Clamp(value, 0, SfxSlotCount - 1);
      if (_selectedSlot == clamped) return;
      SaveCurrentSlot();
      _selectedSlot = clamped;
      LoadSlotFromBus(_selectedSlot);
      ClearSelection();
      Notify();
      Notify(nameof(SlotName));
      RefreshRequested?.Invoke();
    }
  }

  public string SlotName
  {
    get => _slotNames.Length == 0
      ? ""
      : _slotNames[Math.Clamp(_selectedSlot, 0, _slotNames.Length - 1)];
    set
    {
      if (_slotNames.Length == 0) return;
      _slotNames[Math.Clamp(_selectedSlot, 0, _slotNames.Length - 1)] =
        (value ?? "").Length > 8 ? value![..8] : value ?? "";
      Notify();
    }
  }

  public int SelectedChannel
  {
    get => _selectedChannel;
    set { _selectedChannel = Math.Clamp(value, 0, ChannelCount - 1); Notify(); }
  }

  public int CursorStep
  {
    get => _cursorStep;
    set
    {
      _cursorStep = Math.Clamp(value, 0, NotesPerSfx - 1);
      Notify();
      Notify(nameof(StatusMessage));
      RefreshRequested?.Invoke();
    }
  }

  public int CursorRow
  {
    get => _cursorRow;
    set
    {
      _cursorRow = Math.Clamp(value, 0, SfxPitch.PitchNames.Count - 1);
      Notify();
      RefreshRequested?.Invoke();
    }
  }

  public SfxEditorTool Tool
  {
    get => _tool;
    set { _tool = value; Notify(); RefreshRequested?.Invoke(); }
  }

  public int DefaultWaveform
  {
    get => _defaultWaveform;
    set { _defaultWaveform = value & AudioEditorLimits.MaxInstrument; Notify(); }
  }

  public int DefaultVolume
  {
    get => _defaultVolume;
    set { _defaultVolume = Math.Clamp(value, 0, AudioEditorLimits.MaxVolume); Notify(); }
  }

  public SfxEffect DefaultEffect
  {
    get => _defaultEffect;
    set { _defaultEffect = value; Notify(); }
  }

  public int DefaultEffectParam
  {
    get => _defaultEffectParam;
    set { _defaultEffectParam = Math.Clamp(value, 0, 0xFFFF); Notify(); }
  }

  public int Speed
  {
    get => Document.Speed;
    set
    {
      if (Document.Speed == value) return;
      ApplyMutation("Speed", doc => doc.Speed = Math.Max(1, value));
    }
  }

  public int LoopStart
  {
    get => Document.LoopStart;
    set
    {
      var v = Math.Clamp(value, 0, NotesPerSfx - 1);
      if (Document.LoopStart == v) return;
      ApplyMutation("Loop", doc =>
      {
        doc.LoopStart = v;
        if (doc.LoopEnd < v) doc.LoopEnd = v;
      });
    }
  }

  public int LoopEnd
  {
    get => Document.LoopEnd;
    set
    {
      var v = Math.Clamp(value, 0, NotesPerSfx - 1);
      if (Document.LoopEnd == v) return;
      ApplyMutation("Loop", doc =>
      {
        doc.LoopEnd = v;
        if (doc.LoopStart > v) doc.LoopStart = v;
      });
    }
  }

  public SfxFilters Filters
  {
    get => Document.Filters;
    set
    {
      if (Document.Filters == value) return;
      ApplyMutation("Filters", doc => doc.Filters = value);
    }
  }

  public bool IsPlaying
  {
    get => _isPlaying;
    private set { _isPlaying = value; Notify(); Notify(nameof(IsStopped)); }
  }

  public bool IsStopped => !IsPlaying;
  public bool LoopPlayback
  {
    get => _loopPlayback;
    set { _loopPlayback = value; Notify(); }
  }

  public int ZoomLevel
  {
    get => _zoomLevel;
    set
    {
      _zoomLevel = Math.Clamp(value, 1, 4);
      Notify();
      Notify(nameof(CellWidth));
      RefreshRequested?.Invoke();
    }
  }

  public int CellWidth => 16 + (_zoomLevel - 1) * 8;

  public string StatusMessage
  {
    get => BuildStatusMessage();
    set { _statusMessage = value; Notify(); }
  }

  public IReadOnlyCollection<int> SelectedSteps => _selectedSteps;

  public void SetProfile(HardwareProfile profile)
  {
    _profile = profile;
    ApplyProfileLimits(reloadFromBus: _bus != null);
  }

  public void SetChipBus(ChipBus bus)
  {
    if (_bus != null)
      SaveCurrentSlot();
    _bus = bus;
    _profile = bus.Profile;
    ApplyProfileLimits(reloadFromBus: true);
  }

  private void ApplyProfileLimits(bool reloadFromBus)
  {
    var prevSlot = _selectedSlot;
    var prevChannel = _selectedChannel;

    Document.SetProfile(_profile);
    ResizeSlotNames(SfxSlotCount);

    _selectedSlot = Math.Clamp(prevSlot, 0, SfxSlotCount - 1);
    _selectedChannel = Math.Clamp(prevChannel, 0, ChannelCount - 1);
    _cursorStep = Math.Clamp(_cursorStep, 0, NotesPerSfx - 1);

    Notify(nameof(SfxSlotCount));
    Notify(nameof(NotesPerSfx));
    Notify(nameof(ChannelCount));
    Notify(nameof(SelectedSlot));
    Notify(nameof(SelectedChannel));
    Notify(nameof(CursorStep));
    Notify(nameof(LoopStart));
    Notify(nameof(LoopEnd));
    Notify(nameof(SlotName));

    if (reloadFromBus)
      LoadSlotFromBus(_selectedSlot);

    RefreshRequested?.Invoke();
  }

  private void ResizeSlotNames(int count)
  {
    var next = new string[count];
    Array.Copy(_slotNames, next, Math.Min(_slotNames.Length, count));
    _slotNames = next;
  }

  public void RefreshFromBus()
  {
    if (_bus == null) return;
    LoadSlotFromBus(_selectedSlot);
    RefreshRequested?.Invoke();
  }

  public SfxNote GetNote(int step) => Document.GetNote(step);
  public bool HasNoteAt(int step, int midiPitch) => Document.HasNoteAt(step, midiPitch);
  public bool IsStepEmpty(int step) => Document.IsStepEmpty(step);
  public bool IsStepSelected(int step) => _selectedSteps.Contains(step);

  public int GetPlayheadColumn()
  {
    if (!IsPlaying || _bus == null) return -1;
    var ch = _bus.Sound.Engine.Channels[SelectedChannel];
    return ch.IsPlaying ? ch.CurrentNoteIndex : -1;
  }

  public void TickPlayback()
  {
    if (!IsPlaying || _bus == null) return;
    var ch = _bus.Sound.Engine.Channels[SelectedChannel];
    if (!ch.IsPlaying)
    {
      IsPlaying = false;
      _statusMessage = "Playback finished.";
      Notify(nameof(StatusMessage));
    }

    Notify(nameof(PlayheadColumn));
    RefreshRequested?.Invoke();
  }

  public int PlayheadColumn => GetPlayheadColumn();

  public void SelectStep(int step, bool additive = false)
  {
    step = Math.Clamp(step, 0, NotesPerSfx - 1);
    if (!additive)
      _selectedSteps.Clear();
    _selectedSteps.Add(step);
    CursorStep = step;
    var note = Document.GetNote(step);
    if (!note.IsEmpty)
      CursorRow = SfxPitch.MidiToRow(note.Pitch);
    NotifySelection();
  }

  public void ClearSelection()
  {
    _selectedSteps.Clear();
    NotifySelection();
  }

  public void SelectRange(int fromStep, int toStep)
  {
    _selectedSteps.Clear();
    int a = Math.Min(fromStep, toStep);
    int b = Math.Max(fromStep, toStep);
    for (int i = a; i <= b; i++)
      _selectedSteps.Add(i);
    NotifySelection();
  }

  public void SelectAll()
  {
    _selectedSteps.Clear();
    for (int i = 0; i < NotesPerSfx; i++)
      _selectedSteps.Add(i);
    NotifySelection();
  }

  public void PlaceNoteAtCursor()
  {
    PlaceNote(CursorStep, SfxPitch.RowToMidi(CursorRow));
  }

  public void PlaceNote(int step, int midiPitch)
  {
    step = Math.Clamp(step, 0, NotesPerSfx - 1);
    var current = Document.GetNote(step);
    SfxNote after;
    if (!current.IsEmpty && current.Pitch == midiPitch)
      after = new SfxNote();
    else
      after = new SfxNote(midiPitch, DefaultWaveform, DefaultVolume, DefaultEffect, DefaultEffectParam);

    ApplyNoteMutation(step, current, after, "Place note");
    CursorStep = step;
    CursorRow = SfxPitch.MidiToRow(midiPitch);
    PreviewNote(after);
  }

  public void DeleteAtCursor()
  {
    DeleteNote(CursorStep);
  }

  public void DeleteNote(int step)
  {
    step = Math.Clamp(step, 0, NotesPerSfx - 1);
    var current = Document.GetNote(step);
    if (current.IsEmpty) return;
    ApplyNoteMutation(step, current, new SfxNote(), "Delete note");
  }

  public void ClearSlot()
  {
  var before = Document.CaptureSnapshot();
    Document.ClearNotes();
    CommitSnapshot(before, Document.CaptureSnapshot(), "Clear slot");
  }

  public void FillFromCursor()
  {
    var before = Document.CaptureSnapshot();
    int start = CursorStep;
    int end = NotesPerSfx - 1;
    for (int i = start + 1; i < NotesPerSfx; i++)
    {
      if (!Document.IsStepEmpty(i))
      {
        end = i - 1;
        break;
      }
    }

    var note = Document.GetNote(start);
    if (note.IsEmpty)
      note = new SfxNote(SfxPitch.RowToMidi(CursorRow), DefaultWaveform, DefaultVolume, DefaultEffect, DefaultEffectParam);

    for (int i = start; i <= end; i++)
      Document.SetNote(i, note);

    CommitSnapshot(before, Document.CaptureSnapshot(), "Fill");
  }

  private List<int> GetActiveSteps()
  {
    if (_selectedSteps.Count > 0)
      return _selectedSteps.ToList();
    return new List<int> { CursorStep };
  }

  public void CopySelection()
  {
    var steps = GetActiveSteps();
    Clipboard.Copy(steps, Document);
    _statusMessage = "Copied selection.";
    Notify(nameof(StatusMessage));
  }

  public void CutSelection()
  {
    CopySelection();
    var before = Document.CaptureSnapshot();
    foreach (var step in GetActiveSteps())
      Document.SetNote(step, new SfxNote());
    CommitSnapshot(before, Document.CaptureSnapshot(), "Cut");
  }

  public void PasteAtCursor()
  {
    Clipboard.PasteAt(CursorStep, Document, (before, after) => CommitSnapshot(before, after, "Paste"));
  }

  public void DuplicateSelection()
  {
    var steps = GetActiveSteps().OrderBy(s => s).ToList();
    if (steps.Count == 0) return;
    int width = steps[^1] - steps[0] + 1;
    int target = steps[^1] + 1;
    if (target + width > NotesPerSfx)
      target = Math.Max(0, NotesPerSfx - width);

    var before = Document.CaptureSnapshot();
    for (int i = 0; i < steps.Count; i++)
    {
      int dst = target + (steps[i] - steps[0]);
      if (dst < 0 || dst >= NotesPerSfx) continue;
      Document.SetNote(dst, Document.GetNote(steps[i]));
    }

    CommitSnapshot(before, Document.CaptureSnapshot(), "Duplicate");
    SelectRange(target, target + width - 1);
  }

  public void TransposeSelection(int semitones)
  {
    var steps = GetActiveSteps();
    if (steps.Count == 0) return;
    var before = Document.CaptureSnapshot();
    foreach (var step in steps)
    {
      var note = Document.GetNote(step);
      if (note.IsEmpty) continue;
      int newPitch = Math.Clamp(note.Pitch + semitones, 1, 127);
      Document.SetNote(step, new SfxNote(newPitch, note.Instrument, note.Volume, note.Effect, note.EffectParam, note.CustomInst));
    }

    CommitSnapshot(before, Document.CaptureSnapshot(), "Transpose");
  }

  public void ShiftSelection(int deltaSteps)
  {
    var steps = GetActiveSteps().OrderByDescending(s => s).ToList();
    if (steps.Count == 0) return;
    var before = Document.CaptureSnapshot();
    var notes = steps.Select(s => (s, Document.GetNote(s))).ToList();
    foreach (var (step, _) in notes)
      Document.SetNote(step, new SfxNote());

    foreach (var (step, note) in notes)
    {
      int dst = step + deltaSteps;
      if (dst < 0 || dst >= NotesPerSfx || note.IsEmpty) continue;
      Document.SetNote(dst, note);
    }

    CommitSnapshot(before, Document.CaptureSnapshot(), "Shift");
    _selectedSteps.Clear();
    foreach (var (step, note) in notes)
    {
      int dst = step + deltaSteps;
      if (dst >= 0 && dst < NotesPerSfx && !note.IsEmpty)
        _selectedSteps.Add(dst);
    }

    NotifySelection();
  }

  public void SetNoteProperty(int step, int? waveform = null, int? volume = null, SfxEffect? effect = null, int? effectParam = null)
  {
    step = Math.Clamp(step, 0, NotesPerSfx - 1);
    var n = Document.GetNote(step);
    if (n.IsEmpty) return;
    var after = new SfxNote(
      n.Pitch,
      waveform ?? n.Instrument,
      volume ?? n.Volume,
      effect ?? n.Effect,
      effectParam ?? n.EffectParam,
      n.CustomInst);
    ApplyNoteMutation(step, n, after, "Edit note");
  }

  public void ApplyWaveformToSelection(int waveform)
  {
    DefaultWaveform = waveform;
    var steps = GetActiveSteps();
    var before = Document.CaptureSnapshot();
    foreach (var step in steps)
    {
      var n = Document.GetNote(step);
      if (n.IsEmpty) continue;
      Document.SetNote(step, new SfxNote(n.Pitch, waveform, n.Volume, n.Effect, n.EffectParam, n.CustomInst));
    }

    CommitSnapshot(before, Document.CaptureSnapshot(), "Waveform");
  }

  public void CycleEffect(bool backward = false)
  {
    var steps = GetActiveSteps();
    var before = Document.CaptureSnapshot();
    foreach (var step in steps)
    {
      var n = Document.GetNote(step);
      if (n.IsEmpty) continue;
      int fx = (int)n.Effect;
      fx = backward ? (fx + 7) % 8 : (fx + 1) % 8;
      Document.SetNote(step, new SfxNote(n.Pitch, n.Instrument, n.Volume, (SfxEffect)fx, n.EffectParam, n.CustomInst));
    }

    CommitSnapshot(before, Document.CaptureSnapshot(), "Effect");
  }

  public void AdjustVolumeAtCursor(int delta)
  {
    var step = CursorStep;
    var n = Document.GetNote(step);
    if (n.IsEmpty) return;
    SetNoteProperty(step, volume: Math.Clamp(n.Volume + delta, 0, AudioEditorLimits.MaxVolume));
  }

  public void Play(bool fromCursor = false)
  {
    if (_bus == null) return;
    SaveCurrentSlot();
    _bus.Sound.LoadSlotToChannel(SelectedSlot, SelectedChannel);
    _bus.Sound.Engine.SetSfxLoop(SelectedChannel, LoopStart, LoopEnd);
    int offset = fromCursor ? CursorStep : 0;
    _bus.Sound.Sfx(SelectedSlot, SelectedChannel, offset);
    IsPlaying = true;
    _statusMessage = $"Playing slot {SelectedSlot:D2}...";
    Notify(nameof(StatusMessage));
    RefreshRequested?.Invoke();
  }

  public void Stop()
  {
    _bus?.Sound.Sfx(-1, SelectedChannel);
    IsPlaying = false;
    _statusMessage = "Stopped.";
    Notify(nameof(StatusMessage));
    RefreshRequested?.Invoke();
  }

  public void TogglePlayStop(bool fromCursor = false)
  {
    if (IsPlaying) Stop();
    else Play(fromCursor);
  }

  public void PreviewPitch(int midiPitch)
  {
    if (_bus == null) return;
    var note = new SfxNote(midiPitch, DefaultWaveform, DefaultVolume, DefaultEffect, DefaultEffectParam);
    _bus.Sound.PreviewNote(SelectedChannel, note, Speed);
  }

  public void PreviewNote(SfxNote note)
  {
    if (_bus == null || note.IsEmpty) return;
    _bus.Sound.PreviewNote(SelectedChannel, note, Speed);
  }

  public bool HandleKey(string key, bool ctrl, bool shift, bool alt)
  {
    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.Undo))
      return Undo.Undo() && AfterCommand();
    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.Redo))
      return Undo.Redo() && AfterCommand();

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.PlayFromCursor))
    {
      TogglePlayStop(fromCursor: true);
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.PlayStop))
    {
      TogglePlayStop();
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.Stop))
    {
      if (IsPlaying) Stop();
      else ClearSelection();
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.ToggleLoop))
    {
      LoopPlayback = !LoopPlayback;
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.PrevSlot))
    {
      SelectedSlot--;
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.NextSlot))
    {
      SelectedSlot++;
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.MoveLeft))
    {
      CursorStep--;
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.MoveRight))
    {
      CursorStep++;
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.MoveUp))
    {
      CursorRow--;
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.MoveDown))
    {
      CursorRow++;
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.Home))
    {
      CursorStep = 0;
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.End))
    {
      CursorStep = NotesPerSfx - 1;
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.SelectAll))
    {
      SelectAll();
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.Copy))
    {
      CopySelection();
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.Cut))
    {
      CutSelection();
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.Paste))
    {
      PasteAtCursor();
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.Duplicate))
    {
      DuplicateSelection();
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.Delete))
    {
      if (_selectedSteps.Count > 0)
      {
        var before = Document.CaptureSnapshot();
        foreach (var step in _selectedSteps)
          Document.SetNote(step, new SfxNote());
        CommitSnapshot(before, Document.CaptureSnapshot(), "Delete");
      }
      else DeleteAtCursor();
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.ClearSlot))
    {
      ClearSlot();
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.TransposeUp))
    {
      TransposeSelection(1);
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.TransposeDown))
    {
      TransposeSelection(-1);
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.TransposeOctaveUp))
    {
      TransposeSelection(12);
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.TransposeOctaveDown))
    {
      TransposeSelection(-12);
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.ShiftRight))
    {
      ShiftSelection(1);
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.ShiftLeft))
    {
      ShiftSelection(-1);
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.ZoomIn))
    {
      ZoomLevel++;
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.ZoomOut))
    {
      ZoomLevel--;
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.ZoomFit))
    {
      ZoomLevel = 2;
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.ToolPencil))
    {
      Tool = SfxEditorTool.Pencil;
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.ToolEraser))
    {
      Tool = SfxEditorTool.Eraser;
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.ToolSelect))
    {
      Tool = SfxEditorTool.Select;
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.ToolFill))
    {
      Tool = SfxEditorTool.Fill;
      FillFromCursor();
      return true;
    }

    for (int i = 0; i < 8; i++)
    {
      if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.Waveform1 + i))
      {
        ApplyWaveformToSelection(i);
        return true;
      }
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.CycleEffect))
    {
      CycleEffect();
      return true;
    }

    if (SfxEditorInputMap.Matches(key, ctrl, shift, alt, SfxEditorAction.CycleEffectBack))
    {
      CycleEffect(backward: true);
      return true;
    }

    if (key.Equals("Return", StringComparison.OrdinalIgnoreCase))
    {
      PlaceNoteAtCursor();
      return true;
    }

    if (key.Equals("Back", StringComparison.OrdinalIgnoreCase))
    {
      if (_selectedSteps.Count > 0)
      {
        var before = Document.CaptureSnapshot();
        foreach (var step in _selectedSteps)
          Document.SetNote(step, new SfxNote());
        CommitSnapshot(before, Document.CaptureSnapshot(), "Delete");
      }
      else DeleteAtCursor();
      return true;
    }

    return false;
  }

  public bool HandleTextInput(string text)
  {
    if (text is "+" or "=")
    {
      ZoomLevel++;
      return true;
    }

    if (text is "-")
    {
      ZoomLevel--;
      return true;
    }

  if (text.Length == 1 && text[0] is >= '1' and <= '8' && char.IsDigit(text[0]))
    {
      ApplyWaveformToSelection(text[0] - '1');
      return true;
    }

    return false;
  }

  private void ApplyMutation(string label, Action<SfxDocument> mutate)
  {
    var before = Document.CaptureSnapshot();
    mutate(Document);
    CommitSnapshot(before, Document.CaptureSnapshot(), label);
  }

  private void ApplyNoteMutation(int step, SfxNote beforeNote, SfxNote afterNote, string label)
  {
    if (beforeNote.ToRaw() == afterNote.ToRaw()) return;
    var before = Document.CaptureSnapshot();
    Document.SetNote(step, afterNote);
    CommitSnapshot(before, Document.CaptureSnapshot(), label);
  }

  private void CommitSnapshot(SfxSlotSnapshot before, SfxSlotSnapshot after, string label)
  {
    if (SnapshotsEqual(before, after)) return;
    Undo.Execute(new SfxSlotSnapshotCommand(Document, before, after));
    SaveCurrentSlot();
    MarkEdited();
    RefreshRequested?.Invoke();
  }

  private bool AfterCommand()
  {
    SaveCurrentSlot();
    MarkEdited();
    RefreshRequested?.Invoke();
    return true;
  }

  private static bool SnapshotsEqual(SfxSlotSnapshot a, SfxSlotSnapshot b)
  {
    if (a.Speed != b.Speed || a.LoopStart != b.LoopStart || a.LoopEnd != b.LoopEnd || a.Filters != b.Filters)
      return false;
    if (a.Notes.Length != b.Notes.Length) return false;
    for (int i = 0; i < a.Notes.Length; i++)
    {
      if (a.Notes[i].ToRaw() != b.Notes[i].ToRaw())
        return false;
    }

    return true;
  }

  private void SaveCurrentSlot()
  {
    if (_bus == null) return;
    _bus.Sound.SetSlot(SelectedSlot, Document.Notes, Document.Speed, Document.LoopStart, Document.LoopEnd, Document.Filters);
  }

  private void LoadSlotFromBus(int slot)
  {
    if (_bus == null) return;
    var notes = _bus.Sound.GetSlot(slot);
    var loop = _bus.Sound.GetSlotLoop(slot);
    Document.Restore(
      notes,
      _bus.Sound.GetSlotSpeed(slot),
      loop.start,
      loop.end,
      _bus.Sound.GetSlotFilters(slot));
    Notify(nameof(Speed));
    Notify(nameof(LoopStart));
    Notify(nameof(LoopEnd));
    Notify(nameof(Filters));
  }

  private void MarkEdited() => Edited?.Invoke();

  private string BuildStatusMessage()
  {
    var note = Document.GetNote(CursorStep);
    if (note.IsEmpty)
      return "Click or press Enter to place a note.";
    return
      $"Step {CursorStep:X2} · {SfxPitch.NoteToPitchName(note.Pitch)} · {SfxTheme.WaveformNames[note.Instrument & 7]} · Vol {note.Volume} · {SfxTheme.EffectNames[(int)note.Effect]} | Slot {SelectedSlot:D2} · Spd {Speed}";
  }

  private void NotifySelection()
  {
    Notify(nameof(SelectedSteps));
    Notify(nameof(StatusMessage));
    RefreshRequested?.Invoke();
  }

  private void Notify([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
