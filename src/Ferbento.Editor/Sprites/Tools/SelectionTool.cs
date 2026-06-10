namespace Ferbento.Editor.Sprites.Tools;

public sealed class SelectionTool : ISpriteTool
{
    public string Id => "select";

    public void OnPointerDown(SpriteEditorContext ctx, PointerInput input)
    {
        if (ctx.Selection.HasPixelSelection && ctx.Selection.ContainsLocalPixel(input.LocalX, input.LocalY))
        {
            ctx.IsMovingSelection = true;
            ctx.MoveSelectionOffset = (input.LocalX - ctx.Selection.PixelX, input.LocalY - ctx.Selection.PixelY);
            return;
        }

        ctx.Selection.Clear();
        ctx.SelectDragStart = (input.LocalX, input.LocalY);
    }

    public void OnPointerMove(SpriteEditorContext ctx, PointerInput input)
    {
        if (ctx.IsMovingSelection && ctx.MoveSelectionOffset is { } offset && ctx.Selection.PixelBuffer != null)
        {
            var destX = input.LocalX - offset.x;
            var destY = input.LocalY - offset.y;
            ctx.Selection.SetPixelRect(destX, destY,
                destX + ctx.Selection.PixelWidth - 1,
                destY + ctx.Selection.PixelHeight - 1,
                ctx.Selection.PixelBuffer);
            ctx.RequestRefresh?.Invoke();
            return;
        }

        if (ctx.SelectDragStart is { } start)
        {
            ctx.Selection.SetPixelRect(start.x, start.y, input.LocalX, input.LocalY);
            ctx.RequestRefresh?.Invoke();
        }
    }

    public void OnPointerUp(SpriteEditorContext ctx, PointerInput input)
    {
        if (ctx.SelectDragStart is { } start)
        {
            var tile = ctx.Document.TileSize;
            var left = Math.Clamp(Math.Min(start.x, input.LocalX), 0, tile - 1);
            var top = Math.Clamp(Math.Min(start.y, input.LocalY), 0, tile - 1);
            var right = Math.Clamp(Math.Max(start.x, input.LocalX), 0, tile - 1);
            var bottom = Math.Clamp(Math.Max(start.y, input.LocalY), 0, tile - 1);
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
            ctx.SelectDragStart = null;
            ctx.RequestRefresh?.Invoke();
            return;
        }

        if (ctx.IsMovingSelection && ctx.Selection.PixelBuffer != null)
        {
            var tile = ctx.Document.TileSize;
            var destX = ctx.Selection.PixelX;
            var destY = ctx.Selection.PixelY;
            var w = ctx.Selection.PixelWidth;
            var h = ctx.Selection.PixelHeight;

            var before = ctx.Document.GetCurrentSpritePixels();
            var beforeFlags = ctx.Document.GetCurrentFlags();

            var origLeft = destX - (ctx.MoveSelectionOffset?.x ?? 0);
            var origTop = destY - (ctx.MoveSelectionOffset?.y ?? 0);

            var clearBuf = new byte[w * h];
            ctx.Document.WriteLocalRegion(origLeft, origTop, w, h, clearBuf);

            for (var row = 0; row < h; row++)
            for (var col = 0; col < w; col++)
            {
                var lx = destX + col;
                var ly = destY + row;
                if (lx >= 0 && lx < tile && ly >= 0 && ly < tile)
                    ctx.Document.SetLocalPixel(lx, ly, ctx.Selection.PixelBuffer[row * w + col]);
            }

            var after = ctx.Document.GetCurrentSpritePixels();
            ctx.Undo.Execute(new Undo.SpritePatchCommand(
                ctx.Document, ctx.Document.CurrentSpriteIndex, "move",
                before, after, beforeFlags, beforeFlags));
            ctx.IsMovingSelection = false;
            ctx.MoveSelectionOffset = null;
            ctx.MarkEdited();
        }
    }

    public void OnCancel(SpriteEditorContext ctx) => ctx.Selection.Clear();
}
