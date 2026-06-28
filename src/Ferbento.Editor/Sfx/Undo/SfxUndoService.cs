namespace Ferbento.Editor.Sfx.Undo;

public sealed class SfxUndoService
{
  private readonly LinkedList<ISfxUndoCommand> _undoStack = new();
  private readonly LinkedList<ISfxUndoCommand> _redoStack = new();
  private readonly int _maxDepth;

  public int UndoCount => _undoStack.Count;
  public int RedoCount => _redoStack.Count;
  public event Action? StateChanged;

  public SfxUndoService(int maxDepth = 256) => _maxDepth = Math.Max(16, maxDepth);

  public void Execute(ISfxUndoCommand command)
  {
    command.Execute();
    _undoStack.AddLast(command);
    if (_undoStack.Count > _maxDepth)
      _undoStack.RemoveFirst();
    _redoStack.Clear();
    StateChanged?.Invoke();
  }

  public bool Undo()
  {
    if (_undoStack.Count == 0) return false;
    var node = _undoStack.Last!;
    _undoStack.RemoveLast();
    node.Value.Undo();
    _redoStack.AddLast(node.Value);
    StateChanged?.Invoke();
    return true;
  }

  public bool Redo()
  {
    if (_redoStack.Count == 0) return false;
    var node = _redoStack.Last!;
    _redoStack.RemoveLast();
    node.Value.Execute();
    _undoStack.AddLast(node.Value);
    StateChanged?.Invoke();
    return true;
  }

  public void Clear()
  {
    _undoStack.Clear();
    _redoStack.Clear();
    StateChanged?.Invoke();
  }
}
