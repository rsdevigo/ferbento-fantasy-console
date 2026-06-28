using System.Text.Json;
using Ferbento.Profiles.Models;

namespace Ferbento.Profiles.Validation;

public sealed class ProfileValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

public sealed class ProfileValidator
{
    public ProfileValidationResult Validate(string json)
    {
        var result = new ProfileValidationResult();

        HardwareProfile? profile;
        try
        {
            profile = JsonSerializer.Deserialize<HardwareProfile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            result.Errors.Add($"Invalid JSON: {ex.Message}");
            result.IsValid = false;
            return result;
        }

        if (profile is null)
        {
            result.Errors.Add("Profile is null");
            result.IsValid = false;
            return result;
        }

        ValidateVersion(profile, result);
        ValidateName(profile, result);
        ValidateResolution(profile, result);
        ValidatePalette(profile, result);
        ValidateSprites(profile, result);
        ValidateMap(profile, result);
        ValidateMemory(profile, result);
        ValidateAudio(profile, result);
        ValidateCode(profile, result);
        ValidateCartridge(profile, result);

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    public ProfileValidationResult Validate(HardwareProfile profile)
    {
        var result = new ProfileValidationResult();
        ValidateVersion(profile, result);
        ValidateName(profile, result);
        ValidateResolution(profile, result);
        ValidatePalette(profile, result);
        ValidateSprites(profile, result);
        ValidateMap(profile, result);
        ValidateMemory(profile, result);
        ValidateAudio(profile, result);
        ValidateCode(profile, result);
        ValidateCartridge(profile, result);
        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    private static void AddError(ProfileValidationResult result, string message)
    {
        result.Errors.Add(message);
    }

    private static void ValidateVersion(HardwareProfile p, ProfileValidationResult r)
    {
        if (string.IsNullOrWhiteSpace(p.Version))
            AddError(r, "Version is required");
    }

    private static void ValidateName(HardwareProfile p, ProfileValidationResult r)
    {
        if (string.IsNullOrWhiteSpace(p.Name))
            AddError(r, "Name is required");
    }

    private static void ValidateResolution(HardwareProfile p, ProfileValidationResult r)
    {
        if (p.Resolution.Width <= 0 || p.Resolution.Width > 4096)
            AddError(r, "Resolution.Width must be between 1 and 4096");
        if (p.Resolution.Height <= 0 || p.Resolution.Height > 4096)
            AddError(r, "Resolution.Height must be between 1 and 4096");
    }

    private static void ValidatePalette(HardwareProfile p, ProfileValidationResult r)
    {
        if (p.Palette.Colors < 2 || p.Palette.Colors > 256)
            AddError(r, "Palette.Colors must be between 2 and 256");
        if (p.Palette.TransparentIndex < 0 || p.Palette.TransparentIndex >= p.Palette.Colors)
            AddError(r, "Palette.TransparentIndex must be between 0 and Colors-1");
    }

    private static void ValidateSprites(HardwareProfile p, ProfileValidationResult r)
    {
        if (p.Sprites.SheetWidth <= 0 || p.Sprites.SheetWidth > 4096)
            AddError(r, "Sprites.SheetWidth must be between 1 and 4096");
        if (p.Sprites.SheetHeight <= 0 || p.Sprites.SheetHeight > 4096)
            AddError(r, "Sprites.SheetHeight must be between 1 and 4096");
        if (p.Sprites.TileSize <= 0 || p.Sprites.TileSize > 256)
            AddError(r, "Sprites.TileSize must be between 1 and 256");
        if (p.Sprites.MaxOnScreen <= 0)
            AddError(r, "Sprites.MaxOnScreen must be positive");
    }

    private static void ValidateMap(HardwareProfile p, ProfileValidationResult r)
    {
        if (p.Map.Width <= 0 || p.Map.Width > 4096)
            AddError(r, "Map.Width must be between 1 and 4096");
        if (p.Map.Height <= 0 || p.Map.Height > 4096)
            AddError(r, "Map.Height must be between 1 and 4096");
        if (p.Map.TileSize <= 0 || p.Map.TileSize > 256)
            AddError(r, "Map.TileSize must be between 1 and 256");
    }

    private static void ValidateMemory(HardwareProfile p, ProfileValidationResult r)
    {
        if (p.Memory.Ram < 256 || p.Memory.Ram > 16_777_216)
            AddError(r, "Memory.Ram must be between 256 and 16777216");
        if (p.Memory.CartRom < 0 || p.Memory.CartRom > 16_777_216)
            AddError(r, "Memory.CartRom must be between 0 and 16777216");

        var layout = p.Memory.MapLayout;
        if (layout is null) return;

        if (layout.MapPrimaryRows <= 0)
            AddError(r, "Memory.MapLayout.MapPrimaryRows must be positive when MapLayout is set");
        if (layout.MapDataStart < 0 || layout.MapDataStart >= p.Memory.Ram)
            AddError(r, "Memory.MapLayout.MapDataStart must be within RAM");
        if (layout.SharedRegionStart < 0 || layout.SharedRegionStart >= p.Memory.Ram)
            AddError(r, "Memory.MapLayout.SharedRegionStart must be within RAM");
        if (layout.MapPrimaryRows > p.Map.Height)
            AddError(r, "Memory.MapLayout.MapPrimaryRows cannot exceed Map.Height");
    }

    private static void ValidateAudio(HardwareProfile p, ProfileValidationResult r)
    {
        if (p.Audio.Channels < 1 || p.Audio.Channels > 16)
            AddError(r, "Audio.Channels must be between 1 and 16");
        if (p.Audio.SfxSlots < 1 || p.Audio.SfxSlots > 256)
            AddError(r, "Audio.SfxSlots must be between 1 and 256");
        if (p.Audio.MusicPatterns < 1 || p.Audio.MusicPatterns > 256)
            AddError(r, "Audio.MusicPatterns must be between 1 and 256");
        if (p.Audio.SampleRate < 8000 || p.Audio.SampleRate > 192_000)
            AddError(r, "Audio.SampleRate must be between 8000 and 192000");
        if (p.Audio.NotesPerSfx < 1 || p.Audio.NotesPerSfx > 128)
            AddError(r, "Audio.NotesPerSfx must be between 1 and 128");
    }

    private static void ValidateCode(HardwareProfile p, ProfileValidationResult r)
    {
        if (p.Code.MaxTokens < 1 || p.Code.MaxTokens > 65535)
            AddError(r, "Code.MaxTokens must be between 1 and 65535");
    }

    private static void ValidateCartridge(HardwareProfile p, ProfileValidationResult r)
    {
        if (p.Cartridge.MaxBytes < 1 || p.Cartridge.MaxBytes > 16_777_216)
            AddError(r, "Cartridge.MaxBytes must be between 1 and 16777216");
    }
}
