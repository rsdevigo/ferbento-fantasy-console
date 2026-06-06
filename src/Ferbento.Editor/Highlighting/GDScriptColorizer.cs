using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Ferbento.Editor.Highlighting;

public sealed partial class GDScriptColorizer : DocumentColorizingTransformer
{
    private static readonly SolidColorBrush CommentBrush = new(Color.Parse("#6A9955"));
    private static readonly SolidColorBrush KeywordBrush = new(Color.Parse("#569CD6"));
    private static readonly SolidColorBrush ApiBrush = new(Color.Parse("#4EC9B0"));
    private static readonly SolidColorBrush NumberBrush = new(Color.Parse("#B5CEA8"));
    private static readonly SolidColorBrush StringBrush = new(Color.Parse("#CE9178"));

    private static readonly HashSet<string> Keywords =
    [
        "func", "var", "const", "if", "elif", "else", "for", "while", "return",
        "class", "pass", "break", "continue", "match", "when", "await", "yield",
        "and", "or", "not", "true", "false", "null",
    ];

    private static readonly HashSet<string> ApiWords =
    [
        "cls", "pset", "pget", "color", "line", "rect", "rectfill", "circ", "circfill",
        "spr", "sspr", "sget", "sset", "map", "mget", "mset", "pal", "palt", "camera",
        "clip", "flip", "sfx", "music", "btn", "btnp", "peek", "poke", "sin", "cos",
        "rnd", "srand", "flr", "abs", "min", "max", "printh", "stat", "time", "type",
    ];

    [GeneratedRegex(@"#.*")]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"""[^""\\]*(?:\\.[^""\\]*)*""|'[^'\\]*(?:\\.[^'\\]*)*'")]
    private static partial Regex StringRegex();

    [GeneratedRegex(@"\b\d+(\.\d+)?\b")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\b[A-Za-z_][A-Za-z0-9_]*\b")]
    private static partial Regex WordRegex();

    protected override void ColorizeLine(DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line);
        if (text.Length == 0)
            return;

        ApplyMatches(line, CommentRegex(), CommentBrush);
        ApplyMatches(line, StringRegex(), StringBrush);
        ApplyMatches(line, NumberRegex(), NumberBrush);

        var commentIndex = text.IndexOf('#');
        foreach (Match match in WordRegex().Matches(text))
        {
            if (commentIndex >= 0 && match.Index >= commentIndex)
                continue;

            var word = match.Value;
            var brush = Keywords.Contains(word) ? KeywordBrush
                : ApiWords.Contains(word) ? ApiBrush
                : null;

            if (brush == null)
                continue;

            ChangeLinePart(
                line.Offset + match.Index,
                line.Offset + match.Index + match.Length,
                element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }

    private void ApplyMatches(DocumentLine line, Regex regex, IBrush brush)
    {
        var text = CurrentContext.Document.GetText(line);
        foreach (Match match in regex.Matches(text))
        {
            if (match.Length == 0)
                continue;

            ChangeLinePart(
                line.Offset + match.Index,
                line.Offset + match.Index + match.Length,
                element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }
}
