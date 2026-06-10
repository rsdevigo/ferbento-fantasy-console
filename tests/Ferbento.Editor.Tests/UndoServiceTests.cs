using Ferbento.Core.Chips;
using Ferbento.Editor.Sprites;
using Ferbento.Editor.Sprites.Undo;
using Ferbento.Profiles.BuiltIn;

namespace Ferbento.Editor.Tests;

public class UndoServiceTests
{
    [Fact]
    public void Undo_RestoresSpritePixels()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        var doc = new SpriteDocument();
        doc.SetProfile(BuiltInProfiles.Pico8());
        doc.SetChipBus(bus);

        var undo = new UndoService(100);
        var before = doc.GetCurrentSpritePixels();
        doc.SetLocalPixel(0, 0, 8);
        var after = doc.GetCurrentSpritePixels();

        undo.Execute(new SpritePatchCommand(doc, 0, "test", before, after, 0, 0));
        Assert.Equal(8, doc.GetLocalPixel(0, 0));

        Assert.True(undo.Undo());
        Assert.Equal(0, doc.GetLocalPixel(0, 0));
    }

    [Fact]
    public void DrawStrokeCommand_GroupsStroke()
    {
        var bus = new ChipBus(BuiltInProfiles.Pico8());
        var doc = new SpriteDocument();
        doc.SetProfile(BuiltInProfiles.Pico8());
        doc.SetChipBus(bus);

        var undo = new UndoService();
        var stroke = new DrawStrokeCommand(doc, 0);
        undo.PushExecuted(stroke);
        doc.SetLocalPixel(0, 0, 8);
        doc.SetLocalPixel(1, 0, 8);
        stroke.CaptureAfter();

        stroke.Undo();
        Assert.Equal(0, doc.GetLocalPixel(0, 0));
        Assert.Equal(0, doc.GetLocalPixel(1, 0));

        stroke.Execute();
        Assert.Equal(8, doc.GetLocalPixel(0, 0));
        Assert.Equal(8, doc.GetLocalPixel(1, 0));
    }
}
