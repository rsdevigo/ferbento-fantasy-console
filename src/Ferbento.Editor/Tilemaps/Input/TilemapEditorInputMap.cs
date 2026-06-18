namespace Ferbento.Editor.Tilemaps.Input;

public enum TilemapEditorAction
{
    Undo, Redo, Copy, Cut, Paste,
    PrevTile, NextTile,
    ZoomIn, ZoomOut, ToggleHexView,
    SelectTool, ClearSelection, SelectAllMap,
    ToolPencil, ToolFill, ToolEraser, ToolSelect, ToolStamp, ToolPan, ToolShape,
}

public static class TilemapEditorInputMap
{
    public static readonly IReadOnlyDictionary<TilemapEditorAction, string> DefaultBindings = new Dictionary<TilemapEditorAction, string>
    {
        [TilemapEditorAction.Undo] = "Ctrl+Z",
        [TilemapEditorAction.Redo] = "Ctrl+Y",
        [TilemapEditorAction.Copy] = "Ctrl+C",
        [TilemapEditorAction.Cut] = "Ctrl+X",
        [TilemapEditorAction.Paste] = "Ctrl+V",
        [TilemapEditorAction.PrevTile] = "Q",
        [TilemapEditorAction.NextTile] = "W",
        [TilemapEditorAction.ZoomIn] = "OemPlus",
        [TilemapEditorAction.ZoomOut] = "OemMinus",
        [TilemapEditorAction.ToggleHexView] = "Ctrl+H",
        [TilemapEditorAction.SelectTool] = "S",
        [TilemapEditorAction.ClearSelection] = "Return",
        [TilemapEditorAction.SelectAllMap] = "Ctrl+A",
        [TilemapEditorAction.ToolPencil] = "B",
        [TilemapEditorAction.ToolFill] = "G",
        [TilemapEditorAction.ToolEraser] = "E",
        [TilemapEditorAction.ToolSelect] = "Shift",
        [TilemapEditorAction.ToolStamp] = "T",
        [TilemapEditorAction.ToolPan] = "Space",
        [TilemapEditorAction.ToolShape] = "L",
    };

    public static string GetTooltip(string toolId) => toolId switch
    {
        "pencil" => "Paint (B)",
        "eraser" => "Eraser (E)",
        "fill" => "Fill (G)",
        "select" => "Select (S / Shift)",
        "stamp" => "Stamp (T)",
        "pan" => "Pan (Space+drag)",
        "shape" => "Shape line/rect/circle (L)",
        _ => toolId,
    };
}
