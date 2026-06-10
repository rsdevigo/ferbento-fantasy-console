namespace Ferbento.Editor.Sprites.Tools;

public sealed class PickerTool : ISpriteTool
{
    public string Id => "picker";

    public void OnPointerDown(SpriteEditorContext ctx, PointerInput input)
    {
        var color = ctx.Document.GetLocalPixel(input.LocalX, input.LocalY);
        if (input.Shift)
            ctx.Palette.SecondaryColor = color;
        else
            ctx.Palette.PrimaryColor = color;
    }

    public void OnPointerMove(SpriteEditorContext ctx, PointerInput input) { }
    public void OnPointerUp(SpriteEditorContext ctx, PointerInput input) { }
}
