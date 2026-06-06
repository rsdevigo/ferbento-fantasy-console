using Ferbento.Profiles.Models;

namespace Ferbento.Core.Chips;

public sealed class ChipBus
{
    public HardwareProfile Profile { get; }

    public MemoryChip Memory { get; }
    public ColorChip Color { get; }
    public SpriteChip Sprite { get; }
    public TilemapChip Tilemap { get; }
    public DisplayChip Display { get; }
    public SoundChip Sound { get; }
    public MusicChip Music { get; }
    public InputChip Input { get; }

    private readonly IChip[] _chips;

    public ChipBus(HardwareProfile? profile = null)
    {
        Profile = profile ?? HardwareProfile.Pico8();

        Memory = new MemoryChip();
        Color = new ColorChip();
        Sprite = new SpriteChip();
        Tilemap = new TilemapChip();
        Display = new DisplayChip();
        Sound = new SoundChip();
        Music = new MusicChip();
        Input = new InputChip();

        _chips = [Memory, Color, Sprite, Tilemap, Display, Sound, Music, Input];

        Memory.Configure(Profile);
        Color.Configure(Profile);
        Sprite.Configure(Profile);
        Tilemap.Configure(Profile);
        Display.Configure(Profile);
        Sound.Configure(Profile);
        Music.Configure(Profile);

        foreach (var chip in _chips)
            chip.Initialize(this);
    }

    public void Reset()
    {
        foreach (var chip in _chips)
            chip.Reset();
    }

    public void Tick()
    {
        foreach (var chip in _chips)
            chip.Tick();
    }

    public T GetChip<T>() where T : class, IChip =>
        _chips.OfType<T>().First();
}
