using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ferbento.Editor.Views;

public class ProjectManagerView : UserControl
{
    public ProjectManagerView()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Content = BuildLayout();
    }

    private Control BuildLayout()
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        };

        var header = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D30")),
            Padding = new Thickness(8, 4),
            Child = new TextBlock
            {
                Text = "Project Manager",
                Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                FontSize = 12,
            },
        };
        Grid.SetColumnSpan(header, 2);

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(12, 8),
            Children =
            {
                new Button { Content = " New Project ", Padding = new Thickness(12, 4) },
                new Button { Content = " Open Project ", Padding = new Thickness(12, 4) },
                new Button { Content = " Save Project ", Padding = new Thickness(12, 4) },
                new Button { Content = " Save As... ", Padding = new Thickness(12, 4) },
            },
        };
        Grid.SetRow(toolbar, 1);
        Grid.SetColumnSpan(toolbar, 2);

        var projectList = new ListBox
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            Margin = new Thickness(12, 0, 6, 12),
            ItemsSource = new[] { "(no projects yet)" },
        };
        Grid.SetRow(projectList, 2);

        var info = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            Margin = new Thickness(6, 0, 12, 12),
            Width = 250,
            Child = new StackPanel
            {
                Spacing = 6,
                Margin = new Thickness(12),
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = "Project Info", Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")), FontSize = 13 },
                    new TextBlock { Text = "No project selected", Foreground = new SolidColorBrush(Color.Parse("#888888")), FontSize = 11 },
                },
            },
        };
        Grid.SetColumn(info, 1);
        Grid.SetRow(info, 2);

        grid.Children.Add(header);
        grid.Children.Add(toolbar);
        grid.Children.Add(projectList);
        grid.Children.Add(info);

        return grid;
    }
}
