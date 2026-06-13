using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ferbento.Editor.Sprites;

public sealed class ViewportState : INotifyPropertyChanged
{
    private int _zoom = 16;
    private int _panX;
    private int _panY;
    private bool _gridVisible = true;
    private bool _fullscreen;
    private bool _hexView;

    public int Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Clamp(value, 1, 64);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ZoomPercent));
        }
    }

    public int ZoomPercent => _zoom * 100 / 16;

    public int PanX
    {
        get => _panX;
        set { _panX = value; OnPropertyChanged(); }
    }

    public int PanY
    {
        get => _panY;
        set { _panY = value; OnPropertyChanged(); }
    }

    public bool GridVisible
    {
        get => _gridVisible;
        set { _gridVisible = value; OnPropertyChanged(); }
    }

    public bool Fullscreen
    {
        get => _fullscreen;
        set { _fullscreen = value; OnPropertyChanged(); }
    }

    public bool HexView
    {
        get => _hexView;
        set { _hexView = value; OnPropertyChanged(); }
    }

    public void AdjustZoom(int delta, int? centerX = null, int? centerY = null)
    {
        var oldZoom = Zoom;
        Zoom = Zoom + delta;
        if (centerX.HasValue && centerY.HasValue && oldZoom != Zoom)
        {
            var ratio = (double)Zoom / oldZoom;
            PanX = (int)(centerX.Value - (centerX.Value - PanX) * ratio);
            PanY = (int)(centerY.Value - (centerY.Value - PanY) * ratio);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
