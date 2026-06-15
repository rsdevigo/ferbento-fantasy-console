namespace Ferbento.Profiles.Models;

/// <summary>
/// Declarative map RAM layout. When map height exceeds MapPrimaryRows,
/// additional rows are stored at SharedRegionStart (P8: shared with GFX2).
/// </summary>
public sealed class MapMemoryLayout
{
    /// <summary>Rows stored at MapDataStart before overflow into shared region.</summary>
    public int MapPrimaryRows { get; set; }

    /// <summary>Virtual address where overflow rows begin (P8 default: 0x1000).</summary>
    public int SharedRegionStart { get; set; } = 0x1000;

    /// <summary>Base address for primary map rows (P8 default: 0x2000).</summary>
    public int MapDataStart { get; set; } = 0x2000;
}
