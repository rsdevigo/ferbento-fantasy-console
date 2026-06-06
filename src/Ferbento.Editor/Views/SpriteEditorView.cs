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

public class SpriteEditorView : UserControl, IProfileAwareEditor
{
    private readonly SpriteEditorViewModel _vm = new();
    private WriteableBitmap? _canvasBitmap;
    private WriteableBitmap? _sheetBitmap;
    private Image _canvasImage = null!;
    private Image _sheetImage = null!;
    private TextBlock _spriteLabel = null!;
    private Panel _palettePanel = null!;
    private Panel _flagsPanel = null!;
    private ChipBus? _bus;
    private Action? _onEdited;

    public void SetEditedHandler(Action handler) => _onEdited = handler;

    private void NotifyEdited()
    {
        _onEdited?.Invoke();
    }

    private const int CanvasZoom = 16;
    private const int SheetZoom = 2;

    private bool _isDrawing;
    private bool _strokeUndoPushed;
    private (int x, int y)? _selectStart;
    private byte[]? _selectionBuffer;
    private (int x, int y)? _selectionOrigin;
    private bool _isMovingSelection;

    public SpriteEditorView()
    {
        DataContext = _vm;
        Content = BuildLayout();
        Loaded += (_, _) => RebuildBitmaps();

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SpriteEditorViewModel.CurrentSpriteIndex)
                or nameof(SpriteEditorViewModel.SelectedColor)
                or nameof(SpriteEditorViewModel.TileSize)
                or nameof(SpriteEditorViewModel.SheetWidth)
                or nameof(SpriteEditorViewModel.SheetHeight)
                or nameof(SpriteEditorViewModel.PaletteCount))
            {
                if (e.PropertyName is nameof(SpriteEditorViewModel.TileSize)
                    or nameof(SpriteEditorViewModel.SheetWidth)
                    or nameof(SpriteEditorViewModel.SheetHeight)
                    or nameof(SpriteEditorViewModel.PaletteCount))
                {
                    RebuildPaletteSwatches();
                    RebuildBitmaps();
                }
                RefreshCanvas();
                RefreshSheet();
                UpdateSpriteLabel();
                UpdatePaletteHighlight();
            }
            else if (e.PropertyName?.StartsWith("Flag") == true || e.PropertyName == nameof(SpriteEditorViewModel.AllFlags))
                RefreshFlagToggles();
        };
    }

    public void SetProfile(HardwareProfile profile) => _vm.SetProfile(profile);

    public void SetChipBus(ChipBus bus)
    {
        _bus = bus;
        _vm.SetChipBus(bus);
        _vm.Edited += NotifyEdited;
        RebuildPaletteSwatches();
        RebuildBitmaps();
    }

    public void RefreshPalette()
    {
        RebuildPaletteSwatches();
        RefreshCanvas();
        RefreshSheet();
    }

    public void RefreshFromBus()
    {
        RebuildPaletteSwatches();
        RefreshCanvas();
        RefreshSheet();
        RefreshFlagToggles();
    }

    private void RebuildBitmaps()
    {
        var tile = _vm.TileSize;
        var sw = _vm.SheetWidth;
        var sh = _vm.SheetHeight;

        _canvasBitmap = new WriteableBitmap(
            new PixelSize(tile * CanvasZoom, tile * CanvasZoom),
            new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        _sheetBitmap = new WriteableBitmap(
            new PixelSize(sw * SheetZoom, sh * SheetZoom),
            new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

        _canvasImage.Source = _canvasBitmap;
        _sheetImage.Source = _sheetBitmap;
        RefreshCanvas();
        RefreshSheet();
    }

    private void RefreshCanvas()
    {
        if (_canvasBitmap == null) return;
        var tile = _vm.TileSize;

        using var locked = _canvasBitmap.Lock();
        var stride = locked.RowBytes;
        var ptr = locked.Address;

        for (var y = 0; y < tile; y++)
        for (var x = 0; x < tile; x++)
        {
            var color = Pico8Colors.ToBgra(_vm.GetPixel(x, y), _vm.ColorChip);
            FillPixelBlock(ptr, stride, x, y, CanvasZoom, color);
        }

        DrawGridOverlay(ptr, stride, tile * CanvasZoom, CanvasZoom);
        _canvasImage.InvalidateVisual();
    }

    private void RefreshSheet()
    {
        if (_sheetBitmap == null) return;
        var sw = _vm.SheetWidth;
        var sh = _vm.SheetHeight;

        using var locked = _sheetBitmap.Lock();
        var stride = locked.RowBytes;
        var ptr = locked.Address;

        for (var y = 0; y < sh; y++)
        for (var x = 0; x < sw; x++)
        {
            var color = Pico8Colors.ToBgra(_vm.ReadSheetPixel(x, y), _vm.ColorChip);
            FillPixelBlock(ptr, stride, x, y, SheetZoom, color);
        }

        var (ox, oy) = _vm.SpriteOrigin;
        HighlightSpriteRect(ptr, stride, ox, oy, _vm.TileSize, SheetZoom);
        _sheetImage.InvalidateVisual();
    }

    private static unsafe void FillPixelBlock(nint ptr, int stride, int px, int py, int zoom, uint color)
    {
        for (var zy = 0; zy < zoom; zy++)
        for (var zx = 0; zx < zoom; zx++)
        {
            var offset = ((py * zoom + zy) * stride) + ((px * zoom + zx) * 4);
            *(uint*)(ptr + offset) = color;
        }
    }

    private static unsafe void DrawGridOverlay(nint ptr, int stride, int size, int cellSize)
    {
        var gridColor = 0x40FFFFFFu;
        for (var i = cellSize; i < size; i += cellSize)
        {
            for (var j = 0; j < size; j++)
            {
                var hOff = (i * stride) + (j * 4);
                var vOff = (j * stride) + (i * 4);
                *(uint*)(ptr + hOff) = Blend(*(uint*)(ptr + hOff), gridColor);
                *(uint*)(ptr + vOff) = Blend(*(uint*)(ptr + vOff), gridColor);
            }
        }
    }

    private static unsafe void HighlightSpriteRect(nint ptr, int stride, int ox, int oy, int tileSize, int zoom)
    {
        var highlight = 0xFFFFCC00;
        for (var edge = 0; edge < tileSize; edge++)
        for (var t = 0; t < tileSize; t++)
        {
            FillPixelBlock(ptr, stride, ox + t, oy + edge, zoom, highlight);
            FillPixelBlock(ptr, stride, ox + edge, oy + t, zoom, highlight);
        }
    }

    private static uint Blend(uint bg, uint fg)
    {
        var fa = (fg >> 24) & 0xFF;
        if (fa == 0) return bg;
        var fr = (fg >> 16) & 0xFF;
        var fgG = (fg >> 8) & 0xFF;
        var fb = fg & 0xFF;
        var br = (bg >> 16) & 0xFF;
        var bgG = (bg >> 8) & 0xFF;
        var bb = bg & 0xFF;
        var inv = 255 - fa;
        var r = (fr * fa + br * inv) / 255;
        var g = (fgG * fa + bgG * inv) / 255;
        var b = (fb * fa + bb * inv) / 255;
        return 0xFF000000u | (uint)(r << 16) | (uint)(g << 8) | (uint)b;
    }

    private Control BuildLayout()
    {
        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,220"),
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        };

        var sheetPanel = new StackPanel { Margin = new Thickness(8), Spacing = 4 };
        sheetPanel.Children.Add(new TextBlock
        {
            Text = "Sprite Sheet",
            Foreground = Brushes.LightGray,
            FontSize = 11,
        });
        _sheetImage = new Image { Stretch = Stretch.None };
        _sheetImage.PointerPressed += OnSheetPointerPressed;
        sheetPanel.Children.Add(new ScrollViewer
        {
            MaxHeight = 320,
            Content = _sheetImage,
        });

        var center = new StackPanel { Margin = new Thickness(8), Spacing = 8 };
        _spriteLabel = new TextBlock
        {
            Text = _vm.SpriteLabel,
            Foreground = Brushes.LightGray,
            FontSize = 12,
        };
        center.Children.Add(_spriteLabel);

        var nav = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var prevBtn = new Button { Content = "◀", Padding = new Thickness(8, 2) };
        prevBtn.Click += (_, _) => { _vm.CurrentSpriteIndex--; };
        var nextBtn = new Button { Content = "▶", Padding = new Thickness(8, 2) };
        nextBtn.Click += (_, _) => { _vm.CurrentSpriteIndex++; };
        nav.Children.Add(prevBtn);
        nav.Children.Add(nextBtn);
        center.Children.Add(nav);

        if (_vm.UsesGfx2Bank)
        {
            var bankRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            var gfxBtn = new Button { Content = "GFX (0–127)", Padding = new Thickness(8, 2), Tag = 0 };
            var gfx2Btn = new Button { Content = "GFX2 (128–255)", Padding = new Thickness(8, 2), Tag = 1 };
            gfxBtn.Click += (_, _) => _vm.SwitchGfxBank(0);
            gfx2Btn.Click += (_, _) => _vm.SwitchGfxBank(1);
            bankRow.Children.Add(gfxBtn);
            bankRow.Children.Add(gfx2Btn);
            center.Children.Add(bankRow);
        }

        _canvasImage = new Image { Stretch = Stretch.None };
        _canvasImage.PointerPressed += OnCanvasPointerPressed;
        _canvasImage.PointerMoved += OnCanvasPointerMoved;
        center.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderBrush = new SolidColorBrush(Color.Parse("#555555")),
            BorderThickness = new Thickness(1),
            Child = _canvasImage,
        });

        var sidebar = new StackPanel { Margin = new Thickness(8), Spacing = 8 };

        sidebar.Children.Add(new TextBlock { Text = "Palette", Foreground = Brushes.LightGray, FontSize = 11 });
        _palettePanel = new WrapPanel { Width = 200 };
        sidebar.Children.Add(_palettePanel);

        sidebar.Children.Add(new TextBlock { Text = "Sprite Flags", Foreground = Brushes.LightGray, FontSize = 11 });
        _flagsPanel = new WrapPanel { Width = 200 };
        for (var i = 0; i < 8; i++)
        {
            var bit = i;
            var toggle = new ToggleButton
            {
                Content = $"F{bit}",
                Width = 44,
                Height = 28,
                Margin = new Thickness(2),
                FontSize = 11,
                Tag = bit,
            };
            toggle.IsCheckedChanged += (_, _) =>
            {
                if (toggle.IsChecked.HasValue)
                {
                    _vm.PushUndo("flag");
                    _vm.SetFlag(bit, toggle.IsChecked.Value);
                }
            };
            _flagsPanel.Children.Add(toggle);
        }
        sidebar.Children.Add(_flagsPanel);

        sidebar.Children.Add(new TextBlock { Text = "Tools", Foreground = Brushes.LightGray, FontSize = 11 });
        var tools = new WrapPanel { Width = 200 };
        foreach (var (tool, label) in new (string, string)[]
        {
            ("pencil", "✏"), ("fill", "🪣"), ("picker", "💧"), ("select", "⬚"),
            ("flip-h", "↔"), ("flip-v", "↕"), ("rotate", "↻"),
            ("copy", "📋"), ("paste", "📌"),
        })
        {
            var btn = new Border
            {
                Width = 36, Height = 28,
                Background = new SolidColorBrush(Color.Parse("#333337")),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = label,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 14,
                },
                Tag = tool,
                Margin = new Thickness(2),
            };
            btn.PointerPressed += (_, _) => SelectTool(tool);
            tools.Children.Add(btn);
        }
        sidebar.Children.Add(tools);

        var undoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var undoBtn = new Button { Content = "Undo", Padding = new Thickness(8, 2) };
        undoBtn.Click += (_, _) => { _vm.Undo(); RefreshCanvas(); RefreshSheet(); RefreshFlagToggles(); };
        var redoBtn = new Button { Content = "Redo", Padding = new Thickness(8, 2) };
        redoBtn.Click += (_, _) => { _vm.Redo(); RefreshCanvas(); RefreshSheet(); RefreshFlagToggles(); };
        undoRow.Children.Add(undoBtn);
        undoRow.Children.Add(redoBtn);
        sidebar.Children.Add(undoRow);

        Grid.SetColumn(sheetPanel, 0);
        Grid.SetColumn(center, 1);
        Grid.SetColumn(sidebar, 2);
        root.Children.Add(sheetPanel);
        root.Children.Add(center);
        root.Children.Add(sidebar);
        return root;
    }

    private void RebuildPaletteSwatches()
    {
        _palettePanel.Children.Clear();
        var count = _vm.PaletteCount;
        for (var i = 0; i < count; i++)
        {
            var idx = i;
            var swatch = new Border
            {
                Width = 24, Height = 24,
                Background = new SolidColorBrush(Pico8Colors.ToAvaloniaColor(i, _vm.ColorChip)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(1),
            };
            swatch.PointerPressed += (_, _) => _vm.SelectedColor = idx;
            swatch.Tag = idx;
            _palettePanel.Children.Add(swatch);
        }
        UpdatePaletteHighlight();
    }

    private void RefreshFlagToggles()
    {
        for (var i = 0; i < _flagsPanel.Children.Count; i++)
        {
            if (_flagsPanel.Children[i] is ToggleButton toggle)
                toggle.IsChecked = _vm.GetFlag(i) != 0;
        }
    }

    private void SelectTool(string tool)
    {
        _vm.SelectedTool = tool;
        switch (tool)
        {
            case "flip-h": _vm.FlipHorizontal(); RefreshCanvas(); RefreshSheet(); break;
            case "flip-v": _vm.FlipVertical(); RefreshCanvas(); RefreshSheet(); break;
            case "rotate": _vm.Rotate90(); RefreshCanvas(); RefreshSheet(); break;
            case "copy": _vm.CopySprite(); break;
            case "paste": _vm.PasteSprite(); RefreshCanvas(); RefreshSheet(); RefreshFlagToggles(); break;
        }
    }

    private void UpdateSpriteLabel() => _spriteLabel.Text = _vm.SpriteLabel;

    private void UpdatePaletteHighlight()
    {
        foreach (var child in _palettePanel.Children)
        {
            if (child is Border swatch && swatch.Tag is int idx)
            {
                swatch.BorderBrush = idx == _vm.SelectedColor ? Brushes.White : Brushes.Transparent;
                swatch.BorderThickness = new Thickness(idx == _vm.SelectedColor ? 2 : 0);
            }
        }
    }

    private void OnSheetPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pt = e.GetPosition(_sheetImage);
        var sx = (int)(pt.X / SheetZoom);
        var sy = (int)(pt.Y / SheetZoom);
        if (sx < 0 || sy < 0 || sx >= _vm.SheetWidth || sy >= _vm.SheetHeight) return;
        var col = sx / _vm.TileSize;
        var row = sy / _vm.TileSize;
        _vm.CurrentSpriteIndex = row * _vm.SpritesPerRow + col;
        RefreshFlagToggles();
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDrawing = true;
        _strokeUndoPushed = false;
        var pt = e.GetPosition(_canvasImage);
        HandleCanvasAction((int)(pt.X / CanvasZoom), (int)(pt.Y / CanvasZoom), isStart: true);
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDrawing) return;
        var pt = e.GetPosition(_canvasImage);
        HandleCanvasAction((int)(pt.X / CanvasZoom), (int)(pt.Y / CanvasZoom), isStart: false);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isMovingSelection && _selectionBuffer != null && _selectionOrigin is { } origin)
        {
            PasteSelection(origin.x, origin.y);
        }
        _isDrawing = false;
        _isMovingSelection = false;
        _selectStart = null;
    }

    private void HandleCanvasAction(int x, int y, bool isStart)
    {
        var tile = _vm.TileSize;
        if (x < 0 || x >= tile || y < 0 || y >= tile) return;

        switch (_vm.SelectedTool)
        {
            case "picker":
                _vm.SelectedColor = _vm.GetPixel(x, y);
                break;
            case "fill":
                if (isStart) { _vm.FloodFill(x, y, _vm.SelectedColor); RefreshCanvas(); RefreshSheet(); }
                break;
            case "select":
                if (isStart)
                {
                    _selectStart = (x, y);
                    _selectionOrigin = (x, y);
                }
                else if (_selectStart is { } start)
                {
                    _selectionBuffer = ExtractSelection(start.x, start.y, x, y);
                    _selectionOrigin = (Math.Min(start.x, x), Math.Min(start.y, y));
                    _isMovingSelection = true;
                }
                break;
            case "pencil":
            default:
                if (!_strokeUndoPushed) { _vm.PushUndo("draw"); _strokeUndoPushed = true; }
                _vm.SetPixel(x, y, _vm.SelectedColor);
                RefreshCanvas();
                RefreshSheet();
                break;
        }
    }

    private void PasteSelection(int destX, int destY)
    {
        if (_selectionBuffer == null || _selectStart is not { } start) return;
        var left = Math.Min(start.x, destX);
        var top = Math.Min(start.y, destY);
        var right = Math.Max(start.x, destX);
        var bottom = Math.Max(start.y, destY);
        var w = right - left + 1;
        var h = bottom - top + 1;

        _vm.PushUndo("move");
        for (var row = 0; row < h; row++)
        for (var col = 0; col < w; col++)
        {
            var idx = row * w + col;
            if (idx >= _selectionBuffer.Length) continue;
            var px = left + col;
            var py = top + row;
            if (px >= 0 && py >= 0 && px < _vm.TileSize && py < _vm.TileSize)
                _vm.SetPixel(px, py, _selectionBuffer[idx]);
        }
        RefreshCanvas();
        RefreshSheet();
    }

    private byte[] ExtractSelection(int x0, int y0, int x1, int y1)
    {
        var left = Math.Min(x0, x1);
        var right = Math.Max(x0, x1);
        var top = Math.Min(y0, y1);
        var bottom = Math.Max(y0, y1);
        var w = right - left + 1;
        var h = bottom - top + 1;
        var buf = new byte[w * h];
        for (var row = 0; row < h; row++)
        for (var col = 0; col < w; col++)
            buf[row * w + col] = (byte)_vm.GetPixel(left + col, top + row);
        return buf;
    }
}
