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
}
