namespace Ferbento.Core.Memory;

/// <summary>
/// Pico-8 virtual RAM layout (32 kB address space).
/// </summary>
public static class MemoryMap
{
    public const int TotalSize = 0x8000;

    public const int GfxStart = 0x0000;
    public const int GfxEnd = 0x0FFF;

    public const int Gfx2Start = 0x1000;
    public const int Gfx2End = 0x1FFF;

    public const int MapStart = 0x2000;
    public const int MapEnd = 0x2FFF;

    public const int GfxPropsStart = 0x3000;
    public const int GfxPropsEnd = 0x30FF;

    public const int SongStart = 0x3100;
    public const int SongEnd = 0x31FF;

    public const int SfxStart = 0x3200;
    public const int SfxEnd = 0x41FF;

    public const int UserDataStart = 0x4200;
    public const int UserDataEnd = 0x4FFF;

    public const int CartDataStart = 0x5E00;
    public const int CartDataEnd = 0x5EFF;

    public const int DrawStateStart = 0x5F00;
    public const int DrawStateEnd = 0x5F3F;

    public const int HardwareStateStart = 0x5F40;
    public const int HardwareStateEnd = 0x5F7F;

    public const int GpioStart = 0x5F80;
    public const int GpioEnd = 0x5FFF;

    public const int ScreenStart = 0x6000;
    public const int ScreenEnd = 0x7FFF;

    public const int ScreenWidth = 128;
    public const int ScreenHeight = 128;
}
