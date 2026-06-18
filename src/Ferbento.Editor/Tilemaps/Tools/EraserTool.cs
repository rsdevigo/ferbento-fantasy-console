namespace Ferbento.Editor.Tilemaps.Tools;

public sealed class EraserTool : ITilemapTool
{
    public string Id => "eraser";

    public void OnPointerDown(TilemapEditorContext ctx, TilePointerInput input)
    {
        if (!PaintTool.InMap(ctx, input.TileX, input.TileY)) return;
        ctx.BeginStroke(input.TileX, input.TileY);
        Erase(ctx, input.TileX, input.TileY);
    }

    public void OnPointerMove(TilemapEditorContext ctx, TilePointerInput input)
    {
        if (ctx.ActiveStroke == null) return;
        Erase(ctx, input.TileX, input.TileY);
    }

    public void OnPointerUp(TilemapEditorContext ctx, TilePointerInput input)
    {
        if (ctx.ActiveStroke == null) return;
        ctx.EndStroke();
        ctx.NotifyEdited?.Invoke();
    }

    private static void Erase(TilemapEditorContext ctx, int tx, int ty)
    {
        if (!PaintTool.InMap(ctx, tx, ty)) return;
        ctx.ActiveStroke?.IncludeTile(tx, ty);
        ctx.Document.EraseTile(tx, ty);
        ctx.RequestRepaint?.Invoke();
    }
}
