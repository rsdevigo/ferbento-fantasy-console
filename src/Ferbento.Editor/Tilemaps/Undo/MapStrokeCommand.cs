using Ferbento.Editor.Sprites.Undo;

namespace Ferbento.Editor.Tilemaps.Undo;

public sealed class MapStrokeCommand : IUndoCommand
{
    private readonly MapDocument _doc;
    private readonly int _layer;
    private int _x;
    private int _y;
    private int _w;
    private int _h;
    private byte[] _before;
    private byte[] _after;

    public string Name => "paint";

    public MapStrokeCommand(MapDocument doc, int layer, int startX, int startY)
    {
        _doc = doc;
        _layer = layer;
        _x = startX;
        _y = startY;
        _w = 1;
        _h = 1;
        _before = doc.ReadRegion(startX, startY, 1, 1);
        _after = (byte[])_before.Clone();
    }

    public void IncludeTile(int tx, int ty)
    {
        if (tx >= _x && ty >= _y && tx < _x + _w && ty < _y + _h) return;

        var left = Math.Min(_x, tx);
        var top = Math.Min(_y, ty);
        var right = Math.Max(_x + _w - 1, tx);
        var bottom = Math.Max(_y + _h - 1, ty);
        var newW = right - left + 1;
        var newH = bottom - top + 1;
        var expanded = _doc.ReadRegion(left, top, newW, newH);

        for (var row = 0; row < _h; row++)
        for (var col = 0; col < _w; col++)
        {
            var srcIdx = row * _w + col;
            var dstX = (_x - left) + col;
            var dstY = (_y - top) + row;
            expanded[dstY * newW + dstX] = _before[srcIdx];
        }

        _x = left;
        _y = top;
        _w = newW;
        _h = newH;
        _before = expanded;
        _after = (byte[])_before.Clone();
    }

    public void CaptureAfter()
    {
        var current = _doc.ReadRegion(_x, _y, _w, _h);
        if (current.Length == _after.Length)
            Array.Copy(current, _after, current.Length);
        else
            _after = current;
    }

    public void Execute() => _doc.WriteRegion(_x, _y, _w, _h, _after, _layer);

    public void Undo() => _doc.WriteRegion(_x, _y, _w, _h, _before, _layer, setLayerFlags: false);
}
