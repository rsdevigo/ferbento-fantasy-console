using Ferbento.Core.Math;
using Ferbento.Scripting.Compiler.Ir;

namespace Ferbento.Scripting.Compiler.Parsing;

public enum TokenKind
{
    EndOfFile,

    /// <summary>
    /// End of a line that closes a short <c>if</c>/<c>while</c> or a <c>?</c>. The lexer never produces
    /// it: the parser inserts it at the first newline after one of those constructs, like z8lua.
    /// </summary>
    EndOfLine,

    Name,
    Number,
    String,

    // keywords
    And, Break, Do, Else, Elseif, End, False, For, Function, Goto, If, In, Local, Nil, Not, Or,
    Repeat, Return, Then, True, Until, While,

    // arithmetic
    Plus, Minus, Star, Slash, Backslash, Percent, Caret, Hash,

    // bitwise (PICO-8: ^^ is xor, ~ is only unary not)
    Ampersand, Pipe, CaretCaret, Tilde, ShiftLeft, ShiftRight, ShiftRightLogical, RotateLeft, RotateRight,

    // comparison and assignment
    EqualEqual, TildeEqual, Less, LessEqual, Greater, GreaterEqual, Assign,

    // punctuation
    LeftParen, RightParen, LeftBrace, RightBrace, LeftBracket, RightBracket,
    DoubleColon, Semicolon, Colon, Comma, Dot, DotDot, Ellipsis,

    // PICO-8: peek operators (@ peek, % peek2, $ peek4) and ? (print)
    At, Dollar, Question,

    // PICO-8 compound assignment, in the same order as their binary operators
    PlusAssign, MinusAssign, StarAssign, SlashAssign, PercentAssign, CaretAssign, BackslashAssign,
    AmpersandAssign, PipeAssign, BitXorAssign, ShiftLeftAssign, ShiftRightAssign, ShiftRightLogicalAssign,
    RotateLeftAssign, RotateRightAssign, ConcatAssign,
}

/// <summary>
/// A lexical token. <see cref="Text"/> is the exact source text; <see cref="StringValue"/> is the
/// decoded value of a string or the name of a name token; <see cref="NumberValue"/> is the 16.16 value
/// of a number. <see cref="Start"/> and <see cref="Length"/> are offsets into the source.
/// </summary>
public readonly record struct Token(
    TokenKind Kind,
    string Text,
    SourceSpan Span,
    int Start,
    int Length,
    string? StringValue = null,
    FixedPoint NumberValue = default)
{
    /// <summary>True when a newline (outside long strings and comments) comes between this token and the previous one.</summary>
    public bool NewlineBefore { get; init; }

    /// <summary>How the token is shown in error messages: <c>'end'</c>, <c>o nome 'x'</c>, <c>o fim do arquivo</c>.</summary>
    public string Describe() => Kind switch
    {
        TokenKind.EndOfFile => "o fim do arquivo",
        TokenKind.EndOfLine => "o fim da linha",
        TokenKind.Name => $"o nome '{Text}'",
        TokenKind.Number => $"o número {Text}",
        TokenKind.String => "uma string",
        _ => $"'{Text}'",
    };

    public static string Describe(TokenKind kind) => kind switch
    {
        TokenKind.EndOfFile => "o fim do arquivo",
        TokenKind.EndOfLine => "o fim da linha",
        TokenKind.Name => "um nome",
        TokenKind.Number => "um número",
        TokenKind.String => "uma string",
        _ => $"'{Symbol(kind)}'",
    };

    /// <summary>The fixed source text of a keyword or symbol kind.</summary>
    public static string Symbol(TokenKind kind) => kind switch
    {
        TokenKind.And => "and", TokenKind.Break => "break", TokenKind.Do => "do", TokenKind.Else => "else",
        TokenKind.Elseif => "elseif", TokenKind.End => "end", TokenKind.False => "false", TokenKind.For => "for",
        TokenKind.Function => "function", TokenKind.Goto => "goto", TokenKind.If => "if", TokenKind.In => "in",
        TokenKind.Local => "local", TokenKind.Nil => "nil", TokenKind.Not => "not", TokenKind.Or => "or",
        TokenKind.Repeat => "repeat", TokenKind.Return => "return", TokenKind.Then => "then",
        TokenKind.True => "true", TokenKind.Until => "until", TokenKind.While => "while",
        TokenKind.Plus => "+", TokenKind.Minus => "-", TokenKind.Star => "*", TokenKind.Slash => "/",
        TokenKind.Backslash => "\\", TokenKind.Percent => "%", TokenKind.Caret => "^", TokenKind.Hash => "#",
        TokenKind.Ampersand => "&", TokenKind.Pipe => "|", TokenKind.CaretCaret => "^^", TokenKind.Tilde => "~",
        TokenKind.ShiftLeft => "<<", TokenKind.ShiftRight => ">>", TokenKind.ShiftRightLogical => ">>>",
        TokenKind.RotateLeft => "<<>", TokenKind.RotateRight => ">><",
        TokenKind.EqualEqual => "==", TokenKind.TildeEqual => "~=", TokenKind.Less => "<",
        TokenKind.LessEqual => "<=", TokenKind.Greater => ">", TokenKind.GreaterEqual => ">=",
        TokenKind.Assign => "=",
        TokenKind.LeftParen => "(", TokenKind.RightParen => ")", TokenKind.LeftBrace => "{",
        TokenKind.RightBrace => "}", TokenKind.LeftBracket => "[", TokenKind.RightBracket => "]",
        TokenKind.DoubleColon => "::", TokenKind.Semicolon => ";", TokenKind.Colon => ":",
        TokenKind.Comma => ",", TokenKind.Dot => ".", TokenKind.DotDot => "..", TokenKind.Ellipsis => "...",
        TokenKind.At => "@", TokenKind.Dollar => "$", TokenKind.Question => "?",
        TokenKind.PlusAssign => "+=", TokenKind.MinusAssign => "-=", TokenKind.StarAssign => "*=",
        TokenKind.SlashAssign => "/=", TokenKind.PercentAssign => "%=", TokenKind.CaretAssign => "^=",
        TokenKind.BackslashAssign => "\\=", TokenKind.AmpersandAssign => "&=", TokenKind.PipeAssign => "|=",
        TokenKind.BitXorAssign => "^^=", TokenKind.ShiftLeftAssign => "<<=", TokenKind.ShiftRightAssign => ">>=",
        TokenKind.ShiftRightLogicalAssign => ">>>=", TokenKind.RotateLeftAssign => "<<>=",
        TokenKind.RotateRightAssign => ">><=", TokenKind.ConcatAssign => "..=",
        _ => kind.ToString(),
    };
}

/// <summary>
/// A comment, without its <c>--</c>/<c>//</c> marker (or the long brackets). <see cref="OnOwnLine"/> is true
/// when no token comes before it on its first line.
/// </summary>
public sealed record LuaComment(string Text, SourceSpan Span, bool OnOwnLine);
