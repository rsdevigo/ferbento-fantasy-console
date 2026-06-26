namespace Ferbento.Editor.Music.Undo;

public sealed class MusicCellCommand : IMusicUndoCommand
{
    private readonly MusicDocument _doc;
    private readonly int _pattern;
    private readonly int _row;
    private readonly int _channel;
    private readonly int _before;
    private readonly int _after;

    public string Name { get; }

    public MusicCellCommand(MusicDocument doc, int pattern, int row, int channel,
        int before, int after, string name = "Edit note")
    {
        _doc = doc;
        _pattern = pattern;
        _row = row;
        _channel = channel;
        _before = before;
        _after = after;
        Name = name;
    }

    public void Execute() => _doc.SetCell(_pattern, _row, _channel, _after);
    public void Undo() => _doc.SetCell(_pattern, _row, _channel, _before);
}

public sealed class MusicPatternReplaceCommand : IMusicUndoCommand
{
    private readonly MusicDocument _doc;
    private readonly int _pattern;
    private readonly int[] _before;
    private readonly int[] _after;

    public string Name { get; }

    public MusicPatternReplaceCommand(MusicDocument doc, int pattern, int[] after, string name = "Edit pattern")
    {
        _doc = doc;
        _pattern = pattern;
        _before = doc.GetPatternCopy(pattern);
        _after = (int[])after.Clone();
        Name = name;
    }

    public void Execute() => _doc.SetPattern(_pattern, _after);
    public void Undo() => _doc.SetPattern(_pattern, _before);
}

public sealed class MusicPatternClearCommand : IMusicUndoCommand
{
    private readonly MusicPatternReplaceCommand _inner;

    public string Name => "Clear pattern";

    public MusicPatternClearCommand(MusicDocument doc, int pattern)
    {
        _inner = new MusicPatternReplaceCommand(doc, pattern, doc.CreateEmptyPattern(), "Clear pattern");
    }

    public void Execute() => _inner.Execute();
    public void Undo() => _inner.Undo();
}
