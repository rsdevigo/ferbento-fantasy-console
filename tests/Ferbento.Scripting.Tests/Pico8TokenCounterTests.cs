using Ferbento.Scripting.Compiler.Diagnostics;
using Ferbento.Scripting.Compiler.Parsing;

namespace Ferbento.Scripting.Tests;

/// <summary>
/// PICO-8 token count on Lua source. "manual" is the PICO-8 manual (token count section);
/// "zepto8" is the token_counter table in zepto8's src/pico8/parser.cpp, used where the manual says nothing.
/// </summary>
public class Pico8TokenCounterTests
{
    private static int Count(string source) => Pico8TokenCounter.Count(source);

    [Theory]
    [InlineData("", 0)]
    [InlineData("   \n\t  ", 0)]
    [InlineData("-- line\n--[[ long\ncomment ]]", 0)]               // manual: comments are free
    [InlineData("x = 1 // pico comment", 3)]                         // manual: // is a comment too
    [InlineData("x = 5", 3)]                                         // manual: names, operators, numbers
    [InlineData("local x = 5", 3)]                                   // manual: 'local' is free
    [InlineData("print(\"hello\")", 3)]                              // manual: string = 1, bracket pair = 1
    [InlineData("a.b.c = 1", 5)]                                     // manual: '.' is free
    [InlineData("f(a, b, c)", 5)]                                    // manual: ',' is free
    [InlineData(";;x = 1;", 3)]                                      // manual: ';' is free
    [InlineData("function f(a) return a end", 6)]                    // manual: 'end' is free
    [InlineData("x = (a + b) * c", 8)]                               // manual: '()' pair = 1
    public void Manual(string source, int expected) => Assert.Equal(expected, Count(source));

    [Theory]
    [InlineData("t[1] = 2", 5)]                                      // zepto8: '[]' in a[b] = 1
    [InlineData("t = {1, 2, [3] = 4, k = 5}", 12)]                   // zepto8: '{}' = 1, '[x]=' = 2, 'k=' = '=' + name
    [InlineData("obj:move(1)", 4)]                                   // zepto8: ':' in a:b() is free
    [InlineData("function o:m() end", 4)]                            // zepto8: ':' in a function name is free
    [InlineData("local function g() end", 3)]                        // zepto8: 'local' free, '()' of the body = 1
    [InlineData("f = function(...) return ... end", 7)]              // zepto8: '...' = 1
    [InlineData("goto skip ::skip::", 3)]                            // zepto8: '::' is free
    [InlineData("for i = 1, 10 do end", 6)]                          // zepto8: the '=' of a numeric for = 1
    [InlineData("for k, v in pairs(t) do end", 8)]                   // zepto8: 'in' = 1, no extra cost
    [InlineData("f\"s\" f{}", 4)]                                    // zepto8: call with string/table, no '()'
    [InlineData("x = a and not b or c", 8)]                          // zepto8: 'and', 'or', 'not' = 1
    [InlineData("repeat x() until y", 5)]                            // zepto8: 'repeat', 'until' = 1
    [InlineData("if a then elseif b then else end", 7)]              // zepto8: 'elseif', 'else', 'then' = 1
    [InlineData("x = nil y = true z = false", 9)]                    // zepto8: nil/true/false = 1
    [InlineData("x = \"a\" .. [[long]]", 5)]                         // zepto8: '..' = 1, long string = 1
    public void Zepto8(string source, int expected) => Assert.Equal(expected, Count(source));

    [Theory]
    [InlineData("x = -1", 3)]                                        // zepto8: "-2" counts as one
    [InlineData("x = - 1", 4)]                                       // zepto8: "- 2" counts as two
    [InlineData("x = ~0x0f", 3)]                                     // zepto8: same rule for '~'
    [InlineData("x = -y", 4)]                                        // only before a number literal
    [InlineData("x = a-1", 5)]                                       // binary '-' always counts
    [InlineData("x = 2^-1", 5)]                                      // unary after an operator is free
    [InlineData("return -1, -x", 4)]
    public void FreeSignOnNumbers(string source, int expected) => Assert.Equal(expected, Count(source));

    [Theory]
    [InlineData("a += 1", 3)]                                        // zepto8: compop = 1
    [InlineData("s ..= \"x\"", 3)]
    [InlineData("x >>>= 1", 3)]
    [InlineData("x = a != b", 5)]                                    // zepto8: '!=' is an operator like '~='
    [InlineData("x = #t + @0x6000 + %a + $b", 13)]                   // zepto8: '#', '@', '%', '$' are unary operators
    [InlineData("x = a ^^ b >>> 1 <<> 2 \\ 3", 11)]                  // zepto8: PICO-8 binary operators = 1
    [InlineData("?\"hi\", 1", 3)]                                    // zepto8: the '?' of short print = 1
    [InlineData("if (a) b()", 5)]                                    // zepto8: short if, '()' of the condition = 1
    [InlineData("while (i < 3) i += 1", 8)]
    public void Pico8Extensions(string source, int expected) => Assert.Equal(expected, Count(source));

    [Fact]
    public void ShortIf_CostsTheSameAsTheLongForm()
    {
        // "if (a) b()" trades 'then' for the '()' of the condition; 'end' is free anyway.
        Assert.Equal(Count("if a then b() end"), Count("if (a) b()"));
    }

    [Fact]
    public void CountOfTokens_MatchesCountOfSource()
    {
        const string source = "function _draw() cls() ?\"x\" end";
        var tokens = LuaLexer.Tokenize(source, null, new List<Diagnostic>());
        Assert.Equal(Count(source), Pico8TokenCounter.Count(tokens));
    }

    [Fact]
    public void LexerErrors_DoNotStopTheCount()
    {
        Assert.Equal(3, Count("x = ` 1"));
    }

    [Fact]
    public void LargeSource_GoesOverTheLimit()
    {
        var source = string.Join("\n", Enumerable.Repeat("x = 1", 3000));
        Assert.Equal(9000, Count(source));
        Assert.True(Count(source) > 8192);
    }
}
