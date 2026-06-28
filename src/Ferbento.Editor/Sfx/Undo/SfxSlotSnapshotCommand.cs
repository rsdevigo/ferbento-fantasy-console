using Ferbento.Core.Audio;

namespace Ferbento.Editor.Sfx.Undo;

public readonly record struct SfxSlotSnapshot(
  SfxNote[] Notes,
  int Speed,
  int LoopStart,
  int LoopEnd,
  SfxFilters Filters);

public sealed class SfxSlotSnapshotCommand : ISfxUndoCommand
{
  private readonly SfxDocument _document;
  private readonly SfxSlotSnapshot _before;
  private readonly SfxSlotSnapshot _after;

  public SfxSlotSnapshotCommand(SfxDocument document, SfxSlotSnapshot before, SfxSlotSnapshot after)
  {
    _document = document;
    _before = Clone(before);
    _after = Clone(after);
  }

  public void Execute() => Apply(_after);
  public void Undo() => Apply(_before);

  private void Apply(SfxSlotSnapshot snapshot) =>
    _document.Restore(snapshot.Notes, snapshot.Speed, snapshot.LoopStart, snapshot.LoopEnd, snapshot.Filters);

  private static SfxSlotSnapshot Clone(SfxSlotSnapshot snapshot) =>
    new((SfxNote[])snapshot.Notes.Clone(), snapshot.Speed, snapshot.LoopStart, snapshot.LoopEnd, snapshot.Filters);
}
