using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Ferbento.Editor.Sprites;

namespace Ferbento.Editor.Chrome;

public sealed class EditorToolStrip : WrapPanel
{
    private readonly Dictionary<string, bool> _toolEnabled = new();
    private string? _selectedId;

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
            _toolEnabled[toolId] = true;
            btn.PointerPressed += (_, e) =>
            {
                if (!_toolEnabled.GetValueOrDefault(toolId, true)) return;
                ToolSelected?.Invoke(toolId);
                e.Handled = true;
            };
            Children.Add(btn);
        }
        _selectedId = selectedId;
    }

    public void SetToolEnabled(string id, bool enabled)
    {
        _toolEnabled[id] = enabled;
        ApplyToolChrome(id);
    }

    public void HighlightTool(string? selectedId)
    {
        _selectedId = selectedId;
        foreach (var child in Children)
        {
            if (child is Border b && b.Tag is string toolId)
                ApplyToolChrome(toolId);
        }
    }

    private void ApplyToolChrome(string id)
    {
        foreach (var child in Children)
        {
            if (child is not Border b || b.Tag is not string toolId || toolId != id) continue;

            var enabled = _toolEnabled.GetValueOrDefault(id, true);
            var selected = id == _selectedId;
            b.IsHitTestVisible = enabled;
            b.Opacity = enabled ? 1.0 : 0.4;
            b.Cursor = enabled ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.Arrow);
            b.Background = new SolidColorBrush(
                selected && enabled ? EditorTheme.ToolActive : EditorTheme.ToolInactive);
            return;
        }
    }
}
