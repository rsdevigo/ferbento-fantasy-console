namespace Ferbento.Editor.Sprites.Input;

public enum SpriteEditorAction
{
    Undo, Redo, Copy, Cut, Paste,
    PrevSprite, NextSprite, PrevColor, NextColor,
    ZoomIn, ZoomOut, ToggleFullscreen, ToggleGrid, ToggleHexView,
    SelectTool, FlipH, FlipV, Rotate, ShiftUp, ShiftDown, ShiftLeft, ShiftRight,
    MoveUp, MoveDown, MoveLeft, MoveRight,
    ClearSelection, ClearArea, SwapColors, ClearSprite,
    ToolPencil, ToolFill, ToolPicker, ToolSelect, ToolStamp, ToolShape, ToolEraser,
    BrushSmaller, BrushLarger,
}

public static class SpriteEditorInputMap
{
    public static readonly IReadOnlyDictionary<SpriteEditorAction, string> DefaultBindings = new Dictionary<SpriteEditorAction, string>
    {
        [SpriteEditorAction.Undo] = "Ctrl+Z",
        [SpriteEditorAction.Redo] = "Ctrl+Y",
        [SpriteEditorAction.Copy] = "Ctrl+C",
        [SpriteEditorAction.Cut] = "Ctrl+X",
        [SpriteEditorAction.Paste] = "Ctrl+V",
        [SpriteEditorAction.PrevSprite] = "Q",
        [SpriteEditorAction.NextSprite] = "W",
        [SpriteEditorAction.PrevColor] = "1",
        [SpriteEditorAction.NextColor] = "2",
        [SpriteEditorAction.ZoomIn] = "OemPlus",
        [SpriteEditorAction.ZoomOut] = "OemMinus",
        [SpriteEditorAction.ToggleFullscreen] = "Tab",
        [SpriteEditorAction.ToggleGrid] = "Ctrl+G",
        [SpriteEditorAction.ToggleHexView] = "Ctrl+H",
        [SpriteEditorAction.SelectTool] = "S",
        [SpriteEditorAction.FlipH] = "F",
        [SpriteEditorAction.FlipV] = "V",
        [SpriteEditorAction.Rotate] = "R",
        [SpriteEditorAction.ShiftUp] = "Up",
        [SpriteEditorAction.ShiftDown] = "Down",
        [SpriteEditorAction.ShiftLeft] = "Left",
        [SpriteEditorAction.ShiftRight] = "Right",
        [SpriteEditorAction.ClearSelection] = "Return",
        [SpriteEditorAction.ClearArea] = "Delete",
        [SpriteEditorAction.SwapColors] = "X",
        [SpriteEditorAction.ClearSprite] = "Ctrl+Delete",
        [SpriteEditorAction.ToolPencil] = "B",
        [SpriteEditorAction.ToolFill] = "G",
        [SpriteEditorAction.ToolEraser] = "E",
        [SpriteEditorAction.ToolPicker] = "I",
        [SpriteEditorAction.ToolSelect] = "Shift",
        [SpriteEditorAction.ToolStamp] = "T",
        [SpriteEditorAction.ToolShape] = "L",
        [SpriteEditorAction.BrushSmaller] = "OemOpenBrackets",
        [SpriteEditorAction.BrushLarger] = "OemCloseBrackets",
    };

    public static string GetTooltip(string toolId) => toolId switch
    {
        "pencil" => "Brush (B)",
        "fill" => "Fill (G)",
        "eraser" => "Eraser (E)",
        "picker" => "Picker (I)",
        "select" => "Select (S / Shift)",
        "shape" => "Shape: Line/Rect/Circle (L)",
        "stamp" => "Stamp (T)",
        "pan" => "Pan (Space+drag)",
        "flip-h" => "Flip H (F)",
        "flip-v" => "Flip V (V)",
        "rotate" => "Rotate 90° (R)",
        _ => toolId,
    };
}
