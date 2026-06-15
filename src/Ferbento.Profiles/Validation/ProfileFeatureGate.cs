using Ferbento.Profiles.Models;

namespace Ferbento.Profiles.Validation;

public sealed class ProfileFeatureGate
{
    public HardwareProfile Profile { get; }
    public ProfileConstraints Constraints { get; }

    public ProfileFeatureGate(HardwareProfile profile)
    {
        Profile = profile;
        Constraints = new ProfileConstraints(profile);
    }

    public MapMemoryLayout? MapLayout => Profile.Memory.MapLayout;

    public bool UsesSharedMapRows =>
        MapLayout is { MapPrimaryRows: > 0 } layout && Profile.Map.Height > layout.MapPrimaryRows;

    public int MapPrimaryRows => MapLayout?.MapPrimaryRows ?? Profile.Map.Height;

    public int SpriteCount =>
        (Profile.Sprites.SheetWidth / Profile.Sprites.TileSize)
        * (Profile.Sprites.SheetHeight / Profile.Sprites.TileSize);

    public int SpriteBankCount => Math.Max(1, (SpriteCount + 63) / 64);

    public bool UsesGfx2Bank =>
        MapLayout is not null && Profile.Sprites.SheetHeight > 64;

    public bool SupportsMapAssistedMove => SpriteCount > 1;

    public bool UsesHexOverlay => true;

    public bool RequiresVirtualizedCanvas => Profile.Map.Width * Profile.Map.Height > 4096;
}
