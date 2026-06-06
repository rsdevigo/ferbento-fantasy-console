using System.Text.Json;
using Ferbento.Core.Chips;
using Ferbento.Profiles.BuiltIn;

namespace Ferbento.Editor;

public sealed class EditorProjectFile
{
    public int Version { get; set; } = 1;
    public string ProfileName { get; set; } = "Pico-8";
    public string Source { get; set; } = "";
    public string MemoryBase64 { get; set; } = "";

    public static EditorProjectFile FromSession(EditorSession session, string source)
    {
        var mem = session.Bus.Memory.Memory;
        var bytes = mem.Buffer.AsSpan(0, mem.Size).ToArray();
        return new EditorProjectFile
        {
            ProfileName = session.Profile.Name,
            Source = source,
            MemoryBase64 = Convert.ToBase64String(bytes),
        };
    }

    public void ApplyToSession(EditorSession session, out string source)
    {
        source = Source;
        var profile = BuiltInProfiles.GetByName(ProfileName) ?? session.Profile;
        session.ApplyProfile(profile, reloadEditors: false);

        if (!string.IsNullOrEmpty(MemoryBase64))
        {
            var bytes = Convert.FromBase64String(MemoryBase64);
            var mem = session.Bus.Memory.Memory;
            var copyLen = Math.Min(bytes.Length, mem.Size);
            Array.Copy(bytes, mem.Buffer, copyLen);
            session.Bus.Color.LoadFromMemory();
        }

        session.RefreshEditorsFromBus();
    }

    public static async Task SaveAsync(string path, EditorProjectFile project)
    {
        var json = JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    public static async Task<EditorProjectFile> LoadAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<EditorProjectFile>(json)
            ?? throw new InvalidDataException("Invalid project file.");
    }
}
