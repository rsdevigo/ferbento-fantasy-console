using Ferbento.Editor.Tilemaps.Undo;

namespace Ferbento.Editor.Tilemaps.Tools;

public sealed class FillTool : ITilemapTool
{
    public string Id => "fill";

    public void OnPointerDown(TilemapEditorContext ctx, TilePointerInput input)
    {
        ctx.CancelStroke();
        if (!PaintTool.InMap(ctx, input.TileX, input.TileY)) return;
        FloodFill(ctx, input.TileX, input.TileY);
    }

    public void OnPointerMove(TilemapEditorContext ctx, TilePointerInput input) { }

    public void OnPointerUp(TilemapEditorContext ctx, TilePointerInput input) { }

    private static void FloodFill(TilemapEditorContext ctx, int startX, int startY)
    {
        var doc = ctx.Document;
        var targetTile = doc.GetTile(startX, startY);
        var targetPlaced = doc.IsCellPlaced(startX, startY);
        var selected = ctx.SelectedTile;

        if (targetTile == selected && (selected != 0 || targetPlaced))
            return;

        var w = doc.MapWidth;
        var h = doc.MapHeight;
        var before = doc.ReadRegion(0, 0, w, h);
        var beforePlaced = doc.ReadPlacedRegion(0, 0, w, h);
        var stack = new Stack<(int x, int y)>();
        stack.Push((startX, startY));

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            if (x < 0 || y < 0 || x >= w || y >= h) continue;
            if (!MatchesFillTarget(doc, x, y, targetTile, targetPlaced)) continue;
            doc.SetTile(x, y, selected, ctx.CurrentLayer);
            stack.Push((x + 1, y));
            stack.Push((x - 1, y));
            stack.Push((x, y + 1));
            stack.Push((x, y - 1));
        }

        var after = doc.ReadRegion(0, 0, w, h);
        var afterPlaced = doc.ReadPlacedRegion(0, 0, w, h);
        ctx.Undo.Execute(new MapRegionCommand(doc, 0, 0, w, h, before, after, ctx.CurrentLayer, beforePlaced, afterPlaced));
        ctx.RequestRepaint?.Invoke();
        ctx.NotifyEdited?.Invoke();
    }

    internal static bool MatchesFillTarget(MapDocument doc, int x, int y, int targetTile, bool targetPlaced)
    {
        if (doc.GetTile(x, y) != targetTile) return false;
        return targetTile != 0 || doc.IsCellPlaced(x, y) == targetPlaced;
    }
}
