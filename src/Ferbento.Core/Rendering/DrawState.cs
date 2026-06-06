namespace Ferbento.Core.Rendering;

public sealed class DrawState
{
    public int CameraX { get; set; }
    public int CameraY { get; set; }
    public int ClipX { get; set; }
    public int ClipY { get; set; }
    public int ClipW { get; set; } = 128;
    public int ClipH { get; set; } = 128;
    public bool ClipEnabled { get; set; }
    public int PenColor { get; set; } = 7;
    public int CursorX { get; set; }
    public int CursorY { get; set; }
    public ushort FillPattern { get; set; } = 0xFFFF;

    public readonly int[] DrawPalette = Enumerable.Range(0, 16).ToArray();
    public readonly int[] ScreenPalette = Enumerable.Range(0, 16).ToArray();
    public readonly bool[] PaletteTransparent = new bool[16];

    public DrawState()
    {
        PaletteTransparent[0] = true;
    }

    public void Reset()
    {
        CameraX = CameraY = 0;
        ClipX = ClipY = 0;
        ClipW = ClipH = 128;
        ClipEnabled = false;
        PenColor = 7;
        CursorX = CursorY = 0;
        FillPattern = 0xFFFF;
        for (var i = 0; i < 16; i++)
        {
            DrawPalette[i] = i;
            ScreenPalette[i] = i;
            PaletteTransparent[i] = i == 0;
        }
    }
}
