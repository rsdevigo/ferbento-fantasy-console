namespace Ferbento.Editor.Music.Undo;

public sealed class MusicUndoService
{
    private readonly LinkedList<IMusicUndoCommand> _undoStack = new();
    private readonly LinkedList<IMusicUndoCommand> _redoStack = new();
    private readonly int _maxDepth;

    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;
    public event Action? StateChanged;

    public MusicUndoService(int maxDepth = 256) => _maxDepth = Math.Max(100, maxDepth);

    public void Execute(IMusicUndoCommand command)
    {
        command.Execute();
        _undoStack.AddLast(command);
        if (_undoStack.Count > _maxDepth)
            _undoStack.RemoveFirst();
        _redoStack.Clear();
        StateChanged?.Invoke();
    }

    public void PushExecuted(IMusicUndoCommand command)
    {
        _undoStack.AddLast(command);
        if (_undoStack.Count > _maxDepth)
            _undoStack.RemoveFirst();
        _redoStack.Clear();
        StateChanged?.Invoke();
    }

    public bool Undo()
    {
        if (_undoStack.Count == 0) return false;
        var cmd = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        cmd.Undo();
        _redoStack.AddLast(cmd);
        StateChanged?.Invoke();
        return true;
    }

    public bool Redo()
    {
        if (_redoStack.Count == 0) return false;
        var cmd = _redoStack.Last!.Value;
        _redoStack.RemoveLast();
        cmd.Execute();
        _undoStack.AddLast(cmd);
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
