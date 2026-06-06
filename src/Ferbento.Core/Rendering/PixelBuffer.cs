using Ferbento.Core.Memory;

namespace Ferbento.Core.Rendering;

public sealed class PixelBuffer
{
    private readonly byte[] _data;
    public int Width { get; }
    public int Height { get; }

    public PixelBuffer(int width = MemoryMap.ScreenWidth, int height = MemoryMap.ScreenHeight)
    {
        Width = width;
        Height = height;
        _data = new byte[width * height / 2];
    }

    public void Clear(int color = 0)
    {
        var packed = (byte)((color & 0xF) | ((color & 0xF) << 4));
        Array.Fill(_data, packed);
    }

    public int GetPixel(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;
        var idx = (y * Width + x) >> 1;
        var hi = (x & 1) == 1;
        var b = _data[idx];
        return hi ? (b >> 4) & 0xF : b & 0xF;
    }

    public void SetPixel(int x, int y, int color)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        var idx = (y * Width + x) >> 1;
        var hi = (x & 1) == 1;
        color &= 0xF;
        if (hi)
            _data[idx] = (byte)((_data[idx] & 0x0F) | (color << 4));
        else
            _data[idx] = (byte)((_data[idx] & 0xF0) | color);
    }

    public void CopyToMemory(byte[] memory, int offset)
    {
        Array.Copy(_data, 0, memory, offset, _data.Length);
    }

    public void LoadFromMemory(byte[] memory, int offset)
    {
        Array.Copy(memory, offset, _data, 0, _data.Length);
    }

    public byte[] RawData => _data;
}
