namespace Ferbento.Editor.Sprites.Tools;

public sealed class StampTool : ISpriteTool
{
    public string Id => "stamp";

    public void OnPointerDown(SpriteEditorContext ctx, PointerInput input)
    {
        if (!ctx.Clipboard.HasContent || ctx.Clipboard.Pixels == null) return;

        var before = ctx.Document.GetCurrentSpritePixels();
        var beforeFlags = ctx.Document.GetCurrentFlags();
        var pixels = (byte[])ctx.Clipboard.Pixels.Clone();
        var transparent = ctx.Document.ColorChip?.TransparentIndex ?? 0;

        for (var y = 0; y < ctx.Clipboard.Height; y++)
        for (var x = 0; x < ctx.Clipboard.Width; x++)
        {
            var color = pixels[y * ctx.Clipboard.Width + x];
            if (input.Ctrl && color == transparent) continue;
            var destX = input.LocalX + x;
            var destY = input.LocalY + y;
            if (destX >= 0 && destY >= 0 && destX < ctx.Document.TileSize && destY < ctx.Document.TileSize)
                ctx.Document.SetLocalPixel(destX, destY, color);
        }

        var after = ctx.Document.GetCurrentSpritePixels();
        ctx.Undo.Execute(new Undo.SpritePatchCommand(
            ctx.Document, ctx.Document.CurrentSpriteIndex, "stamp",
            before, after, beforeFlags, beforeFlags));
        ctx.MarkEdited();
    }

    public void OnPointerMove(SpriteEditorContext ctx, PointerInput input)
    {
        if (!ctx.Clipboard.HasContent) return;
        ctx.StampPreviewPosition = (input.LocalX, input.LocalY);
        ctx.StampPreviewSkipTransparent = input.Ctrl;
        ctx.RequestRefresh?.Invoke();
    }

    public void OnPointerUp(SpriteEditorContext ctx, PointerInput input) { }

    public static void ClearPreview(SpriteEditorContext ctx)
    {
        if (ctx.StampPreviewPosition == null) return;
        ctx.StampPreviewPosition = null;
        ctx.StampPreviewSkipTransparent = false;
        ctx.RequestRefresh?.Invoke();
    }
}
