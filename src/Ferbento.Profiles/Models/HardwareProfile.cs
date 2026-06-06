namespace Ferbento.Profiles.Models;

public sealed class HardwareProfile
{
    public string Version { get; set; } = "1";
    public string Name { get; set; } = "Pico-8";
    public ResolutionSpec Resolution { get; set; } = new();
    public PaletteSpec Palette { get; set; } = new();
    public SpriteSpec Sprites { get; set; } = new();
    public MapSpec Map { get; set; } = new();
    public MemorySpec Memory { get; set; } = new();
    public AudioSpec Audio { get; set; } = new();
    public CodeSpec Code { get; set; } = new();
    public CartridgeSpec Cartridge { get; set; } = new();

    public static HardwareProfile Pico8() => new()
    {
        Version = "1",
        Name = "Pico-8",
        Resolution = new ResolutionSpec { Width = 128, Height = 128 },
        Palette = new PaletteSpec { Colors = 16, TransparentIndex = 0 },
        Sprites = new SpriteSpec { SheetWidth = 128, SheetHeight = 128, TileSize = 8, MaxOnScreen = 128 },
        Map = new MapSpec { Width = 128, Height = 64, TileSize = 8 },
        Memory = new MemorySpec { Ram = 32768, CartRom = 32768 },
        Audio = new AudioSpec { Channels = 4, SfxSlots = 64, MusicPatterns = 64, SampleRate = 22050 },
        Code = new CodeSpec { MaxTokens = 8192 },
        Cartridge = new CartridgeSpec { MaxBytes = 32768 }
    };
}

public sealed class ResolutionSpec
{
    public int Width { get; set; } = 128;
    public int Height { get; set; } = 128;
}

public sealed class PaletteSpec
{
    public int Colors { get; set; } = 16;
    public int TransparentIndex { get; set; } = 0;
    public string[]? DefaultColors { get; set; }
}

public sealed class SpriteSpec
{
    public int SheetWidth { get; set; } = 128;
    public int SheetHeight { get; set; } = 128;
    public int TileSize { get; set; } = 8;
    public int MaxOnScreen { get; set; } = 128;
}

public sealed class MapSpec
{
    public int Width { get; set; } = 128;
    public int Height { get; set; } = 64;
    public int TileSize { get; set; } = 8;
}

public sealed class MemorySpec
{
    public int Ram { get; set; } = 32768;
    public int CartRom { get; set; } = 32768;
}

public sealed class AudioSpec
{
    public int Channels { get; set; } = 4;
    public int SfxSlots { get; set; } = 64;
    public int MusicPatterns { get; set; } = 64;
    public int SampleRate { get; set; } = 22050;
}

public sealed class CodeSpec
{
    public int MaxTokens { get; set; } = 8192;
}

public sealed class CartridgeSpec
{
    public int MaxBytes { get; set; } = 32768;
}
