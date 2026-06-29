using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Ferbento.Profiles.BuiltIn;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Views;

public class ProfileEditorView : UserControl
{
    private TextBlock _titleBlock = null!;
    private TextBlock _descriptionBlock = null!;
    private StackPanel _propertiesPanel = null!;
    private Func<HardwareProfile, Task<bool>>? _onProfileSelected;

    public ProfileEditorView()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Content = BuildLayout();
    }

    public void SetProfileChangeHandler(Func<HardwareProfile, Task<bool>> handler) =>
        _onProfileSelected = handler;

    public void ShowProfile(HardwareProfile profile) => UpdateDisplay(profile);

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
        };
        foreach (var profile in BuiltInProfiles.All)
        {
            var p = profile;
            var btn = CreateButton(p.Name, "#569CD6");
            btn.Click += (_, _) => SelectProfile(p);
            toolbar.Children.Add(btn);
        }
        toolbar.Children.Add(CreateButton("Import JSON...", "#DCDCAA"));
        Grid.SetRow(toolbar, 1);
        Grid.SetColumnSpan(toolbar, 3);

        _propertiesPanel = new StackPanel { Spacing = 2 };
        var sidebar = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            Padding = new Thickness(4),
            Child = _propertiesPanel,
        };
        Grid.SetRow(sidebar, 2);

        var divider = new GridSplitter
        {
            Width = 4,
            Background = new SolidColorBrush(Color.Parse("#3F3F46")),
        };
        Grid.SetColumn(divider, 1);
        Grid.SetRow(divider, 2);

        _titleBlock = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#569CD6")),
            FontSize = 18,
            FontWeight = FontWeight.Bold,
        };
        _descriptionBlock = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
        };

        var details = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    _titleBlock,
                    _descriptionBlock,
                    new Border
                    {
                        Background = new SolidColorBrush(Color.Parse("#2D2D30")),
                        Padding = new Thickness(8),
                        Margin = new Thickness(0, 8, 0, 0),
                        Child = new TextBlock
                        {
                            Text = "Changing profile reconfigures sprite sheet, tilemap, and palette editors.",
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

        UpdateDisplay(BuiltInProfiles.Pico8());
        return root;
    }

    private async void SelectProfile(HardwareProfile profile)
    {
        if (_onProfileSelected != null)
        {
            var applied = await _onProfileSelected(profile);
            if (applied)
                UpdateDisplay(profile);
            return;
        }

        UpdateDisplay(profile);
    }

    private void UpdateDisplay(HardwareProfile profile)
    {
        _titleBlock.Text = profile.Name;
        _descriptionBlock.Text =
            $"Resolution {profile.Resolution.Width}×{profile.Resolution.Height}, " +
            $"{profile.Palette.Colors} colors, sprite sheet {profile.Sprites.SheetWidth}×{profile.Sprites.SheetHeight} " +
            $"({profile.Sprites.TileSize}px tiles), map {profile.Map.Width}×{profile.Map.Height}.";

        _propertiesPanel.Children.Clear();
        _propertiesPanel.Children.Add(CreateSectionLabel("Display"));
        _propertiesPanel.Children.Add(CreateProperty("Width", profile.Resolution.Width.ToString()));
        _propertiesPanel.Children.Add(CreateProperty("Height", profile.Resolution.Height.ToString()));
        _propertiesPanel.Children.Add(CreateSectionLabel("Palette"));
        _propertiesPanel.Children.Add(CreateProperty("Colors", profile.Palette.Colors.ToString()));
        _propertiesPanel.Children.Add(CreateProperty("Transparent", profile.Palette.TransparentIndex.ToString()));
        _propertiesPanel.Children.Add(CreateSectionLabel("Sprites"));
        _propertiesPanel.Children.Add(CreateProperty("Sheet W", profile.Sprites.SheetWidth.ToString()));
        _propertiesPanel.Children.Add(CreateProperty("Sheet H", profile.Sprites.SheetHeight.ToString()));
        _propertiesPanel.Children.Add(CreateProperty("Tile Size", profile.Sprites.TileSize.ToString()));
        _propertiesPanel.Children.Add(CreateProperty("Max OnScreen", profile.Sprites.MaxOnScreen.ToString()));
        _propertiesPanel.Children.Add(CreateSectionLabel("Map"));
        _propertiesPanel.Children.Add(CreateProperty("Width", profile.Map.Width.ToString()));
        _propertiesPanel.Children.Add(CreateProperty("Height", profile.Map.Height.ToString()));
        _propertiesPanel.Children.Add(CreateSectionLabel("Memory"));
        _propertiesPanel.Children.Add(CreateProperty("RAM", $"{profile.Memory.Ram / 1024} KB"));
        _propertiesPanel.Children.Add(CreateProperty("Cart ROM", $"{profile.Memory.CartRom / 1024} KB"));
        _propertiesPanel.Children.Add(CreateSectionLabel("Audio"));
        _propertiesPanel.Children.Add(CreateProperty("Channels", profile.Audio.Channels.ToString()));
        _propertiesPanel.Children.Add(CreateProperty("SFX Slots", profile.Audio.SfxSlots.ToString()));
        _propertiesPanel.Children.Add(CreateProperty("Octave Min", profile.Audio.SfxOctaveMin.ToString()));
        _propertiesPanel.Children.Add(CreateProperty("Octave Max", profile.Audio.SfxOctaveMax.ToString()));
    }

    private static Button CreateButton(string text, string color) => new()
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

    private static TextBlock CreateSectionLabel(string text) => new()
    {
        Text = text,
        Foreground = new SolidColorBrush(Color.Parse("#569CD6")),
        FontSize = 11,
        FontWeight = FontWeight.Bold,
        Margin = new Thickness(0, 8, 0, 2),
    };

    private static Control CreateProperty(string name, string value)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 1),
        };
        grid.Children.Add(new TextBlock
        {
            Text = name,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            FontSize = 11,
        });
        var valueBlock = new TextBlock
        {
            Text = value,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(valueBlock);
        return grid;
    }
}
