using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
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
    private Color _pendingColor = Colors.Black;
    private string _pendingName = "black";
    private bool _hasPendingChange;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var clamped = Math.Clamp(value, 0, Math.Max(0, ColorCount - 1));
            if (_selectedIndex == clamped) return;
            _selectedIndex = clamped;
            LoadPendingFromSelected();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ColorName));
            OnPropertyChanged(nameof(CommittedName));
            OnPropertyChanged(nameof(CommittedHex));
            OnPropertyChanged(nameof(PendingName));
        }
    }

    public int ColorCount => _bus?.Color.ColorCount ?? _profile.Palette.Colors;
    public int TransparentIndex => _bus?.Color.TransparentIndex ?? _profile.Palette.TransparentIndex;

    public string ProfileName => _profile.Name;
    public bool CanEditColors => _profile.Palette.AllowColorEdit;
    public string EditStatusText => CanEditColors ? "Editing: enabled" : "Editing: disabled";

    public string ColorName => CommittedName;

    public string CommittedName =>
        _bus?.Color.GetColorName(SelectedIndex) ?? Pico8Colors.ColorName(SelectedIndex);

    public string PendingName
    {
        get => _pendingName;
        private set
        {
            _pendingName = value;
            OnPropertyChanged();
        }
    }

    public string CommittedHex => Pico8Colors.ToHex(SelectedIndex, _bus?.Color);

    public string PendingHex =>
        $"#{_pendingColor.R:X2}{_pendingColor.G:X2}{_pendingColor.B:X2}";

    public Color PendingColor
    {
        get => _pendingColor;
        private set
        {
            _pendingColor = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PendingHex));
        }
    }

    public bool HasPendingChange
    {
        get => _hasPendingChange;
        private set
        {
            if (_hasPendingChange == value) return;
            _hasPendingChange = value;
            OnPropertyChanged();
        }
    }

    public List<bool> Transparency { get; } = [];

    public event Action? Edited;
    public event Action? PaletteChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public ColorChip? ColorChip => _bus?.Color;

    public void SetProfile(HardwareProfile profile)
    {
        _profile = profile;
        EnsureLists();
        OnPropertyChanged(nameof(ColorCount));
        OnPropertyChanged(nameof(TransparentIndex));
        OnPropertyChanged(nameof(ProfileName));
        OnPropertyChanged(nameof(CanEditColors));
        OnPropertyChanged(nameof(EditStatusText));
        SelectedIndex = Math.Min(SelectedIndex, Math.Max(0, ColorCount - 1));
    }

    public void SetChipBus(ChipBus bus)
    {
        _bus = bus;
        _drawState = bus.Display.DrawState;
        EnsureLists();
        SyncFromDrawState();
        LoadPendingFromSelected();
        OnPropertyChanged(nameof(ColorCount));
        OnPropertyChanged(nameof(CommittedHex));
    }

    public void SyncFromDrawState()
    {
        if (_drawState == null) return;
        EnsureLists();

        for (var i = 0; i < ColorCount; i++)
            Transparency[i] = _drawState.PaletteTransparent[i];

        OnPropertyChanged(nameof(Transparency));
    }

    public void OnPickerColorChanged(Color color)
    {
        if (!CanEditColors) return;

        PendingColor = color;
        UpdatePendingChangeFlag();
    }

    public void OnPendingNameChanged(string name)
    {
        if (!CanEditColors) return;

        PendingName = name.Trim();
        UpdatePendingChangeFlag();
    }

    public void OnPendingHexChanged(string hex)
    {
        if (!CanEditColors) return;

        hex = hex.TrimStart('#');
        if (hex.Length != 6 || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return;

        OnPickerColorChanged(Color.FromRgb(
            (byte)((rgb >> 16) & 0xFF),
            (byte)((rgb >> 8) & 0xFF),
            (byte)(rgb & 0xFF)));
    }

    public void CommitPendingColor()
    {
        if (!CanEditColors || !HasPendingChange) return;

        var index = SelectedIndex;
        var colorChanged = !ColorsMatchPending(SwatchColor(index), _pendingColor);
        var nameChanged = !string.Equals(CommittedName, _pendingName, StringComparison.Ordinal);

        if (colorChanged)
            SetColorRgb(index, _pendingColor.R, _pendingColor.G, _pendingColor.B);

        if (nameChanged)
        {
            _bus?.Color.SetColorName(index, _pendingName);
            OnPropertyChanged(nameof(CommittedName));
            OnPropertyChanged(nameof(ColorName));
            PaletteChanged?.Invoke();
            NotifyEdited();
        }

        HasPendingChange = false;
        if (colorChanged)
            OnPropertyChanged(nameof(CommittedHex));
    }

    public string GetColorName(int index) =>
        _bus?.Color.GetColorName(index) ?? Pico8Colors.ColorName(index);

    public void SetTransparency(int index, bool transparent)
    {
        if (index < 0 || index >= Transparency.Count) return;
        Transparency[index] = transparent;
        ApplyTransparencyToDrawState(index, transparent);
        OnPropertyChanged(nameof(Transparency));
        NotifyEdited();
    }

    public void ResetToProfileDefaults()
    {
        if (_bus == null) return;
        _bus.Color.ResetToProfileDefaults(_profile);
        LoadPendingFromSelected();
        PaletteChanged?.Invoke();
        OnPropertyChanged(nameof(CommittedHex));
        OnPropertyChanged(nameof(CommittedName));
        NotifyEdited();
    }

    public void ResetToPico8Canonical()
    {
        if (_bus == null) return;
        _bus.Color.ResetToPico8Canonical();
        LoadPendingFromSelected();
        PaletteChanged?.Invoke();
        OnPropertyChanged(nameof(CommittedHex));
        OnPropertyChanged(nameof(CommittedName));
        NotifyEdited();
    }

    public Color CommittedColor() => ToAvaloniaColor(SwatchColor(SelectedIndex));

    public uint SwatchColor(int index) =>
        _bus?.Color.GetColor(index) ?? ColorChip.Pico8Palette[index & 0xF];

    private void EnsureLists()
    {
        var count = ColorCount;
        while (Transparency.Count < count) Transparency.Add(false);
        while (Transparency.Count > count) Transparency.RemoveAt(Transparency.Count - 1);
        if (Transparency.Count > TransparentIndex)
            Transparency[TransparentIndex] = true;
    }

    private void LoadPendingFromSelected()
    {
        PendingColor = CommittedColor();
        PendingName = CommittedName;
        HasPendingChange = false;
    }

    private void UpdatePendingChangeFlag()
    {
        var colorChanged = !ColorsMatchPending(SwatchColor(SelectedIndex), _pendingColor);
        var nameChanged = !string.Equals(CommittedName, _pendingName, StringComparison.Ordinal);
        HasPendingChange = colorChanged || nameChanged;
    }

    private void SetColorRgb(int index, byte r, byte g, byte b)
    {
        if (_bus == null || index < 0 || index >= ColorCount) return;
        _bus.Color.SetColor(index, (uint)(r << 16 | g << 8 | b));
        if (index == SelectedIndex)
            OnPropertyChanged(nameof(CommittedHex));
        PaletteChanged?.Invoke();
        NotifyEdited();
    }

    private void ApplyTransparencyToDrawState(int index, bool transparent)
    {
        if (_drawState == null || index < 0 || index >= _drawState.PaletteTransparent.Length) return;
        _drawState.PaletteTransparent[index] = transparent;
    }

    private static bool ColorsMatchPending(uint rgb, Color pending) =>
        ((rgb >> 16) & 0xFF) == pending.R
        && ((rgb >> 8) & 0xFF) == pending.G
        && (rgb & 0xFF) == pending.B;

    private static Color ToAvaloniaColor(uint rgb) =>
        Color.FromRgb(
            (byte)((rgb >> 16) & 0xFF),
            (byte)((rgb >> 8) & 0xFF),
            (byte)(rgb & 0xFF));

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void NotifyEdited() => Edited?.Invoke();
}
