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
}
