using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace BifyLang.Exceptions;

public class DiagnosticBag
{
    private readonly List<Diagnostic> _diagnostics = [];
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;
    public bool HasErrors => _diagnostics.Any();

    public void Report(string msg, DiagnosticKind kind, SourceLocation location)
    {
        ArgumentNullException.ThrowIfNull(msg);
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(location);
        _diagnostics.Add(new Diagnostic(msg, kind, location));
    }

    [DoesNotReturn]
    public void Fail(string msg, DiagnosticKind kind, SourceLocation location)
    {
        ArgumentNullException.ThrowIfNull(msg);
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(location);
        Diagnostic diagnostic = new Diagnostic(msg, kind, location);
        _diagnostics.Add(diagnostic);
        throw new BifyCompilationException(diagnostic);
    }

    [DoesNotReturn]
    public T Fail<T>(string msg, DiagnosticKind kind, SourceLocation location)
    {
        ArgumentNullException.ThrowIfNull(msg);
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(location);
        Fail(msg, kind, location);
        return default!;
    }
}
