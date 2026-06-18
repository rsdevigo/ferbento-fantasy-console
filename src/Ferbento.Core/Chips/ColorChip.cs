using Ferbento.Core.Memory;
using Ferbento.Profiles;
using Ferbento.Profiles.Models;

namespace Ferbento.Core.Chips;

public sealed class ColorChip : IChip
{
    public const int PalettePersistMagic = 0x50414C00; // "PAL\0"
    public const byte PaletteNamesMarker = 0x4E; // 'N'
    public const int MaxColorNameLength = 32;

    public string Name => "Color";

    public static readonly uint[] Pico8Palette =
    [
        0x000000, 0x1D2B53, 0x7E2553, 0x008751,
        0xAB5236, 0x5F574F, 0xC2C3C7, 0xFFF1E8,
        0xFF004D, 0xFFA300, 0xFFEC27, 0x00E436,
        0x29ADFF, 0x83769C, 0xFF77A8, 0xFFCCAA
    ];

    private ChipBus _bus = null!;
    private uint[] _colors = [];
    private string[] _names = [];
    private string _profileName = "Pico-8";

    public int ColorCount { get; private set; } = 16;
    public int TransparentIndex { get; private set; }

    public IReadOnlyList<uint> Colors => _colors;

    public void Configure(HardwareProfile profile)
    {
        _profileName = profile.Name;
        ColorCount = profile.Palette.Colors;
        TransparentIndex = profile.Palette.TransparentIndex;

        var hex = profile.Palette.DefaultColors
            ?? PaletteDefaults.ForProfile(profile.Name, ColorCount);
        _colors = PaletteDefaults.ToRgbArray(hex.Take(ColorCount));

        if (_colors.Length < ColorCount)
        {
            var expanded = new uint[ColorCount];
            Array.Copy(_colors, expanded, _colors.Length);
            for (var i = _colors.Length; i < ColorCount; i++)
                expanded[i] = (uint)(i * 255 / System.Math.Max(1, ColorCount - 1)) * 0x010101;
            _colors = expanded;
        }

        ResetNamesToProfileDefaults();
    }

    public void Initialize(ChipBus bus)
    {
        _bus = bus;
        TryLoadFromMemory();
    }

    public void Reset()
    {
        for (var i = 0; i < _colors.Length; i++)
            _colors[i] = i < Pico8Palette.Length ? Pico8Palette[i] : (uint)i * 0x010101;
        ResetNamesToProfileDefaults();
        PersistToMemory();
    }

    public void Tick() { }

    public uint GetColor(int index)
    {
        if (_colors.Length == 0) return 0;
        index = System.Math.Clamp(index, 0, _colors.Length - 1);
        return _colors[index];
    }

    public string GetColorName(int index)
    {
        if (_names.Length == 0) return $"color-{index}";
        index = System.Math.Clamp(index, 0, _names.Length - 1);
        return _names[index];
    }

    public void SetColor(int index, uint rgb)
    {
        if (index < 0 || index >= _colors.Length) return;
        _colors[index] = rgb & 0xFFFFFF;
        PersistToMemory();
    }

    public void SetColorName(int index, string name)
    {
        if (index < 0 || index >= _names.Length) return;
        _names[index] = SanitizeName(name);
        PersistToMemory();
    }

    public void ResetToProfileDefaults(HardwareProfile profile)
    {
        Configure(profile);
        PersistToMemory();
    }

    public void ResetToPico8Canonical()
    {
        for (var i = 0; i < _colors.Length; i++)
            _colors[i] = i < Pico8Palette.Length ? Pico8Palette[i] : 0;
        ResetNamesToPico8Canonical();
        PersistToMemory();
    }

    public void LoadFromMemory() => TryLoadFromMemory();

    private void ResetNamesToProfileDefaults()
    {
        _names = PaletteDefaults.DefaultNamesForProfile(_profileName, ColorCount);
    }

    private void ResetNamesToPico8Canonical()
    {
        EnsureNamesArray();
        for (var i = 0; i < _names.Length; i++)
        {
            _names[i] = i < PaletteDefaults.Pico8Names.Length
                ? PaletteDefaults.Pico8Names[i]
                : $"color-{i}";
        }
    }

    private void EnsureNamesArray()
    {
        if (_names.Length == ColorCount) return;
        var defaults = PaletteDefaults.DefaultNamesForProfile(_profileName, ColorCount);
        _names = defaults;
    }

    private static string SanitizeName(string name)
    {
        name = name.Trim();
        if (name.Length == 0) return "unnamed";
        return name.Length <= MaxColorNameLength ? name : name[..MaxColorNameLength];
    }

    private bool CanPersistInMemory()
    {
        var required = NamesPersistOffset() + 1 + _colors.Length * (1 + MaxColorNameLength);
        return required <= _bus.Memory.Memory.Size;
    }

    private int NamesPersistOffset() => MemoryMap.UserDataStart + 6 + _colors.Length * 3;

    private void TryLoadFromMemory()
    {
        if (!CanPersistInMemory()) return;

        var mem = _bus.Memory.Memory;
        var magic = mem.Peek4(MemoryMap.UserDataStart);
        if (magic != PalettePersistMagic) return;

        var count = mem.Peek2(MemoryMap.UserDataStart + 4);
        if (count <= 0 || count > ColorCount) return;

        var offset = MemoryMap.UserDataStart + 6;
        for (var i = 0; i < count && i < _colors.Length; i++)
        {
            var r = mem.Peek(offset++);
            var g = mem.Peek(offset++);
            var b = mem.Peek(offset++);
            _colors[i] = (uint)(r << 16 | g << 8 | b);
        }

        TryLoadNamesFromMemory(offset);
    }

    private void TryLoadNamesFromMemory(int offset)
    {
        var mem = _bus.Memory.Memory;
        if (offset >= mem.Size || mem.Peek(offset) != PaletteNamesMarker) return;

        offset++;
        EnsureNamesArray();

        for (var i = 0; i < _names.Length && offset < mem.Size; i++)
        {
            var rawLen = mem.Peek(offset++);
            if (rawLen == 0)
            {
                _names[i] = PaletteDefaults.DefaultNamesForProfile(_profileName, ColorCount)[i];
                continue;
            }

            var nameLen = System.Math.Min((int)rawLen, MaxColorNameLength);
            if (offset + nameLen > mem.Size) break;

            var chars = new char[nameLen];
            for (var c = 0; c < nameLen; c++)
                chars[c] = (char)mem.Peek(offset++);
            _names[i] = SanitizeName(new string(chars));
        }
    }

    private void PersistToMemory()
    {
        if (!CanPersistInMemory()) return;

        var mem = _bus.Memory.Memory;
        mem.Poke4(MemoryMap.UserDataStart, PalettePersistMagic);
        mem.Poke2(MemoryMap.UserDataStart + 4, (ushort)_colors.Length);

        var offset = MemoryMap.UserDataStart + 6;
        foreach (var rgb in _colors)
        {
            mem.Poke(offset++, (byte)((rgb >> 16) & 0xFF));
            mem.Poke(offset++, (byte)((rgb >> 8) & 0xFF));
            mem.Poke(offset++, (byte)(rgb & 0xFF));
        }

        EnsureNamesArray();
        mem.Poke(offset++, PaletteNamesMarker);
        foreach (var name in _names)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(SanitizeName(name));
            var len = System.Math.Min(bytes.Length, MaxColorNameLength);
            mem.Poke(offset++, (byte)len);
            for (var i = 0; i < len; i++)
                mem.Poke(offset++, bytes[i]);
        }
    }
}
