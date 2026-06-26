using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Ferbento.Core.Chips;
using Ferbento.Editor.Music;
using Ferbento.Profiles.Models;
using Ferbento.Profiles.BuiltIn;

namespace Ferbento.Editor;

public sealed class EditorSession
{
    private readonly List<IProfileAwareEditor> _editors = [];
    private bool _isDirty;

    public HardwareProfile Profile { get; private set; }
    public ChipBus Bus { get; private set; }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (_isDirty == value) return;
            _isDirty = value;
            DirtyChanged?.Invoke();
        }
    }

    public string? ProjectPath { get; private set; }
    public string ProjectDisplayName =>
        ProjectPath != null ? Path.GetFileNameWithoutExtension(ProjectPath) : "Untitled";

    public Func<string>? GetSourceCode { get; set; }
    public Action<string>? SetSourceCode { get; set; }
    public Func<MusicProjectData?>? GetMusicProject { get; set; }
    public Action<MusicProjectData?>? SetMusicProject { get; set; }

    public event Action<HardwareProfile>? ProfileChanged;
    public event Action? DirtyChanged;

    public EditorSession(HardwareProfile? profile = null)
    {
        Profile = profile ?? BuiltInProfiles.Pico8();
        Bus = new ChipBus(Profile);
    }

    public void RegisterEditor(IProfileAwareEditor editor) => _editors.Add(editor);

    public void MarkDirty() => IsDirty = true;

    public void ClearDirty() => IsDirty = false;

    public void SetProfile(HardwareProfile profile) => ApplyProfile(profile);

    public void ApplyProfile(HardwareProfile profile, bool reloadEditors = true)
    {
        Profile = profile;
        Bus = new ChipBus(profile);
        if (reloadEditors)
            PropagateToEditors();
        ProfileChanged?.Invoke(profile);
    }

    public void PropagateToEditors()
    {
        foreach (var editor in _editors)
        {
            editor.SetChipBus(Bus);
            editor.SetProfile(Profile);
            editor.RefreshFromBus();
        }
    }

    public void RefreshEditorsFromBus()
    {
        foreach (var editor in _editors)
            editor.RefreshFromBus();
    }

    public async Task<bool> RequestProfileChangeAsync(HardwareProfile profile, Window owner)
    {
        if (ProfilesMatch(Profile, profile))
            return true;

        if (IsDirty)
        {
            var message =
                $"Switching hardware profile may resize memory and palette limits. " +
                $"Save changes to {ProjectDisplayName} before continuing?";
            var choice = await UnsavedChangesDialog.ShowAsync(owner, ProjectDisplayName, message);

            switch (choice)
            {
                case UnsavedChangesChoice.Save:
                    if (!await SaveProjectAsync(owner))
                        return false;
                    break;
                case UnsavedChangesChoice.DontSave:
                    break;
                case UnsavedChangesChoice.Cancel:
                default:
                    return false;
            }
        }

        ApplyProfile(profile);
        ClearDirty();
        return true;
    }

    public async Task<bool> SaveProjectAsync(Window owner)
    {
        if (ProjectPath == null)
            return await SaveProjectAsAsync(owner);
        return await SaveToPathAsync(ProjectPath);
    }

    public async Task<bool> SaveProjectAsAsync(Window owner)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Project",
            SuggestedFileName = $"{ProjectDisplayName}.fbp",
            DefaultExtension = "fbp",
            FileTypeChoices =
            [
                new FilePickerFileType("Ferbento Project") { Patterns = ["*.fbp"] },
            ],
        });

        if (file == null)
            return false;

        ProjectPath = file.Path.LocalPath;
        return await SaveToPathAsync(ProjectPath);
    }

    private async Task<bool> SaveToPathAsync(string path)
    {
        if (GetSourceCode == null)
            return false;

        var project = EditorProjectFile.FromSession(this, GetSourceCode());
        await EditorProjectFile.SaveAsync(path, project);
        ClearDirty();
        return true;
    }

    public async Task<bool> LoadProjectAsync(string path)
    {
        var project = await EditorProjectFile.LoadAsync(path);
        ProjectPath = path;
        project.ApplyToSession(this, out var source);
        SetSourceCode?.Invoke(source);
        ClearDirty();
        return true;
    }

    public void NewProject(HardwareProfile? profile = null)
    {
        ProjectPath = null;
        ApplyProfile(profile ?? BuiltInProfiles.Pico8());
        SetSourceCode?.Invoke("");
        ClearDirty();
    }

    private static bool ProfilesMatch(HardwareProfile a, HardwareProfile b) =>
        a.Name.Equals(b.Name, StringComparison.OrdinalIgnoreCase)
        && a.Palette.Colors == b.Palette.Colors
        && a.Sprites.SheetWidth == b.Sprites.SheetWidth
        && a.Sprites.SheetHeight == b.Sprites.SheetHeight
        && a.Map.Width == b.Map.Width
        && a.Map.Height == b.Map.Height;
}
