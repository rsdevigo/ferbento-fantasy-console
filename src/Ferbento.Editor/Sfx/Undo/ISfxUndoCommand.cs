namespace Ferbento.Editor.Sfx.Undo;

public interface ISfxUndoCommand
{
  void Execute();
  void Undo();
}
