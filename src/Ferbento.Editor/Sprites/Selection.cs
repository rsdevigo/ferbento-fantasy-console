namespace Ferbento.Editor.Sprites;

public enum SelectionMode
{
    None,
    Pixels,
    Sprites,
}

public sealed class Selection
{
    public SelectionMode Mode { get; private set; } = SelectionMode.None;

    public int PixelX { get; private set; }
    public int PixelY { get; private set; }
    public int PixelWidth { get; private set; }
    public int PixelHeight { get; private set; }

    public byte[]? PixelBuffer { get; set; }

    public HashSet<int> SpriteIndices { get; } = [];

    public bool HasPixelSelection => Mode == SelectionMode.Pixels && PixelWidth > 0 && PixelHeight > 0;
    public bool HasSpriteSelection => Mode == SelectionMode.Sprites && SpriteIndices.Count > 0;

    public void SetPixelRect(int x, int y, int x1, int y1, byte[]? buffer = null)
    {
        Mode = SelectionMode.Pixels;
        SpriteIndices.Clear();
        PixelX = Math.Min(x, x1);
        PixelY = Math.Min(y, y1);
        PixelWidth = Math.Abs(x1 - x) + 1;
        PixelHeight = Math.Abs(y1 - y) + 1;
        PixelBuffer = buffer;
    }

    public void SetSprites(IEnumerable<int> indices)
    {
        Mode = SelectionMode.Sprites;
        PixelBuffer = null;
        PixelWidth = PixelHeight = 0;
        SpriteIndices.Clear();
        foreach (var i in indices)
            SpriteIndices.Add(i);
    }

    public void AddSprite(int index)
    {
        Mode = SelectionMode.Sprites;
        SpriteIndices.Add(index);
        PixelBuffer = null;
        PixelWidth = PixelHeight = 0;
    }

    public void Clear()
    {
        Mode = SelectionMode.None;
        PixelBuffer = null;
        PixelWidth = PixelHeight = 0;
        SpriteIndices.Clear();
    }

    public bool ContainsLocalPixel(int x, int y) =>
        HasPixelSelection &&
        x >= PixelX && x < PixelX + PixelWidth &&
        y >= PixelY && y < PixelY + PixelHeight;
}
