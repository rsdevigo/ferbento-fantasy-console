using Ferbento.Scripting.Parser;

namespace Ferbento.Scripting.Tests;

public class Pico8TokenCounterTests
{
    [Fact]
    public void EmptySource_ZeroTokens()
    {
        Assert.Equal(0, Pico8TokenCounter.Count(""));
    }

    [Fact]
    public void OnlyWhitespace_ZeroTokens()
    {
        Assert.Equal(0, Pico8TokenCounter.Count("   \n  \t  "));
    }

    [Fact]
    public void OnlyComment_ZeroTokens()
    {
        Assert.Equal(0, Pico8TokenCounter.Count("// this is a comment"));
    }

    [Fact]
    public void BlockComment_ZeroTokens()
    {
        Assert.Equal(0, Pico8TokenCounter.Count("/* block comment */"));
    }

    [Fact]
    public void GDScriptComment_ZeroTokens()
    {
        Assert.Equal(0, Pico8TokenCounter.Count("# this is a gdscript comment"));
    }

    [Fact]
    public void Identifiers_Counted()
    {
        Assert.Equal(3, Pico8TokenCounter.Count("foo bar baz"));
    }

    [Fact]
    public void KeywordsAndSymbols_CountedAsTokens()
    {
        Assert.Equal(2, Pico8TokenCounter.Count("func _init"));
    }

    [Fact]
    public void Numbers_Counted()
    {
        Assert.Equal(2, Pico8TokenCounter.Count("42 3.14"));
    }

    [Fact]
    public void HexNumbers_Counted()
    {
        Assert.Equal(1, Pico8TokenCounter.Count("0xFF"));
    }

    [Fact]
    public void Strings_CountedAsOneToken()
    {
        Assert.Equal(1, Pico8TokenCounter.Count("\"hello world\""));
    }

    [Fact]
    public void Operators_Counted()
    {
        Assert.Equal(3, Pico8TokenCounter.Count("a + b"));
    }

    [Fact]
    public void Symbols_Counted()
    {
        Assert.Equal(3, Pico8TokenCounter.Count("a b c"));
    }

    [Fact]
    public void MultiCharOperators_Counted()
    {
        Assert.Equal(3, Pico8TokenCounter.Count("a == b"));
    }

    [Fact]
    public void Assignment_Counted()
    {
        Assert.Equal(3, Pico8TokenCounter.Count("x = 5"));
    }

    [Fact]
    public void TokenCounter_DetectsTokenLimitExceeded()
    {
        var source = string.Join(" ", Enumerable.Repeat("x", 9000));
        var count = Pico8TokenCounter.Count(source);
        Assert.True(count > 8000);
    }
}
