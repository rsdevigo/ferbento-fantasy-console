using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ferbento.Core.Chips;
using Ferbento.Editor.Chrome;
using Ferbento.Editor.Sprites.Animation;
using Ferbento.Editor.Sprites.Input;
using Ferbento.Editor.Sprites.Tools;
using Ferbento.Editor.Sprites.Undo;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Sprites;

public sealed class SpriteEditorController : INotifyPropertyChanged
{
    private const int MaxMultiSpriteTiles = 4;

    public SpriteDocument Document { get; } = new();
    public PaletteState Palette { get; } = new();
    public Selection Selection { get; } = new();
    public ViewportState Viewport { get; } = new();
    public UndoService UndoService { get; } = new(256);
    public SpriteClipboard Clipboard { get; } = new();
    public OnionSkinSettings OnionSkin { get; } = new();
    public TimelineState Timeline { get; } = new();
    public IAnimationProvider AnimationProvider { get; set; } = new NullAnimationProvider();

    private readonly Dictionary<string, ISpriteTool> _tools;
    private readonly ShapeToolHost _shapeHost = new();
    private readonly SpriteEditorContext _ctx;
    private string _selectedTool = "pencil";
    private bool _spaceHeld;
    private double _canvasPanelWidth;
    private double _canvasPanelHeight;
    private bool _canvasPanelSizeKnown;

    public event Action? Edited;
    public event Action? RefreshRequested;

    public bool CanUndo => UndoService.UndoCount > 0;
    public bool CanRedo => UndoService.RedoCount > 0;

    public int TileSize => Document.TileSize;
    public int SheetWidth => Document.SheetWidth;
    public int SheetHeight => Document.SheetHeight;
    public int SpritesPerRow => Document.SpritesPerRow;
    public int SpriteCount => Document.SpriteCount;
    public int PaletteCount => Document.PaletteCount;
    public bool UsesGfx2Bank => Document.UsesGfx2Bank;
    public ColorChip? ColorChip => Document.ColorChip;

    public int SelectedColor
    {
        get => Palette.PrimaryColor;
        set => Palette.PrimaryColor = value;
    }

    public int PrimaryColor => Palette.PrimaryColor;
    public int SecondaryColor => Palette.SecondaryColor;

    public string SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (_selectedTool == value) return;
            if (value != "stamp")
                StampTool.ClearPreview(_ctx);
            if (value != "select")
                ClearSelectPreview(_ctx);
            if (!SupportsCursorPreview(value))
                ClearCursorPreview();
            _selectedTool = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveToolLabel));
        }
    }

    public ShapeMode ShapeMode => _shapeHost.Mode;
    public (int x, int y)? StampPreviewPosition => _ctx.StampPreviewPosition;
    public bool StampPreviewSkipTransparent => _ctx.StampPreviewSkipTransparent;
    public (int x, int y)? SelectAnchor => _ctx.SelectAnchor;
    public (int x, int y)? SelectPreviewEnd => _ctx.SelectPreviewEnd;
    public (int x, int y)? CursorPreviewPosition => _ctx.CursorPreviewPosition;
    public string? CursorPreviewTool => _ctx.CursorPreviewTool;

    public string ActiveToolLabel =>
        _selectedTool == "shape" ? $"shape ({_shapeHost.Mode})" : _selectedTool;

    public int CurrentSpriteIndex
    {
        get => Document.CurrentSpriteIndex;
        set
        {
            if (Document.CurrentSpriteIndex == value) return;
            Document.CurrentSpriteIndex = value;
            if (!Selection.HasSpriteSelection)
                UpdateViewRegion();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SpriteLabel));
            NotifyFlagsChanged();
            RequestRefresh();
        }
    }

    public int GfxBank
    {
        get => Document.GfxBank;
        set
        {
            Document.GfxBank = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SpriteLabel));
        }
    }

    public string SpriteLabel =>
        UsesGfx2Bank && GfxBank == 1
            ? $"Sprite #{CurrentSpriteIndex} (GFX2)"
            : $"Sprite #{CurrentSpriteIndex}";

    public int BrushSize => _ctx.BrushSize;
    public int ViewWidth => Document.ViewWidth;
    public int ViewHeight => Document.ViewHeight;

    public string StatusText
    {
        get
        {
            var view = ViewWidth == ViewHeight && ViewWidth == TileSize
                ? $"{TileSize}×{TileSize}"
                : $"{ViewWidth}×{ViewHeight}";
            var brush = SelectedTool is "pencil" or "eraser" ? $" | Brush {BrushSize}×{BrushSize}" : "";
            return $"Zoom {Viewport.ZoomPercent}% | {view}{brush} | Color {PrimaryColor}/{SecondaryColor} | {ActiveToolLabel}";
        }
    }

    public SpriteEditorController()
    {
        _tools = new Dictionary<string, ISpriteTool>
        {
            ["pencil"] = new PencilTool(),
            ["eraser"] = new EraserTool(),
            ["fill"] = new FillTool(),
            ["picker"] = new PickerTool(),
            ["select"] = new SelectionTool(),
            ["shape"] = _shapeHost,
            ["line"] = new LineTool(),
            ["rect"] = new RectangleTool(),
            ["circle"] = new CircleTool(),
            ["stamp"] = new StampTool(),
            ["pan"] = new PanTool(),
        };

        _ctx = new SpriteEditorContext
        {
            Document = Document,
            Palette = Palette,
            Selection = Selection,
            Viewport = Viewport,
            Undo = UndoService,
            Clipboard = Clipboard,
            NotifyEdited = () => Edited?.Invoke(),
            RequestRefresh = RequestRefresh,
        };

        UndoService.StateChanged += () =>
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        };

        Palette.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PaletteState.PrimaryColor))
                OnPropertyChanged(nameof(SelectedColor));
            OnPropertyChanged(nameof(PrimaryColor));
            OnPropertyChanged(nameof(SecondaryColor));
            OnPropertyChanged(nameof(StatusText));
        };

        Viewport.PropertyChanged += (_, _) =>
            OnPropertyChanged(nameof(StatusText));
    }

    public void UpdateViewRegion()
    {
        if (Selection.HasSpriteSelection && TryGetSelectionTileRect(out var col0, out var row0, out var col1, out var row1))
        {
            ClampTileRectToMax(ref col0, ref row0, ref col1, ref row1);
            var tile = TileSize;
            Document.SetViewRegion(
                col0 * tile, row0 * tile,
                (col1 - col0 + 1) * tile, (row1 - row0 + 1) * tile);
        }
        else
            Document.ResetViewToCurrentSprite();

        OnPropertyChanged(nameof(ViewWidth));
        OnPropertyChanged(nameof(ViewHeight));
        OnPropertyChanged(nameof(StatusText));
        ApplyFitZoom(resetZoom: true);
    }

    public void SetCanvasPanelSize(double width, double height)
    {
        if (width <= 0 || height <= 0) return;
        _canvasPanelWidth = width;
        _canvasPanelHeight = height;
        ApplyFitZoom(resetZoom: !_canvasPanelSizeKnown);
        _canvasPanelSizeKnown = true;
    }

    public int ComputeFitZoom()
    {
        var vw = Math.Max(1, ViewWidth);
        var vh = Math.Max(1, ViewHeight);
        if (_canvasPanelWidth <= 0 || _canvasPanelHeight <= 0)
            return Viewport.Zoom;

        var fitW = (int)(_canvasPanelWidth / vw);
        var fitH = (int)(_canvasPanelHeight / vh);
        return Math.Clamp(Math.Min(fitW, fitH), 1, 64);
    }

    private void ApplyFitZoom(bool resetZoom)
    {
        var fit = ComputeFitZoom();
        Viewport.MaxZoom = fit;
        Viewport.Zoom = resetZoom ? fit : Math.Min(Viewport.Zoom, fit);
    }

    public void NavigateSprite(int delta)
    {
        var index = Selection.HasSpriteSelection ? GetSelectionAnchorIndex() : CurrentSpriteIndex;
        index = Math.Clamp(index + delta, 0, Math.Max(0, SpriteCount - 1));
        Selection.Clear();
        CurrentSpriteIndex = index;
    }

    private int GetSelectionAnchorIndex()
    {
        if (!TryGetSelectionTileRect(out var col0, out var row0, out var col1, out var row1))
            return CurrentSpriteIndex;
        return row1 * SpritesPerRow + col1;
    }

    public void AddSpriteToSelection(int index)
    {
        if (index < 0 || index >= SpriteCount) return;

        var clickCol = index % SpritesPerRow;
        var clickRow = index / SpritesPerRow;

        int col0;
        int row0;
        int col1;
        int row1;
        if (Selection.HasSpriteSelection && TryGetSelectionTileRect(out col0, out row0, out col1, out row1))
        {
            if (!IsTileAdjacentToRect(clickCol, clickRow, col0, row0, col1, row1))
                return;
        }
        else
        {
            var (ox, oy) = Document.SpriteOrigin;
            col0 = col1 = ox / TileSize;
            row0 = row1 = oy / TileSize;
            if (!IsTileAdjacentToRect(clickCol, clickRow, col0, row0, col1, row1))
                return;
        }

        col0 = Math.Min(col0, clickCol);
        col1 = Math.Max(col1, clickCol);
        row0 = Math.Min(row0, clickRow);
        row1 = Math.Max(row1, clickRow);
        if (WouldExceedMaxTiles(col0, row0, col1, row1))
            return;

        SetSelectionTileRect(col0, row0, col1, row1);
        UpdateViewRegion();
        RequestRefresh();
    }

    private bool TryGetSelectionTileRect(out int col0, out int row0, out int col1, out int row1)
    {
        col0 = int.MaxValue;
        row0 = int.MaxValue;
        col1 = 0;
        row1 = 0;
        if (!Selection.HasSpriteSelection || Selection.SpriteIndices.Count == 0)
            return false;

        foreach (var idx in Selection.SpriteIndices)
        {
            var col = idx % SpritesPerRow;
            var row = idx / SpritesPerRow;
            col0 = Math.Min(col0, col);
            row0 = Math.Min(row0, row);
            col1 = Math.Max(col1, col);
            row1 = Math.Max(row1, row);
        }
        return true;
    }

    private static bool IsTileAdjacentToRect(int col, int row, int col0, int row0, int col1, int row1)
    {
        if (col >= col0 && col <= col1 && row >= row0 && row <= row1) return true;
        if (col < col0 - 1 || col > col1 + 1 || row < row0 - 1 || row > row1 + 1) return false;
        return true;
    }

    private static void ClampTileRectToMax(ref int col0, ref int row0, ref int col1, ref int row1)
    {
        if (col1 - col0 + 1 > MaxMultiSpriteTiles)
            col1 = col0 + MaxMultiSpriteTiles - 1;
        if (row1 - row0 + 1 > MaxMultiSpriteTiles)
            row1 = row0 + MaxMultiSpriteTiles - 1;
    }

    private static bool WouldExceedMaxTiles(int col0, int row0, int col1, int row1) =>
        col1 - col0 + 1 > MaxMultiSpriteTiles || row1 - row0 + 1 > MaxMultiSpriteTiles;

    private void SetSelectionTileRect(int col0, int row0, int col1, int row1)
    {
        var indices = new List<int>();
        for (var row = row0; row <= row1; row++)
        for (var col = col0; col <= col1; col++)
        {
            var index = row * SpritesPerRow + col;
            if (index >= 0 && index < SpriteCount)
                indices.Add(index);
        }
        if (indices.Count == 0) return;
        Selection.SetSprites(indices);
        CurrentSpriteIndex = indices[0];
    }

    public void AdjustBrushSize(int delta)
    {
        var sizes = new[] { 1, 2, 4 };
        var idx = Array.IndexOf(sizes, BrushSize);
        if (idx < 0) idx = 0;
        idx = Math.Clamp(idx + delta, 0, sizes.Length - 1);
        _ctx.BrushSize = sizes[idx];
        OnPropertyChanged(nameof(BrushSize));
        OnPropertyChanged(nameof(StatusText));
    }

    public void SelectSpritesInSheetRect(int sheetX0, int sheetY0, int sheetX1, int sheetY1, bool updateViewRegion = true)
    {
        var tile = TileSize;
        if (tile <= 0) return;
        var col0 = Math.Min(sheetX0, sheetX1) / tile;
        var col1 = Math.Max(sheetX0, sheetX1) / tile;
        var row0 = Math.Min(sheetY0, sheetY1) / tile;
        var row1 = Math.Max(sheetY0, sheetY1) / tile;
        ClampTileRectToMax(ref col0, ref row0, ref col1, ref row1);

        SetSelectionTileRect(col0, row0, col1, row1);
        if (updateViewRegion)
            UpdateViewRegion();
        else
            RequestRefresh();
    }

    public void SetProfile(HardwareProfile profile)
    {
        Document.SetProfile(profile);
        Palette.PaletteCount = Document.PaletteCount;
        OnPropertyChanged(nameof(TileSize));
        OnPropertyChanged(nameof(SheetWidth));
        OnPropertyChanged(nameof(SheetHeight));
        OnPropertyChanged(nameof(SpritesPerRow));
        OnPropertyChanged(nameof(SpriteCount));
        OnPropertyChanged(nameof(PaletteCount));
        OnPropertyChanged(nameof(UsesGfx2Bank));
        CurrentSpriteIndex = Math.Min(CurrentSpriteIndex, Math.Max(0, SpriteCount - 1));
        UpdateViewRegion();
    }

    public void SetChipBus(ChipBus bus)
    {
        Document.SetChipBus(bus);
        Palette.PaletteCount = Document.PaletteCount;
        UpdateViewRegion();
    }

    public void SwitchGfxBank(int bank) => GfxBank = bank == 0 ? 0 : 1;

    public int GetFlag(int bit) => Document.GetFlag(CurrentSpriteIndex, bit);
    public int AllFlags => Document.GetCurrentFlags();

    public void SetFlag(int bit, bool value)
    {
        var before = Document.GetCurrentSpritePixels();
        var beforeFlags = Document.GetCurrentFlags();
        Document.SetFlag(CurrentSpriteIndex, bit, value);
        var after = Document.GetCurrentSpritePixels();
        var afterFlags = Document.GetCurrentFlags();
        UndoService.Execute(new SpritePatchCommand(Document, CurrentSpriteIndex, "flag", before, after, beforeFlags, afterFlags));
        NotifyFlagsChanged();
        Edited?.Invoke();
    }

    public void ToggleFlag(int bit) => SetFlag(bit, GetFlag(bit) == 0);

    private void NotifyFlagsChanged()
    {
        for (var i = 0; i < 8; i++)
            OnPropertyChanged($"Flag{i}");
        OnPropertyChanged(nameof(AllFlags));
    }

    public void RequestRefresh() => RefreshRequested?.Invoke();

    public void NotifyEdited() => Edited?.Invoke();

    public void Undo()
    {
        if (UndoService.Undo()) { Edited?.Invoke(); RequestRefresh(); NotifyFlagsChanged(); }
    }

    public void Redo()
    {
        if (UndoService.Redo()) { Edited?.Invoke(); RequestRefresh(); NotifyFlagsChanged(); }
    }

    public ISpriteTool? GetActiveTool() =>
        _tools.TryGetValue(_selectedTool, out var tool) ? tool : _tools["pencil"];

    public void CycleShapeMode()
    {
        _shapeHost.CycleMode();
        OnPropertyChanged(nameof(ShapeMode));
        OnPropertyChanged(nameof(ActiveToolLabel));
        OnPropertyChanged(nameof(StatusText));
    }

    public void OnPointerDown(PointerInput input) => GetActiveTool()?.OnPointerDown(_ctx, input);
    public void OnPointerMove(PointerInput input) => GetActiveTool()?.OnPointerMove(_ctx, input);
    public void OnPointerUp(PointerInput input) => GetActiveTool()?.OnPointerUp(_ctx, input);

    public void UpdateStampPreview(PointerInput input)
    {
        if (SelectedTool != "stamp" || !Clipboard.HasContent) return;
        _tools["stamp"].OnPointerMove(_ctx, input);
    }

    public void ClearStampPreview() => StampTool.ClearPreview(_ctx);

    public void UpdateSelectPreview(PointerInput input)
    {
        if (SelectedTool != "select" || _ctx.SelectAnchor == null) return;
        _ctx.SelectPreviewEnd = (input.LocalX, input.LocalY);
        RequestRefresh();
    }

    public void ClearSelectPreview() => ClearSelectPreview(_ctx);

    public static void ClearSelectPreview(SpriteEditorContext ctx)
    {
        ctx.SelectAnchor = null;
        ctx.SelectPreviewEnd = null;
        ctx.RequestRefresh?.Invoke();
    }

    public static (int x, int y, int w, int h) NormalizeRect((int x, int y) a, (int x, int y) b, int maxX, int maxY)
    {
        var left = Math.Clamp(Math.Min(a.x, b.x), 0, maxX);
        var top = Math.Clamp(Math.Min(a.y, b.y), 0, maxY);
        var right = Math.Clamp(Math.Max(a.x, b.x), 0, maxX);
        var bottom = Math.Clamp(Math.Max(a.y, b.y), 0, maxY);
        return (left, top, right - left + 1, bottom - top + 1);
    }

    private static bool SupportsCursorPreview(string tool) =>
        tool is "pencil" or "eraser" or "fill" or "picker";

    public void UpdateCursorPreview(PointerInput input)
    {
        if (!SupportsCursorPreview(_selectedTool))
        {
            ClearCursorPreview();
            return;
        }

        _ctx.CursorPreviewPosition = (input.LocalX, input.LocalY);
        _ctx.CursorPreviewTool = _selectedTool;
        RequestRefresh();
    }

    public void ClearCursorPreview()
    {
        if (_ctx.CursorPreviewPosition == null) return;
        _ctx.CursorPreviewPosition = null;
        _ctx.CursorPreviewTool = null;
        RequestRefresh();
    }

    public void RefreshStampPreviewModifiers(bool ctrlHeld)
    {
        if (SelectedTool != "stamp" || _ctx.StampPreviewPosition == null) return;
        _ctx.StampPreviewSkipTransparent = ctrlHeld;
        RequestRefresh();
    }

    public void OnPointerDownPan(PointerInput input)
    {
        _tools["pan"].OnPointerDown(_ctx, input);
    }

    public void OnPointerMovePan(PointerInput input)
    {
        if (_ctx.IsPanning)
            _tools["pan"].OnPointerMove(_ctx, input);
    }

    public void OnPointerUpPan(PointerInput input) => _tools["pan"].OnPointerUp(_ctx, input);

    public void HandleEyedropper(PointerInput input)
    {
        var color = Document.GetLocalPixel(input.LocalX, input.LocalY);
        if (input.Shift)
            Palette.SecondaryColor = color;
        else
            Palette.PrimaryColor = color;
    }

    public bool HandleKey(string key, bool ctrl, bool shift, bool alt)
    {
        if (key == "Space")
        {
            _spaceHeld = true;
            return true;
        }

        var action = MapKey(key, ctrl, shift, alt);
        if (action == null) return false;
        return ExecuteAction(action.Value, shift);
    }

    public bool HandleTextInput(string text)
    {
        if (EditorTextShortcuts.TryZoom(
                text,
                () => ExecuteAction(SpriteEditorAction.ZoomIn, shift: false),
                () => ExecuteAction(SpriteEditorAction.ZoomOut, shift: false)))
            return true;

        if (SelectedTool is "pencil" or "eraser" &&
            EditorTextShortcuts.TryBrushSize(
                text,
                () => ExecuteAction(SpriteEditorAction.BrushSmaller, shift: false),
                () => ExecuteAction(SpriteEditorAction.BrushLarger, shift: false)))
            return true;

        return false;
    }

    public void HandleKeyUp(string key)
    {
        if (key == "Space") _spaceHeld = false;
    }

    public bool IsSpaceHeld => _spaceHeld;

    private static SpriteEditorAction? MapKey(string key, bool ctrl, bool shift, bool alt)
    {
        if (ctrl && shift && key == "Delete") return SpriteEditorAction.ClearSprite;
        if (ctrl && key == "Z") return SpriteEditorAction.Undo;
        if (ctrl && key == "Y") return SpriteEditorAction.Redo;
        if (ctrl && key == "C") return SpriteEditorAction.Copy;
        if (ctrl && key == "X") return SpriteEditorAction.Cut;
        if (ctrl && key == "V") return SpriteEditorAction.Paste;
        if (ctrl && key == "G") return SpriteEditorAction.ToggleGrid;
        if (ctrl && key == "H") return SpriteEditorAction.ToggleHexView;
        if (ctrl && key == "Delete") return SpriteEditorAction.ClearSprite;
        if (key == "Q") return SpriteEditorAction.PrevSprite;
        if (key == "W") return SpriteEditorAction.NextSprite;
        if (key == "1") return SpriteEditorAction.PrevColor;
        if (key == "2") return SpriteEditorAction.NextColor;
        if (MapBrushSizeKey(key, ctrl, alt, smaller: true)) return SpriteEditorAction.BrushSmaller;
        if (MapBrushSizeKey(key, ctrl, alt, smaller: false)) return SpriteEditorAction.BrushLarger;
        if (key is "OemPlus" or "Add") return SpriteEditorAction.ZoomIn;
        if (key is "OemMinus" or "Subtract") return SpriteEditorAction.ZoomOut;
        if (key == "Tab") return SpriteEditorAction.ToggleFullscreen;
        if (key == "S" && !ctrl) return SpriteEditorAction.SelectTool;
        if (key == "F") return SpriteEditorAction.FlipH;
        if (key == "V" && !ctrl) return SpriteEditorAction.FlipV;
        if (key == "R") return SpriteEditorAction.Rotate;
        if (key == "Return") return SpriteEditorAction.ClearSelection;
        if (key == "Delete" || key == "Back") return SpriteEditorAction.ClearArea;
        if (key == "X" && !ctrl) return SpriteEditorAction.SwapColors;
        if (key == "B") return SpriteEditorAction.ToolPencil;
        if (key == "G" && !ctrl) return SpriteEditorAction.ToolFill;
        if (key == "E") return SpriteEditorAction.ToolEraser;
        if (key == "I") return SpriteEditorAction.ToolPicker;
        if (key == "T") return SpriteEditorAction.ToolStamp;
        if (key == "L") return SpriteEditorAction.ToolShape;
        if (key == "Left") return shift ? SpriteEditorAction.ShiftLeft : SpriteEditorAction.MoveLeft;
        if (key == "Right") return shift ? SpriteEditorAction.ShiftRight : SpriteEditorAction.MoveRight;
        if (key == "Up") return shift ? SpriteEditorAction.ShiftUp : SpriteEditorAction.MoveUp;
        if (key == "Down") return shift ? SpriteEditorAction.ShiftDown : SpriteEditorAction.MoveDown;
        return null;
    }

    private static bool MapBrushSizeKey(string key, bool ctrl, bool alt, bool smaller)
    {
        // AltGr on ABNT2 sets Ctrl+Alt; allow bracket keys unless only Ctrl is held.
        if (ctrl && !alt) return false;

        return smaller
            ? key is "OemOpenBrackets" or "OemQuotes" or "Apostrophe"
            : key is "OemCloseBrackets" or "OemQuestion" or "Quote";
    }

    private bool ExecuteAction(SpriteEditorAction action, bool shift)
    {
        switch (action)
        {
            case SpriteEditorAction.Undo:
                Undo();
                return true;
            case SpriteEditorAction.Redo:
                Redo();
                return true;
            case SpriteEditorAction.Copy: Copy(); return true;
            case SpriteEditorAction.Cut: Cut(); return true;
            case SpriteEditorAction.Paste: Paste(); return true;
            case SpriteEditorAction.PrevSprite: NavigateSprite(-1); return true;
            case SpriteEditorAction.NextSprite: NavigateSprite(1); return true;
            case SpriteEditorAction.PrevColor:
                if (shift) Palette.PrevColor(secondary: true); else Palette.PrevColor();
                return true;
            case SpriteEditorAction.NextColor:
                if (shift) Palette.NextColor(secondary: true); else Palette.NextColor();
                return true;
            case SpriteEditorAction.ZoomIn: Viewport.AdjustZoom(1); return true;
            case SpriteEditorAction.ZoomOut: Viewport.AdjustZoom(-1); return true;
            case SpriteEditorAction.ToggleFullscreen: Viewport.Fullscreen = !Viewport.Fullscreen; return true;
            case SpriteEditorAction.ToggleGrid: Viewport.GridVisible = !Viewport.GridVisible; return true;
            case SpriteEditorAction.ToggleHexView: Viewport.HexView = !Viewport.HexView; return true;
            case SpriteEditorAction.SelectTool: SelectedTool = "select"; return true;
            case SpriteEditorAction.FlipH: FlipHorizontal(); return true;
            case SpriteEditorAction.FlipV: FlipVertical(); return true;
            case SpriteEditorAction.Rotate: Rotate90(); return true;
            case SpriteEditorAction.ShiftUp: ShiftPixels(0, -1); return true;
            case SpriteEditorAction.ShiftDown: ShiftPixels(0, 1); return true;
            case SpriteEditorAction.ShiftLeft: ShiftPixels(-1, 0); return true;
            case SpriteEditorAction.ShiftRight: ShiftPixels(1, 0); return true;
            case SpriteEditorAction.MoveUp: MoveSelection(0, -1); return true;
            case SpriteEditorAction.MoveDown: MoveSelection(0, 1); return true;
            case SpriteEditorAction.MoveLeft: MoveSelection(-1, 0); return true;
            case SpriteEditorAction.MoveRight: MoveSelection(1, 0); return true;
            case SpriteEditorAction.ClearSelection:
                Selection.Clear();
                ClearSelectPreview(_ctx);
                GetActiveTool()?.OnCancel(_ctx);
                UpdateViewRegion();
                return true;
            case SpriteEditorAction.ClearArea: ClearSelectionArea(); return true;
            case SpriteEditorAction.SwapColors: Palette.Swap(); return true;
            case SpriteEditorAction.ClearSprite: ClearSprite(); return true;
            case SpriteEditorAction.ToolPencil: SelectedTool = "pencil"; return true;
            case SpriteEditorAction.ToolFill: SelectedTool = "fill"; return true;
            case SpriteEditorAction.ToolEraser: SelectedTool = "eraser"; return true;
            case SpriteEditorAction.ToolPicker: SelectedTool = "picker"; return true;
            case SpriteEditorAction.ToolStamp: SelectedTool = "stamp"; return true;
            case SpriteEditorAction.ToolShape:
                if (SelectedTool == "shape") CycleShapeMode();
                else SelectedTool = "shape";
                return true;
            case SpriteEditorAction.BrushSmaller:
                if (SelectedTool is "pencil" or "eraser") AdjustBrushSize(-1);
                return true;
            case SpriteEditorAction.BrushLarger:
                if (SelectedTool is "pencil" or "eraser") AdjustBrushSize(1);
                return true;
        }
        return false;
    }

    public void Copy()
    {
        if (Selection.HasPixelSelection && Selection.PixelBuffer != null)
        {
            Clipboard.CopyPixels(Selection.PixelBuffer, Selection.PixelWidth, Selection.PixelHeight);
            return;
        }

        if (Selection.HasSpriteSelection)
        {
            var sprites = Selection.SpriteIndices.OrderBy(i => i)
                .Select(i => (i, Document.GetSpritePixels(i), Document.GetFlags(i))).ToList();
            Clipboard.CopySprites(sprites, TileSize);
            return;
        }

        Clipboard.CopySprite(Document.GetCurrentSpritePixels(), TileSize, Document.GetCurrentFlags());
    }

    public void Cut()
    {
        Copy();
        if (Selection.HasPixelSelection)
            ClearSelectionArea();
        else if (Selection.HasSpriteSelection)
        {
            foreach (var idx in Selection.SpriteIndices)
                ClearSpriteAt(idx);
        }
        else
            ClearSprite();
    }

    public void Paste()
    {
        if (!Clipboard.HasContent || Clipboard.Pixels == null) return;

        if (Clipboard.IsSpriteLevel && !Selection.HasPixelSelection)
        {
            var before = Document.GetCurrentSpritePixels();
            var beforeFlags = Document.GetCurrentFlags();
            Document.SetCurrentSpritePixels(Clipboard.Pixels);
            Document.SetFlags(CurrentSpriteIndex, Clipboard.Flags);
            var after = Document.GetCurrentSpritePixels();
            UndoService.Execute(new SpritePatchCommand(Document, CurrentSpriteIndex, "paste", before, after, beforeFlags, Clipboard.Flags));
            Edited?.Invoke();
            RequestRefresh();
            NotifyFlagsChanged();
            return;
        }

        var tile = TileSize;
        var before2 = Document.GetCurrentSpritePixels();
        var beforeFlags2 = Document.GetCurrentFlags();
        for (var y = 0; y < Clipboard.Height; y++)
        for (var x = 0; x < Clipboard.Width; x++)
        {
            var color = Clipboard.Pixels[y * Clipboard.Width + x];
            if (x < tile && y < tile)
                Document.SetLocalPixel(x, y, color);
        }
        var after2 = Document.GetCurrentSpritePixels();
        UndoService.Execute(new SpritePatchCommand(Document, CurrentSpriteIndex, "paste", before2, after2, beforeFlags2, beforeFlags2));
        Edited?.Invoke();
        RequestRefresh();
    }

    public void FlipHorizontal()
    {
        if (Selection.HasSpriteSelection)
        {
            var cmd = new MultiSpritePatchCommand(Document, "flip-h");
            foreach (var idx in Selection.SpriteIndices)
            {
                var before = Document.GetSpritePixels(idx);
                var after = PixelDrawing.FlipHorizontal(before, TileSize);
                cmd.AddPatch(idx, before, after, Document.GetFlags(idx), Document.GetFlags(idx));
            }
            UndoService.Execute(cmd);
        }
        else
        {
            var before = Document.GetCurrentSpritePixels();
            var after = PixelDrawing.FlipHorizontal(before, TileSize);
            _ctx.ApplyCurrentSpritePatch("flip-h", after, Document.GetCurrentFlags());
        }
        RequestRefresh();
    }

    public void FlipVertical()
    {
        if (Selection.HasSpriteSelection)
        {
            var cmd = new MultiSpritePatchCommand(Document, "flip-v");
            foreach (var idx in Selection.SpriteIndices)
            {
                var before = Document.GetSpritePixels(idx);
                var after = PixelDrawing.FlipVertical(before, TileSize);
                cmd.AddPatch(idx, before, after, Document.GetFlags(idx), Document.GetFlags(idx));
            }
            UndoService.Execute(cmd);
        }
        else
        {
            var before = Document.GetCurrentSpritePixels();
            var after = PixelDrawing.FlipVertical(before, TileSize);
            _ctx.ApplyCurrentSpritePatch("flip-v", after, Document.GetCurrentFlags());
        }
        RequestRefresh();
    }

    public void Rotate90()
    {
        if (Selection.HasPixelSelection)
        {
            if (Selection.PixelWidth != Selection.PixelHeight) return;
            var w = Selection.PixelWidth;
            var buf = Selection.PixelBuffer ?? Document.ReadLocalRegion(Selection.PixelX, Selection.PixelY, w, w);
            var rotated = PixelDrawing.Rotate90(buf, w);
            var before = Document.GetCurrentSpritePixels();
            var beforeFlags = Document.GetCurrentFlags();
            Document.WriteLocalRegion(Selection.PixelX, Selection.PixelY, w, w, rotated);
            var after = Document.GetCurrentSpritePixels();
            UndoService.Execute(new SpritePatchCommand(Document, CurrentSpriteIndex, "rotate", before, after, beforeFlags, beforeFlags));
            Selection.SetPixelRect(Selection.PixelX, Selection.PixelY, Selection.PixelX + w - 1, Selection.PixelY + w - 1, rotated);
        }
        else
        {
            var before = Document.GetCurrentSpritePixels();
            var after = PixelDrawing.Rotate90(before, TileSize);
            _ctx.ApplyCurrentSpritePatch("rotate", after, Document.GetCurrentFlags());
        }
        RequestRefresh();
    }

    public void ShiftPixels(int dx, int dy)
    {
        if (Selection.HasPixelSelection)
        {
            var w = Selection.PixelWidth;
            var h = Selection.PixelHeight;
            var buf = Document.ReadLocalRegion(Selection.PixelX, Selection.PixelY, w, h);
            var shifted = PixelDrawing.ShiftRegion(buf, w, h, dx, dy);
            var before = Document.GetCurrentSpritePixels();
            var beforeFlags = Document.GetCurrentFlags();
            Document.WriteLocalRegion(Selection.PixelX, Selection.PixelY, w, h, shifted);
            var after = Document.GetCurrentSpritePixels();
            UndoService.Execute(new SpritePatchCommand(Document, CurrentSpriteIndex, "shift", before, after, beforeFlags, beforeFlags));
            Selection.PixelBuffer = shifted;
        }
        else
        {
            var before = Document.GetCurrentSpritePixels();
            var after = PixelDrawing.ShiftCircular(before, TileSize, dx, dy);
            _ctx.ApplyCurrentSpritePatch("shift", after, Document.GetCurrentFlags());
        }
        RequestRefresh();
    }

    public void MoveSelection(int dx, int dy)
    {
        if (!Selection.HasPixelSelection || Selection.PixelBuffer == null) return;
        Selection.SetPixelRect(
            Selection.PixelX + dx, Selection.PixelY + dy,
            Selection.PixelX + dx + Selection.PixelWidth - 1,
            Selection.PixelY + dy + Selection.PixelHeight - 1,
            Selection.PixelBuffer);
        RequestRefresh();
    }

    public void ClearSelectionArea()
    {
        if (Selection.HasPixelSelection)
        {
            var before = Document.GetCurrentSpritePixels();
            var beforeFlags = Document.GetCurrentFlags();
            var clear = new byte[Selection.PixelWidth * Selection.PixelHeight];
            Document.WriteLocalRegion(Selection.PixelX, Selection.PixelY, Selection.PixelWidth, Selection.PixelHeight, clear);
            var after = Document.GetCurrentSpritePixels();
            UndoService.Execute(new SpritePatchCommand(Document, CurrentSpriteIndex, "clear", before, after, beforeFlags, beforeFlags));
            Edited?.Invoke();
            RequestRefresh();
            return;
        }
        ClearSprite();
    }

    public void ClearSprite() => ClearSpriteAt(CurrentSpriteIndex);

    public void ClearSpriteAt(int spriteIndex)
    {
        var clear = new byte[TileSize * TileSize];
        _ctx.ApplySpritePatch("clear", spriteIndex, clear, 0);
        RequestRefresh();
    }

    public void DuplicateSprites()
    {
        var indices = Selection.HasSpriteSelection
            ? Selection.SpriteIndices.OrderBy(i => i).ToList()
            : [CurrentSpriteIndex];

        var cmd = new MultiSpritePatchCommand(Document, "duplicate");
        var dest = Document.FindNextFreeSpriteIndex(indices[^1]);
        if (dest < 0) return;

        foreach (var src in indices)
        {
            if (dest < 0 || dest >= SpriteCount) break;
            var pixels = Document.GetSpritePixels(src);
            var flags = Document.GetFlags(src);
            var empty = new byte[TileSize * TileSize];
            cmd.AddPatch(dest, empty, pixels, 0, flags);
            dest = Document.FindNextFreeSpriteIndex(dest);
        }
        UndoService.Execute(cmd);
        Edited?.Invoke();
        RequestRefresh();
    }

    public void DeleteSprites()
    {
        var indices = Selection.HasSpriteSelection
            ? Selection.SpriteIndices.ToList()
            : [CurrentSpriteIndex];
        var cmd = new MultiSpritePatchCommand(Document, "delete");
        foreach (var idx in indices)
        {
            var before = Document.GetSpritePixels(idx);
            var clear = new byte[TileSize * TileSize];
            cmd.AddPatch(idx, before, clear, Document.GetFlags(idx), 0);
        }
        UndoService.Execute(cmd);
        Edited?.Invoke();
        RequestRefresh();
    }

    public void SelectSpriteAtSheet(int sheetX, int sheetY, bool shift, bool ctrl)
    {
        var col = sheetX / TileSize;
        var row = sheetY / TileSize;
        var index = row * SpritesPerRow + col;
        if (index < 0 || index >= SpriteCount) return;

        if (ctrl)
        {
            Selection.SetSprites(Enumerable.Range(0, SpriteCount));
            UpdateViewRegion();
            RequestRefresh();
            return;
        }

        if (shift)
        {
            AddSpriteToSelection(index);
            return;
        }
        else
        {
            Selection.Clear();
            CurrentSpriteIndex = index;
            UpdateViewRegion();
        }
        RequestRefresh();
    }

    public int GetPixel(int localX, int localY) => Document.GetLocalPixel(localX, localY);
    public int ReadSheetPixel(int sheetX, int sheetY) => Document.GetPixel(sheetX, sheetY);
    public (int sheetX, int sheetY) SpriteOrigin => Document.SpriteOrigin;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
