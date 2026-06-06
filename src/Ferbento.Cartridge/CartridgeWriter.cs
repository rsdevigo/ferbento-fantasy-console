using System.IO.Compression;
using System.Text.Json;
using Ferbento.Profiles.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ferbento.Cartridge;

public static class CartridgeWriter
{
    public static byte[] WritePng(CartridgeData cartridge, int labelWidth = 128, int labelHeight = 128)
    {
        var sections = PrepareSections(cartridge);
        var header = CartridgeHeader.Create(
            totalLength: (uint)sections.Sum(s => s.Data.Length),
            profileCrc32: ComputeProfileCrc32(cartridge.Profile),
            sectionCount: (uint)sections.Count,
            flags: CartridgeFlags.Compressed
        );

        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms))
        {
            WriteHeader(bw, header);
            foreach (var section in sections)
            {
                WriteSection(bw, section);
            }
        }

        var cartridgeBytes = ms.ToArray();

        int minPixels = (cartridgeBytes.Length * 8 + 2) / 3;
        int size = (int)Math.Ceiling(Math.Sqrt(minPixels));
        int imageSize = Math.Max(size, Math.Max(labelWidth, labelHeight));

        using var pngMs = new MemoryStream();
        using var image = new Image<Rgba32>(imageSize, imageSize);

        if (cartridge.LabelImage != null && cartridge.LabelImage.Length > 0)
        {
            try
            {
                using var label = Image.Load<Rgba32>(cartridge.LabelImage);
                label.Mutate(ctx => ctx.Resize(imageSize, imageSize));
                for (int y = 0; y < imageSize; y++)
                    for (int x = 0; x < imageSize; x++)
                        image[x, y] = label[x, y];
            }
            catch
            {
            }
        }

        EncodeDataInPixels(cartridgeBytes, image);
        image.SaveAsPng(pngMs);
        return pngMs.ToArray();
    }

    private static void EncodeDataInPixels(byte[] data, Image<Rgba32> image)
    {
        int totalBits = data.Length * 8;
        int bitIndex = 0;

        for (int y = 0; y < image.Height && bitIndex < totalBits; y++)
        {
            for (int x = 0; x < image.Width && bitIndex < totalBits; x++)
            {
                var pixel = image[x, y];
                byte r = pixel.R, g = pixel.G, b = pixel.B;

                if (bitIndex < totalBits)
                {
                    r = (byte)(pixel.R & 0xFE | ((data[bitIndex / 8] >> (bitIndex % 8)) & 1));
                    bitIndex++;
                }
                if (bitIndex < totalBits)
                {
                    g = (byte)(pixel.G & 0xFE | ((data[bitIndex / 8] >> (bitIndex % 8)) & 1));
                    bitIndex++;
                }
                if (bitIndex < totalBits)
                {
                    b = (byte)(pixel.B & 0xFE | ((data[bitIndex / 8] >> (bitIndex % 8)) & 1));
                    bitIndex++;
                }

                image[x, y] = new Rgba32(r, g, b, pixel.A);
            }
        }
    }

    public static void WritePngToFile(CartridgeData cartridge, string path)
    {
        var pngBytes = WritePng(cartridge);
        File.WriteAllBytes(path, pngBytes);
    }

    private static List<CartridgeSection> PrepareSections(CartridgeData cartridge)
    {
        var sections = new List<CartridgeSection>();

        if (!string.IsNullOrEmpty(cartridge.Title) || !string.IsNullOrEmpty(cartridge.Author) || !string.IsNullOrEmpty(cartridge.Description))
        {
            var meta = new
            {
                title = cartridge.Title,
                author = cartridge.Author,
                description = cartridge.Description,
                version = cartridge.Profile.Version,
                profileName = cartridge.Profile.Name,
            };
            var json = JsonSerializer.Serialize(meta);
            sections.Add(new CartridgeSection
            {
                Type = CartridgeSectionType.Code,
                Data = System.Text.Encoding.UTF8.GetBytes(json),
            });
        }

        if (cartridge.Profile != null)
        {
            var profileJson = JsonSerializer.Serialize(cartridge.Profile);
            sections.Add(new CartridgeSection
            {
                Type = CartridgeSectionType.Profile,
                Data = System.Text.Encoding.UTF8.GetBytes(profileJson),
            });
        }

        if (cartridge.Sections != null)
        {
            foreach (var s in cartridge.Sections)
            {
                var copy = new CartridgeSection
                {
                    Type = s.Type,
                    Data = s.Data,
                };
                copy.ComputeCrc32();
                sections.Add(copy);
            }
        }

        return sections;
    }

    private static void WriteHeader(BinaryWriter bw, CartridgeHeader header)
    {
        bw.Write(header.MagicValue);
        bw.Write(header.VersionValue);
        bw.Write(header.TotalLength);
        bw.Write(header.ProfileCrc32);
        bw.Write((uint)header.Flags);
        bw.Write(header.SectionCount);
    }

    private static void WriteSection(BinaryWriter bw, CartridgeSection section)
    {
        byte[] dataToWrite = section.Data;
        CartridgeFlags flags = CartridgeFlags.None;

        if (section.Data.Length > 0)
        {
            using var compressed = new MemoryStream();
            using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal))
            {
                gzip.Write(section.Data, 0, section.Data.Length);
            }
            var compressedData = compressed.ToArray();

            if (compressedData.Length < section.Data.Length)
            {
                dataToWrite = compressedData;
                flags |= CartridgeFlags.Compressed;
            }
        }

        section.ComputeCrc32();

        bw.Write((byte)section.Type);
        bw.Write(dataToWrite.Length);
        bw.Write((uint)(flags));
        bw.Write(section.Crc32);
        bw.Write(dataToWrite);
    }

    private static uint ComputeProfileCrc32(HardwareProfile profile)
    {
        var json = JsonSerializer.Serialize(profile);
        return Crc32Algorithm.Compute(System.Text.Encoding.UTF8.GetBytes(json));
    }
}

public static class CartridgeLabelGenerator
{
    public static byte[] GenerateLabel(CartridgeData cartridge, int width = 128, int height = 128)
    {
        using var image = new Image<Rgba32>(width, height);

        var bgColor = new Rgba32(0x1D, 0x2B, 0x53);
        image.Mutate(ctx => ctx.BackgroundColor(bgColor));

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }
}
