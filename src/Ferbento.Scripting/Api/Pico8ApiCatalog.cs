using System.Collections.Frozen;
using System.Text.Json;
using Ferbento.Core.Chips;
using Ferbento.Scripting.Sandbox;

namespace Ferbento.Scripting.Api;

/// <summary>A function of the PICO-8 API, as listed in <c>pico8-api.json</c>.</summary>
public sealed record ApiFunction(string Name, string Category, string Signature);

/// <summary>
/// The PICO-8 API, read from <c>Api/pico8-api.json</c> (embedded resource). It is the single list used
/// by the static analysis (warnings about missing functions) and by the editor's autocomplete.
/// </summary>
public static class Pico8ApiCatalog
{
    private static readonly Lazy<IReadOnlyList<ApiFunction>> LazyFunctions = new(Load);

    public static IReadOnlyList<ApiFunction> Functions => LazyFunctions.Value;

    private static IReadOnlyList<ApiFunction> Load()
    {
        using var stream = typeof(Pico8ApiCatalog).Assembly.GetManifestResourceStream("Ferbento.Scripting.Api.pico8-api.json")
            ?? throw new InvalidOperationException("pico8-api.json is missing from the assembly resources.");
        using var document = JsonDocument.Parse(stream);
        return [.. document.RootElement.GetProperty("functions").EnumerateArray().Select(f => new ApiFunction(
            f.GetProperty("name").GetString()!,
            f.GetProperty("category").GetString()!,
            f.GetProperty("signature").GetString()!))];
    }
}

/// <summary>
/// Which API functions exist for a game: the ones the console defines (<see cref="Known"/>) and,
/// among them, the ones Ferbento already implements (<see cref="Implemented"/>). Profiles may
/// restrict the API in the future, which is why this is a value and not a global list.
/// </summary>
public sealed record ApiAvailability(IReadOnlySet<string> Known, IReadOnlySet<string> Implemented)
{
    private static readonly Lazy<ApiAvailability> LazyPico8 = new(CreatePico8);

    /// <summary>The PICO-8 profile: the catalog plus whatever the runtime registers.</summary>
    public static ApiAvailability Pico8 => LazyPico8.Value;

    private static ApiAvailability CreatePico8()
    {
        var implemented = new ScriptSandbox(new ChipBus()).Globals.Names.ToFrozenSet(StringComparer.Ordinal);
        var known = Pico8ApiCatalog.Functions.Select(f => f.Name).Concat(implemented).ToFrozenSet(StringComparer.Ordinal);
        return new ApiAvailability(known, implemented);
    }
}
