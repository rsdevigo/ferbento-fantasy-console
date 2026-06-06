using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Ferbento.Editor.ViewModels;

public sealed class SpriteEditorViewModel : INotifyPropertyChanged
{
    public ObservableCollection<ColorSwatch> Palette { get; } = [];
    public UndoRedoStack UndoRedo { get; } = new(256);

    private byte[] _spriteData = new byte[64];

    public bool CanUndo => UndoRedo.UndoCount > 0;
    public bool CanRedo => UndoRedo.RedoCount > 0;

    private int _selectedColor;
    public int SelectedColor
    {
        get => _selectedColor;
        set { _selectedColor = value; OnPropertyChanged(); }
    }

    private string _selectedTool = "pencil";
    public string SelectedTool
    {
        get => _selectedTool;
        set { _selectedTool = value; OnPropertyChanged(); }
    }

    private int _currentSpriteIndex;
    public int CurrentSpriteIndex
    {
        get => _currentSpriteIndex;
        set { _currentSpriteIndex = value; OnPropertyChanged(); }
    }

    private bool _showGrid = true;
    public bool ShowGrid
    {
        get => _showGrid;
        set { _showGrid = value; OnPropertyChanged(); }
    }

    private bool _flipX;
    public bool FlipX
    {
        get => _flipX;
        set { _flipX = value; OnPropertyChanged(); }
    }

    private bool _flipY;
    public bool FlipY
    {
        get => _flipY;
        set { _flipY = value; OnPropertyChanged(); }
    }

    private byte[]? _clipboard;
    public byte[]? Clipboard
    {
        get => _clipboard;
        set { _clipboard = value; OnPropertyChanged(); }
    }

    public SpriteEditorViewModel()
    {
        for (var i = 0; i < 16; i++)
            Palette.Add(new ColorSwatch { Index = i });
        UndoRedo.StateChanged += () =>
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void FlipCurrentSprite()
    {
        UndoRedo.Push("flip sprite", _spriteData, 0, 64);
    }

    public void CopySprite()
    {
        Clipboard = new byte[64];
    }

    public void PasteSprite()
    {
        if (Clipboard == null) return;
        UndoRedo.Push("paste sprite", _spriteData, 0, 64);
    }

    public void FillSprite(int color)
    {
        UndoRedo.Push("fill sprite", _spriteData, 0, 64);
    }

    public void PickColor(int x, int y)
    {
    }
}

public sealed class ColorSwatch : INotifyPropertyChanged
{
    public int Index { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
}
