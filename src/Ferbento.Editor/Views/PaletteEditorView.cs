using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ferbento.Editor.Views;

public class PaletteEditorView : UserControl
{
    private StackPanel _editArea = null!;
    private TextBlock? _colorNameBlock;
    private TextBlock? _colorHexBlock;
    private TextBlock? _colorRgbBlock;
    private TextBlock? _colorIndexBlock;

    public PaletteEditorView()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Content = BuildLayout();
    }

    private Control BuildLayout()
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        };

        var header = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D30")),
            Padding = new Thickness(8, 4),
            Child = new TextBlock
            {
                Text = "Palette Editor — 16 colors (Pico-8)",
                Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                FontSize = 12,
            },
        };

        var pRgb = new[] { "#000000", "#1D2B53", "#7E2553", "#008751", "#AB5236", "#5F574F", "#C2C3C7", "#FFF1E8", "#FF004D", "#FFA300", "#FFEC27", "#00E436", "#29ADFF", "#83769C", "#FF77A8", "#FFCCAA" };
        var pNames = new[] { "black", "dark-blue", "dark-purple", "dark-green", "brown", "dark-grey", "light-grey", "white", "red", "orange", "yellow", "green", "blue", "indigo", "pink", "peach" };
        var palette = new WrapPanel { Margin = new Thickness(12) };
        for (var i = 0; i < 16; i++)
        {
            var idx = i;
            var color = pRgb[i];
            var r = byte.Parse(color.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
            var g = byte.Parse(color.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
            var b = byte.Parse(color.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
            
            var swatch = new Border
            {
                Width = 70, Height = 50,
                Background = new SolidColorBrush(Color.Parse(color)),
                BorderBrush = i == 0 ? Brushes.White : Brushes.Transparent,
                BorderThickness = new Thickness(i == 0 ? 2 : 0),
                Margin = new Thickness(2),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"{i}",
                            FontSize = 10,
                            Foreground = i < 8 ? Brushes.White : Brushes.Black,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 4, 0, 0),
                        },
                        new TextBlock
                        {
                            Text = pNames[i],
                            FontSize = 9,
                            Foreground = i < 8 ? Brushes.White : Brushes.Black,
                            HorizontalAlignment = HorizontalAlignment.Center,
                        },
                    }
                },
            };
            swatch.PointerPressed += (_, _) =>
            {
                foreach (var child in palette.Children)
                {
                    if (child is Border b)
                        b.BorderThickness = new Thickness(0);
                }
                swatch.BorderThickness = new Thickness(2);
                _colorNameBlock!.Text = pNames[idx];
                _colorHexBlock!.Text = color.ToUpper();
                _colorRgbBlock!.Text = $"RGB({r}, {g}, {b})";
                _colorIndexBlock!.Text = $"Index: {idx}";
            };
            palette.Children.Add(swatch);
        }
        Grid.SetRow(palette, 1);

        _editArea = new StackPanel
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            Margin = new Thickness(12),
            Spacing = 6,
            Children =
            {
                new Border { Padding = new Thickness(12), Child = _colorIndexBlock = new TextBlock
                {
                    Text = "Index: 0",
                    Foreground = new SolidColorBrush(Color.Parse("#569CD6")),
                    FontSize = 12,
                    FontWeight = FontWeight.Bold,
                }},
                new Border { Padding = new Thickness(12, 0, 12, 0), Child = _colorNameBlock = new TextBlock
                {
                    Text = "black",
                    Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                    FontSize = 11,
                }},
                new Border { Padding = new Thickness(12, 0, 12, 0), Child = _colorHexBlock = new TextBlock
                {
                    Text = "#000000",
                    Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                    FontSize = 11,
                }},
                new Border { Padding = new Thickness(12, 0, 12, 0), Child = _colorRgbBlock = new TextBlock
                {
                    Text = "RGB(0, 0, 0)",
                    Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                    FontSize = 11,
                }},
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    Children =
                    {
                        new Button { Content = "Edit", Padding = new Thickness(8, 3), Width = 60 },
                        new Button { Content = "Reset", Padding = new Thickness(8, 3), Width = 60 },
                        new Button { Content = "Copy", Padding = new Thickness(8, 3), Width = 60 },
                    }
                },
            },
        };
        Grid.SetRow(_editArea, 2);

        grid.Children.Add(header);
        grid.Children.Add(palette);
        grid.Children.Add(_editArea);

        return grid;
    }
}
