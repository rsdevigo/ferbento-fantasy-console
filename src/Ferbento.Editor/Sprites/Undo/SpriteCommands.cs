namespace Ferbento.Editor.Sprites.Undo;

public sealed class SpritePatchCommand : IUndoCommand
{
    private readonly SpriteDocument _doc;
    private readonly int _spriteIndex;
    private readonly byte[] _before;
    private readonly byte[] _after;
    private readonly int _beforeFlags;
    private readonly int _afterFlags;

    public string Name { get; }

    public SpritePatchCommand(
        SpriteDocument doc, int spriteIndex, string name,
        byte[] before, byte[] after, int beforeFlags, int afterFlags)
    {
        _doc = doc;
        _spriteIndex = spriteIndex;
        Name = name;
        _before = (byte[])before.Clone();
        _after = (byte[])after.Clone();
        _beforeFlags = beforeFlags;
        _afterFlags = afterFlags;
    }

    public void Execute()
    {
        _doc.SetSpritePixels(_spriteIndex, _after);
        _doc.SetFlags(_spriteIndex, _afterFlags);
    }

    public void Undo()
    {
        _doc.SetSpritePixels(_spriteIndex, _before);
        _doc.SetFlags(_spriteIndex, _beforeFlags);
    }
}

public sealed class MultiSpritePatchCommand : IUndoCommand
{
    private readonly SpriteDocument _doc;
    private readonly List<(int index, byte[] before, byte[] after, int beforeFlags, int afterFlags)> _patches;
    public string Name { get; }

    public MultiSpritePatchCommand(SpriteDocument doc, string name)
    {
        _doc = doc;
        Name = name;
        _patches = [];
    }

    public void AddPatch(int index, byte[] before, byte[] after, int beforeFlags, int afterFlags) =>
        _patches.Add((index, (byte[])before.Clone(), (byte[])after.Clone(), beforeFlags, afterFlags));

    public void Execute()
    {
        foreach (var p in _patches)
        {
            _doc.SetSpritePixels(p.index, p.after);
            _doc.SetFlags(p.index, p.afterFlags);
        }
    }

    public void Undo()
    {
        foreach (var p in _patches)
        {
            _doc.SetSpritePixels(p.index, p.before);
            _doc.SetFlags(p.index, p.beforeFlags);
        }
    }
}

public sealed class LocalRegionCommand : IUndoCommand
{
    private readonly SpriteDocument _doc;
    private readonly int _localX;
    private readonly int _localY;
    private readonly int _width;
    private readonly int _height;
    private readonly byte[] _before;
    private readonly byte[] _after;
    public string Name { get; }

    public LocalRegionCommand(
        SpriteDocument doc, string name,
        int localX, int localY, int width, int height,
        byte[] before, byte[] after)
    {
        _doc = doc;
        Name = name;
        _localX = localX;
        _localY = localY;
        _width = width;
        _height = height;
        _before = (byte[])before.Clone();
        _after = (byte[])after.Clone();
    }

    public void Execute() => _doc.WriteLocalRegion(_localX, _localY, _width, _height, _after);
    public void Undo() => _doc.WriteLocalRegion(_localX, _localY, _width, _height, _before);
}

public sealed class DrawStrokeCommand : IUndoCommand
{
    private readonly SpriteDocument _doc;
    private readonly byte[] _before;
    private byte[] _after;
    public string Name => "draw";

    public DrawStrokeCommand(SpriteDocument doc)
    {
        _doc = doc;
        _before = doc.ReadViewPixels();
        _after = (byte[])_before.Clone();
    }

    public void CaptureAfter()
    {
        var current = _doc.ReadViewPixels();
        if (current.Length == _after.Length)
            Array.Copy(current, _after, current.Length);
        else
            _after = current;
    }

    public void Execute() => _doc.WriteViewPixels(_after);
    public void Undo() => _doc.WriteViewPixels(_before);
}
