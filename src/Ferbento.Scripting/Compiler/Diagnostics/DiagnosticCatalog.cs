namespace Ferbento.Scripting.Compiler.Diagnostics;

/// <summary>
/// Every diagnostic the compiler can report. This file is the single list of codes and the
/// source for the error page of the user documentation. Codes are stable: never reuse or
/// renumber one; retire it instead.
/// <list type="bullet">
/// <item><c>FB00xx</c>: lexer (characters, strings, numbers, comments)</item>
/// <item><c>FB01xx</c>: parser (grammar)</item>
/// <item><c>FB020x</c>: static analysis errors; <c>FB025x</c>: static analysis warnings</item>
/// </list>
/// </summary>
public static class DiagnosticCatalog
{
    private const DiagnosticSeverity Error = DiagnosticSeverity.Error;
    private const DiagnosticSeverity Warning = DiagnosticSeverity.Warning;

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

    // ---- static analysis: errors (FB020x) ----

    public static readonly DiagnosticDescriptor BreakOutsideLoop =
        new("FB0201", Error, "'break' fora de um laço");

    public static readonly DiagnosticDescriptor GotoWithoutLabel =
        new("FB0202", Error, "não existe um label '{0}' visível para este 'goto'");

    public static readonly DiagnosticDescriptor GotoIntoLocalScope =
        new("FB0203", Error, "o 'goto {0}' pula para dentro do escopo do local '{1}'");

    public static readonly DiagnosticDescriptor DuplicateLabel =
        new("FB0204", Error, "o label '{0}' já existe neste bloco (linha {1})");

    public static readonly DiagnosticDescriptor VarargOutsideVarargFunction =
        new("FB0205", Error, "'...' só pode ser usado numa função que tem '...' nos parâmetros");

    public static readonly DiagnosticDescriptor TooManyLocals =
        new("FB0206", Error, "mais de {0} variáveis locais ativas ao mesmo tempo nesta função");

    public static readonly DiagnosticDescriptor TooManyUpvalues =
        new("FB0207", Error, "esta função usa mais de {0} variáveis de funções de fora (upvalues)");

    // ---- static analysis: warnings (FB025x) ----

    public static readonly DiagnosticDescriptor UndefinedGlobal =
        new("FB0250", Warning, "a global '{0}' é lida, mas nunca recebe valor no programa{1}");

    public static readonly DiagnosticDescriptor UnimplementedApiFunction =
        new("FB0251", Warning, "'{0}' é da API do PICO-8, mas ainda não existe no Ferbento");

    public static readonly DiagnosticDescriptor NumberWrapsAround =
        new("FB0252", Warning, "o número {0} não cabe no 16.16 e vira {1}");

    /// <summary>All descriptors, for documentation and for the uniqueness test.</summary>
    public static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        UnexpectedCharacter, UnterminatedString, InvalidEscape, MalformedNumber, UnterminatedLongString,
        UnterminatedLongComment, DecimalEscapeTooLarge, NumberOutOfRange,
        Expected, ExpectedClosing, ExpectedExpression, InvalidAssignmentTarget, NotAStatement, ReturnNotLast,
        BreakOutsideLoop, GotoWithoutLabel, GotoIntoLocalScope, DuplicateLabel, VarargOutsideVarargFunction,
        TooManyLocals, TooManyUpvalues, UndefinedGlobal, UnimplementedApiFunction, NumberWrapsAround,
    ];
}
