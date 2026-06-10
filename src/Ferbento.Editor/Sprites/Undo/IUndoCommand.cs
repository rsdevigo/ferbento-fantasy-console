namespace Ferbento.Editor.Sprites.Undo;

public interface IUndoCommand
{
    string Name { get; }
    void Execute();
    void Undo();
}
