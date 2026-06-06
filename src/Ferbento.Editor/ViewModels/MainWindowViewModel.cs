using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Ferbento.Editor.ViewModels;

public sealed class EditorTab : INotifyPropertyChanged
{
    private bool _isSelected;

    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Icon { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    public ObservableCollection<EditorTab> Tabs { get; } =
    [
        new() { Id = "code", Title = "Code", Icon = "⌨" },
        new() { Id = "sprites", Title = "Sprites", Icon = "🎨" },
        new() { Id = "tilemap", Title = "Tilemap", Icon = "🗺" },
        new() { Id = "palette", Title = "Palette", Icon = "🎭" },
        new() { Id = "sfx", Title = "SFX", Icon = "🔊" },
        new() { Id = "music", Title = "Music", Icon = "🎵" },
        new() { Id = "assets", Title = "Assets", Icon = "📁" },
        new() { Id = "profiles", Title = "Profiles", Icon = "⚙" },
        new() { Id = "debug", Title = "Debug", Icon = "🐞" },
        new() { Id = "project", Title = "Project", Icon = "📦" }
    ];

    private string _selectedTabId = "code";

    public string SelectedTabId
    {
        get => _selectedTabId;
        set
        {
            if (_selectedTabId == value) return;
            _selectedTabId = value;
            foreach (var tab in Tabs)
                tab.IsSelected = tab.Id == value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedTabTitle));
        }
    }

    public string SelectedTabTitle =>
        Tabs.FirstOrDefault(t => t.Id == _selectedTabId)?.Title ?? "Code";

    public MainWindowViewModel()
    {
        Tabs[0].IsSelected = true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
