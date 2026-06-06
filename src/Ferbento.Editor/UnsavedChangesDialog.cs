using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ferbento.Editor;

public enum UnsavedChangesChoice
{
    Save,
    DontSave,
    Cancel,
}

public static class UnsavedChangesDialog
{
    public static async Task<UnsavedChangesChoice> ShowAsync(
        Window owner,
        string projectName,
        string message)
    {
        var choice = UnsavedChangesChoice.Cancel;
        var dialog = new Window
        {
            Title = "Unsaved Changes",
            Width = 480,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new SolidColorBrush(Color.Parse("#252526")),
        };

        var saveBtn = new Button { Content = "Save", MinWidth = 90, Padding = new Thickness(12, 6) };
        var dontSaveBtn = new Button { Content = "Don't Save", MinWidth = 90, Padding = new Thickness(12, 6) };
        var cancelBtn = new Button { Content = "Cancel", MinWidth = 90, Padding = new Thickness(12, 6) };

        saveBtn.Click += (_, _) => { choice = UnsavedChangesChoice.Save; dialog.Close(); };
        dontSaveBtn.Click += (_, _) => { choice = UnsavedChangesChoice.DontSave; dialog.Close(); };
        cancelBtn.Click += (_, _) => { choice = UnsavedChangesChoice.Cancel; dialog.Close(); };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = projectName,
                    Foreground = new SolidColorBrush(Color.Parse("#569CD6")),
                    FontSize = 14,
                    FontWeight = FontWeight.Bold,
                },
                new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.LightGray,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { saveBtn, dontSaveBtn, cancelBtn },
                },
            },
        };

        await dialog.ShowDialog(owner);
        return choice;
    }
}
