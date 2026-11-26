#nullable enable
using BifyLang.Exceptions;

namespace BifyLang.Tests.Infrastructure;

public static class BifyAssert
{
    public static Diagnostic CompileError(string code, DiagnosticKind expectedKind, string? expectedSubstring = null)
    {
        DiagnosticBag diagnostics = new();
        try
        {
            using var compiled = Bify.Compile(code, diagnostics);
        }
        catch (BifyCompilationException)
        {
        }

        return AssertHasDiagnostic(diagnostics, expectedKind, expectedSubstring, "compilation");
    }

    public static Diagnostic ActionThrows(
        DiagnosticKind expectedKind,
        Action action,
        string? expectedSubstring = null,
        DiagnosticBag? diagnostics = null)
    {
        Diagnostic? captured = null;
        try
        {
            action();
        }
        catch (BifyCompilationException ex)
        {
            captured = ex.Diagnostic;
        }

        if (diagnostics != null)
        {
            return AssertHasDiagnostic(diagnostics, expectedKind, expectedSubstring, "action", captured);
        }

        Assert.That(captured, Is.Not.Null, $"Expected diagnostic of kind '{expectedKind}', but no compilation exception was thrown.");
        Assert.That(captured!.Kind, Is.EqualTo(expectedKind),
            $"Expected diagnostic of kind '{expectedKind}', but got '{captured.Kind}': {captured.Message}");
        if (expectedSubstring != null)
        {
            Assert.That(captured.Message, Does.Contain(expectedSubstring));
        }
        return captured;
    }

    public static Diagnostic ParseError(string code, DiagnosticKind expectedKind = DiagnosticKind.SYNTAX, string? expectedSubstring = null)
    {
        DiagnosticBag diagnostics = new();
        try
        {
            Bify.Parse(code, diagnostics);
        }
        catch (BifyCompilationException)
        {
        }

        return AssertHasDiagnostic(diagnostics, expectedKind, expectedSubstring, "parsing");
    }

    public static DiagnosticBag AnySyntaxError(string code)
    {
        DiagnosticBag diagnostics = new();
        try
        {
            Bify.Parse(code, diagnostics);
        }
        catch (BifyCompilationException)
        {
        }

        Assert.That(diagnostics.HasErrors, Is.True, "Expected diagnostic error during parsing, but none occurred.");
        return diagnostics;
    }

    private static Diagnostic AssertHasDiagnostic(
        DiagnosticBag diagnostics,
        DiagnosticKind expectedKind,
        string? expectedSubstring,
        string phase,
        Diagnostic? fallback = null)
    {
        List<Diagnostic> items = [.. diagnostics.Diagnostics];
        if (fallback != null && !items.Contains(fallback))
        {
            items.Add(fallback);
        }

        Assert.That(items.Count, Is.GreaterThan(0), $"Expected diagnostic of kind '{expectedKind}' during {phase}, but none occurred.");

        Diagnostic? match = items.FirstOrDefault(d =>
            d.Kind == expectedKind &&
            (expectedSubstring == null || d.Message.Contains(expectedSubstring, StringComparison.Ordinal)));

        Assert.That(match, Is.Not.Null,
            $"Expected diagnostic of kind '{expectedKind}'{(expectedSubstring != null ? $" containing '{expectedSubstring}'" : "")} during {phase}, " +
            $"but got: [{string.Join("; ", items.Select(d => $"{d.Kind}: {d.Message}"))}]");
        return match!;
    }
}
