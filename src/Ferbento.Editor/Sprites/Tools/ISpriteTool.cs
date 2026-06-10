namespace Ferbento.Editor.Sprites.Tools;

public interface ISpriteTool
{
    string Id { get; }
    void OnPointerDown(SpriteEditorContext ctx, PointerInput input);
    void OnPointerMove(SpriteEditorContext ctx, PointerInput input);
    void OnPointerUp(SpriteEditorContext ctx, PointerInput input);
    void OnCancel(SpriteEditorContext ctx) { }
}

public readonly struct PointerInput
{
    public int LocalX { get; init; }
    public int LocalY { get; init; }
    public bool IsStart { get; init; }
    public bool IsRightButton { get; init; }
    public bool Ctrl { get; init; }
    public bool Shift { get; init; }
    public bool Alt { get; init; }
}
