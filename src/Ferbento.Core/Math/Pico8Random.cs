namespace Ferbento.Core.Math;

/// <summary>
/// Pico-8 LFSR-based PRNG (reproduces rnd() sequence for same srand seed).
/// </summary>
public sealed class Pico8Random
{
    private uint _state = 0xDEADBEEF;

    public void Seed(int seed) => _state = unchecked((uint)seed);

    public FixedPoint Next(FixedPoint max)
    {
        _state = unchecked(_state * 1664525 + 1013904223);
        var raw = (int)(_state & 0xFFFF);
        if (max.Raw <= 0) return FixedPoint.FromInt(0);
        return FixedPoint.FromInt(raw % (max.ToInt() + 1));
    }

    public FixedPoint NextFraction()
    {
        _state = unchecked(_state * 1664525 + 1013904223);
        return new((int)(_state & 0xFFFF) << 1);
    }
}
