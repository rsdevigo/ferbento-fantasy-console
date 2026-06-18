using Ferbento.Core.Chips;
using Ferbento.Editor.Tilemaps;
using Ferbento.Editor.Tilemaps.Input;
using Ferbento.Profiles.BuiltIn;
using Ferbento.Profiles.Validation;

namespace Ferbento.Editor.Tests;

public class TilemapEditorTests
{
    [Fact]
    public void ProfileFeatureGate_Pico8_UsesSharedMapRows()
    {
        var gate = new ProfileFeatureGate(BuiltInProfiles.Pico8());
        Assert.True(gate.UsesSharedMapRows);
        Assert.Equal(4, gate.SpriteBankCount);
    }

    [Fact]
    public void ProfileFeatureGate_GameBoy_LinearMap()
    {
        var gate = new ProfileFeatureGate(BuiltInProfiles.GameBoy());
        Assert.False(gate.UsesSharedMapRows);
        Assert.Equal(4, gate.SpriteBankCount);
    }

    [Fact]
    public void MapDocument_ReadWriteRegion()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        var doc = new MapDocument();
        doc.SetProfile(BuiltInProfiles.Pico8());
        doc.SetChipBus(bus);
        doc.SetTile(0, 0, 5, 0);
        doc.SetTile(1, 0, 7, 0);
        var region = doc.ReadRegion(0, 0, 2, 1);
        Assert.Equal(5, region[0]);
        Assert.Equal(7, region[1]);
    }

    [Fact]
    public void TilemapEditorController_NavigateTile_QW()
    {
        var ctrl = new TilemapEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());
        ctrl.SetChipBus(new ChipBus(BuiltInProfiles.Pico8()));
        ctrl.SelectedTile = 10;
        ctrl.NavigateTile(1);
        Assert.Equal(11, ctrl.SelectedTile);
        ctrl.NavigateTile(-1);
        Assert.Equal(10, ctrl.SelectedTile);
    }

    [Fact]
    public void TilemapEditorInputMap_HasPaintBinding()
    {
        Assert.Equal("B", TilemapEditorInputMap.DefaultBindings[TilemapEditorAction.ToolPencil]);
        Assert.Equal("G", TilemapEditorInputMap.DefaultBindings[TilemapEditorAction.ToolFill]);
        Assert.Equal("E", TilemapEditorInputMap.DefaultBindings[TilemapEditorAction.ToolEraser]);
    }

    [Fact]
    public void TilemapEditorController_HandleTextInput_ZoomPlusMinus()
    {
        var ctrl = new TilemapEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());
        ctrl.SetChipBus(new ChipBus(BuiltInProfiles.Pico8()));
        ctrl.SetPanelSize(512, 384);
        var initial = ctrl.Viewport.Zoom;

        Assert.True(ctrl.HandleTextInput("+"));
        Assert.Equal(initial + 1, ctrl.Viewport.Zoom);

        Assert.True(ctrl.HandleTextInput("-"));
        Assert.Equal(initial, ctrl.Viewport.Zoom);
    }

    [Fact]
    public void Pico8_MGet_Row32_MatchesSharedRegion()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        bus.Tilemap.MSet(3, 32, 99);
        Assert.Equal(99, bus.Tilemap.MGet(3, 32));
    }

    [Fact]
    public void MapDocument_SetTileZero_MarksPlacedWithoutLayerFlag()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        var doc = new MapDocument();
        doc.SetProfile(BuiltInProfiles.Pico8());
        doc.SetChipBus(bus);

        doc.SetTile(10, 5, 0, 0);

        Assert.True(doc.IsCellPlaced(10, 5));
        Assert.Equal(0, bus.Sprite.FGet(0, 0));
    }

    [Fact]
    public void EraserTool_SingleStroke_UndoRestoresTile()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        var ctrl = new TilemapEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());
        ctrl.SetChipBus(bus);
        bus.Tilemap.MSet(10, 5, 7);
        ctrl.Document.SyncPlacedFromTiles(10, 5, 1, 1, [7]);

        ctrl.SelectedTool = "eraser";
        ctrl.HandlePointerDown(10, 5, false, false, false);
        ctrl.HandlePointerUp(10, 5, false, false);

        Assert.Equal(0, bus.Tilemap.MGet(10, 5));
        Assert.False(ctrl.Document.IsCellPlaced(10, 5));
        Assert.True(ctrl.CanUndo);

        ctrl.HandleKey("Z", true, false, false);
        Assert.Equal(7, bus.Tilemap.MGet(10, 5));
        Assert.True(ctrl.Document.IsCellPlaced(10, 5));
    }

    [Fact]
    public void PaintTool_SingleStroke_UndoRestoresTile()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        var ctrl = new TilemapEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());
        ctrl.SetChipBus(bus);
        ctrl.SelectedTool = "pencil";
        ctrl.SelectedTile = 5;

        ctrl.HandlePointerDown(10, 5, false, false, false);
        ctrl.HandlePointerUp(10, 5, false, false);

        Assert.Equal(5, bus.Tilemap.MGet(10, 5));
        Assert.True(ctrl.CanUndo);

        ctrl.HandleKey("Z", true, false, false);
        Assert.Equal(0, bus.Tilemap.MGet(10, 5));
        Assert.True(ctrl.CanRedo);

        ctrl.HandleKey("Y", true, false, false);
        Assert.Equal(5, bus.Tilemap.MGet(10, 5));
    }

    [Fact]
    public void TileStampLayout_MultiSelect_UsesSheetGrid()
    {
        var placements = TileStampLayout.GetPlacements(17, [0, 1, 16, 17], 16);
        Assert.Equal(4, placements.Count);
        Assert.Contains(placements, p => p is { Dx: 0, Dy: 0, TileId: 0 });
        Assert.Contains(placements, p => p is { Dx: 1, Dy: 0, TileId: 1 });
        Assert.Contains(placements, p => p is { Dx: 0, Dy: 1, TileId: 16 });
        Assert.Contains(placements, p => p is { Dx: 1, Dy: 1, TileId: 17 });
    }

    [Fact]
    public void PaintTool_MultiSpriteSelection_PaintsStampLayout()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        var ctrl = new TilemapEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());
        ctrl.SetChipBus(bus);
        ctrl.SelectedTool = "pencil";
        ctrl.Selection.SetNavigatorSprites([0, 1, 16, 17]);
        ctrl.SelectedTile = 17;

        ctrl.HandlePointerDown(10, 10, false, false, false);
        ctrl.HandlePointerUp(10, 10, false, false);

        Assert.Equal(0, bus.Tilemap.MGet(10, 10));
        Assert.Equal(1, bus.Tilemap.MGet(11, 10));
        Assert.Equal(16, bus.Tilemap.MGet(10, 11));
        Assert.Equal(17, bus.Tilemap.MGet(11, 11));
    }

    [Fact]
    public void FillTool_FloodFill_ChangesMap()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        var ctrl = new TilemapEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());
        ctrl.SetChipBus(bus);
        ctrl.SelectedTool = "fill";
        ctrl.SelectedTile = 3;

        ctrl.HandlePointerDown(0, 0, false, false, false);

        Assert.Equal(3, bus.Tilemap.MGet(0, 0));
        Assert.Equal(3, bus.Tilemap.MGet(10, 10));
    }

    [Fact]
    public void FillTool_FloodFillTileZero_MarksEmptyRegionPlaced()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        var ctrl = new TilemapEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());
        ctrl.SetChipBus(bus);
        ctrl.SelectedTool = "fill";
        ctrl.SelectedTile = 0;

        ctrl.HandlePointerDown(0, 0, false, false, false);

        Assert.Equal(0, bus.Tilemap.MGet(0, 0));
        Assert.Equal(0, bus.Tilemap.MGet(10, 10));
        Assert.True(ctrl.Document.IsCellPlaced(0, 0));
        Assert.True(ctrl.Document.IsCellPlaced(10, 10));
        Assert.True(ctrl.CanUndo);

        ctrl.HandleKey("Z", true, false, false);
        Assert.False(ctrl.Document.IsCellPlaced(0, 0));
        Assert.False(ctrl.Document.IsCellPlaced(10, 10));
    }

    [Fact]
    public void CopySelection_SetsHasClipboard()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        var ctrl = new TilemapEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());
        ctrl.SetChipBus(bus);
        ctrl.Selection.SetMapRect(0, 0, 2, 2);
        bus.Tilemap.MSet(0, 0, 5);
        bus.Tilemap.MSet(1, 0, 6);
        ctrl.Document.SyncPlacedFromTiles(0, 0, 2, 1, [5, 6]);

        Assert.False(ctrl.HasClipboard);
        ctrl.CopySelection();
        Assert.True(ctrl.HasClipboard);
        Assert.Equal(2, ctrl.Clipboard.Width);
        Assert.Equal(2, ctrl.Clipboard.Height);
        Assert.Equal(5, ctrl.Clipboard.Data![0]);
        Assert.True(ctrl.Clipboard.Placed![0]);
    }

    [Fact]
    public void StampTool_PastesClipboardWithPlacedState()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        var ctrl = new TilemapEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());
        ctrl.SetChipBus(bus);
        bus.Tilemap.MSet(0, 0, 5);
        bus.Tilemap.MSet(1, 0, 6);
        ctrl.Document.SyncPlacedFromTiles(0, 0, 2, 1, [5, 6]);
        ctrl.Selection.SetMapRect(0, 0, 2, 1);
        ctrl.CopySelection();

        ctrl.SelectedTool = "stamp";
        ctrl.HandlePointerDown(10, 10, false, false, false);

        Assert.Equal(5, bus.Tilemap.MGet(10, 10));
        Assert.Equal(6, bus.Tilemap.MGet(11, 10));
        Assert.True(ctrl.Document.IsCellPlaced(10, 10));
        Assert.True(ctrl.Document.IsCellPlaced(11, 10));
    }

    [Fact]
    public void CopyNavigatorSelection_BuildsStampLayout()
    {
        var ctrl = new TilemapEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());
        ctrl.SetChipBus(new ChipBus(BuiltInProfiles.Pico8()));
        ctrl.Selection.SetNavigatorSprites([0, 1, 16, 17]);
        ctrl.SelectedTile = 17;

        ctrl.CopySelection();

        Assert.True(ctrl.HasClipboard);
        Assert.Equal(2, ctrl.Clipboard.Width);
        Assert.Equal(2, ctrl.Clipboard.Height);
        Assert.Equal(0, ctrl.Clipboard.Data![0]);
        Assert.Equal(1, ctrl.Clipboard.Data![1]);
        Assert.Equal(16, ctrl.Clipboard.Data![2]);
        Assert.Equal(17, ctrl.Clipboard.Data![3]);
        Assert.All(ctrl.Clipboard.Placed!, Assert.True);
    }

    [Fact]
    public void ToolStamp_RequiresClipboard()
    {
        var ctrl = new TilemapEditorController();
        ctrl.SetProfile(BuiltInProfiles.Pico8());
        ctrl.SetChipBus(new ChipBus(BuiltInProfiles.Pico8()));

        Assert.False(ctrl.HandleKey("T", false, false, false));
        Assert.NotEqual("stamp", ctrl.SelectedTool);
    }
}
