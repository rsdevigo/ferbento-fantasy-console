using Ferbento.Core.Memory;
using Ferbento.Core.Rendering;
using Ferbento.Profiles.Models;

namespace Ferbento.Core.Chips;

public sealed class DisplayChip : IChip
{
    public string Name => "Display";

    private ChipBus _bus = null!;
    private int _width = 128;
    private int _height = 128;

    public PixelBuffer Buffer { get; private set; } = null!;
    public PixelBuffer FrontBuffer { get; private set; } = null!;
    public DrawState DrawState { get; } = new();
    public Renderer Renderer { get; private set; } = null!;

    public void Configure(HardwareProfile profile)
    {
        _width = profile.Resolution.Width;
        _height = profile.Resolution.Height;
    }

    public void Initialize(ChipBus bus)
    {
        _bus = bus;
        Buffer = new PixelBuffer(_width, _height);
        FrontBuffer = new PixelBuffer(_width, _height);
        Renderer = new Renderer(Buffer, DrawState);
    }

    public void Reset()
    {
        DrawState.Reset();
        Buffer.Clear();
        FrontBuffer.Clear();
    }

    public void Tick() { }

    public void Flip()
    {
        ApplyScreenPalette();
        Buffer.CopyToMemory(_bus.Memory.Memory.Buffer, MemoryMap.ScreenStart);
        Array.Copy(Buffer.RawData, FrontBuffer.RawData, Buffer.RawData.Length);
    }

    private void ApplyScreenPalette()
    {
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var c = Buffer.GetPixel(x, y);
                var mapped = DrawState.ScreenPalette[c & 0xF];
                Buffer.SetPixel(x, y, mapped);
            }
        }
    }
}
