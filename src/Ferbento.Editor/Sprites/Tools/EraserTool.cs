namespace Ferbento.Editor.Sprites.Tools;

public sealed class EraserTool : ISpriteTool
{
    public string Id => "eraser";

    public void OnPointerDown(SpriteEditorContext ctx, PointerInput input)
    {
        ctx.BeginStroke();
        Paint(ctx, input);
    }

    public void OnPointerMove(SpriteEditorContext ctx, PointerInput input)
    {
        if (ctx.ActiveStroke == null) return;
        var color = EraseColor(ctx);
        if (ctx.LastPointer is { } last)
            PixelDrawing.InterpolateStroke(ctx.Document, last.x, last.y, input.LocalX, input.LocalY, color, ctx.Selection, ctx.BrushSize);
        else
            Paint(ctx, input);
        ctx.LastPointer = (input.LocalX, input.LocalY);
        ctx.RequestRefresh?.Invoke();
    }

    public void OnPointerUp(SpriteEditorContext ctx, PointerInput input)
    {
        ctx.LastPointer = null;
        ctx.EndStroke();
    }

    private static int EraseColor(SpriteEditorContext ctx) =>
        ctx.Document.ColorChip?.TransparentIndex ?? 0;

    private static void Paint(SpriteEditorContext ctx, PointerInput input)
    {
        PixelDrawing.PaintBrush(ctx.Document, input.LocalX, input.LocalY, EraseColor(ctx), ctx.BrushSize, ctx.Selection);
        ctx.LastPointer = (input.LocalX, input.LocalY);
        ctx.RequestRefresh?.Invoke();
    }
}
