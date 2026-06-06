using Ferbento.Profiles.Models;

namespace Ferbento.Core.Chips;

public sealed class ColorChip : IChip
{
    public string Name => "Color";

    public static readonly uint[] Pico8Palette =
    [
        0x000000, 0x1D2B53, 0x7E2553, 0x008751,
        0xAB5236, 0x5F574F, 0xC2C3C7, 0xFFF1E8,
        0xFF004D, 0xFFA300, 0xFFEC27, 0x00E436,
        0x29ADFF, 0x83769C, 0xFF77A8, 0xFFCCAA
    ];

    public int ColorCount { get; private set; } = 16;
    public int TransparentIndex { get; private set; }

    public void Configure(HardwareProfile profile)
    {
        ColorCount = profile.Palette.Colors;
        TransparentIndex = profile.Palette.TransparentIndex;
    }

    public void Initialize(ChipBus bus) { }

    public void Reset() { }

    public void Tick() { }

    public uint GetColor(int index)
    {
        index &= 0xF;
        return index < Pico8Palette.Length ? Pico8Palette[index] : 0;
    }
}
