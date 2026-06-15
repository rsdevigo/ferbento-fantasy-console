using Ferbento.Editor.Tilemaps.Undo;

namespace Ferbento.Editor.Tilemaps.Tools;

public sealed class StampTool : ITilemapTool
{
    public string Id => "stamp";

    public void OnPointerDown(TilemapEditorContext ctx, TilePointerInput input)
    {
        if (!ctx.Clipboard.HasData || !PaintTool.InMap(ctx, input.TileX, input.TileY)) return;

        var w = ctx.Clipboard.Width;
        var h = ctx.Clipboard.Height;
        var before = ctx.Document.ReadRegion(input.TileX, input.TileY, w, h);
        var after = (byte[])ctx.Clipboard.Data!.Clone();
        ctx.Undo.Execute(new MapRegionCommand(ctx.Document, input.TileX, input.TileY, w, h, before, after, ctx.CurrentLayer));
        ctx.NotifyEdited?.Invoke();
    }

    public void OnPointerMove(TilemapEditorContext ctx, TilePointerInput input) { }

    public void OnPointerUp(TilemapEditorContext ctx, TilePointerInput input) { }
}
