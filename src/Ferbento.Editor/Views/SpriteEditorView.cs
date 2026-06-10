using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Ferbento.Core.Chips;
using Ferbento.Editor.Sprites;
using Ferbento.Editor.Sprites.Input;
using Ferbento.Editor.Sprites.Tools;
using Ferbento.Editor.ViewModels;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Views;

public class SpriteEditorView : UserControl, IProfileAwareEditor
{
    private readonly SpriteEditorViewModel _vm = new();
    private SpriteEditorController Ctrl => _vm.Controller;

    private WriteableBitmap? _canvasBitmap;
    private WriteableBitmap? _sheetBitmap;
    private Image _canvasImage = null!;
    private Image _sheetImage = null!;
    private TextBlock _spriteLabel = null!;
    private TextBlock _statusBar = null!;
    private Panel _palettePanel = null!;
    private Panel _flagsPanel = null!;
    private Panel _toolsPanel = null!;
    private Border _canvasBorder = null!;
    private Action? _onEdited;

    private bool _isDrawing;
    private bool _isRightButton;
    private bool _sheetDragging;
    private int _sheetDragStartX;
    private int _sheetDragStartY;
    private KeyModifiers _keyModifiers;

    private const int SheetZoom = 2;

    public void SetEditedHandler(Action handler) => _onEdited = handler;

    public SpriteEditorView()
    {
        DataContext = _vm;
        Content = BuildLayout();
        Loaded += (_, _) => RebuildBitmaps();
        Focusable = true;

        Ctrl.Edited += () => _onEdited?.Invoke();
        Ctrl.RefreshRequested += OnRefreshRequested;

        Ctrl.Viewport.PropertyChanged += (_, e) =>
        {
            UpdateStatusBar();
            if (e.PropertyName is nameof(ViewportState.Zoom) or nameof(ViewportState.ZoomPercent))
                RebuildBitmaps();
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
                if (e.PropertyName is nameof(SpriteEditorController.TileSize)
                    or nameof(SpriteEditorController.SheetWidth)
                    or nameof(SpriteEditorController.SheetHeight)
                    or nameof(SpriteEditorController.PaletteCount)
                    or nameof(SpriteEditorController.ViewWidth)
                    or nameof(SpriteEditorController.ViewHeight))
                {
                    RebuildPaletteSwatches();
                    RebuildBitmaps();
                }
                RefreshCanvas();
                RefreshSheet();
                UpdateSpriteLabel();
                UpdatePaletteHighlight();
                UpdateToolHighlight();
                UpdateStatusBar();
            }
            else if (e.PropertyName?.StartsWith("Flag") == true || e.PropertyName == nameof(SpriteEditorController.AllFlags))
                RefreshFlagToggles();
        };

        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
    }

    private void OnRefreshRequested()
    {
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
        var vw = Ctrl.ViewWidth;
        var vh = Ctrl.ViewHeight;
        var zoom = Ctrl.Viewport.Zoom;
        _canvasBitmap?.Dispose();
        _sheetBitmap?.Dispose();
        _canvasBitmap = new WriteableBitmap(
            new PixelSize(vw * zoom, vh * zoom),
            new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        _sheetBitmap = new WriteableBitmap(
            new PixelSize(Ctrl.SheetWidth * SheetZoom, Ctrl.SheetHeight * SheetZoom),
            new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        _canvasImage.Source = _canvasBitmap;
        _sheetImage.Source = _sheetBitmap;
        RefreshCanvas();
        RefreshSheet();
    }

    private void RefreshCanvas()
    {
        if (_canvasBitmap == null) return;
        var vw = Ctrl.ViewWidth;
        var vh = Ctrl.ViewHeight;
        var zoom = Ctrl.Viewport.Zoom;
        var expected = new PixelSize(vw * zoom, vh * zoom);
        if (_canvasBitmap.PixelSize != expected)
        {
            RebuildBitmaps();
            return;
        }

        using var locked = _canvasBitmap.Lock();
        var stride = locked.RowBytes;
        var ptr = locked.Address;

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

        if (Ctrl.Viewport.GridVisible && zoom >= 4)
            DrawGridOverlay(ptr, stride, vw * zoom, Ctrl.TileSize * zoom);

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

    private void DrawSelectionOverlay(nint ptr, int stride, int zoom, int viewW, int viewH)
    {
        var sel = Ctrl.Selection;
        var overlay = 0x80FFFF00u;
        var left = Math.Clamp(sel.PixelX, 0, viewW - 1);
        var top = Math.Clamp(sel.PixelY, 0, viewH - 1);
        var right = Math.Clamp(sel.PixelX + sel.PixelWidth - 1, 0, viewW - 1);
        var bottom = Math.Clamp(sel.PixelY + sel.PixelHeight - 1, 0, viewH - 1);
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

        using var locked = _sheetBitmap.Lock();
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
            DrawHexOverlay(ptr, stride);

        _sheetImage.InvalidateVisual();
    }

    private static unsafe void DrawHexOverlay(nint ptr, int stride)
    {
        // Hex indices drawn as subtle tint on sprite top-left pixel
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

    private Control BuildLayout()
    {
        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,240"),
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        };

        var sheetPanel = new StackPanel { Margin = new Thickness(8), Spacing = 4 };
        sheetPanel.Children.Add(new TextBlock { Text = "Sprite Sheet", Foreground = Brushes.LightGray, FontSize = 11 });
        _sheetImage = new Image { Stretch = Stretch.None };
        _sheetImage.PointerPressed += OnSheetPointerPressed;
        _sheetImage.PointerMoved += OnSheetPointerMoved;
        sheetPanel.Children.Add(new ScrollViewer { MaxHeight = 320, Content = _sheetImage });

        var sheetOps = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var dupBtn = new Button { Content = "Dup", Padding = new Thickness(6, 2) };
        ToolTip.SetTip(dupBtn, "Duplicate sprite(s)");
        dupBtn.Click += (_, _) => Ctrl.DuplicateSprites();
        var delBtn = new Button { Content = "Del", Padding = new Thickness(6, 2) };
        ToolTip.SetTip(delBtn, "Delete sprite(s)");
        delBtn.Click += (_, _) => Ctrl.DeleteSprites();
        sheetOps.Children.Add(dupBtn);
        sheetOps.Children.Add(delBtn);
        sheetPanel.Children.Add(sheetOps);

        var center = new StackPanel { Margin = new Thickness(8), Spacing = 8 };
        _spriteLabel = new TextBlock { Text = Ctrl.SpriteLabel, Foreground = Brushes.LightGray, FontSize = 12 };
        center.Children.Add(_spriteLabel);

        var nav = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var prevBtn = new Button { Content = "◀ Q", Padding = new Thickness(8, 2) };
        ToolTip.SetTip(prevBtn, "Prev sprite (Q)");
        prevBtn.Click += (_, _) => Ctrl.CurrentSpriteIndex--;
        var nextBtn = new Button { Content = "W ▶", Padding = new Thickness(8, 2) };
        ToolTip.SetTip(nextBtn, "Next sprite (W)");
        nextBtn.Click += (_, _) => Ctrl.CurrentSpriteIndex++;
        nav.Children.Add(prevBtn);
        nav.Children.Add(nextBtn);
        center.Children.Add(nav);

        if (Ctrl.UsesGfx2Bank)
        {
            var bankRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            var gfxBtn = new Button { Content = "GFX (0–127)", Padding = new Thickness(8, 2) };
            var gfx2Btn = new Button { Content = "GFX2 (128–255)", Padding = new Thickness(8, 2) };
            gfxBtn.Click += (_, _) => Ctrl.SwitchGfxBank(0);
            gfx2Btn.Click += (_, _) => Ctrl.SwitchGfxBank(1);
            bankRow.Children.Add(gfxBtn);
            bankRow.Children.Add(gfx2Btn);
            center.Children.Add(bankRow);
        }

        _canvasImage = new Image { Stretch = Stretch.None };
        _canvasImage.PointerPressed += OnCanvasPointerPressed;
        _canvasImage.PointerMoved += OnCanvasPointerMoved;
        _canvasImage.PointerExited += OnCanvasPointerExited;
        _canvasImage.PointerWheelChanged += OnCanvasWheel;
        _canvasBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderBrush = new SolidColorBrush(Color.Parse("#555555")),
            BorderThickness = new Thickness(1),
            Child = _canvasImage,
        };
        center.Children.Add(_canvasBorder);

        _statusBar = new TextBlock { Foreground = Brushes.Gray, FontSize = 10, Text = Ctrl.StatusText };
        center.Children.Add(_statusBar);

        var sidebar = new StackPanel { Margin = new Thickness(8), Spacing = 8 };

        sidebar.Children.Add(new TextBlock { Text = "Palette (L=primary, R=secondary)", Foreground = Brushes.LightGray, FontSize = 11 });
        _palettePanel = new WrapPanel { Width = 220 };
        sidebar.Children.Add(_palettePanel);

        var swapBtn = new Button { Content = "↔ Swap (X)", Padding = new Thickness(8, 2) };
        swapBtn.Click += (_, _) => Ctrl.Palette.Swap();
        sidebar.Children.Add(swapBtn);

        sidebar.Children.Add(new TextBlock { Text = "Sprite Flags", Foreground = Brushes.LightGray, FontSize = 11 });
        _flagsPanel = new WrapPanel { Width = 220 };
        for (var i = 0; i < 8; i++)
        {
            var bit = i;
            var toggle = new ToggleButton { Content = $"F{bit}", Width = 44, Height = 28, Margin = new Thickness(2), FontSize = 11 };
            toggle.IsCheckedChanged += (_, _) =>
            {
                if (toggle.IsChecked.HasValue)
                    Ctrl.SetFlag(bit, toggle.IsChecked.Value);
            };
            _flagsPanel.Children.Add(toggle);
        }
        sidebar.Children.Add(_flagsPanel);

        sidebar.Children.Add(new TextBlock { Text = "Tools", Foreground = Brushes.LightGray, FontSize = 11 });
        _toolsPanel = new WrapPanel { Width = 220 };
        foreach (var (tool, label) in new (string, string)[]
        {
            ("pencil", "✏"), ("fill", "🪣"), ("picker", "💧"), ("select", "⬚"),
            ("shape", "◇"), ("stamp", "📌"),
            ("flip-h", "↔"), ("flip-v", "↕"), ("rotate", "↻"),
        })
        {
            var tip = SpriteEditorInputMap.GetTooltip(tool);
            var btn = new Border
            {
                Width = 36, Height = 28,
                Background = new SolidColorBrush(Color.Parse("#333337")),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = label, HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, FontSize = 14,
                },
                Tag = tool, Margin = new Thickness(2),
            };
            ToolTip.SetTip(btn, tip);
            btn.PointerPressed += (_, _) => SelectTool(tool);
            _toolsPanel.Children.Add(btn);
        }
        sidebar.Children.Add(_toolsPanel);

        var undoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var undoBtn = new Button { Content = "Undo", Padding = new Thickness(8, 2) };
        ToolTip.SetTip(undoBtn, "Ctrl+Z");
        undoBtn.Click += (_, _) => { Ctrl.HandleKey("Z", true, false, false); RefreshCanvas(); RefreshSheet(); RefreshFlagToggles(); };
        var redoBtn = new Button { Content = "Redo", Padding = new Thickness(8, 2) };
        ToolTip.SetTip(redoBtn, "Ctrl+Y");
        redoBtn.Click += (_, _) => { Ctrl.HandleKey("Y", true, false, false); RefreshCanvas(); RefreshSheet(); RefreshFlagToggles(); };
        var clearBtn = new Button { Content = "Clear", Padding = new Thickness(8, 2) };
        ToolTip.SetTip(clearBtn, "Ctrl+Delete");
        clearBtn.Click += (_, _) => Ctrl.ClearSprite();
        undoRow.Children.Add(undoBtn);
        undoRow.Children.Add(redoBtn);
        undoRow.Children.Add(clearBtn);
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

    private void UpdateToolHighlight()
    {
        foreach (var child in _toolsPanel.Children)
        {
            if (child is Border btn && btn.Tag is string tool)
            {
                var active = tool == Ctrl.SelectedTool || (tool == "shape" && Ctrl.SelectedTool == "shape");
                btn.BorderBrush = active ? Brushes.CornflowerBlue : Brushes.Transparent;
            }
        }
    }

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
        RefreshSheet();
        RefreshCanvas();
    }

    private void OnSheetPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_sheetDragging) return;
        var pt = e.GetPosition(_sheetImage);
        var sx = Math.Clamp((int)(pt.X / SheetZoom), 0, Ctrl.SheetWidth - 1);
        var sy = Math.Clamp((int)(pt.Y / SheetZoom), 0, Ctrl.SheetHeight - 1);
        Ctrl.SelectSpritesInSheetRect(_sheetDragStartX, _sheetDragStartY, sx, sy);
        RefreshFlagToggles();
        RefreshSheet();
        RefreshCanvas();
    }

    private void EndSheetDrag(int sx, int sy)
    {
        if (!_sheetDragging) return;
        _sheetDragging = false;
        var tile = Ctrl.TileSize;
        var sameCell = sx / tile == _sheetDragStartX / tile && sy / tile == _sheetDragStartY / tile;
        if (sameCell)
        {
            var index = (sy / tile) * Ctrl.SpritesPerRow + sx / tile;
            if (index >= 0 && index < Ctrl.SpriteCount)
            {
                Ctrl.Selection.AddSprite(index);
                Ctrl.UpdateViewRegion();
            }
        }
        RefreshFlagToggles();
        RefreshSheet();
        RefreshCanvas();
    }

    private PointerInput BuildInput(int x, int y, bool isStart)
    {
        var vw = Ctrl.ViewWidth;
        var vh = Ctrl.ViewHeight;
        x = Math.Clamp(x, 0, vw - 1);
        y = Math.Clamp(y, 0, vh - 1);
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

        if (_isRightButton && Ctrl.SelectedTool == "pencil")
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

        if (!_isDrawing && Ctrl.SelectedTool == "stamp" && Ctrl.Clipboard.HasContent && !Ctrl.IsSpaceHeld)
        {
            Ctrl.UpdateStampPreview(input);
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

    private void OnCanvasPointerExited(object? sender, PointerEventArgs e) =>
        Ctrl.ClearStampPreview();

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
        if (_sheetDragging)
        {
            var pt = e.GetPosition(_sheetImage);
            var sx = Math.Clamp((int)(pt.X / SheetZoom), 0, Ctrl.SheetWidth - 1);
            var sy = Math.Clamp((int)(pt.Y / SheetZoom), 0, Ctrl.SheetHeight - 1);
            EndSheetDrag(sx, sy);
            e.Pointer.Capture(null);
        }
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
            e.Handled = true;

        Ctrl.RefreshStampPreviewModifiers(ctrl);
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _keyModifiers = e.KeyModifiers;
        Ctrl.HandleKeyUp(e.Key.ToString());
        Ctrl.RefreshStampPreviewModifiers(e.KeyModifiers.HasFlag(KeyModifiers.Control));
    }
}
