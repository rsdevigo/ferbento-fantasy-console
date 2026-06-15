using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Ferbento.Core.Chips;
using Ferbento.Editor.Chrome;
using Ferbento.Editor.Sprites.Input;
using Ferbento.Editor.Sprites.Tools;
using Ferbento.Editor.Sprites;
using Ferbento.Editor.ViewModels;

using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Views;

public class SpriteEditorView : UserControl, IProfileAwareEditor
{
    private readonly SpriteEditorViewModel _vm = new();
    private SpriteEditorController Ctrl => _vm.Controller;

    public SpriteEditorController Controller => _vm.Controller;

    private WriteableBitmap? _canvasBitmap;
    private WriteableBitmap? _sheetBitmap;
    private Image _canvasImage = null!;
    private Image _sheetImage = null!;
    private TextBlock _spriteLabel = null!;
    private EditorStatusBar _statusBar = null!;
    private EditorToolStrip _toolStrip = null!;
    private EditorChrome _chrome = null!;
    private Panel _palettePanel = null!;
    private Panel _flagsPanel = null!;
    private Border _canvasBorder = null!;
    private Border _canvasHost = null!;
    private Action? _onEdited;

    private bool _isDrawing;
    private bool _isRightButton;
    private bool _sheetDragging;
    private int _sheetDragStartX;
    private int _sheetDragStartY;
    private bool _rebuildingCanvas;
    private bool _rebuildingSheet;
    private bool _visualRefreshQueued;
    private bool _pendingCanvasRebuild;
    private KeyModifiers _keyModifiers;

    private const int SheetZoom = 2;

    public void SetEditedHandler(Action handler) => _onEdited = handler;

    public SpriteEditorView()
    {
        DataContext = _vm;
        Content = BuildLayout();
        Loaded += (_, _) =>
        {
            RebuildBitmaps();
            UpdateCanvasPanelSize();
        };
        Focusable = true;

        Ctrl.Edited += () => _onEdited?.Invoke();
        Ctrl.RefreshRequested += OnRefreshRequested;

        Ctrl.Viewport.PropertyChanged += (_, e) =>
        {
            UpdateStatusBar();
            if (e.PropertyName is nameof(ViewportState.Zoom) or nameof(ViewportState.ZoomPercent))
                QueueVisualRefresh(rebuildCanvas: true);
        };

        Ctrl.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SpriteEditorController.CurrentSpriteIndex)
                or nameof(SpriteEditorController.SelectedColor)
                or nameof(SpriteEditorController.PrimaryColor)
                or nameof(SpriteEditorController.SecondaryColor)
                or nameof(SpriteEditorController.TileSize)
                or nameof(SpriteEditorController.SheetWidth)
                or nameof(SpriteEditorController.SheetHeight)
                or nameof(SpriteEditorController.PaletteCount)
                or nameof(SpriteEditorController.SelectedTool)
                or nameof(SpriteEditorController.ViewWidth)
                or nameof(SpriteEditorController.ViewHeight)
                or nameof(SpriteEditorController.BrushSize)
                or nameof(SpriteEditorController.StatusText))
            {
                var rebuildCanvas = e.PropertyName is nameof(SpriteEditorController.ViewWidth)
                    or nameof(SpriteEditorController.ViewHeight)
                    or nameof(SpriteEditorController.TileSize);

                if (e.PropertyName is nameof(SpriteEditorController.TileSize)
                    or nameof(SpriteEditorController.SheetWidth)
                    or nameof(SpriteEditorController.SheetHeight)
                    or nameof(SpriteEditorController.PaletteCount))
                {
                    RebuildPaletteSwatches();
                    RebuildSheetBitmap();
                    rebuildCanvas = true;
                }

                QueueVisualRefresh(rebuildCanvas);
                UpdateSpriteLabel();
                UpdatePaletteHighlight();
                UpdateToolHighlight();
                UpdateStatusBar();
            }
            else if (e.PropertyName?.StartsWith("Flag") == true || e.PropertyName == nameof(SpriteEditorController.AllFlags))
                RefreshFlagToggles();
        };

        EditorKeyboardRouting.Attach(this, OnKeyDown, OnKeyUp, OnTextInput);
        Loaded += (_, _) => Focus();
    }

    private void OnRefreshRequested() => QueueVisualRefresh(rebuildCanvas: false);

    private void QueueVisualRefresh(bool rebuildCanvas)
    {
        if (rebuildCanvas)
            _pendingCanvasRebuild = true;
        if (_visualRefreshQueued) return;
        _visualRefreshQueued = true;
        Dispatcher.UIThread.Post(ApplyQueuedVisualRefresh, DispatcherPriority.Background);
    }

    private void ApplyQueuedVisualRefresh()
    {
        _visualRefreshQueued = false;
        if (_pendingCanvasRebuild)
        {
            _pendingCanvasRebuild = false;
            RebuildCanvasBitmap();
            UpdateCanvasViewportSize();
        }

        RefreshCanvas();
        RefreshSheet();
        UpdateStatusBar();
    }

    public void SetProfile(HardwareProfile profile) => Ctrl.SetProfile(profile);
    public void SetChipBus(ChipBus bus) { Ctrl.SetChipBus(bus); RebuildPaletteSwatches(); RebuildBitmaps(); }
    public void RefreshPalette() { RebuildPaletteSwatches(); RefreshCanvas(); RefreshSheet(); }
    public void RefreshFromBus() { RebuildPaletteSwatches(); RefreshCanvas(); RefreshSheet(); RefreshFlagToggles(); }

    private void RebuildBitmaps()
    {
        RebuildCanvasBitmap();
        RebuildSheetBitmap();
        UpdateCanvasViewportSize();
        RefreshCanvas();
        RefreshSheet();
    }

    private PixelSize CanvasBitmapPixelSize()
    {
        var vw = Math.Max(1, Ctrl.ViewWidth);
        var vh = Math.Max(1, Ctrl.ViewHeight);
        var zoom = Math.Max(1, Ctrl.Viewport.Zoom);
        return new PixelSize(vw * zoom, vh * zoom);
    }

    private void UpdateCanvasPanelSize()
    {
        var w = _canvasHost.Bounds.Width;
        var h = _canvasHost.Bounds.Height;
        if (w <= 0 || h <= 0) return;
        Ctrl.SetCanvasPanelSize(w, h);
    }

    private void UpdateCanvasViewportSize()
    {
        var size = CanvasBitmapPixelSize();
        _canvasBorder.Width = size.Width;
        _canvasBorder.Height = size.Height;
        _canvasImage.Width = size.Width;
        _canvasImage.Height = size.Height;
    }

    private void RebuildCanvasBitmap()
    {
        if (_rebuildingCanvas) return;
        _rebuildingCanvas = true;
        try
        {
            var expected = CanvasBitmapPixelSize();
            if (_canvasBitmap?.PixelSize == expected) return;

            var old = _canvasBitmap;
            _canvasImage.Source = null;
            old?.Dispose();
            _canvasBitmap = new WriteableBitmap(
                expected,
                new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            _canvasImage.Source = _canvasBitmap;
        }
        finally
        {
            _rebuildingCanvas = false;
        }
    }

    private void RebuildSheetBitmap()
    {
        if (_rebuildingSheet) return;
        _rebuildingSheet = true;
        try
        {
            var sw = Math.Max(1, Ctrl.SheetWidth);
            var sh = Math.Max(1, Ctrl.SheetHeight);
            var expected = new PixelSize(sw * SheetZoom, sh * SheetZoom);
            if (_sheetBitmap?.PixelSize == expected) return;

            var old = _sheetBitmap;
            _sheetImage.Source = null;
            old?.Dispose();
            _sheetBitmap = new WriteableBitmap(
                expected,
                new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            _sheetImage.Source = _sheetBitmap;
        }
        finally
        {
            _rebuildingSheet = false;
        }
    }

    private void RefreshCanvas()
    {
        if (_canvasBitmap == null) return;
        var vw = Ctrl.ViewWidth;
        var vh = Ctrl.ViewHeight;
        var zoom = Ctrl.Viewport.Zoom;
        if (vw <= 0 || vh <= 0) return;

        UpdateCanvasViewportSize();

        if (_canvasBitmap.PixelSize != CanvasBitmapPixelSize())
        {
            QueueVisualRefresh(rebuildCanvas: true);
            return;
        }

        using (var locked = _canvasBitmap.Lock())
        {
            var stride = locked.RowBytes;
            var ptr = locked.Address;
            var pixelW = locked.Size.Width;
            var pixelH = locked.Size.Height;
            var bg = Pico8Colors.ToBgra(0, Ctrl.ColorChip);

            ClearBitmap(ptr, stride, pixelW, pixelH, bg);

            for (var y = 0; y < vh; y++)
            for (var x = 0; x < vw; x++)
            {
                var color = Pico8Colors.ToBgra(Ctrl.GetPixel(x, y), Ctrl.ColorChip);
                FillPixelBlock(ptr, stride, x, y, zoom, color, vw, vh);
            }

            if (Ctrl.Selection.HasPixelSelection)
                DrawSelectionOverlay(ptr, stride, zoom, vw, vh);

            if (Ctrl.SelectedTool == "stamp" && Ctrl.Clipboard.HasContent && Ctrl.StampPreviewPosition is { } stampPos)
                DrawStampGhostOverlay(ptr, stride, zoom, vw, vh, stampPos.x, stampPos.y);

            if (Ctrl.CursorPreviewPosition is { } cursorPos && Ctrl.CursorPreviewTool is { } cursorTool)
                DrawCursorPreviewOverlay(ptr, stride, zoom, vw, vh, cursorPos.x, cursorPos.y, cursorTool);

            if (Ctrl.Viewport.GridVisible && zoom >= 4)
                DrawGridOverlay(ptr, stride, vw * zoom, Ctrl.TileSize * zoom);
        }

        _canvasImage.InvalidateVisual();
    }

    private void DrawStampGhostOverlay(nint ptr, int stride, int zoom, int viewW, int viewH, int anchorX, int anchorY)
    {
        var clip = Ctrl.Clipboard;
        if (clip.Pixels == null) return;

        var transparent = Ctrl.ColorChip?.TransparentIndex ?? 0;
        var skipTransparent = Ctrl.StampPreviewSkipTransparent;

        for (var y = 0; y < clip.Height; y++)
        for (var x = 0; x < clip.Width; x++)
        {
            var colorIndex = clip.Pixels[y * clip.Width + x];
            if (skipTransparent && colorIndex == transparent) continue;

            var destX = anchorX + x;
            var destY = anchorY + y;
            if (destX < 0 || destY < 0 || destX >= viewW || destY >= viewH) continue;

            var bgra = Pico8Colors.ToBgra(colorIndex, Ctrl.ColorChip);
            var ghost = (bgra & 0x00FFFFFFu) | 0xA0000000u;
            BlendPixelBlock(ptr, stride, destX, destY, zoom, ghost, viewW, viewH);
        }
    }

    private void DrawCursorPreviewOverlay(nint ptr, int stride, int zoom, int viewW, int viewH, int anchorX, int anchorY, string tool)
    {
        var outline = tool == "eraser" ? 0xFFFFFFFFu : 0xFFFFFF00u;

        if (tool is "pencil" or "eraser")
        {
            var size = Ctrl.BrushSize;
            for (var dy = 0; dy < size; dy++)
            for (var dx = 0; dx < size; dx++)
            {
                var px = anchorX + dx;
                var py = anchorY + dy;
                if (px < 0 || py < 0 || px >= viewW || py >= viewH) continue;

                if (tool == "pencil")
                {
                    var bgra = Pico8Colors.ToBgra(Ctrl.PrimaryColor, Ctrl.ColorChip);
                    var tint = (bgra & 0x00FFFFFFu) | 0x66000000u;
                    BlendPixelBlock(ptr, stride, px, py, zoom, tint, viewW, viewH);
                }

                DrawPixelBlockOutline(ptr, stride, px, py, zoom, outline, viewW, viewH);
            }
            return;
        }

        if (tool is "fill" or "picker")
        {
            if (anchorX < 0 || anchorY < 0 || anchorX >= viewW || anchorY >= viewH) return;
            DrawPixelBlockOutline(ptr, stride, anchorX, anchorY, zoom, outline, viewW, viewH);
            DrawCrosshairInBlock(ptr, stride, anchorX, anchorY, zoom, outline, viewW, viewH);
        }
    }

    private static unsafe void DrawPixelBlockOutline(nint ptr, int stride, int px, int py, int zoom, uint color, int maxW, int maxH)
    {
        if (px < 0 || py < 0 || px >= maxW || py >= maxH) return;
        for (var zy = 0; zy < zoom; zy++)
        for (var zx = 0; zx < zoom; zx++)
        {
            if (zy == 0 || zy == zoom - 1 || zx == 0 || zx == zoom - 1)
            {
                var offset = ((py * zoom + zy) * stride) + ((px * zoom + zx) * 4);
                *(uint*)(ptr + offset) = color;
            }
        }
    }

    private static unsafe void DrawCrosshairInBlock(nint ptr, int stride, int px, int py, int zoom, uint color, int maxW, int maxH)
    {
        if (px < 0 || py < 0 || px >= maxW || py >= maxH) return;
        var mid = zoom / 2;
        for (var zx = 0; zx < zoom; zx++)
        {
            var offset = ((py * zoom + mid) * stride) + ((px * zoom + zx) * 4);
            *(uint*)(ptr + offset) = color;
        }
        for (var zy = 0; zy < zoom; zy++)
        {
            var offset = ((py * zoom + zy) * stride) + ((px * zoom + mid) * 4);
            *(uint*)(ptr + offset) = color;
        }
    }

    private void DrawSelectionOverlay(nint ptr, int stride, int zoom, int viewW, int viewH)
    {
        var sel = Ctrl.Selection;
        var overlay = 0x80FFFF00u;
        var maxX = Math.Max(0, viewW - 1);
        var maxY = Math.Max(0, viewH - 1);
        var left = Math.Clamp(sel.PixelX, 0, maxX);
        var top = Math.Clamp(sel.PixelY, 0, maxY);
        var right = Math.Clamp(sel.PixelX + sel.PixelWidth - 1, 0, maxX);
        var bottom = Math.Clamp(sel.PixelY + sel.PixelHeight - 1, 0, maxY);
        for (var y = top; y <= bottom; y++)
        for (var x = left; x <= right; x++)
        {
            var onLeft = x == left;
            var onRight = x == right;
            var onTop = y == top;
            var onBottom = y == bottom;
            for (var zy = 0; zy < zoom; zy++)
            for (var zx = 0; zx < zoom; zx++)
            {
                if ((onTop && zy == 0) || (onBottom && zy == zoom - 1) ||
                    (onLeft && zx == 0) || (onRight && zx == zoom - 1))
                    FillPixelBlock(ptr, stride, x, y, zoom, overlay, viewW, viewH);
            }
        }
    }

    private void RefreshSheet()
    {
        if (_sheetBitmap == null) return;
        var sw = Ctrl.SheetWidth;
        var sh = Ctrl.SheetHeight;

        using (var locked = _sheetBitmap.Lock())
        {
            var stride = locked.RowBytes;
            var ptr = locked.Address;

            for (var y = 0; y < sh; y++)
            for (var x = 0; x < sw; x++)
            {
                var color = Pico8Colors.ToBgra(Ctrl.ReadSheetPixel(x, y), Ctrl.ColorChip);
                FillPixelBlock(ptr, stride, x, y, SheetZoom, color, sw, sh);
            }

            var (vx, vy) = Ctrl.Document.ViewOriginSheet;
            var tile = Ctrl.TileSize;
            if (Ctrl.ViewWidth > tile || Ctrl.ViewHeight > tile)
                HighlightRegionRect(ptr, stride, vx, vy, Ctrl.ViewWidth, Ctrl.ViewHeight, SheetZoom, 0xFFFFCC00, sw, sh);
            else
                HighlightSpriteRect(ptr, stride, vx, vy, tile, SheetZoom, 0xFFFFCC00, sw, sh);

            foreach (var idx in Ctrl.Selection.SpriteIndices)
            {
                var (sx, sy) = Ctrl.Document.SpriteOriginFor(idx);
                HighlightSpriteRect(ptr, stride, sx, sy, Ctrl.TileSize, SheetZoom, 0xFF00CCFF, sw, sh);
            }

            if (Ctrl.Viewport.HexView)
                DrawHexOverlay();
        }

        _sheetImage.InvalidateVisual();
    }

    private static void DrawHexOverlay()
    {
        // Hex indices drawn as subtle tint on sprite top-left pixel
    }

    private static unsafe void ClearBitmap(nint ptr, int stride, int width, int height, uint color)
    {
        for (var y = 0; y < height; y++)
        {
            var row = ptr + y * stride;
            for (var x = 0; x < width; x++)
                *(uint*)(row + x * 4) = color;
        }
    }

    private static unsafe void FillPixelBlock(nint ptr, int stride, int px, int py, int zoom, uint color, int maxW, int maxH)
    {
        if (px < 0 || py < 0 || px >= maxW || py >= maxH) return;
        for (var zy = 0; zy < zoom; zy++)
        for (var zx = 0; zx < zoom; zx++)
        {
            var offset = ((py * zoom + zy) * stride) + ((px * zoom + zx) * 4);
            *(uint*)(ptr + offset) = color;
        }
    }

    private static unsafe void BlendPixelBlock(nint ptr, int stride, int px, int py, int zoom, uint fg, int maxW, int maxH)
    {
        if (px < 0 || py < 0 || px >= maxW || py >= maxH) return;
        for (var zy = 0; zy < zoom; zy++)
        for (var zx = 0; zx < zoom; zx++)
        {
            var offset = ((py * zoom + zy) * stride) + ((px * zoom + zx) * 4);
            *(uint*)(ptr + offset) = Blend(*(uint*)(ptr + offset), fg);
        }
    }

    private static unsafe void DrawGridOverlay(nint ptr, int stride, int size, int cellSize)
    {
        if (cellSize <= 0) return;
        var gridColor = 0x40000000u;
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

    private static unsafe void HighlightSpriteRect(nint ptr, int stride, int ox, int oy, int tileSize, int zoom, uint highlight, int sheetW, int sheetH) =>
        HighlightRegionRect(ptr, stride, ox, oy, tileSize, tileSize, zoom, highlight, sheetW, sheetH);

    private static unsafe void HighlightRegionRect(nint ptr, int stride, int ox, int oy, int w, int h, int zoom, uint highlight, int sheetW, int sheetH)
    {
        for (var x = 0; x < w; x++)
        {
            FillPixelBlock(ptr, stride, ox + x, oy, zoom, highlight, sheetW, sheetH);
            FillPixelBlock(ptr, stride, ox + x, oy + h - 1, zoom, highlight, sheetW, sheetH);
        }
        for (var y = 0; y < h; y++)
        {
            FillPixelBlock(ptr, stride, ox, oy + y, zoom, highlight, sheetW, sheetH);
            FillPixelBlock(ptr, stride, ox + w - 1, oy + y, zoom, highlight, sheetW, sheetH);
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

    private Grid BuildLayout()
    {
        _chrome = new EditorChrome();

        _spriteLabel = new TextBlock { Text = Ctrl.SpriteLabel, Foreground = Brushes.LightGray, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) };

        _canvasImage = new Image { Stretch = Stretch.None };
        _canvasImage.PointerPressed += OnCanvasPointerPressed;
        _canvasImage.PointerMoved += OnCanvasPointerMoved;
        _canvasImage.PointerExited += OnCanvasPointerExited;
        _canvasImage.PointerWheelChanged += OnCanvasWheel;
        _canvasBorder = new Border
        {
            Background = new SolidColorBrush(EditorTheme.CanvasBackground),
            BorderBrush = new SolidColorBrush(EditorTheme.CanvasBorder),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = _canvasImage,
        };
        _canvasHost = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ClipToBounds = true,
            Child = _canvasBorder,
        };
        _canvasHost.SizeChanged += (_, _) => UpdateCanvasPanelSize();
        _chrome.PrimaryHost.PanelSizeChanged += (_, _) => UpdateCanvasPanelSize();

        var canvasGrid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        canvasGrid.Children.Add(_spriteLabel);
        Grid.SetRow(_spriteLabel, 0);
        var nav = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var prevBtn = new Button { Content = "◀ Q", Padding = new Thickness(8, 2) };
        prevBtn.Click += (_, _) => Ctrl.NavigateSprite(-1);
        var nextBtn = new Button { Content = "W ▶", Padding = new Thickness(8, 2) };
        nextBtn.Click += (_, _) => Ctrl.NavigateSprite(1);
        nav.Children.Add(prevBtn);
        nav.Children.Add(nextBtn);
        if (Ctrl.UsesGfx2Bank)
        {
            var gfxBtn = new Button { Content = "GFX (0–127)", Padding = new Thickness(8, 2) };
            gfxBtn.Click += (_, _) => Ctrl.SwitchGfxBank(0);
            var gfx2Btn = new Button { Content = "GFX2 (128–255)", Padding = new Thickness(8, 2) };
            gfx2Btn.Click += (_, _) => Ctrl.SwitchGfxBank(1);
            nav.Children.Add(gfxBtn);
            nav.Children.Add(gfx2Btn);
        }
        canvasGrid.Children.Add(nav);
        Grid.SetRow(nav, 1);
        canvasGrid.Children.Add(_canvasHost);
        Grid.SetRow(_canvasHost, 2);
        _chrome.PrimaryHost.Child = canvasGrid;

        _toolStrip = _chrome.ToolStrip;
        _toolStrip.SetTools(
        [
            ("pencil", null, Sprites.SpriteToolIcons.CreateBrushIcon),
            ("eraser", null, Sprites.SpriteToolIcons.CreateEraserIcon),
            ("fill", "🪣", null),
            ("picker", "💧", null),
            ("select", "⬚", null),
            ("shape", "◇", null),
            ("stamp", "📌", null),
            ("flip-h", "↔", null),
            ("flip-v", "↕", null),
            ("rotate", "↻", null),
        ], Ctrl.SelectedTool);
        _toolStrip.ToolSelected += tool =>
        {
            if (tool is "flip-h" or "flip-v" or "rotate")
            {
                if (tool == "flip-h") Ctrl.FlipHorizontal();
                else if (tool == "flip-v") Ctrl.FlipVertical();
                else Ctrl.Rotate90();
            }
            else SelectTool(tool);
        };

        _chrome.ContextStrip.Children.Add(new TextBlock { Text = "Palette", Foreground = Brushes.LightGray, FontSize = EditorTheme.SectionLabelFontSize, VerticalAlignment = VerticalAlignment.Center });
        _palettePanel = new WrapPanel();
        _chrome.ContextStrip.Children.Add(_palettePanel);
        var swapBtn = new Button { Content = "↔ X", Padding = new Thickness(6, 2) };
        swapBtn.Click += (_, _) => Ctrl.Palette.Swap();
        _chrome.ContextStrip.Children.Add(swapBtn);
        _flagsPanel = new WrapPanel();
        for (var i = 0; i < 8; i++)
        {
            var bit = i;
            var toggle = new ToggleButton { Content = $"F{bit}", Width = 44, Height = 28, Margin = new Thickness(2), FontSize = 11 };
            toggle.IsCheckedChanged += (_, _) => { if (toggle.IsChecked.HasValue) Ctrl.SetFlag(bit, toggle.IsChecked.Value); };
            _flagsPanel.Children.Add(toggle);
        }
        _chrome.ContextStrip.Children.Add(_flagsPanel);
        var undoBtn = new Button { Content = "Undo", Padding = new Thickness(6, 2) };
        undoBtn.Click += (_, _) => { Ctrl.HandleKey("Z", true, false, false); RefreshCanvas(); RefreshSheet(); RefreshFlagToggles(); };
        var redoBtn = new Button { Content = "Redo", Padding = new Thickness(6, 2) };
        redoBtn.Click += (_, _) => { Ctrl.HandleKey("Y", true, false, false); RefreshCanvas(); RefreshSheet(); RefreshFlagToggles(); };
        var clearBtn = new Button { Content = "Clear", Padding = new Thickness(6, 2) };
        clearBtn.Click += (_, _) => Ctrl.ClearSprite();
        _chrome.ContextStrip.Children.Add(undoBtn);
        _chrome.ContextStrip.Children.Add(redoBtn);
        _chrome.ContextStrip.Children.Add(clearBtn);

        var sheetPanel = new StackPanel { Spacing = 4 };
        sheetPanel.Children.Add(new TextBlock { Text = "Sprite Sheet", Foreground = Brushes.LightGray, FontSize = EditorTheme.SectionLabelFontSize });
        _sheetImage = new Image { Stretch = Stretch.None };
        _sheetImage.PointerPressed += OnSheetPointerPressed;
        _sheetImage.PointerMoved += OnSheetPointerMoved;
        _sheetImage.PointerReleased += OnSheetPointerReleased;
        sheetPanel.Children.Add(new ScrollViewer { MaxHeight = 160, Content = _sheetImage, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });
        var sheetOps = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var dupBtn = new Button { Content = "Dup", Padding = new Thickness(6, 2) };
        dupBtn.Click += (_, _) => Ctrl.DuplicateSprites();
        var delBtn = new Button { Content = "Del", Padding = new Thickness(6, 2) };
        delBtn.Click += (_, _) => Ctrl.DeleteSprites();
        sheetOps.Children.Add(dupBtn);
        sheetOps.Children.Add(delBtn);
        sheetPanel.Children.Add(sheetOps);
        _chrome.NavigatorHost.Content = sheetPanel;

        _statusBar = _chrome.StatusFooter;
        _statusBar.Text = Ctrl.StatusText;

        EditorKeyboardRouting.PreventFocusSteal(_chrome);
        _chrome.PointerPressed += (_, _) => Focus();

        return _chrome;
    }

    private void RebuildPaletteSwatches()
    {
        _palettePanel.Children.Clear();
        for (var i = 0; i < Ctrl.PaletteCount; i++)
        {
            var idx = i;
            var swatch = new Border
            {
                Width = 24, Height = 24,
                Background = new SolidColorBrush(Pico8Colors.ToAvaloniaColor(i, Ctrl.ColorChip)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(1),
                Tag = idx,
            };
            swatch.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(swatch).Properties.IsRightButtonPressed)
                    Ctrl.Palette.SecondaryColor = idx;
                else
                    Ctrl.Palette.PrimaryColor = idx;
            };
            _palettePanel.Children.Add(swatch);
        }
        UpdatePaletteHighlight();
    }

    private void RefreshFlagToggles()
    {
        for (var i = 0; i < _flagsPanel.Children.Count; i++)
        {
            if (_flagsPanel.Children[i] is ToggleButton toggle)
                toggle.IsChecked = Ctrl.GetFlag(i) != 0;
        }
    }

    private void SelectTool(string tool)
    {
        switch (tool)
        {
            case "flip-h": Ctrl.FlipHorizontal(); RefreshCanvas(); RefreshSheet(); return;
            case "flip-v": Ctrl.FlipVertical(); RefreshCanvas(); RefreshSheet(); return;
            case "rotate": Ctrl.Rotate90(); RefreshCanvas(); RefreshSheet(); return;
            case "shape":
                if (Ctrl.SelectedTool == "shape") Ctrl.CycleShapeMode();
                else Ctrl.SelectedTool = "shape";
                UpdateToolHighlight();
                return;
        }
        Ctrl.SelectedTool = tool;
        UpdateToolHighlight();
    }

    private void UpdateSpriteLabel() => _spriteLabel.Text = Ctrl.SpriteLabel;
    private void UpdateStatusBar() => _statusBar.Text = Ctrl.StatusText;

    private void UpdatePaletteHighlight()
    {
        foreach (var child in _palettePanel.Children)
        {
            if (child is Border swatch && swatch.Tag is int idx)
            {
                var isPrimary = idx == Ctrl.PrimaryColor;
                var isSecondary = idx == Ctrl.SecondaryColor;
                swatch.BorderBrush = isPrimary ? Brushes.White : isSecondary ? Brushes.LightGray : Brushes.Transparent;
                swatch.BorderThickness = new Thickness(isPrimary ? 3 : isSecondary ? 1 : 0);
            }
        }
    }

    private void UpdateToolHighlight() =>
        _toolStrip.HighlightTool(Ctrl.SelectedTool == "shape" ? "shape" : Ctrl.SelectedTool);

    private void OnSheetPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pt = e.GetPosition(_sheetImage);
        var sx = (int)(pt.X / SheetZoom);
        var sy = (int)(pt.Y / SheetZoom);
        if (sx < 0 || sy < 0 || sx >= Ctrl.SheetWidth || sy >= Ctrl.SheetHeight) return;
        var mods = e.KeyModifiers;
        if (mods.HasFlag(KeyModifiers.Shift) && !mods.HasFlag(KeyModifiers.Control))
        {
            _sheetDragging = true;
            _sheetDragStartX = sx;
            _sheetDragStartY = sy;
            e.Pointer.Capture(_sheetImage);
            return;
        }
        Ctrl.SelectSpriteAtSheet(sx, sy, false, mods.HasFlag(KeyModifiers.Control));
        RefreshFlagToggles();
    }

    private void OnSheetPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_sheetDragging) return;
        var pt = e.GetPosition(_sheetImage);
        var (sx, sy) = SheetCoordsFromPointer(pt);
        var tile = Ctrl.TileSize;
        if (tile <= 0) return;
        var sameCell = sx / tile == _sheetDragStartX / tile && sy / tile == _sheetDragStartY / tile;
        if (sameCell) return;

        Ctrl.SelectSpritesInSheetRect(_sheetDragStartX, _sheetDragStartY, sx, sy, updateViewRegion: false);
        RefreshFlagToggles();
        QueueVisualRefresh(rebuildCanvas: false);
    }

    private void OnSheetPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_sheetDragging) return;
        var pt = e.GetPosition(_sheetImage);
        var (sx, sy) = SheetCoordsFromPointer(pt);
        var startX = _sheetDragStartX;
        var startY = _sheetDragStartY;
        _sheetDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
        FinishSheetDrag(sx, sy, startX, startY);
    }

    private (int sx, int sy) SheetCoordsFromPointer(Point pt)
    {
        var maxX = Math.Max(0, Ctrl.SheetWidth - 1);
        var maxY = Math.Max(0, Ctrl.SheetHeight - 1);
        var sx = Math.Clamp((int)(pt.X / SheetZoom), 0, maxX);
        var sy = Math.Clamp((int)(pt.Y / SheetZoom), 0, maxY);
        return (sx, sy);
    }

    private void FinishSheetDrag(int sx, int sy, int startX, int startY)
    {
        var tile = Ctrl.TileSize;
        if (tile <= 0) return;
        var sameCell = sx / tile == startX / tile && sy / tile == startY / tile;
        var endX = sx;
        var endY = sy;

        Dispatcher.UIThread.Post(() =>
        {
            if (sameCell)
            {
                var index = (endY / tile) * Ctrl.SpritesPerRow + endX / tile;
                if (index >= 0 && index < Ctrl.SpriteCount)
                    Ctrl.AddSpriteToSelection(index);
            }
            else
            {
                Ctrl.UpdateViewRegion();
            }

            RefreshFlagToggles();
        }, DispatcherPriority.Background);
    }

    private PointerInput BuildInput(int x, int y, bool isStart)
    {
        var vw = Ctrl.ViewWidth;
        var vh = Ctrl.ViewHeight;
        x = Math.Clamp(x, 0, Math.Max(0, vw - 1));
        y = Math.Clamp(y, 0, Math.Max(0, vh - 1));
        return new PointerInput
        {
            LocalX = x, LocalY = y, IsStart = isStart,
        IsRightButton = _isRightButton,
        Ctrl = _keyModifiers.HasFlag(KeyModifiers.Control),
        Shift = _keyModifiers.HasFlag(KeyModifiers.Shift),
        Alt = _keyModifiers.HasFlag(KeyModifiers.Alt),
        };
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
        _isDrawing = true;
        _isRightButton = e.GetCurrentPoint(_canvasImage).Properties.IsRightButtonPressed;
        var pt = e.GetPosition(_canvasImage);
        var zoom = Ctrl.Viewport.Zoom;
        var x = (int)(pt.X / zoom);
        var y = (int)(pt.Y / zoom);
        var input = BuildInput(x, y, isStart: true);

        if (Ctrl.IsSpaceHeld || Ctrl.SelectedTool == "pan")
        {
            Ctrl.OnPointerDownPan(input);
            return;
        }

        if (_isRightButton && Ctrl.SelectedTool is "pencil" or "eraser")
        {
            Ctrl.HandleEyedropper(input);
            return;
        }

        Ctrl.OnPointerDown(input);
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        var pt = e.GetPosition(_canvasImage);
        var zoom = Ctrl.Viewport.Zoom;
        var x = (int)(pt.X / zoom);
        var y = (int)(pt.Y / zoom);
        var input = BuildInput(x, y, isStart: false);

        if (!_isDrawing && !Ctrl.IsSpaceHeld)
        {
            if (Ctrl.SelectedTool == "stamp" && Ctrl.Clipboard.HasContent)
            {
                Ctrl.UpdateStampPreview(input);
                return;
            }

            Ctrl.UpdateCursorPreview(input);
            return;
        }

        if (!_isDrawing) return;

        if (Ctrl.IsSpaceHeld)
        {
            Ctrl.OnPointerMovePan(input);
            return;
        }

        Ctrl.OnPointerMove(input);
    }

    private void OnCanvasPointerExited(object? sender, PointerEventArgs e)
    {
        Ctrl.ClearStampPreview();
        Ctrl.ClearCursorPreview();
    }

    private void OnCanvasWheel(object? sender, PointerWheelEventArgs e)
    {
        var delta = e.Delta.Y > 0 ? 1 : -1;
        var pt = e.GetPosition(_canvasImage);
        Ctrl.Viewport.AdjustZoom(delta, (int)pt.X, (int)pt.Y);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDrawing)
        {
            var pt = e.GetPosition(_canvasImage);
            var zoom = Ctrl.Viewport.Zoom;
            var input = BuildInput((int)(pt.X / zoom), (int)(pt.Y / zoom), isStart: false);
            if (Ctrl.IsSpaceHeld)
                Ctrl.OnPointerUpPan(input);
            else
                Ctrl.OnPointerUp(input);
        }
        _isDrawing = false;
        _isRightButton = false;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _keyModifiers = e.KeyModifiers;
        var key = e.Key.ToString();
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        if (Ctrl.HandleKey(key, ctrl, shift, alt))
        {
            e.Handled = true;
            QueueVisualRefresh(rebuildCanvas: true);
        }

        Ctrl.RefreshStampPreviewModifiers(ctrl);
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;
        if (Ctrl.HandleTextInput(e.Text))
        {
            e.Handled = true;
            QueueVisualRefresh(rebuildCanvas: true);
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _keyModifiers = e.KeyModifiers;
        Ctrl.HandleKeyUp(e.Key.ToString());
        Ctrl.RefreshStampPreviewModifiers(e.KeyModifiers.HasFlag(KeyModifiers.Control));
    }
}
