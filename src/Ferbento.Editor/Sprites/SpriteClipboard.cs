namespace Ferbento.Editor.Sprites;

public sealed class SpriteClipboard
{
    public byte[]? Pixels { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int Flags { get; private set; }
    public bool IsSpriteLevel { get; private set; }
    public List<int> SpriteIndices { get; } = [];

    public bool HasContent => Pixels != null && Pixels.Length > 0;

    public void CopyPixels(byte[] pixels, int width, int height, int flags = 0)
    {
        Pixels = (byte[])pixels.Clone();
        Width = width;
        Height = height;
        Flags = flags;
        IsSpriteLevel = false;
        SpriteIndices.Clear();
    }

    public void CopySprite(byte[] pixels, int tileSize, int flags)
    {
        Pixels = (byte[])pixels.Clone();
        Width = tileSize;
        Height = tileSize;
        Flags = flags;
        IsSpriteLevel = true;
        SpriteIndices.Clear();
    }

    public void CopySprites(IReadOnlyList<(int index, byte[] pixels, int flags)> sprites, int tileSize)
    {
        SpriteIndices.Clear();
        if (sprites.Count == 1)
        {
            CopySprite(sprites[0].pixels, tileSize, sprites[0].flags);
            SpriteIndices.Add(sprites[0].index);
            return;
        }

        IsSpriteLevel = true;
        Width = tileSize;
        Height = tileSize;
        Flags = 0;
        Pixels = sprites[0].pixels;
        foreach (var s in sprites)
            SpriteIndices.Add(s.index);
    }

    public void Clear()
    {
        Pixels = null;
        Width = Height = 0;
        Flags = 0;
        IsSpriteLevel = false;
        SpriteIndices.Clear();
    }
}
