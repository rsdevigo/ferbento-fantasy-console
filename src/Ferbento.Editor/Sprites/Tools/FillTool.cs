namespace Ferbento.Editor.Sprites.Tools;

public sealed class FillTool : ISpriteTool
{
    public string Id => "fill";

    public void OnPointerDown(SpriteEditorContext ctx, PointerInput input)
    {
        var before = ctx.Document.ReadViewPixels();
        PixelDrawing.FloodFill(ctx.Document, input.LocalX, input.LocalY, ctx.DrawColor(input), ctx.Selection);
        var after = ctx.Document.ReadViewPixels();
        ctx.Undo.Execute(new Undo.LocalRegionCommand(
            ctx.Document, "fill", 0, 0, ctx.Document.ViewWidth, ctx.Document.ViewHeight, before, after));
        ctx.MarkEdited();
    }

    public void OnPointerMove(SpriteEditorContext ctx, PointerInput input) { }
    public void OnPointerUp(SpriteEditorContext ctx, PointerInput input) { }
}
