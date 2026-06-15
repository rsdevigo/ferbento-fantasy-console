namespace Ferbento.Editor.Tilemaps.Tools;

public sealed class SelectionTool : ITilemapTool
{
    public string Id => "select";

    public void OnPointerDown(TilemapEditorContext ctx, TilePointerInput input)
    {
        if (!PaintTool.InMap(ctx, input.TileX, input.TileY)) return;
        ctx.DragStart = (input.TileX, input.TileY);
    }

    public void OnPointerMove(TilemapEditorContext ctx, TilePointerInput input)
    {
        if (ctx.DragStart is not { } start) return;
        var left = Math.Min(start.x, input.TileX);
        var top = Math.Min(start.y, input.TileY);
        var w = Math.Abs(input.TileX - start.x) + 1;
        var h = Math.Abs(input.TileY - start.y) + 1;
        ctx.Selection.SetMapRect(left, top, w, h);
    }

    public void OnPointerUp(TilemapEditorContext ctx, TilePointerInput input)
    {
        if (ctx.DragStart is not { } start)
        {
            ctx.DragStart = null;
            return;
        }

        var left = Math.Min(start.x, input.TileX);
        var top = Math.Min(start.y, input.TileY);
        var w = Math.Abs(input.TileX - start.x) + 1;
        var h = Math.Abs(input.TileY - start.y) + 1;
        ctx.Selection.SetMapRect(left, top, w, h);
        ctx.DragStart = null;
    }
}
