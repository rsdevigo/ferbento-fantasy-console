using Ferbento.Cartridge;
using Ferbento.Profiles.Models;
using SixLabors.ImageSharp;

namespace Ferbento.Compatibility.Tests;

public class CartridgeRoundtripTests
{
    [Fact]
    public void Roundtrip_MinimalCartridge()
    {
        var original = new CartridgeData
        {
            Title = "Test Cart",
            Author = "Ferbento",
            Description = "Roundtrip test",
            Profile = HardwareProfile.Pico8(),
        };

        var png = CartridgeWriter.WritePng(original);
        Assert.NotNull(png);
        Assert.True(png.Length > 0);

        var loaded = CartridgeReader.ReadPng(png);
        Assert.Equal(original.Title, loaded.Title);
        Assert.Equal(original.Author, loaded.Author);
        Assert.Equal(original.Description, loaded.Description);
        Assert.Equal(original.Profile.Name, loaded.Profile.Name);
    }

    [Fact]
    public void Roundtrip_WithSpriteGfxSection()
    {
        var profile = HardwareProfile.Pico8();
        var gfxData = new byte[8192];
        new Random(42).NextBytes(gfxData);

        var original = new CartridgeData
        {
            Title = "Sprite Test",
            Profile = profile,
        };
        original.Sections.Add(new CartridgeSection
        {
            Type = CartridgeSectionType.Gfx,
            Data = gfxData,
        });

        var png = CartridgeWriter.WritePng(original);
        var loaded = CartridgeReader.ReadPng(png);

        var gfxSection = loaded.Sections.Find(s => s.Type == CartridgeSectionType.Gfx);
        Assert.NotNull(gfxSection);
        Assert.Equal(gfxData, gfxSection.Data);
    }

    [Fact]
    public void Roundtrip_WithAllSections()
    {
        var profile = HardwareProfile.Pico8();
        var original = new CartridgeData
        {
            Title = "Full Cart",
            Author = "Test",
            Description = "All sections",
            Profile = profile,
        };

        original.Sections.Add(new CartridgeSection { Type = CartridgeSectionType.Gfx, Data = new byte[128] });
        original.Sections.Add(new CartridgeSection { Type = CartridgeSectionType.Map, Data = new byte[256] });
        original.Sections.Add(new CartridgeSection { Type = CartridgeSectionType.Sfx, Data = new byte[64] });
        original.Sections.Add(new CartridgeSection { Type = CartridgeSectionType.Music, Data = new byte[32] });

        var png = CartridgeWriter.WritePng(original);
        var loaded = CartridgeReader.ReadPng(png);

        Assert.Equal(original.Title, loaded.Title);
        foreach (var section in original.Sections)
        {
            var found = loaded.Sections.Find(s => s.Type == section.Type);
            Assert.NotNull(found);
            Assert.Equal(section.Data, found.Data);
        }
    }

    [Fact]
    public void Roundtrip_PngFile()
    {
        var original = new CartridgeData
        {
            Title = "File Test",
            Profile = HardwareProfile.Pico8(),
        };

        var tempPath = Path.Combine(Path.GetTempPath(), "ferbento_test_cart.png");
        try
        {
            CartridgeWriter.WritePngToFile(original, tempPath);
            Assert.True(File.Exists(tempPath));

            var loaded = CartridgeReader.ReadPngFromFile(tempPath);
            Assert.Equal(original.Title, loaded.Title);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void InvalidMagic_Throws()
    {
        var bogus = new byte[] { 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        Assert.Throws<InvalidDataException>(() => CartridgeReader.ReadFromBytes(bogus));
    }

    [Fact]
    public void LabelImage_Roundtrips()
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(128, 128);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        var labelBytes = ms.ToArray();

        var original = new CartridgeData
        {
            Title = "Label Test",
            Profile = HardwareProfile.Pico8(),
            LabelImage = labelBytes,
        };

        var png = CartridgeWriter.WritePng(original);
        var loaded = CartridgeReader.ReadPng(png);

        Assert.NotNull(loaded.LabelImage);
        Assert.True(loaded.LabelImage.Length > 0);
    }
}
