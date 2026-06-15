namespace Ferbento.Editor.Tilemaps;

public sealed class TilemapClipboard
{
    public byte[]? Data { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public bool HasData => Data is not null && Width > 0 && Height > 0;

    public void Set(byte[] data, int width, int height)
    {
        Data = data;
        Width = width;
        Height = height;
    }

    public void Clear()
    {
        Data = null;
        Width = 0;
        Height = 0;
    }
}
