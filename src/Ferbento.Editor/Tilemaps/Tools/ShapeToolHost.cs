using Ferbento.Editor.Tilemaps.Undo;

namespace Ferbento.Editor.Tilemaps.Tools;

public enum TileShapeMode { Line, Rect, Circle }

public sealed class ShapeToolHost : ITilemapTool
{
    public string Id => "shape";
    public TileShapeMode Mode { get; set; } = TileShapeMode.Rect;

    public void CycleMode() =>
        Mode = Mode switch
        {
            TileShapeMode.Line => TileShapeMode.Rect,
            TileShapeMode.Rect => TileShapeMode.Circle,
            _ => TileShapeMode.Line,
        };

    public void OnPointerDown(TilemapEditorContext ctx, TilePointerInput input)
    {
        if (!PaintTool.InMap(ctx, input.TileX, input.TileY)) return;
        ctx.DragStart = (input.TileX, input.TileY);
    }

    public void OnPointerMove(TilemapEditorContext ctx, TilePointerInput input) { }

    public void OnPointerUp(TilemapEditorContext ctx, TilePointerInput input)
    {
        if (ctx.DragStart is not { } start) return;
        ApplyShape(ctx, start.x, start.y, input.TileX, input.TileY);
        ctx.DragStart = null;
    }

    private void ApplyShape(TilemapEditorContext ctx, int x0, int y0, int x1, int y1)
    {
        var doc = ctx.Document;
        var left = Math.Min(x0, x1);
        var right = Math.Max(x0, x1);
        var top = Math.Min(y0, y1);
        var bottom = Math.Max(y0, y1);
        var w = right - left + 1;
        var h = bottom - top + 1;
        if (w <= 0 || h <= 0) return;

        var before = doc.ReadRegion(left, top, w, h);
        var after = (byte[])before.Clone();

        for (var y = top; y <= bottom; y++)
        for (var x = left; x <= right; x++)
        {
            if (!ShouldFill(Mode, x, y, x0, y0, x1, y1)) continue;
            var lx = x - left;
            var ly = y - top;
            after[ly * w + lx] = (byte)ctx.SelectedTile;
            doc.SetTile(x, y, ctx.SelectedTile, ctx.CurrentLayer);
        }

        ctx.Undo.Execute(new MapRegionCommand(doc, left, top, w, h, before, after, ctx.CurrentLayer));
        ctx.NotifyEdited?.Invoke();
    }

    private static bool ShouldFill(TileShapeMode mode, int x, int y, int x0, int y0, int x1, int y1)
    {
        return mode switch
        {
            TileShapeMode.Line => OnLine(x, y, x0, y0, x1, y1),
            TileShapeMode.Rect => true,
            TileShapeMode.Circle => InCircle(x, y, x0, y0, x1, y1),
            _ => false,
        };
    }

    private static bool OnLine(int x, int y, int x0, int y0, int x1, int y1)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;
        var cx = x0;
        var cy = y0;
        while (true)
        {
            if (cx == x && cy == y) return true;
            if (cx == x1 && cy == y1) break;
            var e2 = 2 * err;
            if (e2 > -dy) { err -= dy; cx += sx; }
            if (e2 < dx) { err += dx; cy += sy; }
        }
        return x == x1 && y == y1;
    }

    private static bool InCircle(int x, int y, int x0, int y0, int x1, int y1)
    {
        var cx = (x0 + x1) / 2.0;
        var cy = (y0 + y1) / 2.0;
        var rx = Math.Abs(x1 - x0) / 2.0 + 0.5;
        var ry = Math.Abs(y1 - y0) / 2.0 + 0.5;
        if (rx < 0.5) rx = 0.5;
        if (ry < 0.5) ry = 0.5;
        var nx = (x - cx) / rx;
        var ny = (y - cy) / ry;
        return nx * nx + ny * ny <= 1.0;
    }
}
