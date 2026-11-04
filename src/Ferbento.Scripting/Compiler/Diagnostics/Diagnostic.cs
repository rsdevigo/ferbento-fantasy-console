using System.Globalization;
using Ferbento.Scripting.Compiler.Ir;

namespace Ferbento.Scripting.Compiler.Diagnostics;

public enum DiagnosticSeverity
{
    Error,
    Warning,
}

/// <summary>
/// A kind of diagnostic: stable code, severity and message template (Portuguese, shown to the user).
/// All descriptors live in <see cref="DiagnosticCatalog"/>.
/// </summary>
public sealed record DiagnosticDescriptor(string Code, DiagnosticSeverity Severity, string MessageFormat)
{
    public Diagnostic Create(SourceSpan span, params object[] args) =>
        new(this, string.Format(CultureInfo.InvariantCulture, MessageFormat, args), span);
}

/// <summary>An error or warning about the source, with the position it refers to.</summary>
public sealed record Diagnostic(DiagnosticDescriptor Descriptor, string Message, SourceSpan Span)
{
    public string Code => Descriptor.Code;

    public DiagnosticSeverity Severity => Descriptor.Severity;

    public bool IsError => Severity == DiagnosticSeverity.Error;

    public override string ToString() =>
        $"{Span}: {(IsError ? "erro" : "aviso")} {Code}: {Message}";
}
