using Avalonia.Media;
using Ferbento.Core.Chips;

namespace Ferbento.Editor;

public static class Pico8Colors
{
    public static readonly string[] Names =
    [
        "black", "dark-blue", "dark-purple", "dark-green",
        "brown", "dark-grey", "light-grey", "white",
        "red", "orange", "yellow", "green",
        "blue", "indigo", "pink", "peach"
    ];

    public static uint ToBgra(int index, ColorChip? colorChip = null, uint backgroundForTransparent = 0xFF2A2A2A)
    {
        index = Math.Max(0, index);
        if (colorChip != null && index < colorChip.ColorCount)
        {
            if (index == colorChip.TransparentIndex)
                return backgroundForTransparent;
            return 0xFF000000 | colorChip.GetColor(index);
        }

        index &= 0xF;
        if (index == 0) return backgroundForTransparent;
        var rgb = ColorChip.Pico8Palette[index];
        return 0xFF000000 | rgb;
    }

    public static Color ToAvaloniaColor(int index, ColorChip? colorChip = null)
    {
        uint rgb;
        if (colorChip != null && index >= 0 && index < colorChip.ColorCount)
            rgb = colorChip.GetColor(index);
        else
            rgb = ColorChip.Pico8Palette[index & 0xF];

        return Color.FromRgb(
            (byte)((rgb >> 16) & 0xFF),
            (byte)((rgb >> 8) & 0xFF),
            (byte)(rgb & 0xFF));
    }

    public static string ToHex(int index, ColorChip? colorChip = null)
    {
        uint rgb;
        if (colorChip != null && index >= 0 && index < colorChip.ColorCount)
            rgb = colorChip.GetColor(index);
        else
            rgb = ColorChip.Pico8Palette[index & 0xF];
        return $"#{(rgb & 0xFFFFFF):X6}";
    }

    public static string ColorName(int index) =>
        index >= 0 && index < Names.Length ? Names[index] : $"color-{index}";
}
