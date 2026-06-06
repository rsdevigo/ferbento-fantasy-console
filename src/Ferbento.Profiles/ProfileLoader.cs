using System.Text.Json;
using Ferbento.Profiles.Models;

namespace Ferbento.Profiles;

public static class ProfileLoader
{
    public static HardwareProfile LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<HardwareProfile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize profile");
    }

    public static HardwareProfile? TryLoadFromFile(string path)
    {
        try { return LoadFromFile(path); }
        catch { return null; }
    }

    public static List<HardwareProfile> LoadAllFromDirectory(string directory)
    {
        var profiles = new List<HardwareProfile>();
        if (!Directory.Exists(directory)) return profiles;

        foreach (var file in Directory.GetFiles(directory, "*.json"))
        {
            var profile = TryLoadFromFile(file);
            if (profile is not null)
                profiles.Add(profile);
        }

        return profiles;
    }
}
