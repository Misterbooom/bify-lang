using System.Collections.Generic;
using System.Linq;

namespace BifyLang.CodeGen.Core.Validators;

public static class FlagChecker
{
    private static readonly HashSet<string> _allowedFunctionFlags =
    [
        "public",
        "private"
    ];

    private static readonly HashSet<string> _allowedMethodFlags =
    [
        "public",
        "private",
        "protected",
        "virtual",
        "override"
    ];
    private static readonly HashSet<string> _allowedStructMethodFlags =
    [
        "public",
        "private",
    ];
    private static readonly HashSet<string> _allowedAttributeFlags =
    [
        "public",
        "private",
        "protected",
        "weak",
        "mut",
        "val"
    ];
    private static readonly HashSet<string> _allowedStructAttributeFlags =
    [
        "public",
        "private",
        "mut",
        "val"
    ];
    private static readonly HashSet<string> _allowedClassFlags =
    [
        "public",
        "private",
        "static",
        "final",
        "abstract"
    ];

    private static readonly HashSet<string> _allowedStructFlags = [
        "public",
        "private",
    ];

    private static readonly HashSet<string> _allowedVariableFlags =
    [
        "mut",
        "val",
        "weak"
    ];

    private static readonly HashSet<string> _allowedParameterFlags =
    [
        "mut",
        "val"
    ];

    public static bool CheckFunctionFlags(
        IEnumerable<string> flags,
        out string? error)
    {
        return Check(
            flags,
            _allowedFunctionFlags,
            "function",
            out error
        );
    }

    public static bool CheckMethodFlags(
        IEnumerable<string> flags,
        out string? error)
    {
        if (!Check(flags, _allowedMethodFlags, "method", out error))
            return false;

        var flagList = flags.ToList();
        if (flagList.Contains("virtual") && flagList.Contains("override"))
        {
            error = "Method cannot be both 'virtual' and 'override'.";
            return false;
        }

        return true;
    }

    public static bool CheckAttributeFlags(
        IEnumerable<string> flags,
        out string? error)
    {
        return Check(
            flags,
            _allowedAttributeFlags,
            "attribute",
            out error
        );
    }

    public static bool CheckClassFlags(
        IEnumerable<string> flags,
        out string? error)
    {
        return Check(
            flags,
            _allowedClassFlags,
            "class",
            out error
        );
    }

    public static bool CheckStructAttributeFlags(IEnumerable<string> flags,
        out string? error)
    {
        return Check(
            flags,
            _allowedStructAttributeFlags,
            "struct",
            out error
        );
    }

    public static bool CheckStructMethodFlags(IEnumerable<string> flags,
        out string? error)
    {
        return Check(
            flags,
            _allowedStructMethodFlags,
            "struct method",
            out error
        );
    }

    public static bool CheckVariableFlags(
        IEnumerable<string> flags,
        out string? error)
    {
        return Check(
            flags,
            _allowedVariableFlags,
            "variable",
            out error
        );
    }

    public static bool CheckParameterFlags(
        IEnumerable<string> flags,
        out string? error)
    {
        return Check(
            flags,
            _allowedParameterFlags,
            "parameter",
            out error
        );
    }

    public static bool CheckStructFlags(IEnumerable<string> flags, out string? error)
    {
        return Check(
            flags,
            _allowedStructFlags,
            "struct",
            out error
        );
    }

    private static bool Check(
        IEnumerable<string> flags,
        HashSet<string> allowedFlags,
        string target,
        out string? error)
    {
        error = null;

        var flagList = flags.ToList();

        foreach (var flag in flagList)
        {
            if (!allowedFlags.Contains(flag))
            {
                error = $"Flag '{flag}' is not allowed on {target}.";
                return false;
            }
        }

        var duplicate = flagList
            .GroupBy(x => x)
            .FirstOrDefault(x => x.Count() > 1);

        if (duplicate != null)
        {
            error = $"Flag '{duplicate.Key}' is specified multiple times.";
            return false;
        }

        var accessModifierCount = flagList.Count(flag =>
            flag is "public" or "private" or "protected"
        );

        if (accessModifierCount > 1)
        {
            error =
                $"Only one access modifier can be specified on {target}.";

            return false;
        }

        error = null;
        return true;
    }
}
