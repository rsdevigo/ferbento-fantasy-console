using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ferbento.Editor.Chrome;

public sealed class EditorStatusBar : TextBlock
{
    public EditorStatusBar()
    {
        Foreground = Brushes.Gray;
        FontSize = EditorTheme.StatusFontSize;
        Margin = new Thickness(0, 4, 0, 0);
    }
}
