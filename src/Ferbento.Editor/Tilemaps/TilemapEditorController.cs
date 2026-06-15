using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ferbento.Core.Chips;
using Ferbento.Editor.Chrome;
using Ferbento.Editor.Sprites;
using Ferbento.Editor.Sprites.Undo;
using Ferbento.Editor.Tilemaps.Input;
using Ferbento.Editor.Tilemaps.Tools;
using Ferbento.Editor.Tilemaps.Undo;
using Ferbento.Profiles.Models;
using Ferbento.Profiles.Validation;

namespace Ferbento.Editor.Tilemaps;

public sealed class TilemapEditorController : INotifyPropertyChanged
{
    private readonly Dictionary<string, ITilemapTool> _tools;
    private readonly ShapeToolHost _shapeHost = new();
    private readonly TilemapEditorContext _ctx;
    private HardwareProfile _profile = HardwareProfile.Pico8();
    private string _selectedTool = "pencil";
    private int _selectedTile = 1;
    private int _currentLayer;
    private int _spriteBank;
    private int _hoverMapX = -1;
    private int _hoverMapY = -1;
    private bool _spaceHeld;
    private int _selectAllStage;
    private string _crossTabHint = "";
    private double _panelWidth = 512;
    private double _panelHeight = 384;

    public MapDocument Document { get; } = new();
    public MapSelection Selection { get; } = new();
    public ViewportState Viewport { get; } = new();
    public UndoService UndoService { get; } = new(256);
    public TilemapClipboard Clipboard { get; } = new();
    public ProfileFeatureGate Features { get; private set; }

    public event Action? Edited;
    public event Action? RefreshRequested;

    public bool CanUndo => UndoService.UndoCount > 0;
    public bool CanRedo => UndoService.RedoCount > 0;

    public int MapWidth => Document.MapWidth;
    public int MapHeight => Document.MapHeight;
    public int TileSize => Document.TileSize;
    public int SpriteCount => Document.SpriteCount;
    public int SpritesPerRow => Document.SpritesPerRow;
    public int SpriteBankCount => Features.SpriteBankCount;
    public ColorChip? ColorChip => Document.ColorChip;

    public string SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (_selectedTool == value) return;
            _ctx.CancelStroke();
            _selectedTool = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public int SelectedTile
    {
        get => _selectedTile;
        set
        {
            _selectedTile = Math.Clamp(value, 0, Math.Max(0, SpriteCount - 1));
            OnPropertyChanged();
            OnPropertyChanged(nameof(TileLabel));
            OnPropertyChanged(nameof(StatusText));
            RequestRefresh();
        }
    }

    public int CurrentLayer
    {
        get => _currentLayer;
        set { _currentLayer = Math.Clamp(value, 0, 7); OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); RequestRefresh(); }
    }

    public int SpriteBank
    {
        get => _spriteBank;
        set
        {
            _spriteBank = Math.Clamp(value, 0, Math.Max(0, SpriteBankCount - 1));
            OnPropertyChanged();
            RequestRefresh();
        }
    }

    public int BankStartIndex => _spriteBank * 64;
    public int BankEndIndex => Math.Min(BankStartIndex + 63, SpriteCount - 1);

    public string TileLabel => $"Tile #{_selectedTile:D3}  Layer {_currentLayer}  Bank {_spriteBank}";

    public string StatusText
    {
        get
        {
            var coords = _hoverMapX >= 0 ? $"X:{_hoverMapX:D3} Y:{_hoverMapY:D3}" : "X:--- Y:---";
            var hint = string.IsNullOrEmpty(_crossTabHint) ? "" : $"  {_crossTabHint}";
            return $"{coords}  Z:{Viewport.ZoomPercent}%  tool:{SelectedTool}  {TileLabel}{hint}";
        }
    }

    public string CrossTabHint
    {
        get => _crossTabHint;
        set { _crossTabHint = value; OnPropertyChanged(nameof(StatusText)); }
    }

    public int HoverMapX => _hoverMapX;
    public int HoverMapY => _hoverMapY;
    public bool HasHoverTile => _hoverMapX >= 0 && _hoverMapY >= 0;

    public TilemapEditorController()
    {
        _tools = new Dictionary<string, ITilemapTool>
        {
            ["pencil"] = new PaintTool(),
            ["fill"] = new FillTool(),
            ["select"] = new SelectionTool(),
            ["stamp"] = new StampTool(),
            ["pan"] = new PanTool(),
            ["shape"] = _shapeHost,
        };

        _ctx = new TilemapEditorContext
        {
            Document = Document,
            Selection = Selection,
            Viewport = Viewport,
            Undo = UndoService,
            Clipboard = Clipboard,
            NotifyEdited = () => { Edited?.Invoke(); OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); },
        };

        UndoService.StateChanged += () =>
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        };

        Features = new ProfileFeatureGate(_profile);
    }

    public void SetProfile(HardwareProfile profile)
    {
        _profile = profile;
        Document.SetProfile(profile);
        Features = new ProfileFeatureGate(profile);
        SelectedTile = Math.Min(SelectedTile, Math.Max(0, SpriteCount - 1));
        OnPropertyChanged(nameof(MapWidth));
        OnPropertyChanged(nameof(MapHeight));
        OnPropertyChanged(nameof(TileSize));
        OnPropertyChanged(nameof(SpriteCount));
        OnPropertyChanged(nameof(SpritesPerRow));
        OnPropertyChanged(nameof(SpriteBankCount));
    }

    public void SetChipBus(ChipBus bus) => Document.SetChipBus(bus);

    public void SetPanelSize(double width, double height)
    {
        _panelWidth = Math.Max(1, width);
        _panelHeight = Math.Max(1, height);
        ApplyFitZoom();
    }

    public int ComputeFitZoom()
    {
        var tile = TileSize;
        var mapPxW = MapWidth * tile;
        var mapPxH = MapHeight * tile;
        if (mapPxW <= 0 || mapPxH <= 0) return 1;
        var zx = (int)(_panelWidth / mapPxW * 16);
        var zy = (int)(_panelHeight / mapPxH * 16);
        return Math.Max(1, Math.Min(Math.Min(zx, zy), Viewport.MaxZoom));
    }

    public void ApplyFitZoom()
    {
        Viewport.Zoom = ComputeFitZoom();
        Viewport.PanX = 0;
        Viewport.PanY = 0;
    }

    public void SetHoverTile(int mapX, int mapY)
    {
        if (_hoverMapX == mapX && _hoverMapY == mapY) return;
        _hoverMapX = mapX;
        _hoverMapY = mapY;
        OnPropertyChanged(nameof(StatusText));
        RequestRefresh();
    }

    public void ClearHoverTile()
    {
        if (_hoverMapX < 0 && _hoverMapY < 0) return;
        _hoverMapX = _hoverMapY = -1;
        OnPropertyChanged(nameof(StatusText));
        RequestRefresh();
    }

    public void NavigateTile(int delta)
    {
        SelectedTile = Math.Clamp(SelectedTile + delta, BankStartIndex, BankEndIndex);
    }

    public void SelectTileFromNavigator(int tileIndex, bool shift)
    {
        if (shift && Selection.NavigatorSprites.Count > 0)
        {
            Selection.NavigatorSprites.Add(tileIndex);
            SelectedTile = tileIndex;
        }
        else if (shift)
        {
            Selection.NavigatorSprites.Add(SelectedTile);
            Selection.NavigatorSprites.Add(tileIndex);
            SelectedTile = tileIndex;
        }
        else
        {
            Selection.ClearNavigator();
            SelectedTile = tileIndex;
        }
        OnPropertyChanged(nameof(StatusText));
        RequestRefresh();
    }

    public void SelectTilesInNavigatorRect(int col0, int row0, int col1, int row1)
    {
        var left = Math.Min(col0, col1);
        var right = Math.Max(col0, col1);
        var top = Math.Min(row0, row1);
        var bottom = Math.Max(row0, row1);
        var indices = new List<int>();
        for (var row = top; row <= bottom; row++)
        for (var col = left; col <= right; col++)
        {
            var idx = BankStartIndex + row * 8 + col;
            if (idx <= BankEndIndex)
                indices.Add(idx);
        }
        if (indices.Count > 0)
        {
            Selection.SetNavigatorSprites(indices);
            SelectedTile = indices[^1];
        }
        RequestRefresh();
    }

    public void HandlePointerDown(int tileX, int tileY, bool ctrl, bool shift, bool right)
    {
        SyncContext();
        var tool = EffectiveTool();
        var input = new TilePointerInput { TileX = tileX, TileY = tileY, IsStart = true, Ctrl = ctrl, Shift = shift, SpaceHeld = _spaceHeld };
        _tools[tool].OnPointerDown(_ctx, input);
        RequestRefresh();
    }

    public void HandlePointerMove(int tileX, int tileY, bool ctrl, bool shift)
    {
        SyncContext();
        SetHoverTile(tileX, tileY);
        var tool = EffectiveTool();
        var input = new TilePointerInput { TileX = tileX, TileY = tileY, IsStart = false, Ctrl = ctrl, Shift = shift, SpaceHeld = _spaceHeld };
        _tools[tool].OnPointerMove(_ctx, input);
        RequestRefresh();
    }

    public void HandlePointerUp(int tileX, int tileY, bool ctrl, bool shift)
    {
        SyncContext();
        var tool = EffectiveTool();
        var input = new TilePointerInput { TileX = tileX, TileY = tileY, IsStart = false, Ctrl = ctrl, Shift = shift, SpaceHeld = _spaceHeld };
        _tools[tool].OnPointerUp(_ctx, input);
        RequestRefresh();
    }

    public void PanByPixels(int dx, int dy)
    {
        Viewport.PanX += dx;
        Viewport.PanY += dy;
    }

    public void SetSpaceHeld(bool held) => _spaceHeld = held;

    private string EffectiveTool() =>
        _spaceHeld ? "pan" : (_tools.ContainsKey(_selectedTool) ? _selectedTool : "pencil");

    private void SyncContext()
    {
        _ctx.SelectedTile = SelectedTile;
        _ctx.CurrentLayer = CurrentLayer;
    }

    public bool HandleKey(string key, bool ctrl, bool shift, bool alt)
    {
        var action = MapKey(key, ctrl, shift, alt);
        if (action is null) return false;
        return ExecuteAction(action.Value);
    }

    public bool HandleTextInput(string text)
    {
        if (EditorTextShortcuts.TryZoom(
                text,
                () => ExecuteAction(TilemapEditorAction.ZoomIn),
                () => ExecuteAction(TilemapEditorAction.ZoomOut)))
            return true;

        return text switch
        {
            "<" => ExecuteAction(TilemapEditorAction.ZoomOut),
            ">" => ExecuteAction(TilemapEditorAction.ZoomIn),
            _ => false,
        };
    }

    private TilemapEditorAction? MapKey(string key, bool ctrl, bool shift, bool alt)
    {
        if (ctrl && key == "Z") return TilemapEditorAction.Undo;
        if (ctrl && key == "Y") return TilemapEditorAction.Redo;
        if (ctrl && key == "C") return TilemapEditorAction.Copy;
        if (ctrl && key == "X") return TilemapEditorAction.Cut;
        if (ctrl && key == "V") return TilemapEditorAction.Paste;
        if (ctrl && key == "A") return TilemapEditorAction.SelectAllMap;
        if (ctrl && key == "H") return TilemapEditorAction.ToggleHexView;
        if (key == "Q") return TilemapEditorAction.PrevTile;
        if (key == "W") return TilemapEditorAction.NextTile;
        if (key == "OemPlus" || key == "Add") return TilemapEditorAction.ZoomIn;
        if (key == "OemMinus" || key == "Subtract") return TilemapEditorAction.ZoomOut;
        if (key == "Return") return TilemapEditorAction.ClearSelection;
        if (key == "S" || (shift && key == "LeftShift")) return TilemapEditorAction.SelectTool;
        if (key == "B") return TilemapEditorAction.ToolPencil;
        if (key == "G") return TilemapEditorAction.ToolFill;
        if (key == "T") return TilemapEditorAction.ToolStamp;
        if (key == "L") return TilemapEditorAction.ToolShape;
        if (key == "Space") return TilemapEditorAction.ToolPan;
        return null;
    }

    private bool ExecuteAction(TilemapEditorAction action)
    {
        switch (action)
        {
            case TilemapEditorAction.Undo:
                if (UndoService.Undo()) { Edited?.Invoke(); RequestRefresh(); }
                return true;
            case TilemapEditorAction.Redo:
                if (UndoService.Redo()) { Edited?.Invoke(); RequestRefresh(); }
                return true;
            case TilemapEditorAction.Copy:
                CopySelection();
                return true;
            case TilemapEditorAction.Cut:
                CutSelection();
                return true;
            case TilemapEditorAction.Paste:
                SelectedTool = "stamp";
                return true;
            case TilemapEditorAction.PrevTile:
                NavigateTile(-1);
                return true;
            case TilemapEditorAction.NextTile:
                NavigateTile(1);
                return true;
            case TilemapEditorAction.ZoomIn:
                Viewport.AdjustZoom(1);
                return true;
            case TilemapEditorAction.ZoomOut:
                Viewport.AdjustZoom(-1);
                return true;
            case TilemapEditorAction.ToggleHexView:
                Viewport.HexView = !Viewport.HexView;
                RequestRefresh();
                return true;
            case TilemapEditorAction.ClearSelection:
                Selection.ClearAll();
                RequestRefresh();
                return true;
            case TilemapEditorAction.SelectAllMap:
                SelectAllMapToggle();
                return true;
            case TilemapEditorAction.SelectTool:
                SelectedTool = "select";
                return true;
            case TilemapEditorAction.ToolPencil:
                SelectedTool = "pencil";
                return true;
            case TilemapEditorAction.ToolFill:
                SelectedTool = "fill";
                return true;
            case TilemapEditorAction.ToolStamp:
                SelectedTool = "stamp";
                return true;
            case TilemapEditorAction.ToolPan:
                SelectedTool = "pan";
                return true;
            case TilemapEditorAction.ToolShape:
                if (SelectedTool == "shape") _shapeHost.CycleMode();
                else SelectedTool = "shape";
                return true;
        }
        return false;
    }

    private void SelectAllMapToggle()
    {
        if (Features.UsesSharedMapRows)
        {
            if (_selectAllStage == 0)
            {
                Selection.SetMapRect(0, 0, MapWidth, Features.MapPrimaryRows);
                _selectAllStage = 1;
            }
            else
            {
                Selection.SetMapRect(0, 0, MapWidth, MapHeight);
                _selectAllStage = 0;
            }
        }
        else
        {
            Selection.SetMapRect(0, 0, MapWidth, MapHeight);
        }
        RequestRefresh();
    }

    public void CopySelection()
    {
        if (Selection.HasMapRect && Selection.MapRect is { } r)
        {
            var data = Document.ReadRegion(r.x, r.y, r.w, r.h);
            Clipboard.Set(data, r.w, r.h);
            return;
        }
        CrossTabHint = "Select map region first, or use navigator + Ctrl+X for sprites";
    }

    public void CutSelection()
    {
        CopySelection();
        if (!Selection.HasMapRect || Selection.MapRect is not { } r) return;
        var before = Document.ReadRegion(r.x, r.y, r.w, r.h);
        var after = new byte[before.Length];
        UndoService.Execute(new MapRegionCommand(Document, r.x, r.y, r.w, r.h, before, after, CurrentLayer));
        Edited?.Invoke();
        RequestRefresh();
    }

    public void NotifyEdited() => Edited?.Invoke();

    public void RequestRefresh() => RefreshRequested?.Invoke();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
