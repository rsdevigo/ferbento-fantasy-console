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

    public void OnPointerUp(TilemapEditorContext ctx, TilePointerInput input)
    {
        if (ctx.ActiveStroke == null) return;
        ctx.EndStroke();
        ctx.NotifyEdited?.Invoke();
    }

    private static void Paint(TilemapEditorContext ctx, int tx, int ty)
    {
        if (!InMap(ctx, tx, ty)) return;

        var placements = TileStampLayout.GetPlacements(
            ctx.SelectedTile,
            ctx.Selection.NavigatorSprites,
            ctx.Document.SpritesPerRow);

        var painted = false;
        foreach (var placement in placements)
        {
            var mx = tx + placement.Dx;
            var my = ty + placement.Dy;
            if (!InMap(ctx, mx, my)) continue;
            ctx.ActiveStroke?.IncludeTile(mx, my);
            ctx.Document.SetTile(mx, my, placement.TileId, ctx.CurrentLayer);
            painted = true;
        }

        if (painted)
            ctx.RequestRepaint?.Invoke();
    }

    internal static bool InMap(TilemapEditorContext ctx, int tx, int ty) =>
        tx >= 0 && ty >= 0 && tx < ctx.Document.MapWidth && ty < ctx.Document.MapHeight;
}
