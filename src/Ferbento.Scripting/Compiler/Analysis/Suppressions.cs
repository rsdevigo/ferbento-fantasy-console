using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Ferbento.Scripting.Compiler.Diagnostics;
using Ferbento.Scripting.Compiler.Parsing;

namespace Ferbento.Scripting.Compiler.Analysis;

/// <summary>
/// <c>-- fb-ignore: FB0250, FB0251</c> (or <c>// fb-ignore: …</c>) silences those warnings; without codes it
/// silences every warning. At the end of a line it applies to that line; alone on its line, to the next one.
/// Errors cannot be silenced.
/// </summary>
internal sealed partial class Suppressions
{
    private readonly Dictionary<int, HashSet<string>?> _byLine = [];

    [GeneratedRegex(@"\bfb-ignore\b(?:\s*:\s*(?<codes>FB\d{4}(?:\s*,\s*FB\d{4})*))?", RegexOptions.IgnoreCase)]
    private static partial Regex IgnorePattern();

    public static Suppressions FromComments(ImmutableArray<LuaComment> comments)
    {
        var suppressions = new Suppressions();
        if (comments.IsDefault) return suppressions;

        foreach (var comment in comments)
        {
            var match = IgnorePattern().Match(comment.Text);
            if (!match.Success) continue;

            var lastLine = comment.Span.Line + comment.Text.Count(c => c == '\n');
            var line = comment.OnOwnLine ? lastLine + 1 : comment.Span.Line;
            var codes = match.Groups["codes"].Success
                ? match.Groups["codes"].Value.Split(',', StringSplitOptions.TrimEntries).Select(c => c.ToUpperInvariant())
                : null;
            suppressions.Add(line, codes);
        }
        return suppressions;
    }

    private void Add(int line, IEnumerable<string>? codes)
    {
        if (codes is null)
        {
            _byLine[line] = null;
            return;
        }
        if (_byLine.TryGetValue(line, out var existing))
        {
            existing?.UnionWith(codes);
            return;
        }
        _byLine[line] = [.. codes];
    }

    public bool Silences(Diagnostic diagnostic) =>
        _byLine.TryGetValue(diagnostic.Span.Line, out var codes) && (codes is null || codes.Contains(diagnostic.Code));
}
