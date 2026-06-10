using Ferbento.Editor.Sprites.Tools;
using Ferbento.Editor.Sprites.Undo;

namespace Ferbento.Editor.Sprites;

public sealed class SpriteEditorContext
{
    public required SpriteDocument Document { get; init; }
    public required PaletteState Palette { get; init; }
    public required Selection Selection { get; init; }
    public required ViewportState Viewport { get; init; }
    public required UndoService Undo { get; init; }
    public required SpriteClipboard Clipboard { get; init; }
    public Action? NotifyEdited { get; init; }
    public Action? RequestRefresh { get; init; }

    public DrawStrokeCommand? ActiveStroke { get; set; }
    public (int x, int y)? ShapeStart { get; set; }
    public (int x, int y)? LastPointer { get; set; }
    public bool IsPanning { get; set; }
    public (int x, int y)? PanStart { get; set; }
    public (int panX, int panY)? PanOrigin { get; set; }
    public (int x, int y)? SelectDragStart { get; set; }
    public bool IsMovingSelection { get; set; }
    public (int x, int y)? MoveSelectionOffset { get; set; }
    public (int x, int y)? StampPreviewPosition { get; set; }
    public bool StampPreviewSkipTransparent { get; set; }
    public int BrushSize { get; set; } = 1;

    public int DrawColor(PointerInput input) =>
        input.IsRightButton ? Palette.SecondaryColor : Palette.PrimaryColor;

    public void MarkEdited()
    {
        NotifyEdited?.Invoke();
        RequestRefresh?.Invoke();
    }

    public void BeginStroke()
    {
        if (ActiveStroke != null) return;
        ActiveStroke = new DrawStrokeCommand(Document);
        Undo.PushExecuted(ActiveStroke);
    }

    public void EndStroke()
    {
        if (ActiveStroke == null) return;
        ActiveStroke.CaptureAfter();
        ActiveStroke = null;
        MarkEdited();
    }

    public void ApplySpritePatch(string name, int spriteIndex, byte[] after, int afterFlags)
    {
        var before = Document.GetSpritePixels(spriteIndex);
        var beforeFlags = Document.GetFlags(spriteIndex);
        Undo.Execute(new SpritePatchCommand(Document, spriteIndex, name, before, after, beforeFlags, afterFlags));
        MarkEdited();
    }

    public void ApplyCurrentSpritePatch(string name, byte[] after, int afterFlags) =>
        ApplySpritePatch(name, Document.CurrentSpriteIndex, after, afterFlags);
}
