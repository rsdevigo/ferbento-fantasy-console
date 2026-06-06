namespace Ferbento.Editor.ViewModels;

public sealed class UndoRedoEntry
{
    public required string Name { get; init; }
    public required byte[] Data { get; init; }
    public int Offset { get; init; }
    public int Length { get; init; }
}

public sealed class UndoRedoStack
{
    private readonly LinkedList<UndoRedoEntry> _undoStack = new();
    private readonly LinkedList<UndoRedoEntry> _redoStack = new();
    private readonly int _maxDepth;

    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;
    public event Action? StateChanged;

    public UndoRedoStack(int maxDepth = 256)
    {
        _maxDepth = maxDepth;
    }

    public UndoRedoEntry Push(string name, byte[] buffer, int offset, int length)
    {
        var snapshot = new byte[length];
        Array.Copy(buffer, offset, snapshot, 0, length);
        var entry = new UndoRedoEntry
        {
            Name = name,
            Data = snapshot,
            Offset = offset,
            Length = length,
        };

        _undoStack.AddLast(entry);
        if (_undoStack.Count > _maxDepth)
            _undoStack.RemoveFirst();

        _redoStack.Clear();
        StateChanged?.Invoke();
        return entry;
    }

    public UndoRedoEntry? Undo(byte[] currentBuffer, int offset, int length)
    {
        if (_undoStack.Count == 0) return null;

        var entry = _undoStack.Last!.Value;
        _undoStack.RemoveLast();

        var currentSnapshot = new byte[length];
        Array.Copy(currentBuffer, offset, currentSnapshot, 0, length);
        _redoStack.AddLast(new UndoRedoEntry
        {
            Name = entry.Name,
            Data = currentSnapshot,
            Offset = offset,
            Length = length,
        });

        StateChanged?.Invoke();
        return entry;
    }

    public UndoRedoEntry? Redo(byte[] currentBuffer, int offset, int length)
    {
        if (_redoStack.Count == 0) return null;

        var entry = _redoStack.Last!.Value;
        _redoStack.RemoveLast();

        var currentSnapshot = new byte[length];
        Array.Copy(currentBuffer, offset, currentSnapshot, 0, length);
        _undoStack.AddLast(new UndoRedoEntry
        {
            Name = entry.Name,
            Data = currentSnapshot,
            Offset = offset,
            Length = length,
        });

        StateChanged?.Invoke();
        return entry;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke();
    }
}
