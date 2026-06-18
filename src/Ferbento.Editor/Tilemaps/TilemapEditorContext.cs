using Ferbento.Editor.Sprites;
using Ferbento.Editor.Sprites.Undo;
using Ferbento.Editor.Tilemaps.Undo;

namespace Ferbento.Editor.Tilemaps;

public sealed class TilemapEditorContext
{
    public required MapDocument Document { get; init; }
    public required MapSelection Selection { get; init; }
    public required ViewportState Viewport { get; init; }
    public required UndoService Undo { get; init; }
    public required TilemapClipboard Clipboard { get; init; }
    public int SelectedTile { get; set; }
    public int CurrentLayer { get; set; }
    public MapStrokeCommand? ActiveStroke { get; set; }
    public (int x, int y)? DragStart { get; set; }
    public Action? NotifyEdited { get; set; }
    public Action? RequestRepaint { get; set; }

    public void BeginStroke(int x, int y) =>
        ActiveStroke = new MapStrokeCommand(Document, CurrentLayer, x, y);

    public void EndStroke()
    {
        if (ActiveStroke == null) return;
        ActiveStroke.CaptureAfter();
        Undo.PushExecuted(ActiveStroke);
        ActiveStroke = null;
    }

    public void CancelStroke() => ActiveStroke = null;
}
