using Ferbento.Editor.Tilemaps.Undo;

namespace Ferbento.Editor.Tilemaps.Tools;

public sealed class PaintTool : ITilemapTool
{
    public string Id => "pencil";

    public void OnPointerDown(TilemapEditorContext ctx, TilePointerInput input) =>
        PaintAt(ctx, input.TileX, input.TileY, input.IsStart);

    public void OnPointerMove(TilemapEditorContext ctx, TilePointerInput input)
    {
        if (input.IsStart) return;
        PaintAt(ctx, input.TileX, input.TileY, isStart: false);
    }

    public void OnPointerUp(TilemapEditorContext ctx, TilePointerInput input) =>
        ctx.StrokeUndoPushed = false;

    private static void PaintAt(TilemapEditorContext ctx, int tx, int ty, bool isStart)
    {
        if (!InMap(ctx, tx, ty)) return;
        if (isStart && !ctx.StrokeUndoPushed)
        {
            PushStrokeUndo(ctx, tx, ty, 1, 1);
            ctx.StrokeUndoPushed = true;
        }
        ctx.Document.SetTile(tx, ty, ctx.SelectedTile, ctx.CurrentLayer);
        ExpandStrokeUndo(ctx, tx, ty);
        ctx.NotifyEdited?.Invoke();
    }

    internal static bool InMap(TilemapEditorContext ctx, int tx, int ty) =>
        tx >= 0 && ty >= 0 && tx < ctx.Document.MapWidth && ty < ctx.Document.MapHeight;

    internal static void PushStrokeUndo(TilemapEditorContext ctx, int x, int y, int w, int h)
    {
        var data = ctx.Document.ReadRegion(x, y, w, h);
        ctx.StrokeUndoRegion = (x, y, w, h);
        ctx.Undo.PushExecuted(new MapRegionCommand(ctx.Document, x, y, w, h, data, data, ctx.CurrentLayer));
    }

    internal static void ExpandStrokeUndo(TilemapEditorContext ctx, int tx, int ty)
    {
        if (ctx.StrokeUndoRegion is not { } r) return;
        var left = Math.Min(r.x, tx);
        var top = Math.Min(r.y, ty);
        var right = Math.Max(r.x + r.w - 1, tx);
        var bottom = Math.Max(r.y + r.h - 1, ty);
        var w = right - left + 1;
        var h = bottom - top + 1;
        if (left == r.x && top == r.y && w == r.w && h == r.h) return;

        ctx.Undo.Undo();
        var before = ctx.Document.ReadRegion(left, top, w, h);
        ctx.StrokeUndoRegion = (left, top, w, h);
        ctx.Undo.PushExecuted(new MapRegionCommand(ctx.Document, left, top, w, h, before, before, ctx.CurrentLayer));
    }
}
