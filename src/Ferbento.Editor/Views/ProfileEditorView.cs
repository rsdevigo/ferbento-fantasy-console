using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ferbento.Editor.Views;

public class ProfileEditorView : UserControl
{
    public ProfileEditorView()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Content = BuildLayout();
    }

    private Control BuildLayout()
    {
        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("250,4,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        };

        var header = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D30")),
            Padding = new Thickness(8, 4),
            Child = new TextBlock
            {
                Text = "Hardware Profile",
                Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                FontSize = 12,
            },
        };
        Grid.SetColumnSpan(header, 3);

        var toolbar = new WrapPanel
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D30")),
            Margin = new Thickness(4, 4, 0, 0),
            Children =
            {
                CreateButton("Pico-8", "#569CD6"),
                CreateButton("Game Boy", "#4EC9B0"),
                CreateButton("GBC", "#4EC9B0"),
                CreateButton("NES", "#CE9178"),
                CreateButton("SNES", "#CE9178"),
                CreateButton("Mega Drive", "#CE9178"),
                CreateButton("Import JSON...", "#DCDCAA"),
            }
        };
        Grid.SetRow(toolbar, 1);
        Grid.SetColumnSpan(toolbar, 3);

        var sidebar = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            Padding = new Thickness(4),
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    CreateSectionLabel("Display"),
                    CreateProperty("Width", "128"),
                    CreateProperty("Height", "128"),
                    CreateSectionLabel("Palette"),
                    CreateProperty("Colors", "16"),
                    CreateProperty("Transparent", "0"),
                    CreateSectionLabel("Sprites"),
                    CreateProperty("Sheet W", "128"),
                    CreateProperty("Sheet H", "128"),
                    CreateProperty("Tile Size", "8"),
                    CreateProperty("Max OnScreen", "128"),
                    CreateSectionLabel("Map"),
                    CreateProperty("Width", "128"),
                    CreateProperty("Height", "64"),
                    CreateSectionLabel("Memory"),
                    CreateProperty("RAM", "32 KB"),
                    CreateProperty("Cart ROM", "32 KB"),
                }
            }
        };
        Grid.SetRow(sidebar, 2);

        var divider = new GridSplitter
        {
            Width = 4,
            Background = new SolidColorBrush(Color.Parse("#3F3F46")),
        };
        Grid.SetColumn(divider, 1);
        Grid.SetRow(divider, 2);

        var details = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Pico-8 Compatible Profile",
                        Foreground = new SolidColorBrush(Color.Parse("#569CD6")),
                        FontSize = 18,
                        FontWeight = FontWeight.Bold,
                    },
                    new TextBlock
                    {
                        Text = "All settings match the Pico-8 fantasy console specification.",
                        Foreground = new SolidColorBrush(Color.Parse("#888888")),
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    new Border
                    {
                        Background = new SolidColorBrush(Color.Parse("#2D2D30")),
                        Padding = new Thickness(8),
                        Margin = new Thickness(0, 8, 0, 0),
                        Child = new TextBlock
                        {
                            Text = "Warning: Changing profile settings may break cartridges designed for a specific hardware target.",
                            Foreground = new SolidColorBrush(Color.Parse("#DCDCAA")),
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap,
                        },
                    }
                }
            }
        };
        Grid.SetColumn(details, 2);
        Grid.SetRow(details, 2);

        root.Children.Add(header);
        root.Children.Add(toolbar);
        root.Children.Add(sidebar);
        root.Children.Add(divider);
        root.Children.Add(details);

        return root;
    }

    private static Button CreateButton(string text, string color)
    {
        return new Button
        {
            Content = text,
            Margin = new Thickness(4),
            Padding = new Thickness(10, 4),
            Background = new SolidColorBrush(Color.Parse(color)),
            Foreground = Brushes.White,
            FontSize = 12,
            BorderThickness = new Thickness(0),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        };
    }

    private static TextBlock CreateSectionLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.Parse("#569CD6")),
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 8, 0, 2),
        };
    }

    private static Control CreateProperty(string name, string value)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 1),
        };

        var nameBlock = new TextBlock
        {
            Text = name,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            FontSize = 11,
        };

        var valueBlock = new TextBlock
        {
            Text = value,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(valueBlock, 1);

        grid.Children.Add(nameBlock);
        grid.Children.Add(valueBlock);
        return grid;
    }
}
