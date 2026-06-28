using Ferbento.Core.Audio;
using Ferbento.Editor.Sfx.Undo;

namespace Ferbento.Editor.Sfx;

public sealed class SfxClipboard
{
  public bool HasData { get; private set; }
  public int AnchorStep { get; private set; }
  private readonly List<(int stepOffset, SfxNote note)> _entries = new();

  public void Copy(IReadOnlyList<int> steps, SfxDocument document)
  {
    _entries.Clear();
    if (steps.Count == 0)
    {
      HasData = false;
      return;
    }

    AnchorStep = steps.Min();
    foreach (var step in steps.OrderBy(s => s))
    {
      var note = document.GetNote(step);
      if (note.IsEmpty) continue;
      _entries.Add((step - AnchorStep, note));
    }

    HasData = _entries.Count > 0;
  }

  public void PasteAt(int targetStep, SfxDocument document, Action<SfxSlotSnapshot, SfxSlotSnapshot> apply)
  {
    if (!HasData) return;
    var before = document.CaptureSnapshot();
    foreach (var (offset, note) in _entries)
    {
      int step = targetStep + offset;
      if (step < 0 || step >= document.NotesPerSfx) continue;
      document.SetNote(step, note);
    }

    apply(before, document.CaptureSnapshot());
  }
}
