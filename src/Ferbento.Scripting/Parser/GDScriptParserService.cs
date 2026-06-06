using GDShrapt.Reader;

namespace Ferbento.Scripting.Parser;

public sealed class GDScriptParserService
{
    private readonly GDScriptReader _reader = new();

    public GDClassDeclaration? ParseClass(string source) => _reader.ParseFileContent(source);

    public List<GDStatement> ParseStatements(string source) => _reader.ParseStatements(source);

    public GDClassDeclaration ParseScript(string source) => _reader.ParseFileContent(source);

    public List<GDSyntaxToken> Tokenize(string source) => _reader.ParseUnspecifiedContent(source);
}
