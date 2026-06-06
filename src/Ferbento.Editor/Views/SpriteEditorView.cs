using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Ferbento.Core.Chips;
using Ferbento.Editor.ViewModels;

namespace Ferbento.Editor.Views;

public class SpriteEditorView : UserControl
{
    private readonly SpriteEditorViewModel _viewModel = new();
    private WriteableBitmap? _bitmap;
    private Image _canvasImage = null!;
    private ChipBus? _bus;

    private const int SpriteSize = 8;
    private const int Zoom = 4;
    private static readonly uint[] Pico8Palette = new uint[]
    {
        0xFF000000, 0xFF1D2B53, 0xFF7E2553, 0xFF008751,
        0xFFAB5236, 0xFF5F574F, 0xFFC2C3C7, 0xFFFFF1E8,
        0xFFFF004D, 0xFFFFA300, 0xFFFFEC27, 0xFF00E436,
        0xFF29ADFF, 0xFF83769C, 0xFFFF77A8, 0xFFFFCCAA
    };

    public SpriteEditorView()
    {
        DataContext = _viewModel;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        Content = BuildLayout();
        
        Loaded += (_, _) => InitializeBitmap();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SpriteEditorViewModel.CurrentSpriteIndex) ||
                e.PropertyName == nameof(SpriteEditorViewModel.SelectedColor))
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
        _bitmap = new WriteableBitmap(
            new PixelSize(SpriteSize * Zoom, SpriteSize * Zoom),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
        
        _canvasImage.Source = _bitmap;
        RefreshCanvas();
    }

    private void RefreshCanvas()
    {
        if (_bitmap == null || _bus == null) return;

        var spriteIndex = _viewModel.CurrentSpriteIndex;
        var spriteX = (spriteIndex % 16) * SpriteSize;
        var spriteY = (spriteIndex / 16) * SpriteSize;

        using var locked = _bitmap.Lock();
        var stride = locked.RowBytes;
        var ptr = locked.Address;

        for (int y = 0; y < SpriteSize; y++)
        {
            for (int x = 0; x < SpriteSize; x++)
            {
                int colorIndex = _bus.Sprite.SGet(spriteX + x, spriteY + y);
                uint color = Pico8Palette[colorIndex & 0xF];
                
                for (int zy = 0; zy < Zoom; zy++)
                {
                    for (int zx = 0; zx < Zoom; zx++)
                    {
                        int offset = ((y * Zoom + zy) * stride) + ((x * Zoom + zx) * 4);
                        unsafe
                        {
                            *(uint*)(ptr + offset) = color;
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
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        };

        var canvas = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#3F3F46")),
            Width = SpriteSize * Zoom,
            Height = SpriteSize * Zoom,
            Margin = new Thickness(12),
            BorderBrush = new SolidColorBrush(Color.Parse("#555555")),
            BorderThickness = new Thickness(1),
        };
        
        _canvasImage = new Image
        {
            Stretch = Stretch.None,
        };
        _canvasImage.PointerPressed += OnCanvasPointerPressed;
        _canvasImage.PointerMoved += OnCanvasPointerMoved;
        canvas.Child = _canvasImage;
        Grid.SetRowSpan(canvas, 3);

        var palette = new WrapPanel
        {
            Margin = new Thickness(0, 12, 12, 6),
            Width = 200,
        };
        for (var i = 0; i < 16; i++)
        {
            var idx = i;
            var color = Pico8Palette[i];
            var r = (byte)((color >> 16) & 0xFF);
            var g = (byte)((color >> 8) & 0xFF);
            var b = (byte)(color & 0xFF);
            var swatch = new Border
            {
                Width = 24, Height = 24,
                Background = new SolidColorBrush(Color.FromRgb(r, g, b)),
                BorderBrush = i == _viewModel.SelectedColor ? Brushes.White : Brushes.Transparent,
                BorderThickness = new Thickness(i == _viewModel.SelectedColor ? 2 : 0),
                Tag = i,
                Margin = new Thickness(1),
            };
            swatch.PointerPressed += (_, _) => 
            {
                _viewModel.SelectedColor = idx;
                foreach (var child in palette.Children)
                {
                    if (child is Border b && b.Tag is int t)
                        b.BorderThickness = new Thickness(t == idx ? 2 : 0);
                }
            };
            palette.Children.Add(swatch);
        }
        Grid.SetColumn(palette, 1);

        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(0, 0, 12, 6),
        };
        Grid.SetColumn(tools, 1);
        Grid.SetRow(tools, 1);
        foreach (var tool in new[] { "pencil", "fill", "picker", "select", "flip-h", "flip-v", "copy", "paste" })
        {
            var btn = new Button { Content = tool, FontSize = 11, Padding = new Thickness(6, 2), Tag = tool };
            btn.Click += (_, _) => OnToolClick(tool);
            tools.Children.Add(btn);
        }

        var info = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Margin = new Thickness(0, 0, 12, 12),
            VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                new TextBlock { Text = "Sprite #0", Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")), FontSize = 12 },
                new TextBlock { Text = "8×8 px", Foreground = new SolidColorBrush(Color.Parse("#888888")), FontSize = 12 },
            },
        };
        Grid.SetColumn(info, 1);
        Grid.SetRow(info, 2);

        grid.Children.Add(canvas);
        grid.Children.Add(palette);
        grid.Children.Add(tools);
        grid.Children.Add(info);

        return grid;
    }

    private bool _isDrawing = false;

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDrawing = true;
        var point = e.GetPosition(_canvasImage);
        DrawAtPoint(point);
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDrawing) return;
        var point = e.GetPosition(_canvasImage);
        DrawAtPoint(point);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDrawing)
        {
            _isDrawing = false;
            _viewModel.UndoRedo.Push("draw", GetSpriteData(), 0, SpriteSize * SpriteSize);
        }
    }

    private void DrawAtPoint(Point point)
    {
        if (_bus == null) return;

        int x = (int)(point.X / Zoom);
        int y = (int)(point.Y / Zoom);
        
        if (x < 0 || x >= SpriteSize || y < 0 || y >= SpriteSize) return;

        var spriteIndex = _viewModel.CurrentSpriteIndex;
        var spriteX = (spriteIndex % 16) * SpriteSize;
        var spriteY = (spriteIndex / 16) * SpriteSize;

        _bus.Sprite.SSet(spriteX + x, spriteY + y, _viewModel.SelectedColor);
        RefreshCanvas();
    }

    private byte[] GetSpriteData()
    {
        if (_bus == null) return new byte[SpriteSize * SpriteSize];
        
        var data = new byte[SpriteSize * SpriteSize];
        var spriteIndex = _viewModel.CurrentSpriteIndex;
        var spriteX = (spriteIndex % 16) * SpriteSize;
        var spriteY = (spriteIndex / 16) * SpriteSize;

        for (int y = 0; y < SpriteSize; y++)
        {
            for (int x = 0; x < SpriteSize; x++)
            {
                data[y * SpriteSize + x] = (byte)_bus.Sprite.SGet(spriteX + x, spriteY + y);
            }
        }
        return data;
    }

    private void OnToolClick(string tool)
    {
        switch (tool)
        {
            case "fill":
                _viewModel.FillSprite(_viewModel.SelectedColor);
                break;
            case "picker":
                _viewModel.SelectedTool = "picker";
                break;
            case "flip-h":
                _viewModel.FlipX = !_viewModel.FlipX;
                break;
            case "flip-v":
                _viewModel.FlipY = !_viewModel.FlipY;
                break;
            case "copy":
                _viewModel.CopySprite();
                break;
            case "paste":
                _viewModel.PasteSprite();
                break;
        }
    }
}
