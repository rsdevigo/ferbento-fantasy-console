using Ferbento.Core.Chips;
using Ferbento.Editor.Sprites;
using Ferbento.Editor.Sprites.Input;
using Ferbento.Editor.Sprites.Tools;
using Ferbento.Editor.Sprites.Undo;
using Ferbento.Profiles.BuiltIn;

namespace Ferbento.Editor.Tests;

public class SpriteEditorUxTests
{
    private static SpriteEditorContext CreateContext(int brushSize = 1)
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        var doc = new SpriteDocument();
        doc.SetProfile(BuiltInProfiles.Pico8());
        doc.SetChipBus(bus);

        return new SpriteEditorContext
        {
            Document = doc,
            Palette = new PaletteState { PrimaryColor = 8 },
            Selection = new Selection(),
            Viewport = new ViewportState(),
            Undo = new UndoService(256),
            Clipboard = new SpriteClipboard(),
            BrushSize = brushSize,
        };
    }

    [Fact]
    public void ViewportState_MaxZoom64()
    {
        var viewport = new ViewportState { Zoom = 100 };
        Assert.Equal(64, viewport.Zoom);
        Assert.Equal(400, viewport.ZoomPercent);
    }

    [Fact]
    public void ViewportState_ZoomClampsToMaxZoom()
    {
        var viewport = new ViewportState { MaxZoom = 8, Zoom = 16 };
        Assert.Equal(8, viewport.Zoom);
    }

    [Fact]
    public void HandleKey_BrushSizeBrackets()
    {
        var controller = new SpriteEditorController();
        controller.HandleKey("B", false, false, false);
        controller.AdjustBrushSize(1);
        Assert.Equal(2, controller.BrushSize);

        Assert.True(controller.HandleKey("OemOpenBrackets", false, false, false));
        Assert.Equal(1, controller.BrushSize);

        Assert.True(controller.HandleKey("OemCloseBrackets", false, false, false));
        Assert.Equal(2, controller.BrushSize);
    }

    [Fact]
    public void HandleKey_BrushSmaller_AllowsAltGrModifiers()
    {
        var controller = new SpriteEditorController();
        controller.HandleKey("B", false, false, false);
        controller.AdjustBrushSize(1);
        Assert.Equal(2, controller.BrushSize);

        Assert.True(controller.HandleKey("OemQuotes", ctrl: true, shift: false, alt: true));
        Assert.Equal(1, controller.BrushSize);
    }

    [Fact]
    public void HandleTextInput_BrushSizeBrackets()
    {
        var controller = new SpriteEditorController();
        controller.HandleKey("B", false, false, false);
        controller.AdjustBrushSize(1);
        Assert.Equal(2, controller.BrushSize);

        Assert.True(controller.HandleTextInput("["));
        Assert.Equal(1, controller.BrushSize);

        Assert.True(controller.HandleTextInput("]"));
        Assert.Equal(2, controller.BrushSize);
    }

    [Fact]
    public void HandleKey_ZoomPlusMinus()
    {
        var controller = new SpriteEditorController();
        controller.Viewport.Zoom = 8;
        controller.Viewport.MaxZoom = 32;

        Assert.True(controller.HandleKey("OemPlus", false, false, false));
        Assert.Equal(9, controller.Viewport.Zoom);

        Assert.True(controller.HandleKey("OemMinus", false, false, false));
        Assert.Equal(8, controller.Viewport.Zoom);
    }

    [Fact]
    public void HandleTextInput_ZoomPlusMinus()
    {
        var controller = new SpriteEditorController();
        controller.Viewport.Zoom = 8;
        controller.Viewport.MaxZoom = 32;

        Assert.True(controller.HandleTextInput("+"));
        Assert.Equal(9, controller.Viewport.Zoom);

        Assert.True(controller.HandleTextInput("-"));
        Assert.Equal(8, controller.Viewport.Zoom);
    }

    [Fact]
    public void ComputeFitZoom_UsesPanelAndViewSize()
    {
        var controller = new SpriteEditorController();
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        controller.SetChipBus(bus);
        controller.SetCanvasPanelSize(128, 128);
        controller.UpdateViewRegion();

        Assert.Equal(16, controller.ComputeFitZoom());
    }

    [Fact]
    public void NavigateSprite_UsesLastTileInMultiSelection()
    {
        var controller = new SpriteEditorController();
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        controller.SetChipBus(bus);
        controller.SelectSpritesInSheetRect(0, 0, 15, 7);

        controller.NavigateSprite(1);

        Assert.False(controller.Selection.HasSpriteSelection);
        Assert.Equal(2, controller.CurrentSpriteIndex);
        Assert.Equal(8, controller.ViewWidth);
    }

    [Fact]
    public void SpriteEditorInputMap_BrushBindings()
    {
        Assert.Equal("B", SpriteEditorInputMap.DefaultBindings[SpriteEditorAction.ToolPencil]);
        Assert.Equal("G", SpriteEditorInputMap.DefaultBindings[SpriteEditorAction.ToolFill]);
        Assert.Equal("E", SpriteEditorInputMap.DefaultBindings[SpriteEditorAction.ToolEraser]);
    }

    [Fact]
    public void EraserTool_PaintsTransparentIndex()
    {
        var ctx = CreateContext();
        var doc = ctx.Document;
        doc.SetLocalPixel(2, 2, 8);

        var tool = new EraserTool();
        tool.OnPointerDown(ctx, new PointerInput { LocalX = 2, LocalY = 2, IsStart = true });
        tool.OnPointerUp(ctx, new PointerInput { LocalX = 2, LocalY = 2 });

        Assert.Equal(0, doc.GetLocalPixel(2, 2));
    }

    [Fact]
    public void EraserTool_RespectsBrushSize()
    {
        var ctx = CreateContext(brushSize: 2);
        var doc = ctx.Document;
        doc.SetLocalPixel(1, 1, 8);
        doc.SetLocalPixel(2, 1, 8);
        doc.SetLocalPixel(1, 2, 8);
        doc.SetLocalPixel(2, 2, 8);

        var tool = new EraserTool();
        tool.OnPointerDown(ctx, new PointerInput { LocalX = 1, LocalY = 1, IsStart = true });
        tool.OnPointerUp(ctx, new PointerInput { LocalX = 1, LocalY = 1 });

        Assert.Equal(0, doc.GetLocalPixel(1, 1));
        Assert.Equal(0, doc.GetLocalPixel(2, 1));
        Assert.Equal(0, doc.GetLocalPixel(1, 2));
        Assert.Equal(0, doc.GetLocalPixel(2, 2));
    }

    [Fact]
    public void EraserTool_RespectsSelection()
    {
        var ctx = CreateContext();
        var doc = ctx.Document;
        doc.SetLocalPixel(0, 0, 8);
        doc.SetLocalPixel(1, 0, 8);
        ctx.Selection.SetPixelRect(0, 0, 0, 0);

        var tool = new EraserTool();
        tool.OnPointerDown(ctx, new PointerInput { LocalX = 0, LocalY = 0, IsStart = true });
        tool.OnPointerUp(ctx, new PointerInput { LocalX = 1, LocalY = 0 });

        Assert.Equal(0, doc.GetLocalPixel(0, 0));
        Assert.Equal(8, doc.GetLocalPixel(1, 0));
    }

    [Fact]
    public void UpdateCursorPreview_SetsPositionForBrushTools()
    {
        var controller = new SpriteEditorController();
        controller.HandleKey("B", false, false, false);

        controller.UpdateCursorPreview(new PointerInput { LocalX = 3, LocalY = 4 });

        Assert.Equal((3, 4), controller.CursorPreviewPosition);
        Assert.Equal("pencil", controller.CursorPreviewTool);
    }

    [Fact]
    public void ClearCursorPreview_RemovesPosition()
    {
        var controller = new SpriteEditorController();
        controller.UpdateCursorPreview(new PointerInput { LocalX = 1, LocalY = 1 });
        controller.ClearCursorPreview();

        Assert.Null(controller.CursorPreviewPosition);
        Assert.Null(controller.CursorPreviewTool);
    }

    [Fact]
    public void AddSpriteToSelection_ExpandsViewRegionToBoundingBox()
    {
        var controller = new SpriteEditorController();
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        controller.SetChipBus(bus);
        controller.CurrentSpriteIndex = 0;

        controller.AddSpriteToSelection(1);

        Assert.Equal(16, controller.ViewWidth);
        Assert.Equal(8, controller.ViewHeight);
        Assert.Equal(2, controller.Selection.SpriteIndices.Count);
    }

    [Fact]
    public void AddSpriteToSelection_RejectsNonAdjacentSprite()
    {
        var controller = new SpriteEditorController();
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        controller.SetChipBus(bus);
        controller.CurrentSpriteIndex = 0;

        controller.AddSpriteToSelection(2);

        Assert.Equal(8, controller.ViewWidth);
        Assert.Equal(8, controller.ViewHeight);
        Assert.False(controller.Selection.HasSpriteSelection);
    }

    [Fact]
    public void AddSpriteToSelection_RejectsExpansionBeyondFourTiles()
    {
        var controller = new SpriteEditorController();
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        controller.SetChipBus(bus);
        controller.SelectSpritesInSheetRect(0, 0, 31, 7);
        Assert.Equal(32, controller.ViewWidth);
        Assert.Equal(4, controller.Selection.SpriteIndices.Count);

        controller.AddSpriteToSelection(4);

        Assert.Equal(32, controller.ViewWidth);
        Assert.Equal(4, controller.Selection.SpriteIndices.Count);
    }

    [Fact]
    public void UpdateViewRegion_UsesRectangularSpriteSelection()
    {
        var controller = new SpriteEditorController();
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        controller.SetChipBus(bus);
        controller.Selection.SetSprites([1]);

        controller.UpdateViewRegion();

        Assert.Equal(8, controller.ViewWidth);
        Assert.Equal(8, controller.ViewHeight);
    }

    [Fact]
    public void ClearSelection_ResetsViewRegionToSingleSprite()
    {
        var controller = new SpriteEditorController();
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        controller.SetChipBus(bus);
        controller.AddSpriteToSelection(1);
        controller.Selection.Clear();

        controller.UpdateViewRegion();

        Assert.Equal(8, controller.ViewWidth);
        Assert.Equal(8, controller.ViewHeight);
    }
}
