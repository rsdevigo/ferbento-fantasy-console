namespace Ferbento.Scripting.Parser;

/// <summary>
/// Counts tokens of the GDScript source run by the current interpreter. It only exists so that
/// <c>ScriptSandbox</c> keeps its token limit until it moves to the Lua pipeline (story 012);
/// the PICO-8 count is <see cref="Ferbento.Scripting.Compiler.Parsing.Pico8TokenCounter"/>.
/// </summary>
public static class GdScriptTokenCounter
{
    public static int Count(string source)
    {
        var count = 0;
        var i = 0;

        while (i < source.Length)
        {
            var c = source[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
                continue;
            }

            if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) i++;
                i += 2;
                continue;
            }

            if (c == '#' && i + 1 < source.Length && source[i + 1] == ' ')
            {
                while (i < source.Length && source[i] != '\n') i++;
                continue;
            }

            if (c == '"' || c == '\'')
            {
                var quote = c;
                i++;
                while (i < source.Length && source[i] != quote)
                {
                    if (source[i] == '\\') i++;
                    i++;
                }
                if (i < source.Length) i++;
                count++;
                continue;
            }

            if (char.IsDigit(c) || (c == '.' && i + 1 < source.Length && char.IsDigit(source[i + 1])))
            {
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '.' || source[i] == 'x' || source[i] == 'X')) i++;
                count++;
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) i++;
                count++;
                continue;
            }

            var op = c switch
            {
                '+' or '-' or '*' or '/' or '%' or '^' or '~' or '!' or '=' or '<' or '>' or '&' or '|' or ':' or '@' or '$' => true,
                _ => false
            };
            if (op)
            {
                i++;
                if (i < source.Length && (c, source[i]) is ('=', '=') or ('!', '=') or ('<', '=') or ('>', '=') or ('<', '<') or ('>', '>') or (':', '=') or ('+', '=') or ('-', '=') or ('*', '=') or ('/', '=') or ('%', '=') or ('&', '=') or ('|', '=') or ('^', '=') or ('&', '&') or ('|', '|') or ('-', '>'))
                    i++;
                count++;
                continue;
            }

            if (c is '(' or ')' or '[' or ']' or '{' or '}' or ',' or ';' or '.')
            {
                i++;
                count++;
                continue;
            }

            i++;
        }

        return count;
    }
}
