using System;

namespace BifyLang.Exceptions;

public enum DiagnosticKind
{
    TYPE,
    SYNTAX,
    NAME,
    ATTRIBUTE,
    CAST,
    ARITHMETIC,
    OVERFLOW,
    IMPORT,
    CIRCULAR_INHERITANCE,
    ACCESS_VIOLATION,
    ABSTRACT_METHOD_NOT_IMPLEMENTED,
    CANNOT_INSTANTIATE_ABSTRACT_CLASS,
    UNKNOWN,
}

public sealed record SourceLocation(
    string FilePath,
    int LineNumber,
    int ColumnNumber = 0,
    string? ModuleName = null,
    string? FunctionName = null
);

public sealed class BifyCompilationException : Exception
{
    public Diagnostic? Diagnostic { get; }
    public bool IsPoisonAbort => Diagnostic == null;

    public BifyCompilationException(Diagnostic diagnostic) : base(diagnostic.Message)
    {
        Diagnostic = diagnostic;
    }

    public BifyCompilationException() : base("Poisoned symbol encountered")
    {
    }
}

public sealed record Diagnostic
(
    string Message,
    DiagnosticKind Kind,
    SourceLocation Location
);
