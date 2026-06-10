using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ferbento.Editor.Sprites;

public sealed class PaletteState : INotifyPropertyChanged
{
    private int _paletteCount = 16;
    private int _primaryColor;
    private int _secondaryColor = 1;

    public int PaletteCount
    {
        get => _paletteCount;
        set
        {
            _paletteCount = Math.Max(1, value);
            PrimaryColor = _primaryColor;
            SecondaryColor = _secondaryColor;
            OnPropertyChanged();
        }
    }

    public int PrimaryColor
    {
        get => _primaryColor;
        set
        {
            var max = Math.Max(0, _paletteCount - 1);
            _primaryColor = Math.Clamp(value, 0, max);
            OnPropertyChanged();
        }
    }

    public int SecondaryColor
    {
        get => _secondaryColor;
        set
        {
            var max = Math.Max(0, _paletteCount - 1);
            _secondaryColor = Math.Clamp(value, 0, max);
            OnPropertyChanged();
        }
    }

    public int SelectedColor
    {
        get => PrimaryColor;
        set => PrimaryColor = value;
    }

    public void Swap()
    {
        (_primaryColor, _secondaryColor) = (_secondaryColor, _primaryColor);
        OnPropertyChanged(nameof(PrimaryColor));
        OnPropertyChanged(nameof(SecondaryColor));
        OnPropertyChanged(nameof(SelectedColor));
    }

    public void NextColor(bool secondary = false)
    {
        var max = Math.Max(0, _paletteCount - 1);
        if (secondary)
            SecondaryColor = SecondaryColor >= max ? 0 : SecondaryColor + 1;
        else
            PrimaryColor = PrimaryColor >= max ? 0 : PrimaryColor + 1;
    }

    public void PrevColor(bool secondary = false)
    {
        var max = Math.Max(0, _paletteCount - 1);
        if (secondary)
            SecondaryColor = SecondaryColor <= 0 ? max : SecondaryColor - 1;
        else
            PrimaryColor = PrimaryColor <= 0 ? max : PrimaryColor - 1;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
