namespace Ferbento.Editor.Sprites.Tools;

public sealed class PanTool : ISpriteTool
{
    public string Id => "pan";

    public void OnPointerDown(SpriteEditorContext ctx, PointerInput input)
    {
        ctx.IsPanning = true;
        ctx.PanStart = (input.LocalX, input.LocalY);
        ctx.PanOrigin = (ctx.Viewport.PanX, ctx.Viewport.PanY);
    }

    public void OnPointerMove(SpriteEditorContext ctx, PointerInput input)
    {
        if (!ctx.IsPanning || ctx.PanStart is not { } start || ctx.PanOrigin is not { } origin) return;
        var zoom = ctx.Viewport.Zoom;
        ctx.Viewport.PanX = origin.panX + (input.LocalX - start.x) * zoom;
        ctx.Viewport.PanY = origin.panY + (input.LocalY - start.y) * zoom;
        ctx.RequestRefresh?.Invoke();
    }

    public void OnPointerUp(SpriteEditorContext ctx, PointerInput input)
    {
        ctx.IsPanning = false;
        ctx.PanStart = null;
        ctx.PanOrigin = null;
    }
}
