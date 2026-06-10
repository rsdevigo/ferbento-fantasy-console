namespace Ferbento.Editor.Sprites.Tools;

public enum ShapeMode { Line, Rectangle, Circle }

public sealed class ShapeToolHost : ISpriteTool
{
    public string Id => "shape";
    public ShapeMode Mode { get; set; } = ShapeMode.Line;

    public void CycleMode()
    {
        Mode = Mode switch
        {
            ShapeMode.Line => ShapeMode.Rectangle,
            ShapeMode.Rectangle => ShapeMode.Circle,
            _ => ShapeMode.Line,
        };
    }

    public void OnPointerDown(SpriteEditorContext ctx, PointerInput input)
    {
        ctx.ShapeStart = (input.LocalX, input.LocalY);
        ctx.BeginStroke();
    }

    public void OnPointerMove(SpriteEditorContext ctx, PointerInput input)
    {
        if (ctx.ShapeStart is not { } start || ctx.ActiveStroke == null) return;

        var preview = ctx.Document.GetCurrentSpritePixels();
        ctx.ActiveStroke.Undo();

        var endX = input.LocalX;
        var endY = input.LocalY;
        if (input.Shift && Mode == ShapeMode.Line)
            (endX, endY) = PixelDrawing.SnapLine(start.x, start.y, endX, endY);

        var color = ctx.DrawColor(input);
        var filled = input.Ctrl;

        switch (Mode)
        {
            case ShapeMode.Line:
                PixelDrawing.DrawLine(ctx.Document, start.x, start.y, endX, endY, color, ctx.Selection);
                break;
            case ShapeMode.Rectangle:
                PixelDrawing.DrawRect(ctx.Document, start.x, start.y, endX, endY, color, filled, ctx.Selection);
                break;
            case ShapeMode.Circle:
                PixelDrawing.DrawCircle(ctx.Document, start.x, start.y, endX, endY, color, filled, input.Shift, ctx.Selection);
                break;
        }

        ctx.RequestRefresh?.Invoke();
    }

    public void OnPointerUp(SpriteEditorContext ctx, PointerInput input)
    {
        ctx.ShapeStart = null;
        ctx.EndStroke();
    }
}

public sealed class LineTool : ISpriteTool
{
    public string Id => "line";
    private readonly ShapeToolHost _host = new() { Mode = ShapeMode.Line };
    public void OnPointerDown(SpriteEditorContext ctx, PointerInput input) => _host.OnPointerDown(ctx, input);
    public void OnPointerMove(SpriteEditorContext ctx, PointerInput input) => _host.OnPointerMove(ctx, input);
    public void OnPointerUp(SpriteEditorContext ctx, PointerInput input) => _host.OnPointerUp(ctx, input);
}

public sealed class RectangleTool : ISpriteTool
{
    public string Id => "rect";
    private readonly ShapeToolHost _host = new() { Mode = ShapeMode.Rectangle };
    public void OnPointerDown(SpriteEditorContext ctx, PointerInput input) => _host.OnPointerDown(ctx, input);
    public void OnPointerMove(SpriteEditorContext ctx, PointerInput input) => _host.OnPointerMove(ctx, input);
    public void OnPointerUp(SpriteEditorContext ctx, PointerInput input) => _host.OnPointerUp(ctx, input);
}

public sealed class CircleTool : ISpriteTool
{
    public string Id => "circle";
    private readonly ShapeToolHost _host = new() { Mode = ShapeMode.Circle };
    public void OnPointerDown(SpriteEditorContext ctx, PointerInput input) => _host.OnPointerDown(ctx, input);
    public void OnPointerMove(SpriteEditorContext ctx, PointerInput input) => _host.OnPointerMove(ctx, input);
    public void OnPointerUp(SpriteEditorContext ctx, PointerInput input) => _host.OnPointerUp(ctx, input);
}
