using Ferbento.Core.Chips;
using Ferbento.Editor.Sprites;
using Ferbento.Profiles.BuiltIn;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Tests;

public class SpriteDocumentTests
{
    private static SpriteDocument CreateDoc(HardwareProfile? profile = null)
    {
        var p = profile ?? BuiltInProfiles.Pico8();
        var bus = new ChipBus(p);
        var doc = new SpriteDocument();
        doc.SetProfile(p);
        doc.SetChipBus(bus);
        return doc;
    }

    [Fact]
    public void GetSetLocalPixel_RoundTrips()
    {
        var doc = CreateDoc();
        doc.SetLocalPixel(3, 4, 8);
        Assert.Equal(8, doc.GetLocalPixel(3, 4));
    }

    [Fact]
    public void GetSetFlags_RoundTrips()
    {
        var doc = CreateDoc();
        doc.SetFlags(0, 0b10101010);
        Assert.Equal(0b10101010, doc.GetFlags(0));
    }

    [Fact]
    public void ReadWriteRegion_PreservesPixels()
    {
        var doc = CreateDoc();
        doc.SetLocalPixel(0, 0, 1);
        doc.SetLocalPixel(1, 0, 2);
        doc.SetLocalPixel(0, 1, 3);
        doc.SetLocalPixel(1, 1, 4);

        var region = doc.ReadLocalRegion(0, 0, 2, 2);
        doc.SetLocalPixel(0, 0, 0);
        doc.WriteLocalRegion(0, 0, 2, 2, region);

        Assert.Equal(1, doc.GetLocalPixel(0, 0));
        Assert.Equal(2, doc.GetLocalPixel(1, 0));
        Assert.Equal(3, doc.GetLocalPixel(0, 1));
        Assert.Equal(4, doc.GetLocalPixel(1, 1));
    }

    [Fact]
    public void SpriteOrigin_MapsIndexToSheetCoords()
    {
        var doc = CreateDoc();
        doc.CurrentSpriteIndex = 17;
        var (ox, oy) = doc.SpriteOrigin;
        Assert.Equal(8, ox);
        Assert.Equal(8, oy);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    public void TileSize_MatchesProfile(int expectedTile)
    {
        var profile = expectedTile == 16 ? BuiltInProfiles.Snes() : BuiltInProfiles.Pico8();
        var doc = new SpriteDocument();
        doc.SetProfile(profile);
        Assert.Equal(expectedTile, doc.TileSize);
    }
}
