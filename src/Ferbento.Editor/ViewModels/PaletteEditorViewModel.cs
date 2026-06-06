using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ferbento.Core.Chips;
using Ferbento.Core.Rendering;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.ViewModels;

public sealed class PaletteEditorViewModel : INotifyPropertyChanged
{
    private ChipBus? _bus;
    private DrawState? _drawState;
    private HardwareProfile _profile = HardwareProfile.Pico8();

    private int _selectedIndex;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            _selectedIndex = Math.Clamp(value, 0, Math.Max(0, ColorCount - 1));
            OnPropertyChanged();
            OnPropertyChanged(nameof(ColorName));
            OnPropertyChanged(nameof(ColorHex));
            OnPropertyChanged(nameof(ColorRgb));
        }
    }

    public int ColorCount => _bus?.Color.ColorCount ?? _profile.Palette.Colors;
    public int TransparentIndex => _bus?.Color.TransparentIndex ?? _profile.Palette.TransparentIndex;

    public string ColorName => Pico8Colors.ColorName(SelectedIndex);
    public string ColorHex => Pico8Colors.ToHex(SelectedIndex, _bus?.Color);

    public string ColorRgb
    {
        get
        {
            var rgb = _bus?.Color.GetColor(SelectedIndex) ?? ColorChip.Pico8Palette[SelectedIndex & 0xF];
            return $"RGB({(rgb >> 16) & 0xFF}, {(rgb >> 8) & 0xFF}, {rgb & 0xFF})";
        }
    }

    public List<bool> Transparency { get; } = [];
    public List<int> DrawRemap { get; } = [];
    public List<int> ScreenRemap { get; } = [];

    public event Action? Edited;

    public void SetProfile(HardwareProfile profile)
    {
        _profile = profile;
        EnsureLists();
        OnPropertyChanged(nameof(ColorCount));
        OnPropertyChanged(nameof(TransparentIndex));
        SelectedIndex = Math.Min(SelectedIndex, Math.Max(0, ColorCount - 1));
    }

    public void SetChipBus(ChipBus bus)
    {
        _bus = bus;
        _drawState = bus.Display.DrawState;
        EnsureLists();
        SyncFromDrawState();
        OnPropertyChanged(nameof(ColorCount));
        OnPropertyChanged(nameof(ColorHex));
        OnPropertyChanged(nameof(ColorRgb));
    }

    private void EnsureLists()
    {
        var count = ColorCount;
        while (Transparency.Count < count) Transparency.Add(false);
        while (Transparency.Count > count) Transparency.RemoveAt(Transparency.Count - 1);
        while (DrawRemap.Count < count) DrawRemap.Add(DrawRemap.Count);
        while (DrawRemap.Count > count) DrawRemap.RemoveAt(DrawRemap.Count - 1);
        while (ScreenRemap.Count < count) ScreenRemap.Add(ScreenRemap.Count);
        while (ScreenRemap.Count > count) ScreenRemap.RemoveAt(ScreenRemap.Count - 1);
        if (Transparency.Count > TransparentIndex)
            Transparency[TransparentIndex] = true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void SyncFromDrawState()
    {
        if (_drawState == null) return;
        EnsureLists();

        for (var i = 0; i < ColorCount; i++)
        {
            DrawRemap[i] = _drawState.DrawPalette[i];
            ScreenRemap[i] = _drawState.ScreenPalette[i];
            Transparency[i] = _drawState.PaletteTransparent[i];
        }
        OnPropertyChanged(nameof(DrawRemap));
        OnPropertyChanged(nameof(ScreenRemap));
        OnPropertyChanged(nameof(Transparency));
    }

    public void ApplyToDrawState()
    {
        if (_drawState == null) return;

        var remapSlots = Math.Min(ColorCount, _drawState.DrawPalette.Length);
        for (var i = 0; i < remapSlots; i++)
        {
            _drawState.DrawPalette[i] = ClampRemap(DrawRemap[i]);
            _drawState.ScreenPalette[i] = ClampRemap(ScreenRemap[i]);
        }

        for (var i = 0; i < Math.Min(ColorCount, _drawState.PaletteTransparent.Length); i++)
            _drawState.PaletteTransparent[i] = Transparency[i];
    }

    private int ClampRemap(int index) =>
        Math.Clamp(index, 0, Math.Max(0, ColorCount - 1));

    public void SetDrawRemap(int from, int to)
    {
        if (from < 0 || from >= DrawRemap.Count) return;
        DrawRemap[from] = ClampRemap(to);
        ApplyToDrawState();
        OnPropertyChanged(nameof(DrawRemap));
        NotifyEdited();
    }

    public void SetScreenRemap(int from, int to)
    {
        if (from < 0 || from >= ScreenRemap.Count) return;
        ScreenRemap[from] = ClampRemap(to);
        ApplyToDrawState();
        OnPropertyChanged(nameof(ScreenRemap));
        NotifyEdited();
    }

    public void SetTransparency(int index, bool transparent)
    {
        if (index < 0 || index >= Transparency.Count) return;
        Transparency[index] = transparent;
        ApplyToDrawState();
        OnPropertyChanged(nameof(Transparency));
        NotifyEdited();
    }

    public void SetColorRgb(int index, byte r, byte g, byte b)
    {
        if (_bus == null || index < 0 || index >= ColorCount) return;
        _bus.Color.SetColor(index, (uint)(r << 16 | g << 8 | b));
        if (index == SelectedIndex)
        {
            OnPropertyChanged(nameof(ColorHex));
            OnPropertyChanged(nameof(ColorRgb));
        }
        PaletteChanged?.Invoke();
        NotifyEdited();
    }

    public void SetColorHex(int index, string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6 || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return;
        SetColorRgb(index, (byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
    }

    public void ResetDrawPalette()
    {
        for (var i = 0; i < ColorCount; i++) DrawRemap[i] = i;
        ApplyToDrawState();
        OnPropertyChanged(nameof(DrawRemap));
        NotifyEdited();
    }

    public void ResetScreenPalette()
    {
        for (var i = 0; i < ColorCount; i++) ScreenRemap[i] = i;
        ApplyToDrawState();
        OnPropertyChanged(nameof(ScreenRemap));
        NotifyEdited();
    }

    public void ResetTransparency()
    {
        for (var i = 0; i < ColorCount; i++)
            Transparency[i] = i == TransparentIndex;
        ApplyToDrawState();
        OnPropertyChanged(nameof(Transparency));
        NotifyEdited();
    }

    public void ResetToProfileDefaults()
    {
        if (_bus == null) return;
        _bus.Color.ResetToProfileDefaults(_profile);
        PaletteChanged?.Invoke();
        OnPropertyChanged(nameof(ColorHex));
        OnPropertyChanged(nameof(ColorRgb));
        NotifyEdited();
    }

    public void ResetToPico8Canonical()
    {
        if (_bus == null) return;
        _bus.Color.ResetToPico8Canonical();
        PaletteChanged?.Invoke();
        OnPropertyChanged(nameof(ColorHex));
        OnPropertyChanged(nameof(ColorRgb));
        NotifyEdited();
    }

    private void NotifyEdited() => Edited?.Invoke();

    public uint PreviewColor(int index, bool useScreenPalette)
    {
        var mapped = useScreenPalette ? ScreenRemap[index] : DrawRemap[index];
        return _bus?.Color.GetColor(mapped) ?? ColorChip.Pico8Palette[mapped & 0xF];
    }

    public uint SwatchColor(int index) =>
        _bus?.Color.GetColor(index) ?? ColorChip.Pico8Palette[index & 0xF];

    public ColorChip? ColorChip => _bus?.Color;

    public event Action? PaletteChanged;
}
