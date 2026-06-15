using Ferbento.Editor.Sprites;

namespace Ferbento.Editor.Tilemaps.Tools;

public sealed class PanTool : ITilemapTool
{
    public string Id => "pan";
    private int _lastX;
    private int _lastY;

    public void OnPointerDown(TilemapEditorContext ctx, TilePointerInput input)
    {
        _lastX = input.TileX;
        _lastY = input.TileY;
    }

    public void OnPointerMove(TilemapEditorContext ctx, TilePointerInput input)
    {
        if (!input.IsStart) return;
        var dx = (input.TileX - _lastX) * ctx.Document.TileSize;
        var dy = (input.TileY - _lastY) * ctx.Document.TileSize;
        ctx.Viewport.PanX -= dx;
        ctx.Viewport.PanY -= dy;
        _lastX = input.TileX;
        _lastY = input.TileY;
    }

    public void OnPointerUp(TilemapEditorContext ctx, TilePointerInput input) { }
}
