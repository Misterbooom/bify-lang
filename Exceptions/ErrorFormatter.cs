using System;
using System.IO;
using System.Linq;
using Spectre.Console;

namespace BifyLang.Exceptions;

public static class ErrorFormatter
{
    public static void PrintException(BifyCompilationException exception)
    {
        if (exception is null) return;
        PrintDiagnostic(exception.Diagnostic);
    }

    public static void PrintDiagnostics(DiagnosticBag bag)
    {
        if (bag is null) return;
        foreach (var diagnostic in bag.Diagnostics)
        {
            PrintDiagnostic(diagnostic);
        }
    }

    public static void PrintDiagnostic(Diagnostic? diagnostic)
    {
        if (diagnostic is null) return;

        string errorType = $"{diagnostic.Kind}Error";
        var loc = diagnostic.Location;

        var panel = new Panel($"[indianred1]{Markup.Escape(diagnostic.Message)}[/]")
        {
            Header = new PanelHeader($"[red1 bold]{Markup.Escape(errorType)}[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0)
        };
        AnsiConsole.Write(panel);

        string moduleName = loc.ModuleName ?? string.Empty;
        string context = string.IsNullOrWhiteSpace(loc.FunctionName)
            ? moduleName
            : $"{moduleName}.{loc.FunctionName}";

        string contextSuffix = string.IsNullOrWhiteSpace(context)
            ? string.Empty
            : $" in [red1]{Markup.Escape(context)}[/]";

        string columnSuffix = loc.ColumnNumber > 0
            ? $", Column [yellow]{loc.ColumnNumber}[/]"
            : string.Empty;

        AnsiConsole.MarkupLine(
            $"    File [orangered1]'{Markup.Escape(loc.FilePath ?? string.Empty)}'[/], Line [yellow]{loc.LineNumber}[/]{columnSuffix}{contextSuffix}"
        );

        PrintErrorLine(loc.FilePath, loc.LineNumber, loc.ColumnNumber);
    }

    private static void PrintErrorLine(string? filePath, int lineNumber, int columnNumber)
    {
        if (string.IsNullOrWhiteSpace(filePath) || lineNumber <= 0 || !File.Exists(filePath))
            return;

        string? rawLine = File.ReadLines(filePath).Skip(lineNumber - 1).FirstOrDefault();
        if (rawLine is null)
            return;

        const string tab = "    ";
        string expandedLine = rawLine.Replace("\t", tab);
        string trimmedLine = expandedLine.TrimStart(' ');
        int trimmedSpaces = expandedLine.Length - trimmedLine.Length;

        string lineNumStr = lineNumber.ToString();
        AnsiConsole.MarkupLine($"    [yellow]{lineNumStr}[/] | {Markup.Escape(trimmedLine)}");

        if (columnNumber > 0)
        {
            int zeroBasedCol = Math.Clamp(columnNumber - 1, 0, rawLine.Length);
            int visualCol = rawLine[..zeroBasedCol].Replace("\t", tab).Length;
            int caretOffset = Math.Max(0, visualCol - trimmedSpaces);

            string gutterSpaces = new string(' ', lineNumStr.Length);
            string pointerSpaces = new string(' ', caretOffset);

            AnsiConsole.MarkupLine($"    {gutterSpaces} | {pointerSpaces}[red1 bold]^^[/]");
        }
    }
}
