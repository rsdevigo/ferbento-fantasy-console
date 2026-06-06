using Ferbento.Profiles.BuiltIn;
using Ferbento.Profiles.Models;

namespace Ferbento.Profiles.Validation;

public static class ProfileMatcher
{
    public static HardwareProfile? Match(string name)
    {
        return BuiltInProfiles.GetByName(name);
    }

    public static HardwareProfile? MatchByResolution(int width, int height)
    {
        return BuiltInProfiles.All.FirstOrDefault(p =>
            p.Resolution.Width == width && p.Resolution.Height == height);
    }

    public static HardwareProfile? MatchByMemory(int ram, int cartRom)
    {
        return BuiltInProfiles.All.FirstOrDefault(p =>
            p.Memory.Ram == ram && p.Memory.CartRom == cartRom);
    }

    public static string SuggestProfile(HardwareProfile profile)
    {
        var match = BuiltInProfiles.All.FirstOrDefault(b =>
            b.Resolution.Width == profile.Resolution.Width &&
            b.Resolution.Height == profile.Resolution.Height &&
            b.Palette.Colors == profile.Palette.Colors);

        if (match is not null)
            return match.Name;

        return "Custom";
    }
}
