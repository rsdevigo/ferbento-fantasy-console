using Avalonia.Controls;
using Avalonia.Media;

namespace Ferbento.Editor.Sprites;

public static class SpriteToolIcons
{
    public static Control CreateBrushIcon() => new PathIcon
    {
        Width = 16,
        Height = 16,
        Foreground = Brushes.White,
        Data = Geometry.Parse("M3,14 L5,12 L11,6 L10,5 L4,11 L2,13 Z M11,5 L12,4 L13,5 L12,6 Z"),
    };

    public static Control CreateEraserIcon() => new PathIcon
    {
        Width = 16,
        Height = 16,
        Foreground = Brushes.White,
        Data = Geometry.Parse("M4,12 L10,6 L13,9 L7,15 L4,12 Z M11,5 L12,4 L14,6 L13,7 Z"),
    };
}
