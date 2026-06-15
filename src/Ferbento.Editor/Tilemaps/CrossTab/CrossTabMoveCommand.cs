using Ferbento.Editor.Sprites;
using Ferbento.Editor.Sprites.Undo;

namespace Ferbento.Editor.Tilemaps.CrossTab;

public sealed class SheetRegionCommand : IUndoCommand
{
    private readonly SpriteDocument _doc;
    private readonly int _x;
    private readonly int _y;
    private readonly int _w;
    private readonly int _h;
    private readonly byte[] _before;
    private readonly byte[] _after;

    public string Name => "sprite-cut";

    public SheetRegionCommand(SpriteDocument doc, int x, int y, int w, int h, byte[] before, byte[] after)
    {
        _doc = doc;
        _x = x;
        _y = y;
        _w = w;
        _h = h;
        _before = (byte[])before.Clone();
        _after = (byte[])after.Clone();
    }

    public void Execute() => _doc.WriteRegion(_x, _y, _w, _h, _after);
    public void Undo() => _doc.WriteRegion(_x, _y, _w, _h, _before);
}

public sealed class CrossTabMoveCommand : IUndoCommand
{
    private readonly MapDocument _mapDoc;
    private readonly int _mx;
    private readonly int _my;
    private readonly int _mw;
    private readonly int _mh;
    private readonly byte[] _mapBefore;
    private readonly byte[] _mapAfter;
    private readonly int _layer;

    private readonly SpriteDocument _spriteDoc;
    private readonly int _cutX;
    private readonly int _cutY;
    private readonly int _cutW;
    private readonly int _cutH;
    private readonly byte[] _cutBefore;
    private readonly byte[] _cutAfter;

    private readonly int _destX;
    private readonly int _destY;
    private readonly int _destW;
    private readonly int _destH;
    private readonly byte[] _destBefore;
    private readonly byte[] _destAfter;

    public string Name => "map-assisted-move";

    public CrossTabMoveCommand(
        MapDocument mapDoc, int mx, int my, int mw, int mh, byte[] mapBefore, byte[] mapAfter, int layer,
        SpriteDocument spriteDoc,
        int cutX, int cutY, int cutW, int cutH, byte[] cutBefore, byte[] cutAfter,
        int destX, int destY, byte[] destBefore, byte[] destAfter)
    {
        _mapDoc = mapDoc;
        _mx = mx; _my = my; _mw = mw; _mh = mh;
        _mapBefore = mapBefore; _mapAfter = mapAfter; _layer = layer;
        _spriteDoc = spriteDoc;
        _cutX = cutX; _cutY = cutY; _cutW = cutW; _cutH = cutH;
        _cutBefore = cutBefore; _cutAfter = cutAfter;
        _destX = destX; _destY = destY;
        _destW = destAfter.Length > 0 ? cutW : cutW;
        _destH = destAfter.Length > 0 ? cutH : cutH;
        _destBefore = destBefore; _destAfter = destAfter;
    }

    public void Execute()
    {
        _mapDoc.WriteRegion(_mx, _my, _mw, _mh, _mapAfter, _layer, setLayerFlags: false);
        _spriteDoc.WriteRegion(_cutX, _cutY, _cutW, _cutH, _cutAfter);
        _spriteDoc.WriteRegion(_destX, _destY, _destW, _destH, _destAfter);
    }

    public void Undo()
    {
        _mapDoc.WriteRegion(_mx, _my, _mw, _mh, _mapBefore, _layer, setLayerFlags: false);
        _spriteDoc.WriteRegion(_destX, _destY, _destW, _destH, _destBefore);
        _spriteDoc.WriteRegion(_cutX, _cutY, _cutW, _cutH, _cutBefore);
    }
}
