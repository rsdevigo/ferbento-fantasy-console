using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Ferbento.Editor.Chrome;

public sealed class SpriteNavigatorView : UserControl
{
    private WriteableBitmap? _bitmap;
    private Image _image = null!;
    private readonly StackPanel _bankTabs = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private int _tileSize = 8;
    private int _bankCount = 1;
    private int _currentBank;
    private int _selectedTile;
    private int _zoom = 3;
    private HashSet<int> _highlightTiles = [];
    private Action<int, int, int, int, bool>? _tilePressed;
    private Action<int, int, int, int>? _tileDragSelect;
    private bool _dragging;
    private int _dragCol;
    private int _dragRow;

    public SpriteNavigatorView()
    {
        var root = new StackPanel { Spacing = 4 };
        root.Children.Add(_bankTabs);
        _image = new Image { Stretch = Stretch.None };
        _image.PointerPressed += OnPointerPressed;
        _image.PointerMoved += OnPointerMoved;
        _image.PointerReleased += OnPointerReleased;
        root.Children.Add(new ScrollViewer { MaxHeight = 140, Content = _image, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
    }

    public void Configure(int tileSize, int bankCount, int zoom = 3)
    {
        _tileSize = tileSize;
        _bankCount = Math.Max(1, bankCount);
        _zoom = zoom;
        RebuildBankTabs();
    }

    public void SetCallbacks(
        Action<int, int, int, int, bool> tilePressed,
        Action<int, int, int, int>? tileDragSelect = null)
    {
        _tilePressed = tilePressed;
        _tileDragSelect = tileDragSelect;
    }

    public void SetSelectedTile(int tileIndex, int bank)
    {
        _selectedTile = tileIndex;
        _currentBank = bank;
        UpdateBankHighlight();
    }

    public void SetHighlightTiles(IEnumerable<int> tiles)
    {
        _highlightTiles = tiles.ToHashSet();
    }

    public void Render(Action<nint, int, int, int, int> drawTile, int tilesPerRowInBank = 8)
    {
        var cols = tilesPerRowInBank;
        var rows = 8;
        var w = cols * _tileSize * _zoom;
        var h = rows * _tileSize * _zoom;
        _bitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var locked = _bitmap.Lock();
        Clear(locked.Address, locked.RowBytes, w, h, 0xFF1A1A1A);

        for (var row = 0; row < rows; row++)
        for (var col = 0; col < cols; col++)
        {
            var tileIndex = _currentBank * 64 + row * cols + col;
            drawTile(locked.Address, locked.RowBytes, col, row, tileIndex);
            if (tileIndex == _selectedTile || _highlightTiles.Contains(tileIndex))
                DrawHighlight(locked.Address, locked.RowBytes, col, row, tileIndex == _selectedTile ? 0xFFFFCC00u : 0xFF00CCFFu);
        }

        _image.Source = _bitmap;
    }

    private void RebuildBankTabs()
    {
        _bankTabs.Children.Clear();
        if (_bankCount <= 1) return;
        for (var b = 0; b < _bankCount; b++)
        {
            var bank = b;
            var btn = new Button
            {
                Content = b.ToString(),
                Padding = new Thickness(8, 2),
                FontSize = EditorTheme.SectionLabelFontSize,
            };
            btn.Click += (_, _) =>
            {
                _currentBank = bank;
                UpdateBankHighlight();
                BankChanged?.Invoke(bank);
            };
            _bankTabs.Children.Add(btn);
        }
        UpdateBankHighlight();
    }

    private void UpdateBankHighlight()
    {
        foreach (var child in _bankTabs.Children)
        {
            if (child is Button btn && btn.Content is string s && int.TryParse(s, out var b))
            {
                btn.Background = b == _currentBank
                    ? new SolidColorBrush(EditorTheme.ToolActive)
                    : Brushes.Transparent;
            }
        }
    }

    public event Action<int>? BankChanged;

    public int CurrentBank => _currentBank;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var (col, row) = HitTest(e);
        _dragging = true;
        _dragCol = col;
        _dragRow = row;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _tilePressed?.Invoke(col, row, _currentBank, TileIndex(col, row), shift);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging) return;
        var (col, row) = HitTest(e);
        _tileDragSelect?.Invoke(_dragCol, _dragRow, col, row);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => _dragging = false;

    private (int col, int row) HitTest(PointerEventArgs e)
    {
        var pt = e.GetPosition(_image);
        var col = (int)(pt.X / (_tileSize * _zoom));
        var row = (int)(pt.Y / (_tileSize * _zoom));
        return (Math.Clamp(col, 0, 7), Math.Clamp(row, 0, 7));
    }

    private int TileIndex(int col, int row) => _currentBank * 64 + row * 8 + col;

    private static unsafe void Clear(nint ptr, int stride, int w, int h, uint color)
    {
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
            *(uint*)(ptr + y * stride + x * 4) = color;
    }

    public static unsafe void FillPixelBlock(nint ptr, int stride, int px, int py, int zoom, uint color, int maxW, int maxH)
    {
        if (px < 0 || py < 0 || px >= maxW || py >= maxH) return;
        for (var zy = 0; zy < zoom; zy++)
        for (var zx = 0; zx < zoom; zx++)
            *(uint*)(ptr + (py * zoom + zy) * stride + (px * zoom + zx) * 4) = color;
    }

    private void DrawHighlight(nint ptr, int stride, int col, int row, uint color)
    {
        var tile = _tileSize;
        var ox = col * tile * _zoom;
        var oy = row * tile * _zoom;
        var w = tile * _zoom;
        var h = tile * _zoom;
        unsafe
        {
            for (var x = ox; x < ox + w; x++)
            {
                *(uint*)(ptr + oy * stride + x * 4) = color;
                *(uint*)(ptr + (oy + h - 1) * stride + x * 4) = color;
            }
            for (var y = oy; y < oy + h; y++)
            {
                *(uint*)(ptr + y * stride + ox * 4) = color;
                *(uint*)(ptr + y * stride + (ox + w - 1) * 4) = color;
            }
        }
    }
}
