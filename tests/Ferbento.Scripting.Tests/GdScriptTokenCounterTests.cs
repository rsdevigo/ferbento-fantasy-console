using Ferbento.Scripting.Parser;

namespace Ferbento.Scripting.Tests;

public class GdScriptTokenCounterTests
{
    [Fact]
    public void EmptySource_ZeroTokens()
    {
        Assert.Equal(0, GdScriptTokenCounter.Count(""));
    }

    [Fact]
    public void OnlyWhitespace_ZeroTokens()
    {
        Assert.Equal(0, GdScriptTokenCounter.Count("   \n  \t  "));
    }

    [Fact]
    public void OnlyComment_ZeroTokens()
    {
        Assert.Equal(0, GdScriptTokenCounter.Count("// this is a comment"));
    }

    [Fact]
    public void BlockComment_ZeroTokens()
    {
        Assert.Equal(0, GdScriptTokenCounter.Count("/* block comment */"));
    }

    [Fact]
    public void GDScriptComment_ZeroTokens()
    {
        Assert.Equal(0, GdScriptTokenCounter.Count("# this is a gdscript comment"));
    }

    [Fact]
    public void Identifiers_Counted()
    {
        Assert.Equal(3, GdScriptTokenCounter.Count("foo bar baz"));
    }

    [Fact]
    public void KeywordsAndSymbols_CountedAsTokens()
    {
        Assert.Equal(2, GdScriptTokenCounter.Count("func _init"));
    }

    [Fact]
    public void Numbers_Counted()
    {
        Assert.Equal(2, GdScriptTokenCounter.Count("42 3.14"));
    }

    [Fact]
    public void HexNumbers_Counted()
    {
        Assert.Equal(1, GdScriptTokenCounter.Count("0xFF"));
    }

    [Fact]
    public void Strings_CountedAsOneToken()
    {
        Assert.Equal(1, GdScriptTokenCounter.Count("\"hello world\""));
    }

    [Fact]
    public void Operators_Counted()
    {
        Assert.Equal(3, GdScriptTokenCounter.Count("a + b"));
    }

    [Fact]
    public void Symbols_Counted()
    {
        Assert.Equal(3, GdScriptTokenCounter.Count("a b c"));
    }

    [Fact]
    public void MultiCharOperators_Counted()
    {
        Assert.Equal(3, GdScriptTokenCounter.Count("a == b"));
    }

    [Fact]
    public void Assignment_Counted()
    {
        Assert.Equal(3, GdScriptTokenCounter.Count("x = 5"));
    }

    [Fact]
    public void TokenCounter_DetectsTokenLimitExceeded()
    {
        var source = string.Join(" ", Enumerable.Repeat("x", 9000));
        var count = GdScriptTokenCounter.Count(source);
        Assert.True(count > 8000);
    }
}
