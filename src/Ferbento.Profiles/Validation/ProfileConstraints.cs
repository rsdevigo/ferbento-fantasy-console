using Ferbento.Profiles.Models;

namespace Ferbento.Profiles.Validation;

public sealed class ProfileConstraints
{
    public HardwareProfile Profile { get; }

    public ProfileConstraints(HardwareProfile profile)
    {
        Profile = profile;
    }

    public int MaxScreenWidth => Profile.Resolution.Width;
    public int MaxScreenHeight => Profile.Resolution.Height;
    public int MaxPaletteColors => Profile.Palette.Colors;
    public int TransparentColorIndex => Profile.Palette.TransparentIndex;
    public int SpriteSheetWidth => Profile.Sprites.SheetWidth;
    public int SpriteSheetHeight => Profile.Sprites.SheetHeight;
    public int SpriteTileSize => Profile.Sprites.TileSize;
    public int MaxSpritesOnScreen => Profile.Sprites.MaxOnScreen;
    public int MapWidth => Profile.Map.Width;
    public int MapHeight => Profile.Map.Height;
    public int MapTileSize => Profile.Map.TileSize;
    public int TotalRam => Profile.Memory.Ram;
    public int CartRomSize => Profile.Memory.CartRom;
    public int AudioChannels => Profile.Audio.Channels;
    public int SfxSlotCount => Profile.Audio.SfxSlots;
    public int MusicPatternCount => Profile.Audio.MusicPatterns;
    public int AudioSampleRate => Profile.Audio.SampleRate;
    public int MaxTokens => Profile.Code.MaxTokens;
    public int CartridgeMaxBytes => Profile.Cartridge.MaxBytes;

    public bool IsValidSpriteCoord(int x, int y)
    {
        return x >= 0 && x < SpriteSheetWidth / SpriteTileSize
            && y >= 0 && y < SpriteSheetHeight / SpriteTileSize;
    }

    public bool IsValidMapCoord(int x, int y)
    {
        return x >= 0 && x < MapWidth && y >= 0 && y < MapHeight;
    }

    public bool IsValidScreenCoord(int x, int y)
    {
        return x >= 0 && x < MaxScreenWidth && y >= 0 && y < MaxScreenHeight;
    }

    public bool IsValidColorIndex(int index)
    {
        return index >= 0 && index < MaxPaletteColors;
    }

    public bool IsValidSfxSlot(int slot)
    {
        return slot >= 0 && slot < SfxSlotCount;
    }

    public bool IsValidMusicPattern(int pattern)
    {
        return pattern >= 0 && pattern < MusicPatternCount;
    }

    public bool IsValidMemoryAddress(int address)
    {
        return address >= 0 && address < TotalRam;
    }
}
