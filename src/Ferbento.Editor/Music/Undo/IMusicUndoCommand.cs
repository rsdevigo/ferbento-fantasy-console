namespace Ferbento.Editor.Music.Undo;

public interface IMusicUndoCommand
{
    string Name { get; }
    void Execute();
    void Undo();
}
