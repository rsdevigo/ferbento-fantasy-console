namespace Ferbento.Scripting.Compiler.Diagnostics;

/// <summary>
/// Every diagnostic the compiler can report. This file is the single list of codes and the
/// source for the error page of the user documentation. Codes are stable: never reuse or
/// renumber one; retire it instead.
/// <list type="bullet">
/// <item><c>FB00xx</c>: lexer (characters, strings, numbers, comments)</item>
/// <item><c>FB01xx</c>: parser (grammar)</item>
/// <item><c>FB02xx</c>: static analysis (story 005)</item>
/// </list>
/// </summary>
public static class DiagnosticCatalog
{
    private const DiagnosticSeverity Error = DiagnosticSeverity.Error;

    // ---- lexer ----

    public static readonly DiagnosticDescriptor UnexpectedCharacter =
        new("FB0001", Error, "caractere inesperado {0}");

    public static readonly DiagnosticDescriptor UnterminatedString =
        new("FB0002", Error, "string não terminada: falta o {0} de fechamento nesta linha");

    public static readonly DiagnosticDescriptor InvalidEscape =
        new("FB0003", Error, "escape inválido '\\{0}' na string");

    public static readonly DiagnosticDescriptor MalformedNumber =
        new("FB0004", Error, "número malformado '{0}'");

    public static readonly DiagnosticDescriptor UnterminatedLongString =
        new("FB0005", Error, "string longa não terminada: falta o {0}");

    public static readonly DiagnosticDescriptor UnterminatedLongComment =
        new("FB0006", Error, "comentário longo não terminado: falta o {0}");

    public static readonly DiagnosticDescriptor DecimalEscapeTooLarge =
        new("FB0007", Error, "escape decimal '\\{0}' maior que 255");

    public static readonly DiagnosticDescriptor NumberOutOfRange =
        new("FB0008", Error, "número fora do alcance: '{0}'");

    // ---- parser ----

    public static readonly DiagnosticDescriptor Expected =
        new("FB0101", Error, "esperava {0}, mas encontrou {1}");

    public static readonly DiagnosticDescriptor ExpectedClosing =
        new("FB0102", Error, "esperava {0} para fechar o {1} da linha {2}, mas encontrou {3}");

    public static readonly DiagnosticDescriptor ExpectedExpression =
        new("FB0103", Error, "esperava uma expressão, mas encontrou {0}");

    public static readonly DiagnosticDescriptor InvalidAssignmentTarget =
        new("FB0104", Error, "não é possível atribuir a esta expressão: só a variáveis e a campos de tabela");

    public static readonly DiagnosticDescriptor NotAStatement =
        new("FB0105", Error, "esta expressão não é um statement: só chamadas e atribuições podem ficar sozinhas numa linha");

    public static readonly DiagnosticDescriptor ReturnNotLast =
        new("FB0106", Error, "'return' precisa ser o último statement do bloco (use 'do return end' para sair no meio)");

    /// <summary>All descriptors, for documentation and for the uniqueness test.</summary>
    public static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        UnexpectedCharacter, UnterminatedString, InvalidEscape, MalformedNumber, UnterminatedLongString,
        UnterminatedLongComment, DecimalEscapeTooLarge, NumberOutOfRange,
        Expected, ExpectedClosing, ExpectedExpression, InvalidAssignmentTarget, NotAStatement, ReturnNotLast,
    ];
}
