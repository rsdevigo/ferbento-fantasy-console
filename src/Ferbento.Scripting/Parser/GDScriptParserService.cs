using GDShrapt.Reader;

namespace Ferbento.Scripting.Parser;

public sealed class GDScriptParserService
{
    // GDShrapt.Reader keeps shared static state while resolving operators
    // (GDResolvingHelper / GDPatternResolver) and is not thread-safe: concurrent parses
    // corrupt each other (BUG-004). All parses in the process go through this lock.
    private static readonly object ParseLock = new();

    private readonly GDScriptReader _reader = new();

    public GDClassDeclaration? ParseClass(string source) => ParseScript(source);

    public List<GDStatement> ParseStatements(string source)
    {
        lock (ParseLock) return _reader.ParseStatements(source);
    }

    public GDClassDeclaration ParseScript(string source)
    {
        lock (ParseLock) return _reader.ParseFileContent(source);
    }

    public List<GDSyntaxToken> Tokenize(string source)
    {
        lock (ParseLock) return _reader.ParseUnspecifiedContent(source);
    }
}
