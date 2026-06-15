using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ferbento.Editor.Chrome;

public sealed class EditorCanvasHost : Border
{
    public event Action<double, double>? PanelSizeChanged;

    public EditorCanvasHost()
    {
        Background = new SolidColorBrush(EditorTheme.CanvasBackground);
        BorderBrush = new SolidColorBrush(EditorTheme.CanvasBorder);
        BorderThickness = new Thickness(1);
        ClipToBounds = true;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        MinHeight = 120;
        SizeChanged += (_, _) => PanelSizeChanged?.Invoke(Bounds.Width, Bounds.Height);
    }
}
