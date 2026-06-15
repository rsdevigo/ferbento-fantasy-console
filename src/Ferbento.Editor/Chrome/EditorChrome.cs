using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ferbento.Editor.Chrome;

public sealed class EditorChrome : Grid
{
    public EditorCanvasHost PrimaryHost { get; }
    public EditorToolStrip ToolStrip { get; }
    public Panel ContextStrip { get; }
    public ContentControl NavigatorHost { get; }
    public EditorStatusBar StatusFooter { get; }

    public EditorChrome()
    {
        Background = new SolidColorBrush(EditorTheme.Background);
        RowDefinitions = new RowDefinitions("*,Auto,Auto,Auto,Auto");

        PrimaryHost = new EditorCanvasHost();
        ToolStrip = new EditorToolStrip();
        ContextStrip = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, Margin = new Avalonia.Thickness(0, 4) };
        NavigatorHost = new ContentControl();
        StatusFooter = new EditorStatusBar();

        Grid.SetRow(PrimaryHost, 0);
        Grid.SetRow(ToolStrip, 1);
        Grid.SetRow(ContextStrip, 2);
        Grid.SetRow(NavigatorHost, 3);
        Grid.SetRow(StatusFooter, 4);

        Children.Add(PrimaryHost);
        Children.Add(ToolStrip);
        Children.Add(ContextStrip);
        Children.Add(NavigatorHost);
        Children.Add(StatusFooter);
    }
}
