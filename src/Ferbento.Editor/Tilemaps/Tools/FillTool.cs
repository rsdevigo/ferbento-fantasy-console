using Ferbento.Editor.Tilemaps.Undo;

namespace Ferbento.Editor.Tilemaps.Tools;

public sealed class FillTool : ITilemapTool
{
    public string Id => "fill";

    public void OnPointerDown(TilemapEditorContext ctx, TilePointerInput input)
    {
        if (!PaintTool.InMap(ctx, input.TileX, input.TileY)) return;
        FloodFill(ctx, input.TileX, input.TileY);
    }

    public void OnPointerMove(TilemapEditorContext ctx, TilePointerInput input) { }

    public void OnPointerUp(TilemapEditorContext ctx, TilePointerInput input) { }

    private static void FloodFill(TilemapEditorContext ctx, int startX, int startY)
    {
        var doc = ctx.Document;
        var target = doc.GetTile(startX, startY);
        if (target == ctx.SelectedTile) return;

        var w = doc.MapWidth;
        var h = doc.MapHeight;
        var before = doc.ReadRegion(0, 0, w, h);
        var stack = new Stack<(int x, int y)>();
        stack.Push((startX, startY));

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            if (x < 0 || y < 0 || x >= w || y >= h) continue;
            if (doc.GetTile(x, y) != target) continue;
            doc.SetTile(x, y, ctx.SelectedTile, ctx.CurrentLayer);
            stack.Push((x + 1, y));
            stack.Push((x - 1, y));
            stack.Push((x, y + 1));
            stack.Push((x, y - 1));
        }

        var after = doc.ReadRegion(0, 0, w, h);
        ctx.Undo.Execute(new MapRegionCommand(doc, 0, 0, w, h, before, after, ctx.CurrentLayer));
        ctx.NotifyEdited?.Invoke();
    }
}
