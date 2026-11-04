using System.Collections.Immutable;
using Ferbento.Scripting.Compiler.Diagnostics;

namespace Ferbento.Scripting.Compiler.Parsing;

/// <summary>
/// Counts code tokens the way PICO-8 does, on the tokens of <see cref="LuaLexer"/>.
/// <list type="bullet">
/// <item>Names, numbers, strings, keywords and operators cost 1 each.</item>
/// <item>A pair of brackets costs 1: the opening <c>(</c>, <c>[</c> or <c>{</c> counts and the closing one doesn't.</item>
/// <item>Free: <c>,</c> <c>.</c> <c>:</c> <c>::</c> <c>;</c> <c>local</c> <c>end</c> and comments.</item>
/// <item>A <c>-</c> or <c>~</c> glued to a number literal is free: <c>-1</c> costs 1 and <c>- 1</c> costs 2.</item>
/// </list>
/// The rules come from the PICO-8 manual and, where it says nothing, from zepto8's token counter.
/// </summary>
public static class Pico8TokenCounter
{
    /// <summary>Counts the tokens of <paramref name="source"/>. Lexing errors don't stop the count.</summary>
    public static int Count(string source) => Count(LuaLexer.Tokenize(source, null, new List<Diagnostic>()));

    public static int Count(ImmutableArray<Token> tokens)
    {
        var count = 0;
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (IsFree(token.Kind)) continue;
            if (IsFreeSign(tokens, i)) continue;
            count++;
        }
        return count;
    }

    private static bool IsFree(TokenKind kind) => kind is
        TokenKind.EndOfFile or TokenKind.EndOfLine or
        TokenKind.End or TokenKind.Local or
        TokenKind.Comma or TokenKind.Dot or TokenKind.Colon or TokenKind.DoubleColon or TokenKind.Semicolon or
        TokenKind.RightParen or TokenKind.RightBracket or TokenKind.RightBrace;

    /// <summary>A unary <c>-</c> or <c>~</c> immediately followed (no space) by a number literal.</summary>
    private static bool IsFreeSign(ImmutableArray<Token> tokens, int i)
    {
        var token = tokens[i];
        if (token.Kind is not (TokenKind.Minus or TokenKind.Tilde)) return false;
        if (i + 1 >= tokens.Length) return false;

        var next = tokens[i + 1];
        if (next.Kind != TokenKind.Number || next.Start != token.Start + token.Length) return false;

        // After an operand the '-' is binary ("a-1" is three tokens); '~' is never binary in PICO-8.
        return token.Kind == TokenKind.Tilde || i == 0 || !EndsOperand(tokens[i - 1].Kind);
    }

    private static bool EndsOperand(TokenKind kind) => kind is
        TokenKind.Name or TokenKind.Number or TokenKind.String or TokenKind.Ellipsis or
        TokenKind.True or TokenKind.False or TokenKind.Nil or TokenKind.End or
        TokenKind.RightParen or TokenKind.RightBracket or TokenKind.RightBrace;
}
