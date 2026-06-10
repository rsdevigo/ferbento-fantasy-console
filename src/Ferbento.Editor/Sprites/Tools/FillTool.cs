namespace Ferbento.Editor.Sprites.Tools;

public sealed class FillTool : ISpriteTool
{
    public string Id => "fill";

    public void OnPointerDown(SpriteEditorContext ctx, PointerInput input)
    {
        var before = ctx.Document.GetCurrentSpritePixels();
        var beforeFlags = ctx.Document.GetCurrentFlags();
        PixelDrawing.FloodFill(ctx.Document, input.LocalX, input.LocalY, ctx.DrawColor(input), ctx.Selection);
        var after = ctx.Document.GetCurrentSpritePixels();
        ctx.Undo.Execute(new Undo.SpritePatchCommand(
            ctx.Document, ctx.Document.CurrentSpriteIndex, "fill",
            before, after, beforeFlags, beforeFlags));
        ctx.MarkEdited();
    }

    public void OnPointerMove(SpriteEditorContext ctx, PointerInput input) { }
    public void OnPointerUp(SpriteEditorContext ctx, PointerInput input) { }
}
