namespace Ferbento.Editor.Sprites.Tools;

public sealed class PencilTool : ISpriteTool
{
    public string Id => "pencil";

    public void OnPointerDown(SpriteEditorContext ctx, PointerInput input)
    {
        if (input.Ctrl)
        {
            var color = ctx.DrawColor(input);
            var old = ctx.Document.GetLocalPixel(input.LocalX, input.LocalY);
            ctx.BeginStroke();
            PixelDrawing.ReplaceColor(ctx.Document, old, color, ctx.Selection);
            ctx.EndStroke();
            return;
        }

        ctx.BeginStroke();
        Paint(ctx, input);
    }

    public void OnPointerMove(SpriteEditorContext ctx, PointerInput input)
    {
        if (ctx.ActiveStroke == null) return;
        if (ctx.LastPointer is { } last)
            PixelDrawing.InterpolateStroke(ctx.Document, last.x, last.y, input.LocalX, input.LocalY, ctx.DrawColor(input), ctx.Selection, ctx.BrushSize);
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

    private static void Paint(SpriteEditorContext ctx, PointerInput input)
    {
        PixelDrawing.PaintBrush(ctx.Document, input.LocalX, input.LocalY, ctx.DrawColor(input), ctx.BrushSize, ctx.Selection);
        ctx.LastPointer = (input.LocalX, input.LocalY);
        ctx.RequestRefresh?.Invoke();
    }
}
