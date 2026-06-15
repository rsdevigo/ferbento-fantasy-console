using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Ferbento.Editor.Sprites;

namespace Ferbento.Editor.Chrome;

public sealed class EditorToolStrip : WrapPanel
{
    public event Action<string>? ToolSelected;

    public EditorToolStrip()
    {
        Width = double.NaN;
        Margin = new Thickness(0, 4);
    }

    public void SetTools(IEnumerable<(string id, string? label, Func<Control>? iconFactory)> tools, string? selectedId)
    {
        Children.Clear();
        foreach (var (id, label, iconFactory) in tools)
        {
            var toolId = id;
            Control child = iconFactory != null
                ? iconFactory()
                : new TextBlock
                {
                    Text = label,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 14,
                };

            var btn = new Border
            {
                Width = EditorTheme.ToolButtonWidth,
                Height = EditorTheme.ToolButtonHeight,
                Background = new SolidColorBrush(id == selectedId ? EditorTheme.ToolActive : EditorTheme.ToolInactive),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Child = child,
                Tag = toolId,
                Margin = new Thickness(2),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            ToolTip.SetTip(btn, id);
            btn.PointerPressed += (_, e) =>
            {
                ToolSelected?.Invoke(toolId);
                e.Handled = true;
            };
            Children.Add(btn);
        }
    }

    public void HighlightTool(string? selectedId)
    {
        foreach (var child in Children)
        {
            if (child is Border b && b.Tag is string id)
                b.Background = new SolidColorBrush(id == selectedId ? EditorTheme.ToolActive : EditorTheme.ToolInactive);
        }
    }
}
