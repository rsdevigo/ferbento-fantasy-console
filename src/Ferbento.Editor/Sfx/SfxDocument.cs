using Ferbento.Core.Audio;
using Ferbento.Editor.Sfx.Undo;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Sfx;

public sealed class SfxDocument
{
  private SfxNote[] _notes = new SfxNote[32];

  public int NotesPerSfx { get; private set; } = 32;
  public int SfxSlotCount { get; private set; } = 64;
  public int ChannelCount { get; private set; } = 4;
  public int Speed { get; set; } = 4;
  public int LoopStart { get; set; }
  public int LoopEnd { get; set; } = 31;
  public SfxFilters Filters { get; set; }

  public SfxNote[] Notes => _notes;

  public void SetProfile(HardwareProfile profile)
  {
    var limits = AudioEditorLimits.FromProfile(profile);
    NotesPerSfx = limits.NotesPerSfx;
    SfxSlotCount = limits.SfxSlotCount;
    ChannelCount = limits.ChannelCount;
    if (_notes.Length != NotesPerSfx)
      _notes = new SfxNote[NotesPerSfx];
    LoopStart = Math.Clamp(LoopStart, 0, NotesPerSfx - 1);
    LoopEnd = Math.Clamp(LoopEnd, LoopStart, NotesPerSfx - 1);
  }

  public SfxNote GetNote(int step)
  {
    if (step < 0 || step >= NotesPerSfx) return new SfxNote();
    return _notes[step];
  }

  public void SetNote(int step, SfxNote note)
  {
    if (step < 0 || step >= NotesPerSfx) return;
    _notes[step] = note;
  }

  public bool IsStepEmpty(int step) => GetNote(step).IsEmpty;

  public bool HasNoteAt(int step, int midiPitch)
  {
    var note = GetNote(step);
    return !note.IsEmpty && note.Pitch == midiPitch;
  }

  public SfxSlotSnapshot CaptureSnapshot() =>
    new((SfxNote[])_notes.Clone(), Speed, LoopStart, LoopEnd, Filters);

  public void Restore(SfxNote[] notes, int speed, int loopStart, int loopEnd, SfxFilters filters)
  {
    int count = Math.Min(notes.Length, NotesPerSfx);
    Array.Clear(_notes, 0, _notes.Length);
    Array.Copy(notes, _notes, count);
    Speed = speed;
    LoopStart = Math.Clamp(loopStart, 0, NotesPerSfx - 1);
    LoopEnd = Math.Clamp(loopEnd, LoopStart, NotesPerSfx - 1);
    Filters = filters;
  }

  public void ClearNotes()
  {
    for (int i = 0; i < NotesPerSfx; i++)
      _notes[i] = new SfxNote();
  }
}
