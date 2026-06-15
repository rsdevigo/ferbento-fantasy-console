namespace Ferbento.Editor.Tilemaps.Tools;

public sealed class PaintTool : ITilemapTool
{
    public string Id => "pencil";

    public void OnPointerDown(TilemapEditorContext ctx, TilePointerInput input)
    {
        if (!InMap(ctx, input.TileX, input.TileY)) return;
        ctx.BeginStroke(input.TileX, input.TileY);
        Paint(ctx, input.TileX, input.TileY);
    }

    public void OnPointerMove(TilemapEditorContext ctx, TilePointerInput input)
    {
        if (ctx.ActiveStroke == null) return;
        Paint(ctx, input.TileX, input.TileY);
    }

    public void OnPointerUp(TilemapEditorContext ctx, TilePointerInput input) =>
        ctx.EndStroke();

    private static void Paint(TilemapEditorContext ctx, int tx, int ty)
    {
        if (!InMap(ctx, tx, ty)) return;
        ctx.ActiveStroke?.IncludeTile(tx, ty);
        ctx.Document.SetTile(tx, ty, ctx.SelectedTile, ctx.CurrentLayer);
        ctx.NotifyEdited?.Invoke();
    }

    internal static bool InMap(TilemapEditorContext ctx, int tx, int ty) =>
        tx >= 0 && ty >= 0 && tx < ctx.Document.MapWidth && ty < ctx.Document.MapHeight;
}
