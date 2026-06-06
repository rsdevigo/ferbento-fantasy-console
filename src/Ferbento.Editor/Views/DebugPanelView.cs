using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ferbento.Editor.Views;

public class DebugPanelView : UserControl
{
    public DebugPanelView()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Content = BuildLayout();
    }

    private Control BuildLayout()
    {
        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        };

        var header = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D30")),
            Padding = new Thickness(8, 4),
            Child = new TextBlock
            {
                Text = "Debugger",
                Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                FontSize = 12,
            },
        };
        Grid.SetColumnSpan(header, 2);

        var toolbar = new WrapPanel
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D30")),
            Children =
            {
                CreateToolButton("▶", "#4EC9B0", "Continue"),
                CreateToolButton("⏎", "#569CD6", "Step Over"),
                CreateToolButton("↴", "#569CD6", "Step Into"),
                CreateToolButton("↩", "#569CD6", "Step Out"),
                CreateToolButton("■", "#CE9178", "Stop"),
            }
        };
        Grid.SetRow(toolbar, 1);
        Grid.SetColumnSpan(toolbar, 2);

        var callStackPanel = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            Padding = new Thickness(4),
            Child = new StackPanel
            {
                Children =
                {
                    CreateSectionLabel("Call Stack"),
                    new ListBox
                    {
                        Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
                        Height = 200,
                        ItemsSource = new[] { "_init()", "_update()", "move_player() line 42", "check_collision() line 87" },
                    }
                }
            }
        };
        Grid.SetRow(callStackPanel, 2);

        var watchPanel = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            Padding = new Thickness(4),
            Child = new StackPanel
            {
                Children =
                {
                    CreateSectionLabel("Variables"),
                    new ListBox
                    {
                        Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
                        Height = 200,
                        ItemsSource = new[]
                        {
                            "x = 128.0000",
                            "y = 64.0000",
                            "score = 42",
                            "player = [table]",
                            "bullets = [table] (3)",
                            "frame = 1200",
                        },
                    }
                }
            }
        };
        Grid.SetColumn(watchPanel, 1);
        Grid.SetRow(watchPanel, 2);

        root.Children.Add(header);
        root.Children.Add(toolbar);
        root.Children.Add(callStackPanel);
        root.Children.Add(watchPanel);

        return root;
    }

    private static TextBlock CreateSectionLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.Parse("#569CD6")),
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 2, 0, 2),
        };
    }

    private static Button CreateToolButton(string icon, string color, string tooltip)
    {
        var button = new Button
        {
            Content = icon,
            Margin = new Thickness(2),
            Padding = new Thickness(6, 2),
            Background = new SolidColorBrush(Color.Parse("#3F3F46")),
            Foreground = new SolidColorBrush(Color.Parse(color)),
            FontSize = 14,
            BorderThickness = new Thickness(0),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        };
        ToolTip.SetTip(button, tooltip);
        return button;
    }
}
