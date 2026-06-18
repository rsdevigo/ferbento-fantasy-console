using Ferbento.Editor.Sprites.Undo;

namespace Ferbento.Editor.Tilemaps.Undo;

public sealed class MapRegionCommand : IUndoCommand
{
    private readonly MapDocument _doc;
    private readonly int _x;
    private readonly int _y;
    private readonly int _w;
    private readonly int _h;
    private readonly byte[] _before;
    private readonly byte[] _after;
    private readonly bool[]? _beforePlaced;
    private readonly bool[]? _afterPlaced;
    private readonly int _layer;

    public string Name { get; }

    public MapRegionCommand(
        MapDocument doc, int x, int y, int w, int h,
        byte[] before, byte[] after, int layer,
        bool[]? beforePlaced = null, bool[]? afterPlaced = null)
    {
        _doc = doc;
        _x = x;
        _y = y;
        _w = w;
        _h = h;
        _before = (byte[])before.Clone();
        _after = (byte[])after.Clone();
        _beforePlaced = beforePlaced is null ? null : (bool[])beforePlaced.Clone();
        _afterPlaced = afterPlaced is null ? null : (bool[])afterPlaced.Clone();
        _layer = layer;
        Name = "map";
    }

    public void Execute()
    {
        _doc.WriteRegion(_x, _y, _w, _h, _after, _layer);
        if (_afterPlaced != null)
            _doc.ApplyPlacedRegion(_x, _y, _w, _h, _afterPlaced);
        else
            _doc.SyncPlacedFromTiles(_x, _y, _w, _h, _after);
    }

    public void Undo()
    {
        _doc.WriteRegion(_x, _y, _w, _h, _before, _layer, setLayerFlags: false);
        if (_beforePlaced != null)
            _doc.ApplyPlacedRegion(_x, _y, _w, _h, _beforePlaced);
        else
            _doc.SyncPlacedFromTiles(_x, _y, _w, _h, _before);
    }
}
