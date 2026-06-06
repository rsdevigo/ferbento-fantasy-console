using Ferbento.Profiles.Models;

namespace Ferbento.Cartridge;

public enum CartridgeSectionType : byte
{
    Code = 0,
    Gfx = 1,
    Map = 2,
    Sfx = 3,
    Music = 4,
    Profile = 5,
    Label = 6,
}

[Flags]
public enum CartridgeFlags : uint
{
    None = 0,
    Compressed = 1 << 0,
    Encrypted = 1 << 1,
}

public readonly struct CartridgeHeader
{
    public const uint Magic = 0x46455242; // "FERB" little-endian
    public const ushort Version = 1;

    public uint MagicValue { get; init; }
    public ushort VersionValue { get; init; }
    public uint TotalLength { get; init; }
    public uint ProfileCrc32 { get; init; }
    public CartridgeFlags Flags { get; init; }
    public uint SectionCount { get; init; }

    public static CartridgeHeader Create(uint totalLength, uint profileCrc32, uint sectionCount, CartridgeFlags flags = CartridgeFlags.Compressed)
    {
        return new CartridgeHeader
        {
            MagicValue = Magic,
            VersionValue = Version,
            TotalLength = totalLength,
            ProfileCrc32 = profileCrc32,
            Flags = flags,
            SectionCount = sectionCount,
        };
    }
}

public readonly struct CartridgeSectionHeader
{
    public CartridgeSectionType Type { get; init; }
    public uint CompressedLength { get; init; }
    public uint UncompressedLength { get; init; }
    public uint Crc32 { get; init; }
}

public sealed class CartridgeSection
{
    public CartridgeSectionType Type { get; init; }
    public byte[] Data { get; init; } = Array.Empty<byte>();
    public uint Crc32 { get; set; }

    public void ComputeCrc32()
    {
        Crc32 = Crc32Algorithm.Compute(Data);
    }

    public bool VerifyCrc32()
    {
        return Crc32 == Crc32Algorithm.Compute(Data);
    }
}

public sealed class CartridgeData
{
    public CartridgeHeader Header { get; set; } = new();
    public List<CartridgeSection> Sections { get; set; } = new();
    public HardwareProfile Profile { get; set; } = HardwareProfile.Pico8();
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public byte[]? LabelImage { get; set; }
}

public static class Crc32Algorithm
{
    private static readonly uint[] Table = InitializeTable();

    private static uint[] InitializeTable()
    {
        var table = new uint[256];
        const uint polynomial = 0xEDB88320;
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
            }
            table[i] = crc;
        }
        return table;
    }

    public static uint Compute(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFF];
        }
        return ~crc;
    }

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFF];
        }
        return ~crc;
    }
}
