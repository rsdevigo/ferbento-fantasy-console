using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ferbento.Editor.Views;

public class AssetBrowserView : UserControl
{
    public AssetBrowserView()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Content = BuildLayout();
    }

    private Control BuildLayout()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("200,4,*"),
            RowDefinitions = new RowDefinitions("Auto,*"),
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        };

        var header = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D30")),
            Padding = new Thickness(8, 4),
            Child = new TextBlock
            {
                Text = "Asset Browser",
                Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                FontSize = 12,
            },
        };
        Grid.SetColumnSpan(header, 3);

        var categories = new ListBox
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            ItemsSource = new[] { "Sprites", "Tilemaps", "SFX", "Music", "Code", "Profiles" },
        };
        Grid.SetRow(categories, 1);

        var divider = new GridSplitter
        {
            Width = 4,
            Background = new SolidColorBrush(Color.Parse("#3F3F46")),
        };
        Grid.SetColumn(divider, 1);
        Grid.SetRow(divider, 1);

        var content = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            Child = new TextBlock
            {
                Text = "Select an asset category to browse",
                Foreground = new SolidColorBrush(Color.Parse("#888888")),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 14,
            },
        };
        Grid.SetColumn(content, 2);
        Grid.SetRow(content, 1);

        grid.Children.Add(header);
        grid.Children.Add(categories);
        grid.Children.Add(divider);
        grid.Children.Add(content);

        return grid;
    }
}
