#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Types.Data;

namespace BifyLang.CodeGen.Core.Validators;

public static class FlagChecker
{
    private static readonly HashSet<string> _functionFlags = ["public", "private"];
    private static readonly HashSet<string> _methodFlags = ["public", "private", "protected", "virtual", "override", "abstract", "final"];
    private static readonly HashSet<string> _structMethodFlags = ["public", "private"];
    private static readonly HashSet<string> _attributeFlags = ["public", "private", "protected", "weak", "mut", "val"];
    private static readonly HashSet<string> _structAttributeFlags = ["public", "private", "mut", "val"];
    private static readonly HashSet<string> _classFlags = ["public", "private", "static", "final", "abstract"];
    private static readonly HashSet<string> _structFlags = ["public", "private"];
    private static readonly HashSet<string> _variableFlags = ["mut", "val", "weak"];
    private static readonly HashSet<string> _parameterFlags = ["mut", "val"];

    private static readonly Dictionary<string, FieldFlags> _fieldMap = new()
    {
        ["val"] = FieldFlags.CONST,
        ["weak"] = FieldFlags.WEAK,
        ["static"] = FieldFlags.STATIC,
    };

    private static readonly Dictionary<string, MethodFlags> _methodMap = new()
    {
        ["virtual"] = MethodFlags.VIRTUAL,
        ["override"] = MethodFlags.OVERRIDE,
        ["abstract"] = MethodFlags.ABSTRACT,
        ["final"] = MethodFlags.FINAL,
        ["static"] = MethodFlags.STATIC,
    };

    private static readonly Dictionary<string, TypeFlags> _typeMap = new()
    {
        ["public"] = TypeFlags.PUBLIC,
        ["static"] = TypeFlags.STATIC,
        ["final"] = TypeFlags.FINAL,
        ["abstract"] = TypeFlags.ABSTRACT,
    };

    public static bool ParseFieldFlags(
        AstFlag? flagNode,
        bool isStruct,
        out AccessLevel accessLevel,
        out FieldFlags flags,
        out string? error)
    {
        var list = GetFlags(flagNode);
        accessLevel = ParseAccessLevel(list);
        flags = MapFlags(list, _fieldMap);
        return isStruct ? CheckStructAttributeFlags(list, out error) : CheckAttributeFlags(list, out error);
    }

    public static bool ParseMethodFlags(
        AstFlag? flagNode,
        bool isStruct,
        out AccessLevel accessLevel,
        out MethodFlags flags,
        out string? error)
    {
        var list = GetFlags(flagNode);
        accessLevel = ParseAccessLevel(list);
        flags = MapFlags(list, _methodMap);
        return isStruct ? CheckStructMethodFlags(list, out error) : CheckMethodFlags(list, out error);
    }

    public static bool ParseTypeFlags(
        AstFlag? flagNode,
        bool isStruct,
        out TypeFlags flags,
        out string? error)
    {
        var list = GetFlags(flagNode);
        flags = MapFlags(list, _typeMap);
        return isStruct ? CheckStructFlags(list, out error) : CheckClassFlags(list, out error);
    }

    public static bool CheckFunctionFlags(IEnumerable<string> flags, out string? error) =>
        Check(flags, _functionFlags, "function", out error);

    public static bool CheckMethodFlags(IEnumerable<string> flags, out string? error)
    {
        var list = flags as ICollection<string> ?? flags.ToList();
        if (!Check(list, _methodFlags, "method", out error))
            return false;

        if (list.Contains("virtual") && list.Contains("override"))
            return Fail("conflicting flags 'virtual' and 'override'", out error);

        if (list.Contains("abstract") && list.Contains("override"))
            return Fail("conflicting flags 'abstract' and 'override'", out error);

        if (list.Contains("abstract") && list.Contains("final"))
            return Fail("conflicting flags 'abstract' and 'final'", out error);

        if (list.Contains("final") && !list.Contains("override"))
            return Fail("'final' method requires 'override'", out error);

        return true;
    }

    public static bool CheckAttributeFlags(IEnumerable<string> flags, out string? error) =>
        Check(flags, _attributeFlags, "attribute", out error);

    public static bool CheckClassFlags(IEnumerable<string> flags, out string? error)
    {
        var list = flags as ICollection<string> ?? flags.ToList();
        if (!Check(list, _classFlags, "class", out error))
            return false;

        if (list.Contains("abstract") && list.Contains("final"))
            return Fail("conflicting flags 'abstract' and 'final'", out error);

        return true;
    }

    public static bool CheckStructAttributeFlags(IEnumerable<string> flags, out string? error) =>
        Check(flags, _structAttributeFlags, "struct", out error);

    public static bool CheckStructMethodFlags(IEnumerable<string> flags, out string? error) =>
        Check(flags, _structMethodFlags, "struct method", out error);

    public static bool CheckVariableFlags(IEnumerable<string> flags, out string? error) =>
        Check(flags, _variableFlags, "variable", out error);

    public static bool CheckParameterFlags(IEnumerable<string> flags, out string? error) =>
        Check(flags, _parameterFlags, "parameter", out error);

    public static bool CheckStructFlags(IEnumerable<string> flags, out string? error) =>
        Check(flags, _structFlags, "struct", out error);

    private static List<string> GetFlags(AstFlag? node) =>
        node?.Flags?.Select(f => f.Token.Value).ToList() ?? [];

    private static AccessLevel ParseAccessLevel(List<string> list)
    {
        if (list.Contains("private")) return AccessLevel.PRIVATE;
        if (list.Contains("protected")) return AccessLevel.PROTECTED;
        return AccessLevel.PUBLIC;
    }

    private static T MapFlags<T>(List<string> list, Dictionary<string, T> map) where T : struct, Enum
    {
        int res = 0;
        foreach (var f in list)
        {
            if (map.TryGetValue(f, out var val))
                res |= Convert.ToInt32(val);
        }
        return (T)Enum.ToObject(typeof(T), res);
    }

    private static bool Check(IEnumerable<string> flags, HashSet<string> allowed, string target, out string? error)
    {
        var seen = new HashSet<string>();
        bool hasAccess = false;

        foreach (var f in flags)
        {
            if (!allowed.Contains(f))
                return Fail($"flag '{f}' not allowed on {target}", out error);

            if (!seen.Add(f))
                return Fail($"duplicate flag '{f}'", out error);

            if (f is "public" or "private" or "protected")
            {
                if (hasAccess)
                    return Fail($"multiple access modifiers on {target}", out error);
                hasAccess = true;
            }
        }

        error = null;
        return true;
    }

    private static bool Fail(string msg, out string? error)
    {
        error = msg;
        return false;
    }
}
