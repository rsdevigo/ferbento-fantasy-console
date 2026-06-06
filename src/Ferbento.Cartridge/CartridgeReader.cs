using System.IO.Compression;
using System.Text.Json;
using Ferbento.Profiles.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ferbento.Cartridge;

public static class CartridgeReader
{
    public static CartridgeData ReadPng(byte[] pngBytes)
    {
        var cartridgeBytes = DecodeFromPng(pngBytes);
        var data = ReadFromBytes(cartridgeBytes);
        data.LabelImage = pngBytes;
        return data;
    }

    public static CartridgeData ReadPngFromFile(string path)
    {
        var pngBytes = File.ReadAllBytes(path);
        return ReadPng(pngBytes);
    }

    public static CartridgeData ReadFromBytes(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        var header = ReadHeader(br);
        if (header.MagicValue != CartridgeHeader.Magic)
            throw new InvalidDataException("Invalid cartridge magic number");

        if (header.VersionValue != CartridgeHeader.Version)
            throw new InvalidDataException($"Unsupported cartridge version: {header.VersionValue}");

        var sections = new List<CartridgeSection>();
        for (int i = 0; i < header.SectionCount; i++)
        {
            var section = ReadSection(br, header.Flags.HasFlag(CartridgeFlags.Compressed));
            if (!section.VerifyCrc32())
                throw new InvalidDataException($"CRC32 mismatch in section {section.Type}");
            sections.Add(section);
        }

        var cartridge = new CartridgeData
        {
            Header = header,
            Sections = sections,
        };

        ParseSections(cartridge);
        return cartridge;
    }

    private static CartridgeHeader ReadHeader(BinaryReader br)
    {
        return new CartridgeHeader
        {
            MagicValue = br.ReadUInt32(),
            VersionValue = br.ReadUInt16(),
            TotalLength = br.ReadUInt32(),
            ProfileCrc32 = br.ReadUInt32(),
            Flags = (CartridgeFlags)br.ReadUInt32(),
            SectionCount = br.ReadUInt32(),
        };
    }

    private static CartridgeSection ReadSection(BinaryReader br, bool compressed)
    {
        var type = (CartridgeSectionType)br.ReadByte();
        int length = br.ReadInt32();
        var flags = (CartridgeFlags)br.ReadUInt32();
        var crc32 = br.ReadUInt32();
        var data = br.ReadBytes(length);

        if (compressed && (flags & CartridgeFlags.Compressed) != 0)
        {
            using var compressedMs = new MemoryStream(data);
            using var gzip = new GZipStream(compressedMs, CompressionMode.Decompress);
            using var decompressed = new MemoryStream();
            gzip.CopyTo(decompressed);
            data = decompressed.ToArray();
        }

        return new CartridgeSection
        {
            Type = type,
            Data = data,
            Crc32 = crc32,
        };
    }

    private static void ParseSections(CartridgeData cartridge)
    {
        foreach (var section in cartridge.Sections)
        {
            switch (section.Type)
            {
                case CartridgeSectionType.Code:
                    ParseMetadata(cartridge, section.Data);
                    break;
                case CartridgeSectionType.Profile:
                    cartridge.Profile = JsonSerializer.Deserialize<HardwareProfile>(section.Data) ?? HardwareProfile.Pico8();
                    break;
            }
        }
    }

    private static void ParseMetadata(CartridgeData cartridge, byte[] data)
    {
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("title", out var title))
                cartridge.Title = title.GetString() ?? "";
            if (root.TryGetProperty("author", out var author))
                cartridge.Author = author.GetString() ?? "";
            if (root.TryGetProperty("description", out var desc))
                cartridge.Description = desc.GetString() ?? "";
        }
        catch
        {
            // Ignore parse errors
        }
    }

    private static byte[] DecodeFromPng(byte[] pngBytes)
    {
        using var image = Image.Load<Rgba32>(pngBytes);
        int width = image.Width;
        int height = image.Height;

        var bits = new List<bool>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = image[x, y];
                bits.Add((pixel.R & 1) != 0);
                bits.Add((pixel.G & 1) != 0);
                bits.Add((pixel.B & 1) != 0);
            }
        }

        var bytes = new byte[bits.Count / 8];
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = 0;
            for (int j = 0; j < 8; j++)
            {
                if (bits[i * 8 + j])
                    b |= (byte)(1 << j);
            }
            bytes[i] = b;
        }

        return bytes;
    }
}
