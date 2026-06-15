using Ferbento.Editor.Sprites;
using Ferbento.Editor.Sprites.Undo;

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
    public bool StrokeUndoPushed { get; set; }
    public (int x, int y, int w, int h)? StrokeUndoRegion { get; set; }
    public (int x, int y)? DragStart { get; set; }
    public Action? NotifyEdited { get; set; }
}
