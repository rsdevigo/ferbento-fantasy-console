namespace Ferbento.Editor.Tilemaps;

/// <summary>
/// Maps navigator multi-select to relative map placements using the sprite sheet grid.
/// </summary>
public static class TileStampLayout
{
    public readonly record struct Placement(int Dx, int Dy, int TileId);

    public static IReadOnlyList<Placement> GetPlacements(
        int selectedTile,
        IEnumerable<int> navigatorSprites,
        int spritesPerRow)
    {
        var indices = navigatorSprites.ToHashSet();
        if (indices.Count == 0)
            return [new Placement(0, 0, selectedTile)];

        if (indices.Count == 1)
            return [new Placement(0, 0, indices.First())];

        var ordered = indices.OrderBy(i => i).ToList();
        var minCol = ordered.Min(i => i % spritesPerRow);
        var minRow = ordered.Min(i => i / spritesPerRow);
        return ordered.Select(i => new Placement(
            i % spritesPerRow - minCol,
            i / spritesPerRow - minRow,
            i)).ToList();
    }
}
