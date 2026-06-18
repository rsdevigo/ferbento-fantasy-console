using Ferbento.Profiles.Models;

namespace Ferbento.Profiles.BuiltIn;

// DefaultColors on PaletteSpec are populated from PaletteDefaults at profile creation.

public static class BuiltInProfiles
{
    public static IReadOnlyList<HardwareProfile> All => new[]
    {
        Pico8(),
        GameBoy(),
        GameBoyColor(),
        Nes(),
        Snes(),
        MegaDrive()
    };

    public static HardwareProfile? GetByName(string name)
    {
        return All.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public static HardwareProfile Pico8() => new()
    {
        Name = "Pico-8",
        Resolution = new ResolutionSpec { Width = 128, Height = 128 },
        Palette = new PaletteSpec
        {
            Colors = 16,
            TransparentIndex = 0,
            DefaultColors = PaletteDefaults.Pico8Hex,
            AllowColorEdit = true,
        },
        Sprites = new SpriteSpec { SheetWidth = 128, SheetHeight = 128, TileSize = 8, MaxOnScreen = 128 },
        Map = new MapSpec { Width = 128, Height = 64, TileSize = 8 },
        Memory = new MemorySpec
        {
            Ram = 32768,
            CartRom = 32768,
            MapLayout = new MapMemoryLayout { MapPrimaryRows = 32 },
        },
        Audio = new AudioSpec { Channels = 4, SfxSlots = 64, MusicPatterns = 64, SampleRate = 22050 },
        Code = new CodeSpec { MaxTokens = 8192 },
        Cartridge = new CartridgeSpec { MaxBytes = 32768 }
    };

    public static HardwareProfile GameBoy() => new()
    {
        Name = "Game Boy",
        Resolution = new ResolutionSpec { Width = 160, Height = 144 },
        Palette = new PaletteSpec
        {
            Colors = 4,
            TransparentIndex = 0,
            DefaultColors = PaletteDefaults.GameBoyHex,
        },
        Sprites = new SpriteSpec { SheetWidth = 128, SheetHeight = 128, TileSize = 8, MaxOnScreen = 40 },
        Map = new MapSpec { Width = 32, Height = 32, TileSize = 8 },
        Memory = new MemorySpec { Ram = 8192, CartRom = 32768 },
        Audio = new AudioSpec { Channels = 4, SfxSlots = 32, MusicPatterns = 32, SampleRate = 44100 },
        Code = new CodeSpec { MaxTokens = 4096 },
        Cartridge = new CartridgeSpec { MaxBytes = 262144 }
    };

    public static HardwareProfile GameBoyColor() => new()
    {
        Name = "Game Boy Color",
        Resolution = new ResolutionSpec { Width = 160, Height = 144 },
        Palette = new PaletteSpec { Colors = 56, TransparentIndex = 0 },
        Sprites = new SpriteSpec { SheetWidth = 128, SheetHeight = 256, TileSize = 8, MaxOnScreen = 40 },
        Map = new MapSpec { Width = 32, Height = 32, TileSize = 8 },
        Memory = new MemorySpec { Ram = 32768, CartRom = 1048576 },
        Audio = new AudioSpec { Channels = 4, SfxSlots = 32, MusicPatterns = 32, SampleRate = 44100 },
        Code = new CodeSpec { MaxTokens = 4096 },
        Cartridge = new CartridgeSpec { MaxBytes = 2097152 }
    };

    public static HardwareProfile Nes() => new()
    {
        Name = "NES",
        Resolution = new ResolutionSpec { Width = 256, Height = 240 },
        Palette = new PaletteSpec { Colors = 64, TransparentIndex = 63 },
        Sprites = new SpriteSpec { SheetWidth = 128, SheetHeight = 128, TileSize = 8, MaxOnScreen = 64 },
        Map = new MapSpec { Width = 64, Height = 64, TileSize = 8 },
        Memory = new MemorySpec { Ram = 2048, CartRom = 262144 },
        Audio = new AudioSpec { Channels = 5, SfxSlots = 32, MusicPatterns = 32, SampleRate = 44100 },
        Code = new CodeSpec { MaxTokens = 4096 },
        Cartridge = new CartridgeSpec { MaxBytes = 524288 }
    };

    public static HardwareProfile Snes() => new()
    {
        Name = "SNES",
        Resolution = new ResolutionSpec { Width = 256, Height = 224 },
        Palette = new PaletteSpec { Colors = 256, TransparentIndex = 0 },
        Sprites = new SpriteSpec { SheetWidth = 256, SheetHeight = 256, TileSize = 16, MaxOnScreen = 128 },
        Map = new MapSpec { Width = 64, Height = 64, TileSize = 16 },
        Memory = new MemorySpec { Ram = 131072, CartRom = 4194304 },
        Audio = new AudioSpec { Channels = 8, SfxSlots = 64, MusicPatterns = 64, SampleRate = 44100 },
        Code = new CodeSpec { MaxTokens = 8192 },
        Cartridge = new CartridgeSpec { MaxBytes = 8388608 }
    };

    public static HardwareProfile MegaDrive() => new()
    {
        Name = "Mega Drive",
        Resolution = new ResolutionSpec { Width = 320, Height = 224 },
        Palette = new PaletteSpec { Colors = 64, TransparentIndex = 0 },
        Sprites = new SpriteSpec { SheetWidth = 256, SheetHeight = 256, TileSize = 8, MaxOnScreen = 80 },
        Map = new MapSpec { Width = 64, Height = 64, TileSize = 8 },
        Memory = new MemorySpec { Ram = 65536, CartRom = 4194304 },
        Audio = new AudioSpec { Channels = 6, SfxSlots = 64, MusicPatterns = 64, SampleRate = 44100 },
        Code = new CodeSpec { MaxTokens = 8192 },
        Cartridge = new CartridgeSpec { MaxBytes = 8388608 }
    };
}
