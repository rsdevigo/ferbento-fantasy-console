using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Ferbento.Core.Chips;
using Ferbento.Editor.Chrome;
using Ferbento.Editor.Sprites;
using Ferbento.Editor.Tilemaps;
using Ferbento.Editor.Tilemaps.CrossTab;
using Ferbento.Editor.Tilemaps.Input;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Views;

public class TilemapEditorView : UserControl, IProfileAwareEditor
{
    private readonly TilemapEditorController _ctrl = new();
    private readonly EditorChrome _chrome = new();
    private readonly SpriteNavigatorView _navigator = new();
    private readonly MapAssistedMoveService _moveService = new();
    private WriteableBitmap? _mapBitmap;
    private Image _mapImage = null!;
    private ChipBus? _bus;
    private Action? _onEdited;
    private SpriteEditorController? _spriteCtrl;
    private bool _isPainting;
    private bool _panDragging;
    private int _panLastX;
    private int _panLastY;
    private KeyModifiers _keyModifiers;

    public TilemapEditorController Controller => _ctrl;

    public void SetEditedHandler(Action handler) => _onEdited = handler;

    public void SetSpriteController(SpriteEditorController spriteCtrl) => _spriteCtrl = spriteCtrl;

    public TilemapEditorView()
    {
        BuildChrome();
        Content = _chrome;
        Focusable = true;

        _ctrl.Edited += () => _onEdited?.Invoke();
        _ctrl.RefreshRequested += RefreshAll;

        _ctrl.Viewport.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ViewportState.Zoom) or nameof(ViewportState.HexView))
                RebuildMapBitmap();
        };

        _ctrl.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(TilemapEditorController.MapWidth)
                or nameof(TilemapEditorController.MapHeight)
                or nameof(TilemapEditorController.TileSize)
                or nameof(TilemapEditorController.SelectedTool)
                or nameof(TilemapEditorController.SelectedTile)
                or nameof(TilemapEditorController.CurrentLayer)
                or nameof(TilemapEditorController.SpriteBankCount)
                or nameof(TilemapEditorController.StatusText))
            {
                UpdateUi();
            }
        };

        EditorKeyboardRouting.Attach(this, OnKeyDown, OnKeyUp, OnTextInput);
        Loaded += (_, _) =>
        {
            RebuildMapBitmap();
            RefreshNavigator();
            Focus();
        };
    }

    private void BuildChrome()
    {
        _mapImage = new Image
        {
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _mapImage.PointerPressed += OnMapPressed;
        _mapImage.PointerMoved += OnMapMoved;
        _mapImage.PointerReleased += OnMapReleased;
        _mapImage.PointerWheelChanged += OnMapWheel;
        _mapImage.PointerExited += (_, _) => _ctrl.ClearHoverTile();

        _chrome.PrimaryHost.Child = _mapImage;
        _chrome.PrimaryHost.PanelSizeChanged += (w, h) => _ctrl.SetPanelSize(w, h);

        _chrome.ToolStrip.SetTools(
        [
            ("pencil", null, Sprites.SpriteToolIcons.CreateBrushIcon),
            ("fill", "🪣", null),
            ("select", "⬚", null),
            ("stamp", "📌", null),
            ("pan", "✋", null),
            ("shape", "◇", null),
        ], _ctrl.SelectedTool);
        _chrome.ToolStrip.ToolSelected += id => _ctrl.SelectedTool = id;

        BuildContextStrip();
        _navigator.Configure(8, 4);
        _navigator.SetCallbacks(OnNavigatorTile, OnNavigatorDrag);
        _navigator.BankChanged += bank => _ctrl.SpriteBank = bank;
        _chrome.NavigatorHost.Content = _navigator;

        EditorKeyboardRouting.PreventFocusSteal(_chrome);
        _chrome.PointerPressed += (_, _) => Focus();
    }

    private void BuildContextStrip()
    {
        _chrome.ContextStrip.Children.Clear();
        for (var layer = 0; layer < 8; layer++)
        {
            var l = layer;
            var btn = new Button { Content = $"L{layer}", FontSize = 11, Padding = new Thickness(6, 2), Tag = l };
            btn.Click += (_, _) => _ctrl.CurrentLayer = l;
            _chrome.ContextStrip.Children.Add(btn);
        }

        var undoBtn = new Button { Content = "Undo", FontSize = 11, Padding = new Thickness(6, 2) };
        undoBtn.Click += (_, _) => _ctrl.HandleKey("Z", true, false, false);
        var redoBtn = new Button { Content = "Redo", FontSize = 11, Padding = new Thickness(6, 2) };
        redoBtn.Click += (_, _) => _ctrl.HandleKey("Y", true, false, false);
        _chrome.ContextStrip.Children.Add(undoBtn);
        _chrome.ContextStrip.Children.Add(redoBtn);
    }

    public void SetProfile(HardwareProfile profile)
    {
        _ctrl.SetProfile(profile);
        _navigator.Configure(_ctrl.TileSize, _ctrl.SpriteBankCount);
    }

    public void SetChipBus(ChipBus bus)
    {
        _bus = bus;
        _ctrl.SetChipBus(bus);
        RebuildMapBitmap();
        RefreshNavigator();
    }

    public void RefreshPalette() => RefreshAll();

    public void RefreshFromBus()
    {
        RebuildMapBitmap();
        RefreshNavigator();
    }

    private void UpdateUi()
    {
        _chrome.ToolStrip.HighlightTool(_ctrl.SelectedTool);
        _chrome.StatusFooter.Text = _ctrl.StatusText;
        RefreshNavigator();
        RebuildMapBitmap();
    }

    private void RefreshAll() => UpdateUi();

    private MapEditorViewport CurrentViewport()
    {
        var tile = _ctrl.TileSize;
        var zoom = _ctrl.Viewport.Zoom;
        return MapEditorViewport.Compute(
            _ctrl.MapWidth, _ctrl.MapHeight, tile, zoom,
            _ctrl.Viewport.PanX, _ctrl.Viewport.PanY,
            _chrome.PrimaryHost.Bounds.Width, _chrome.PrimaryHost.Bounds.Height);
    }

    private void RebuildMapBitmap()
    {
        if (_bus == null) return;
        var tile = _ctrl.TileSize;
        var zoom = _ctrl.Viewport.Zoom;
        var vp = CurrentViewport();

        if (vp.BitmapWidth <= 0 || vp.BitmapHeight <= 0) return;

        _mapBitmap?.Dispose();
        _mapBitmap = new WriteableBitmap(
            new PixelSize(vp.BitmapWidth, vp.BitmapHeight),
            new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

        using var locked = _mapBitmap.Lock();
        ClearBitmap(locked.Address, locked.RowBytes, vp.BitmapWidth, vp.BitmapHeight, 0xFF3F3F46);

        for (var vty = 0; vty < vp.TilesHigh; vty++)
        for (var vtx = 0; vtx < vp.TilesWide; vtx++)
        {
            var mapX = vp.StartMapX + vtx;
            var mapY = vp.StartMapY + vty;
            if (mapX >= _ctrl.MapWidth || mapY >= _ctrl.MapHeight) continue;
            DrawMapTile(locked.Address, locked.RowBytes, vp, mapX, mapY, tile, zoom);
        }

        if (_ctrl.Selection.MapRect is { } sel)
            DrawSelection(locked.Address, locked.RowBytes, sel, vp);

        if (_ctrl.HasHoverTile && _ctrl.SelectedTool is "pencil" or "fill" or "stamp")
            DrawPaintPreview(locked.Address, locked.RowBytes, _ctrl.HoverMapX, _ctrl.HoverMapY, vp, tile, zoom);

        _mapImage.Source = _mapBitmap;
        _mapImage.Width = vp.BitmapWidth;
        _mapImage.Height = vp.BitmapHeight;
    }

    private void DrawPaintPreview(nint ptr, int stride, int mapX, int mapY, MapEditorViewport vp, int tile, int zoom)
    {
        if (mapX < 0 || mapY < 0 || mapX >= _ctrl.MapWidth || mapY >= _ctrl.MapHeight) return;
        if (!vp.IsOnBitmap(mapX, mapY)) return;

        var (tileScreenX, tileScreenY) = TileScreenOrigin(vp, mapX, mapY);
        DrawTileOutlineScreen(ptr, stride, tileScreenX, tileScreenY, tile, zoom, 0xFFFFFF00u);

        if (_bus == null) return;
        var tileId = _ctrl.SelectedTile;
        var col = tileId % _ctrl.SpritesPerRow;
        var row = tileId / _ctrl.SpritesPerRow;
        var sx = col * tile;
        var sy = row * tile;
        var transparent = _ctrl.ColorChip?.TransparentIndex ?? 0;

        for (var py = 0; py < tile; py++)
        for (var px = 0; px < tile; px++)
        {
            var ci = _bus.Sprite.SGet(sx + px, sy + py);
            if (ci == transparent) continue;
            var bgra = Pico8Colors.ToBgra(ci, _ctrl.ColorChip, MapTileRenderColors.Checkerboard(mapX, mapY));
            var ghost = (bgra & 0x00FFFFFFu) | 0x88000000u;
            BlendBlockScreen(ptr, stride, tileScreenX + px * zoom, tileScreenY + py * zoom, zoom, ghost);
        }
    }

    private static (int x, int y) TileScreenOrigin(MapEditorViewport vp, int mapX, int mapY)
    {
        var x = (mapX - vp.StartMapX) * vp.CellPixels - vp.SubPixelOffsetX;
        var y = (mapY - vp.StartMapY) * vp.CellPixels - vp.SubPixelOffsetY;
        return (x, y);
    }

    private static unsafe void DrawTileOutlineScreen(nint ptr, int stride, int x, int y, int tile, int zoom, uint color)
    {
        var w = tile * zoom;
        var h = tile * zoom;
        for (var dx = 0; dx < w; dx++)
        {
            PutPixel(ptr, stride, x + dx, y, color);
            PutPixel(ptr, stride, x + dx, y + h - 1, color);
        }
        for (var dy = 0; dy < h; dy++)
        {
            PutPixel(ptr, stride, x, y + dy, color);
            PutPixel(ptr, stride, x + w - 1, y + dy, color);
        }
    }

    private static unsafe void PutPixel(nint ptr, int stride, int x, int y, uint color)
    {
        if (x < 0 || y < 0) return;
        *(uint*)(ptr + y * stride + x * 4) = color;
    }

    private static unsafe void BlendBlockScreen(nint ptr, int stride, int x, int y, int zoom, uint fg)
    {
        for (var zy = 0; zy < zoom; zy++)
        for (var zx = 0; zx < zoom; zx++)
        {
            var px = x + zx;
            var py = y + zy;
            if (px < 0 || py < 0) continue;
            var offset = py * stride + px * 4;
            var bg = *(uint*)(ptr + offset);
            *(uint*)(ptr + offset) = Blend(bg, fg);
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

    private void DrawMapTile(nint ptr, int stride, MapEditorViewport vp, int mapX, int mapY, int tile, int zoom)
    {
        var (tileScreenX, tileScreenY) = TileScreenOrigin(vp, mapX, mapY);
        var tileId = _bus!.Tilemap.MGet(mapX, mapY);
        if (_bus.Sprite.FGet(tileId, _ctrl.CurrentLayer) == 0)
        {
            var bg = MapTileRenderColors.Checkerboard(mapX, mapY);
            FillBlockScreen(ptr, stride, tileScreenX, tileScreenY, zoom, tile, bg);
            if (_ctrl.Viewport.HexView)
                DrawHexDigit(ptr, stride, tileScreenX, tileScreenY, tileId);
            return;
        }

        var col = tileId % _ctrl.SpritesPerRow;
        var row = tileId / _ctrl.SpritesPerRow;
        var sx = col * tile;
        var sy = row * tile;
        for (var py = 0; py < tile; py++)
        for (var px = 0; px < tile; px++)
        {
            var ci = _bus.Sprite.SGet(sx + px, sy + py);
            var color = MapTileRenderColors.SpritePixel(ci, mapX, mapY, _ctrl.ColorChip);
            FillBlockScreen(ptr, stride, tileScreenX + px * zoom, tileScreenY + py * zoom, zoom, 1, color);
        }
        if (_ctrl.Viewport.HexView)
            DrawHexDigit(ptr, stride, tileScreenX, tileScreenY, tileId);
    }

    private static void DrawHexDigit(nint ptr, int stride, int px, int py, int value)
    {
        unsafe
        {
            *(uint*)(ptr + py * stride + px * 4) = 0xFFCCCCCC;
            if (px + 4 < stride / 4)
                *(uint*)(ptr + py * stride + (px + 4) * 4) = (uint)(0xFF000000 | ((value & 0xF0) << 16) | ((value & 0x0F) << 8));
        }
    }

    private void DrawSelection(nint ptr, int stride, (int x, int y, int w, int h) sel, MapEditorViewport vp)
    {
        var color = 0xFFFFCC00u;
        var tile = _ctrl.TileSize;
        var zoom = _ctrl.Viewport.Zoom;
        var left = (sel.x - vp.StartMapX) * vp.CellPixels - vp.SubPixelOffsetX;
        var top = (sel.y - vp.StartMapY) * vp.CellPixels - vp.SubPixelOffsetY;
        var right = left + sel.w * tile * zoom;
        var bottom = top + sel.h * tile * zoom;
        unsafe
        {
            for (var x = left; x < right; x++)
            {
                PutPixel(ptr, stride, x, top, color);
                PutPixel(ptr, stride, x, bottom - 1, color);
            }
            for (var y = top; y < bottom; y++)
            {
                PutPixel(ptr, stride, left, y, color);
                PutPixel(ptr, stride, right - 1, y, color);
            }
        }
    }

    private void RefreshNavigator()
    {
        if (_bus == null) return;
        _navigator.Configure(_ctrl.TileSize, _ctrl.SpriteBankCount);
        _navigator.SetSelectedTile(_ctrl.SelectedTile, _ctrl.SpriteBank);
        _navigator.SetHighlightTiles(_ctrl.Selection.NavigatorSprites);
        _navigator.Render((ptr, stride, col, row, tileIndex) =>
        {
            if (tileIndex >= _ctrl.SpriteCount) return;
            var tcol = tileIndex % _ctrl.SpritesPerRow;
            var trow = tileIndex / _ctrl.SpritesPerRow;
            var tile = _ctrl.TileSize;
            for (var py = 0; py < tile; py++)
            for (var px = 0; px < tile; px++)
            {
                var c = Pico8Colors.ToBgra(_bus.Sprite.SGet(tcol * tile + px, trow * tile + py), _ctrl.ColorChip);
                SpriteNavigatorView.FillPixelBlock(ptr, stride, col * tile + px, row * tile + py, 3, c, 8 * tile, 8 * tile);
            }
        });
    }

    private (int mapX, int mapY) MapCoords(PointerEventArgs e)
    {
        var pt = e.GetPosition(_mapImage);
        return CurrentViewport().PointerToMapTile(pt.X, pt.Y);
    }

    private void UpdateHoverFromPointer(PointerEventArgs e)
    {
        var (mapX, mapY) = MapCoords(e);
        if (mapX >= 0 && mapX < _ctrl.MapWidth && mapY >= 0 && mapY < _ctrl.MapHeight)
            _ctrl.SetHoverTile(mapX, mapY);
        else
            _ctrl.ClearHoverTile();
    }

    private void OnMapPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
        UpdateHoverFromPointer(e);
        var (tx, ty) = MapCoords(e);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (_ctrl.SelectedTool == "pan" || _keyModifiers.HasFlag(KeyModifiers.Shift) && _ctrl.SelectedTool != "select")
        {
            _panDragging = true;
            _panLastX = (int)e.GetPosition(_mapImage).X;
            _panLastY = (int)e.GetPosition(_mapImage).Y;
            return;
        }

        _isPainting = true;
        _ctrl.HandlePointerDown(tx, ty, ctrl, shift, e.GetCurrentPoint(_mapImage).Properties.IsRightButtonPressed);
    }

    private void OnMapMoved(object? sender, PointerEventArgs e)
    {
        UpdateHoverFromPointer(e);

        if (_panDragging)
        {
            var pt = e.GetPosition(_mapImage);
            _ctrl.PanByPixels((int)pt.X - _panLastX, (int)pt.Y - _panLastY);
            _panLastX = (int)pt.X;
            _panLastY = (int)pt.Y;
            RebuildMapBitmap();
            return;
        }

        if (!_isPainting) return;
        var (mapX, mapY) = MapCoords(e);
        _ctrl.HandlePointerMove(mapX, mapY, e.KeyModifiers.HasFlag(KeyModifiers.Control), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
    }

    private void OnMapReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_panDragging) { _panDragging = false; return; }
        if (!_isPainting) return;
        var (tx, ty) = MapCoords(e);
        _ctrl.HandlePointerUp(tx, ty, e.KeyModifiers.HasFlag(KeyModifiers.Control), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        _isPainting = false;
    }

    private void OnMapWheel(object? sender, PointerWheelEventArgs e)
    {
        _ctrl.Viewport.AdjustZoom(e.Delta.Y > 0 ? 1 : -1);
        e.Handled = true;
    }

    private void OnNavigatorTile(int col, int row, int bank, int tileIndex, bool shift)
    {
        if (tileIndex >= _ctrl.SpriteCount) return;
        _ctrl.SpriteBank = bank;
        _ctrl.SelectTileFromNavigator(tileIndex, shift);

        if (_moveService.HasCutPending && _spriteCtrl != null && _bus != null
            && _ctrl.Selection.MapRect is not null)
        {
            _moveService.ApplyPaste(_bus, _ctrl, _spriteCtrl, tileIndex);
        }
    }

    private void OnNavigatorDrag(int c0, int r0, int c1, int r1) =>
        _ctrl.SelectTilesInNavigatorRect(c0, r0, c1, r1);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _keyModifiers = e.KeyModifiers;
        if (e.Key == Key.Space) _ctrl.SetSpaceHeld(true);

        var key = e.Key.ToString();
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        if (ctrl && key == "X" && _spriteCtrl != null && _ctrl.Selection.HasNavigatorSelection)
        {
            if (_moveService.BeginCut(_spriteCtrl, _ctrl.Selection.NavigatorSprites))
            {
                _ctrl.CrossTabHint = _moveService.StatusHint;
                e.Handled = true;
                return;
            }
        }

        if (ctrl && key == "V" && _moveService.HasCutPending && _spriteCtrl != null && _bus != null)
        {
            if (_moveService.ApplyPaste(_bus, _ctrl, _spriteCtrl, _ctrl.SelectedTile))
            {
                e.Handled = true;
                return;
            }
        }

        if (_ctrl.HandleKey(key, ctrl, shift, alt))
        {
            e.Handled = true;
            RefreshAll();
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _keyModifiers = e.KeyModifiers;
        if (e.Key == Key.Space) _ctrl.SetSpaceHeld(false);
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text != null && _ctrl.HandleTextInput(e.Text))
        {
            e.Handled = true;
            RefreshAll();
        }
    }

    private static unsafe void ClearBitmap(nint ptr, int stride, int w, int h, uint color)
    {
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
            *(uint*)(ptr + y * stride + x * 4) = color;
    }

    private static unsafe void FillBlockScreen(nint ptr, int stride, int screenX, int screenY, int zoom, int blockSize, uint color)
    {
        for (var ty = 0; ty < blockSize; ty++)
        for (var tx = 0; tx < blockSize; tx++)
        for (var zy = 0; zy < zoom; zy++)
        for (var zx = 0; zx < zoom; zx++)
            PutPixel(ptr, stride, screenX + (tx * zoom) + zx, screenY + (ty * zoom) + zy, color);
    }
}
