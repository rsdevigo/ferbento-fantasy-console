namespace Ferbento.Profiles;

public static class PaletteDefaults
{
    public static readonly string[] Pico8Hex =
    [
        "000000", "1D2B53", "7E2553", "008751",
        "AB5236", "5F574F", "C2C3C7", "FFF1E8",
        "FF004D", "FFA300", "FFEC27", "00E436",
        "29ADFF", "83769C", "FF77A8", "FFCCAA"
    ];

    public static readonly string[] GameBoyHex =
    [
        "0F380F", "306230", "8BAC0F", "9BBC0F"
    ];

    public static string[] ForProfile(string profileName, int colorCount)
    {
        if (profileName.Equals("Pico-8", StringComparison.OrdinalIgnoreCase) && colorCount <= Pico8Hex.Length)
            return Pico8Hex.Take(colorCount).ToArray();

        if (profileName.Equals("Game Boy", StringComparison.OrdinalIgnoreCase) && colorCount <= GameBoyHex.Length)
            return GameBoyHex.Take(colorCount).ToArray();

        if (profileName.Equals("Game Boy Color", StringComparison.OrdinalIgnoreCase))
            return GenerateGbcDefaults(colorCount);

        return GenerateRamp(colorCount);
    }

    private static string[] GenerateGbcDefaults(int count)
    {
        var result = new string[count];
        for (var i = 0; i < count; i++)
        {
            if (i < Pico8Hex.Length)
                result[i] = Pico8Hex[i];
            else
                result[i] = GenerateRamp(1)[0];
        }
        return result;
    }

    private static string[] GenerateRamp(int count)
    {
        var result = new string[count];
        for (var i = 0; i < count; i++)
        {
            var t = count <= 1 ? 0 : i * 255 / (count - 1);
            result[i] = $"{t:X2}{t:X2}{t:X2}";
        }
        return result;
    }

    public static uint ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
            return Convert.ToUInt32(hex, 16);
        return 0;
    }

    public static uint[] ToRgbArray(IEnumerable<string> hexColors) =>
        hexColors.Select(ParseHex).ToArray();
}
