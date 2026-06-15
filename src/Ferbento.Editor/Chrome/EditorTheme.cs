using Avalonia.Media;

namespace Ferbento.Editor.Chrome;

public static class EditorTheme
{
    public static readonly Color Background = Color.Parse("#1E1E1E");
    public static readonly Color CanvasBackground = Color.Parse("#3F3F46");
    public static readonly Color CanvasBorder = Color.Parse("#555555");
    public static readonly Color ToolInactive = Color.Parse("#333337");
    public static readonly Color ToolActive = Color.Parse("#569CD6");
    public static readonly Color SectionLabel = Colors.LightGray;
    public const int ToolButtonWidth = 36;
    public const int ToolButtonHeight = 28;
    public const int StatusFontSize = 10;
    public const int SectionLabelFontSize = 11;
}
