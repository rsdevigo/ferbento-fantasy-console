namespace Ferbento.Editor.Tilemaps;

public sealed class MapSelection
{
    public (int x, int y, int w, int h)? MapRect { get; private set; }
    public HashSet<int> NavigatorSprites { get; } = [];

    public bool HasMapRect => MapRect is not null;
    public bool HasNavigatorSelection => NavigatorSprites.Count > 0;

    public void SetMapRect(int x, int y, int w, int h) =>
        MapRect = (x, y, Math.Max(1, w), Math.Max(1, h));

    public void ClearMapRect() => MapRect = null;

    public void ClearNavigator() => NavigatorSprites.Clear();

    public void ClearAll()
    {
        ClearMapRect();
        ClearNavigator();
    }

    public void SetNavigatorSprites(IEnumerable<int> indices)
    {
        NavigatorSprites.Clear();
        foreach (var i in indices)
            NavigatorSprites.Add(i);
    }
}
