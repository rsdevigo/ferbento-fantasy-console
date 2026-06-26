using Avalonia.Media;
using Ferbento.Editor.Chrome;

namespace Ferbento.Editor.Music;

public static class MusicTheme
{
    public static readonly Color Background = EditorTheme.Background;
    public static readonly Color Panel = EditorTheme.ToolInactive;
    public static readonly Color PanelBorder = EditorTheme.CanvasBorder;
    public static readonly Color Accent = EditorTheme.ToolActive;
    public static readonly Color Text = Colors.White;
    public static readonly Color TextDim = EditorTheme.SectionLabel;
    public static readonly Color ButtonFace = EditorTheme.ToolInactive;
    public static readonly Color ButtonActive = EditorTheme.ToolActive;
    public static readonly Color GridBg = EditorTheme.CanvasBackground;
    public static readonly Color Playhead = EditorTheme.ToolActive;

    public static readonly Color[] ChannelColors =
    [
        Color.Parse("#66BB6A"),
        Color.Parse("#42A5F5"),
        Color.Parse("#FFA726"),
        Color.Parse("#EF5350"),
        Color.Parse("#AB47BC"),
        Color.Parse("#26C6DA"),
        Color.Parse("#FFEE58"),
        Color.Parse("#8D6E63"),
    ];

    public static Color ChannelColor(int channel) =>
        ChannelColors[channel % ChannelColors.Length];
}
