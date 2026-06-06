using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Ferbento.Core.Chips;
using Ferbento.Editor.ViewModels;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Views;

public class TilemapEditorView : UserControl, IProfileAwareEditor
{
    private readonly TilemapEditorViewModel _vm = new();
    private WriteableBitmap? _mapBitmap;
    private WriteableBitmap? _tilePickerBitmap;
    private Image _mapImage = null!;
    private Image _tilePickerImage = null!;
    private TextBlock _tileLabel = null!;
    private ChipBus? _bus;
    private Action? _onEdited;
    private readonly List<Button> _layerButtons = [];

    public void SetEditedHandler(Action handler) => _onEdited = handler;

    private void NotifyEdited() => _onEdited?.Invoke();

    private const int MapZoom = 2;
    private const int PickerZoom = 3;

    private bool _isPainting;
    private bool _strokeUndoPushed;
    private (int x, int y)? _rectStart;
    private (int x, int y, int w, int h)? _selection;
    private int _lastUndoX;
    private int _lastUndoY;
    private int _lastUndoW;
    private int _lastUndoH;

    public TilemapEditorView()
    {
        DataContext = _vm;
        Content = BuildLayout();
        Loaded += (_, _) => InitializeBitmaps();

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(TilemapEditorViewModel.CurrentLayer)
                or nameof(TilemapEditorViewModel.SelectedTile))
            {
                UpdateTileLabel();
                HighlightSelectedTile();
                if (e.PropertyName == nameof(TilemapEditorViewModel.CurrentLayer))
                {
                    UpdateLayerButtons();
                    RefreshMap();
                }
            }
            else if (e.PropertyName is nameof(TilemapEditorViewModel.MapWidth)
                or nameof(TilemapEditorViewModel.MapHeight)
                or nameof(TilemapEditorViewModel.TileSize)
                or nameof(TilemapEditorViewModel.SheetWidth)
                or nameof(TilemapEditorViewModel.SheetHeight))
            {
                InitializeBitmaps();
            }
        };
    }

    public void SetProfile(HardwareProfile profile) => _vm.SetProfile(profile);

    public void SetChipBus(ChipBus bus)
    {
        _bus = bus;
        _vm.SetChipBus(bus);
        _vm.Edited += NotifyEdited;
        InitializeBitmaps();
        RefreshMap();
        RefreshTilePicker();
    }

    public void RefreshPalette()
    {
        RefreshMap();
        RefreshTilePicker();
    }

    public void RefreshFromBus()
    {
        InitializeBitmaps();
        RefreshMap();
        RefreshTilePicker();
    }

    private void InitializeBitmaps()
    {
        if (_bus == null) return;

        var tile = _vm.TileSize;
        var w = _vm.MapWidth * tile * MapZoom;
        var h = _vm.MapHeight * tile * MapZoom;
        _mapBitmap = new WriteableBitmap(
            new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        _tilePickerBitmap = new WriteableBitmap(
            new PixelSize(_vm.SheetWidth * PickerZoom, _vm.SheetHeight * PickerZoom),
            new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

        _mapImage.Source = _mapBitmap;
        _tilePickerImage.Source = _tilePickerBitmap;
        RefreshMap();
        RefreshTilePicker();
    }

    private void RefreshMap()
    {
        if (_mapBitmap == null || _bus == null) return;
        var tile = _vm.TileSize;

        using var locked = _mapBitmap.Lock();
        var stride = locked.RowBytes;
        var ptr = locked.Address;

        for (var ty = 0; ty < _vm.MapHeight; ty++)
        for (var tx = 0; tx < _vm.MapWidth; tx++)
        {
            var tileId = _bus.Tilemap.MGet(tx, ty);
            if (_bus.Sprite.FGet(tileId, _vm.CurrentLayer) == 0)
            {
                FillTileBackground(ptr, stride, tx, ty, tile);
                continue;
            }

            var col = tileId % _vm.SpritesPerRow;
            var row = tileId / _vm.SpritesPerRow;
            var spriteX = col * tile;
            var spriteY = row * tile;

            for (var py = 0; py < tile; py++)
            for (var px = 0; px < tile; px++)
            {
                var colorIndex = _bus.Sprite.SGet(spriteX + px, spriteY + py);
                var color = Pico8Colors.ToBgra(colorIndex, _vm.ColorChip);
                var mapPx = tx * tile + px;
                var mapPy = ty * tile + py;
                FillPixelBlock(ptr, stride, mapPx, mapPy, MapZoom, color);
            }
        }

        if (_selection is { } sel)
            DrawSelectionRect(ptr, stride, sel.x, sel.y, sel.w, sel.h);

        _mapImage.InvalidateVisual();
    }

    private void RefreshTilePicker()
    {
        if (_tilePickerBitmap == null || _bus == null) return;

        using var locked = _tilePickerBitmap.Lock();
        var stride = locked.RowBytes;
        var ptr = locked.Address;

        for (var y = 0; y < _vm.SheetHeight; y++)
        for (var x = 0; x < _vm.SheetWidth; x++)
        {
            var color = Pico8Colors.ToBgra(_bus.Sprite.SGet(x, y), _vm.ColorChip);
            FillPixelBlock(ptr, stride, x, y, PickerZoom, color);
        }

        HighlightSelectedTileInPicker(ptr, stride);
        _tilePickerImage.InvalidateVisual();
    }

    private void HighlightSelectedTileInPicker(nint ptr, int stride)
    {
        var tile = _vm.TileSize;
        var tileId = _vm.SelectedTile;
        var ox = (tileId % _vm.SpritesPerRow) * tile;
        var oy = (tileId / _vm.SpritesPerRow) * tile;
        var highlight = 0xFFFFCC00;
        for (var edge = 0; edge < tile; edge++)
        for (var t = 0; t < tile; t++)
        {
            FillPixelBlock(ptr, stride, ox + t, oy + edge, PickerZoom, highlight);
            FillPixelBlock(ptr, stride, ox + edge, oy + t, PickerZoom, highlight);
        }
    }

    private void HighlightSelectedTile() => RefreshTilePicker();

    private static unsafe void FillPixelBlock(nint ptr, int stride, int px, int py, int zoom, uint color)
    {
        for (var zy = 0; zy < zoom; zy++)
        for (var zx = 0; zx < zoom; zx++)
        {
            var offset = ((py * zoom + zy) * stride) + ((px * zoom + zx) * 4);
            *(uint*)(ptr + offset) = color;
        }
    }

    private static unsafe void FillTileBackground(nint ptr, int stride, int tx, int ty, int tileSize)
    {
        var bg = 0xFF1A1A1A;
        for (var py = 0; py < tileSize; py++)
        for (var px = 0; px < tileSize; px++)
        {
            var checker = ((tx + px) + (ty + py)) % 2 == 0 ? bg : 0xFF222222;
            FillPixelBlock(ptr, stride, tx * tileSize + px, ty * tileSize + py, MapZoom, checker);
        }
    }

    private void DrawSelectionRect(nint ptr, int stride, int tx, int ty, int tw, int th)
    {
        var tile = _vm.TileSize;
        var color = 0xFFFFCC00;
        var left = tx * tile * MapZoom;
        var top = ty * tile * MapZoom;
        var right = (tx + tw) * tile * MapZoom;
        var bottom = (ty + th) * tile * MapZoom;

        unsafe
        {
            for (var x = left; x < right; x++)
            {
                *(uint*)(ptr + top * stride + x * 4) = color;
                *(uint*)(ptr + (bottom - 1) * stride + x * 4) = color;
            }
            for (var y = top; y < bottom; y++)
            {
                *(uint*)(ptr + y * stride + left * 4) = color;
                *(uint*)(ptr + y * stride + (right - 1) * 4) = color;
            }
        }
    }

    private Control BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        };

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(8, 4),
        };
        foreach (var (tool, label) in new (string, string)[]
        {
            ("pencil", "✏ Paint"), ("fill", "🪣 Fill"), ("rect", "▭ Rect"),
            ("select", "⬚ Select"), ("copy", "📋"), ("paste", "📌"),
        })
        {
            var t = tool;
            var btn = new Button { Content = label, FontSize = 11, Padding = new Thickness(6, 2), Tag = t };
            btn.Click += (_, _) => _vm.SelectedTool = t;
            toolbar.Children.Add(btn);
        }

        for (var layer = 0; layer < 8; layer++)
        {
            var l = layer;
            var layerBtn = new Button
            {
                Content = $"L{layer}",
                FontSize = 11,
                Padding = new Thickness(6, 2),
                Tag = l,
            };
            layerBtn.Click += (_, _) => _vm.CurrentLayer = l;
            _layerButtons.Add(layerBtn);
            toolbar.Children.Add(layerBtn);
        }
        UpdateLayerButtons();

        var undoBtn = new Button { Content = "Undo", FontSize = 11, Padding = new Thickness(6, 2) };
        undoBtn.Click += (_, _) =>
        {
            _vm.UndoRegion(_lastUndoX, _lastUndoY, _lastUndoW, _lastUndoH);
            RefreshMap();
        };
        toolbar.Children.Add(undoBtn);

        Grid.SetColumnSpan(toolbar, 2);
        root.Children.Add(toolbar);

        var left = new StackPanel { Margin = new Thickness(8), Spacing = 4, Width = 400 };
        left.Children.Add(new TextBlock { Text = "Tiles", Foreground = Brushes.LightGray, FontSize = 11 });
        _tilePickerImage = new Image { Stretch = Stretch.None };
        _tilePickerImage.PointerPressed += OnTilePickerPressed;
        left.Children.Add(new ScrollViewer { MaxHeight = 220, Content = _tilePickerImage });
        _tileLabel = new TextBlock { Text = "Tile #0", Foreground = Brushes.LightGray, FontSize = 11 };
        left.Children.Add(_tileLabel);

        _mapImage = new Image { Stretch = Stretch.None };
        _mapImage.PointerPressed += OnMapPointerPressed;
        _mapImage.PointerMoved += OnMapPointerMoved;

        Grid.SetRow(left, 1);
        Grid.SetRow(_mapImage, 1);
        Grid.SetColumn(_mapImage, 1);
        root.Children.Add(left);
        var mapScroller = new ScrollViewer
        {
            Margin = new Thickness(8),
            Content = _mapImage,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Grid.SetRow(mapScroller, 1);
        Grid.SetColumn(mapScroller, 1);
        root.Children.Add(mapScroller);

        return root;
    }

    private void UpdateTileLabel() => _tileLabel.Text = $"Tile #{_vm.SelectedTile}  Layer {_vm.CurrentLayer}";

    private void UpdateLayerButtons()
    {
        foreach (var btn in _layerButtons)
        {
            if (btn.Tag is int layer)
            {
                btn.Background = layer == _vm.CurrentLayer
                    ? new SolidColorBrush(Color.Parse("#569CD6"))
                    : Brushes.Transparent;
                btn.Foreground = layer == _vm.CurrentLayer ? Brushes.White : Brushes.LightGray;
            }
        }
    }

    private void OnTilePickerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pt = e.GetPosition(_tilePickerImage);
        var sx = (int)(pt.X / PickerZoom);
        var sy = (int)(pt.Y / PickerZoom);
        if (sx < 0 || sy < 0 || sx >= _vm.SheetWidth || sy >= _vm.SheetHeight) return;
        var col = sx / _vm.TileSize;
        var row = sy / _vm.TileSize;
        _vm.SelectedTile = row * _vm.SpritesPerRow + col;
        HighlightSelectedTile();
    }

    private void OnMapPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isPainting = true;
        _strokeUndoPushed = false;
        var (tx, ty) = MapCoords(e);
        HandleMapAction(tx, ty, isStart: true);
    }

    private void OnMapPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPainting) return;
        var (tx, ty) = MapCoords(e);
        HandleMapAction(tx, ty, isStart: false);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isPainting && _vm.SelectedTool == "rect" && _rectStart is { } start)
        {
            var (tx, ty) = MapCoords(e);
            _vm.FillRect(start.x, start.y, tx, ty);
            RecordUndoRegion(start.x, start.y, tx, ty);
            RefreshMap();
        }
        _isPainting = false;
        _rectStart = null;
    }

    private (int x, int y) MapCoords(PointerEventArgs e)
    {
        var pt = e.GetPosition(_mapImage);
        var tile = _vm.TileSize;
        var tx = (int)(pt.X / (tile * MapZoom));
        var ty = (int)(pt.Y / (tile * MapZoom));
        return (tx, ty);
    }

    private void HandleMapAction(int tx, int ty, bool isStart)
    {
        if (tx < 0 || ty < 0 || tx >= _vm.MapWidth || ty >= _vm.MapHeight) return;

        switch (_vm.SelectedTool)
        {
            case "fill":
                if (isStart)
                {
                    _vm.FloodFill(tx, ty);
                    RecordUndoRegion(0, 0, _vm.MapWidth - 1, _vm.MapHeight - 1);
                    RefreshMap();
                }
                break;
            case "rect":
                if (isStart) _rectStart = (tx, ty);
                break;
            case "select":
                if (isStart) _rectStart = (tx, ty);
                else if (_rectStart is { } s)
                {
                    var left = Math.Min(s.x, tx);
                    var top = Math.Min(s.y, ty);
                    var w = Math.Abs(tx - s.x) + 1;
                    var h = Math.Abs(ty - s.y) + 1;
                    _selection = (left, top, w, h);
                    RefreshMap();
                }
                break;
            case "copy":
                if (isStart && _selection is { } sel)
                    _vm.CopySelection(sel.x, sel.y, sel.w, sel.h);
                break;
            case "paste":
                if (isStart)
                {
                    _vm.PasteSelection(tx, ty);
                    RecordUndoRegion(tx, ty, tx, ty);
                    RefreshMap();
                }
                break;
            case "pencil":
            default:
                if (!_strokeUndoPushed)
                {
                    _vm.PushUndoRegion(tx, ty, 1, 1, "paint");
                    _strokeUndoPushed = true;
                    _lastUndoX = tx;
                    _lastUndoY = ty;
                    _lastUndoW = 1;
                    _lastUndoH = 1;
                }
                _vm.PaintTile(tx, ty);
                RefreshMap();
                break;
        }
    }

    private void RecordUndoRegion(int x0, int y0, int x1, int y1)
    {
        _lastUndoX = Math.Min(x0, x1);
        _lastUndoY = Math.Min(y0, y1);
        _lastUndoW = Math.Abs(x1 - x0) + 1;
        _lastUndoH = Math.Abs(y1 - y0) + 1;
    }
}
