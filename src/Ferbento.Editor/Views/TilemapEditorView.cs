using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Ferbento.Core.Chips;
using Ferbento.Core.Memory;
using Ferbento.Editor.ViewModels;

namespace Ferbento.Editor.Views;

public class TilemapEditorView : UserControl
{
    private readonly TilemapEditorViewModel _viewModel = new();
    private WriteableBitmap? _bitmap;
    private Image _canvasImage = null!;
    private ChipBus? _bus;

    private const int TileSize = 8;
    private const int Zoom = 2;
    private static readonly uint[] Pico8Palette = new uint[]
    {
        0xFF000000, 0xFF1D2B53, 0xFF7E2553, 0xFF008751,
        0xFFAB5236, 0xFF5F574F, 0xFFC2C3C7, 0xFFFFF1E8,
        0xFFFF004D, 0xFFFFA300, 0xFFFFEC27, 0xFF00E436,
        0xFF29ADFF, 0xFF83769C, 0xFFFF77A8, 0xFFFFCCAA
    };

    public TilemapEditorView()
    {
        DataContext = _viewModel;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        Content = BuildLayout();
        
        Loaded += (_, _) => InitializeBitmap();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TilemapEditorViewModel.CurrentLayer))
                RefreshCanvas();
        };
    }

    public void SetChipBus(ChipBus bus)
    {
        _bus = bus;
        RefreshCanvas();
    }

    private void InitializeBitmap()
    {
        if (_bus == null) return;
        
        int width = _bus.Tilemap.GetType().GetProperty("Width")?.GetValue(_bus.Tilemap) as int? ?? 128;
        int height = _bus.Tilemap.GetType().GetProperty("Height")?.GetValue(_bus.Tilemap) as int? ?? 64;
        
        _bitmap = new WriteableBitmap(
            new PixelSize(width * TileSize * 2, height * TileSize * 2),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
        
        _canvasImage.Source = _bitmap;
        RefreshCanvas();
    }

    private void RefreshCanvas()
    {
        if (_bitmap == null || _bus == null) return;

        int mapWidth = _bus.Tilemap.GetType().GetProperty("Width")?.GetValue(_bus.Tilemap) as int? ?? 128;
        int mapHeight = _bus.Tilemap.GetType().GetProperty("Height")?.GetValue(_bus.Tilemap) as int? ?? 64;
        int zoom = 2;

        using var locked = _bitmap.Lock();
        var stride = locked.RowBytes;
        var ptr = locked.Address;

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int tileId = _bus.Tilemap.MGet(x, y);
                int spriteX = (tileId % 16) * 8;
                int spriteY = (tileId / 16) * 8;

                for (int ty = 0; ty < 8; ty++)
                {
                    for (int tx = 0; tx < 8; tx++)
                    {
                        int colorIndex = _bus.Sprite.SGet(spriteX + tx, spriteY + ty);
                        uint color = 0xFF000000;
                        if (colorIndex != 0)
                        {
                            color = 0xFF000000 | (uint)colorIndex;
                        }
                        
                        for (int zy = 0; zy < 2; zy++)
                        {
                            for (int zx = 0; zx < 2; zx++)
                            {
                                int px = (x * 8 + tx) * 2 + zx;
                                int py = (y * 8 + ty) * 2 + zy;
                                int offset = (py * stride) + (px * 4);
                                unsafe
                                {
                                    *(uint*)(ptr + offset) = colorIndex != 0 ? 0xFFFFFFFF : 0xFF000000;
                                }
                            }
                        }
                    }
                }
            }
        }
        _canvasImage.InvalidateVisual();
    }

    private Control BuildLayout()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,*"),
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        };

        var header = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D30")),
            Padding = new Thickness(8, 4),
            Child = new TextBlock
            {
                Text = "Tilemap Editor",
                Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                FontSize = 12,
            },
        };
        Grid.SetColumnSpan(header, 2);

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(8),
        };
        foreach (var tool in new[] { "pencil", "fill", "rect", "select", "layer-0", "layer-1", "layer-2", "layer-3", "flip-h", "flip-v", "rotate" })
        {
            var btn = new Button { Content = tool, FontSize = 11, Padding = new Thickness(6, 2), Tag = tool };
            btn.Click += (_, _) => OnToolClick(tool);
            toolbar.Children.Add(btn);
        }
        Grid.SetRow(toolbar, 0);
        Grid.SetColumn(toolbar, 1);

        var canvas = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            Margin = new Thickness(8),
        };
        
        _canvasImage = new Image
        {
            Stretch = Stretch.Uniform,
        };
        canvas.Child = _canvasImage;
        Grid.SetRow(canvas, 1);
        Grid.SetColumnSpan(canvas, 2);

        var sidebar = new StackPanel
        {
            Margin = new Thickness(8),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Layers", Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")), FontSize = 12 },
                new CheckBox { Content = "Layer 0", IsChecked = true, Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")) },
                new CheckBox { Content = "Layer 1", IsChecked = false, Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")) },
                new CheckBox { Content = "Layer 2", IsChecked = false, Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")) },
                new CheckBox { Content = "Layer 3", IsChecked = false, Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")) },
            }
        };
        Grid.SetRow(sidebar, 1);

        grid.Children.Add(header);
        grid.Children.Add(toolbar);
        grid.Children.Add(canvas);
        grid.Children.Add(sidebar);

        return grid;
    }

    private void OnToolClick(string tool)
    {
        switch (tool)
        {
            case "flip-h":
                _viewModel.FlipX();
                break;
            case "flip-v":
                _viewModel.FlipY();
                break;
            case "rotate":
                _viewModel.Rotate();
                break;
            case "fill":
                _viewModel.FillMap();
                break;
            case "stamp":
                _viewModel.Stamp();
                break;
        }
    }
}
