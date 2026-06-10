using Ferbento.Core.Chips;
using Ferbento.Editor.Sprites;
using Ferbento.Profiles.BuiltIn;

namespace Ferbento.Editor.Tests;

public class PixelDrawingTests
{
    private static SpriteDocument CreateDoc()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        var doc = new SpriteDocument();
        doc.SetProfile(BuiltInProfiles.Pico8());
        doc.SetChipBus(bus);
        return doc;
    }

    [Fact]
    public void DrawLine_Bresenham_ConnectsEndpoints()
    {
        var doc = CreateDoc();
        PixelDrawing.DrawLine(doc, 0, 0, 7, 0, 8);
        Assert.Equal(8, doc.GetLocalPixel(0, 0));
        Assert.Equal(8, doc.GetLocalPixel(7, 0));
        Assert.Equal(8, doc.GetLocalPixel(4, 0));
    }

    [Fact]
    public void DrawRect_Outline_DrawsBorder()
    {
        var doc = CreateDoc();
        PixelDrawing.DrawRect(doc, 1, 1, 5, 5, 8, filled: false);
        Assert.Equal(8, doc.GetLocalPixel(1, 1));
        Assert.Equal(8, doc.GetLocalPixel(5, 5));
        Assert.Equal(0, doc.GetLocalPixel(3, 3));
    }

    [Fact]
    public void DrawRect_Filled_FillsInterior()
    {
        var doc = CreateDoc();
        PixelDrawing.DrawRect(doc, 1, 1, 3, 3, 8, filled: true);
        Assert.Equal(8, doc.GetLocalPixel(2, 2));
    }

    [Fact]
    public void FloodFill_DoesNotLeakOutsideTile()
    {
        var doc = CreateDoc();
        doc.SetLocalPixel(0, 0, 1);
        doc.SetLocalPixel(1, 0, 1);
        doc.SetLocalPixel(0, 1, 1);
        doc.SetLocalPixel(2, 2, 2);
        PixelDrawing.FloodFill(doc, 0, 0, 8);
        Assert.Equal(8, doc.GetLocalPixel(1, 0));
        Assert.Equal(2, doc.GetLocalPixel(2, 2));
    }

    [Fact]
    public void FlipHorizontal_MirrorsPixels()
    {
        var data = new byte[64];
        data[0] = 1;
        data[7] = 2;
        var flipped = PixelDrawing.FlipHorizontal(data, 8);
        Assert.Equal(2, flipped[0]);
        Assert.Equal(1, flipped[7]);
    }

    [Fact]
    public void Rotate90_RequiresSquare()
    {
        var data = new byte[64];
        data[0] = 5;
        var rotated = PixelDrawing.Rotate90(data, 8);
        Assert.Equal(5, rotated[7]);
    }

    [Fact]
    public void ShiftCircular_WrapsPixels()
    {
        var data = new byte[64];
        data[0] = 9;
        var shifted = PixelDrawing.ShiftCircular(data, 8, 1, 0);
        Assert.Equal(9, shifted[1]);
        Assert.Equal(0, shifted[0]);
    }
}
