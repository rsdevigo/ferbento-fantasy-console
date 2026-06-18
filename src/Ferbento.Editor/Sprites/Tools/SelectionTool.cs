namespace Ferbento.Editor.Sprites.Tools;

public sealed class SelectionTool : ISpriteTool
{
    public string Id => "select";

    public void OnPointerDown(SpriteEditorContext ctx, PointerInput input)
    {
        if (ctx.SelectAnchor is { } anchor)
        {
            FinalizeSelection(ctx, anchor, (input.LocalX, input.LocalY), input);
            ctx.SelectAnchor = null;
            ctx.SelectPreviewEnd = null;
            return;
        }

        ctx.Selection.Clear();
        ctx.SelectAnchor = (input.LocalX, input.LocalY);
        ctx.SelectPreviewEnd = (input.LocalX, input.LocalY);
        ctx.RequestRefresh?.Invoke();
    }

    public void OnPointerMove(SpriteEditorContext ctx, PointerInput input) { }

    public void OnPointerUp(SpriteEditorContext ctx, PointerInput input) { }

    public void OnCancel(SpriteEditorContext ctx)
    {
        ctx.Selection.Clear();
        ctx.SelectAnchor = null;
        ctx.SelectPreviewEnd = null;
    }

    internal static void FinalizeSelection(
        SpriteEditorContext ctx,
        (int x, int y) anchor,
        (int x, int y) end,
        PointerInput input)
    {
        var vw = ctx.Document.ViewWidth;
        var vh = ctx.Document.ViewHeight;
        var maxX = Math.Max(0, vw - 1);
        var maxY = Math.Max(0, vh - 1);
        var left = Math.Clamp(Math.Min(anchor.x, end.x), 0, maxX);
        var top = Math.Clamp(Math.Min(anchor.y, end.y), 0, maxY);
        var right = Math.Clamp(Math.Max(anchor.x, end.x), 0, maxX);
        var bottom = Math.Clamp(Math.Max(anchor.y, end.y), 0, maxY);
        var w = right - left + 1;
        var h = bottom - top + 1;
        var buf = ctx.Document.ReadLocalRegion(left, top, w, h);

        if (input.Ctrl)
        {
            for (var i = 0; i < buf.Length; i++)
                if (buf[i] != 0) buf[i] = 0;
            ctx.BeginStroke();
            ctx.Document.WriteLocalRegion(left, top, w, h, buf);
            ctx.EndStroke();
        }

        ctx.Selection.SetPixelRect(left, top, right, bottom, buf);
        ctx.RequestRefresh?.Invoke();
    }
}
