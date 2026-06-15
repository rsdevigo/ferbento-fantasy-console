namespace Ferbento.Editor.Tilemaps.Tools;

public interface ITilemapTool
{
    string Id { get; }
    void OnPointerDown(TilemapEditorContext ctx, TilePointerInput input);
    void OnPointerMove(TilemapEditorContext ctx, TilePointerInput input);
    void OnPointerUp(TilemapEditorContext ctx, TilePointerInput input);
}

public readonly struct TilePointerInput
{
    public int TileX { get; init; }
    public int TileY { get; init; }
    public bool IsStart { get; init; }
    public bool Ctrl { get; init; }
    public bool Shift { get; init; }
    public bool SpaceHeld { get; init; }
}
