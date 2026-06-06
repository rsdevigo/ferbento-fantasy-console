using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ferbento.Editor.ViewModels;

public sealed class TilemapEditorViewModel : INotifyPropertyChanged
{
    private int _currentLayer;
    public int CurrentLayer
    {
        get => _currentLayer;
        set { _currentLayer = value; OnPropertyChanged(); }
    }

    private string _selectedTool = "pencil";
    public string SelectedTool
    {
        get => _selectedTool;
        set { _selectedTool = value; OnPropertyChanged(); }
    }

    private int _tileSize = 8;
    public int TileSize
    {
        get => _tileSize;
        set { _tileSize = value; OnPropertyChanged(); }
    }

    private bool _showGrid = true;
    public bool ShowGrid
    {
        get => _showGrid;
        set { _showGrid = value; OnPropertyChanged(); }
    }

    public TilemapEditorViewModel()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void FlipX() { }
    public void FlipY() { }
    public void Rotate() { }
    public void Stamp() { }
    public void FillMap() { }
}
