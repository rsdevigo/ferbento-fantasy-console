using Avalonia.Media;
using Ferbento.Core.Chips;

namespace Ferbento.Editor.Sfx;

public static class SfxSlotColors
{
    public const int PaletteCount = 16;

    /// <summary>Pico-8-style default: cycles palette indices 1–15 (skips transparent black).</summary>
    public static int DefaultIndex(int slot) => (Math.Abs(slot) % 15) + 1;

    public static int ClampIndex(int index) => Math.Clamp(index, 0, PaletteCount - 1);

    public static Color ToColor(int index, ColorChip? colorChip = null) =>
        Pico8Colors.ToAvaloniaColor(ClampIndex(index), colorChip);
}
