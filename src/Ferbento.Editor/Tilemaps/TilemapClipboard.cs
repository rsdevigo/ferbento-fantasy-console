namespace Ferbento.Editor.Tilemaps;

public sealed class TilemapClipboard
{
    public byte[]? Data { get; private set; }
    public bool[]? Placed { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public bool HasData => Data is not null && Placed is not null && Width > 0 && Height > 0;

    public void Set(byte[] data, bool[] placed, int width, int height)
    {
        if (data.Length != width * height || placed.Length != width * height)
            throw new ArgumentException("Clipboard data/placed size must match width * height.");

        Data = (byte[])data.Clone();
        Placed = (bool[])placed.Clone();
        Width = width;
        Height = height;
    }

    public void Clear()
    {
        Data = null;
        Placed = null;
        Width = 0;
        Height = 0;
    }
}
