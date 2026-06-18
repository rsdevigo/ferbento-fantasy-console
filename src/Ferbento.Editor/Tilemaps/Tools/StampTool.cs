using Ferbento.Editor.Tilemaps.Undo;

namespace Ferbento.Editor.Tilemaps.Tools;

public sealed class StampTool : ITilemapTool
{
    public string Id => "stamp";

    public void OnPointerDown(TilemapEditorContext ctx, TilePointerInput input)
    {
        if (!ctx.Clipboard.HasData || !PaintTool.InMap(ctx, input.TileX, input.TileY)) return;

        var doc = ctx.Document;
        var w = ctx.Clipboard.Width;
        var h = ctx.Clipboard.Height;
        var before = doc.ReadRegion(input.TileX, input.TileY, w, h);
        var beforePlaced = doc.ReadPlacedRegion(input.TileX, input.TileY, w, h);
        var after = (byte[])ctx.Clipboard.Data!.Clone();
        var afterPlaced = (bool[])ctx.Clipboard.Placed!.Clone();
        ctx.Undo.Execute(new MapRegionCommand(
            doc, input.TileX, input.TileY, w, h,
            before, after, ctx.CurrentLayer, beforePlaced, afterPlaced));
        ctx.RequestRepaint?.Invoke();
        ctx.NotifyEdited?.Invoke();
    }

    public void OnPointerMove(TilemapEditorContext ctx, TilePointerInput input) { }

    public void OnPointerUp(TilemapEditorContext ctx, TilePointerInput input) { }
}
