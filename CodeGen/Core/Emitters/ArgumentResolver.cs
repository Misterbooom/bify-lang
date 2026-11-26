#nullable enable
using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Expressions;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Core.Emitters;

public static class ArgumentResolver
{
    public static ArgumentInfo?[] Resolve(
        CodeGenContext ctx,
        AstNode[] argNodes,
        IReadOnlyList<ParameterInfo> parameters,
        AstNode callSite,
        string targetLabel,
        bool isVarArg = false)
    {
        ValidateArgCount(
            ctx,
            parameters.Count,
            argNodes.Length,
            parameters.Count(i => i.Value != null),
            isVarArg,
            callSite,
            targetLabel);

        var rawArgs = BuildRawArguments(ctx, argNodes);
        return BindArguments(ctx, rawArgs, parameters, callSite);
    }

    private static void ValidateArgCount(
        CodeGenContext ctx,
        int expected,
        int provided,
        int optionalCount,
        bool isVarArg,
        AstNode callSite,
        string targetLabel)
    {
        int minRequired = expected - optionalCount;
        bool valid = isVarArg
            ? provided >= minRequired
            : provided >= minRequired && provided <= expected;

        if (!valid)
        {
            string expectedText = isVarArg
                ? $"at least {minRequired}"
                : minRequired == expected
                    ? $"{expected}"
                    : $"{minRequired} to {expected}";

            ctx.Fail($"{targetLabel} expects {expectedText} argument(s), got {provided}", DiagnosticKind.TYPE, callSite);
        }
    }

    private static List<(ArgumentInfo Info, AstNode Node)> BuildRawArguments(CodeGenContext ctx, AstNode[] argNodes)
    {
        var dispatcher = new ExpressionDispatcher(ctx);
        var result = new List<(ArgumentInfo, AstNode)>(argNodes.Length);

        for (int i = 0; i < argNodes.Length; i++)
        {
            AstNode node = argNodes[i];
            if (node is AstNamedArg namedArg)
            {
                var val = EmitValue(ctx, dispatcher, namedArg.Value);
                result.Add((new ArgumentInfo(namedArg.Name.Token.Value, val, isPositional: false), node));
                continue;
            }

            var posVal = EmitValue(ctx, dispatcher, node);
            result.Add((new ArgumentInfo(string.Empty, posVal, isPositional: true, Position: i), node));
        }

        return result;
    }

    private static ArgumentInfo[] BindArguments(
        CodeGenContext ctx,
        List<(ArgumentInfo Info, AstNode Node)> rawArgs,
        IReadOnlyList<ParameterInfo> parameters,
        AstNode callSite)
    {
        var bound = new ArgumentInfo?[parameters.Count];
        var varArgs = new List<ArgumentInfo>();
        bool seenNamed = false;

        foreach (var (arg, node) in rawArgs)
        {
            if (arg.isPositional)
            {
                if (seenNamed)
                    ctx.Fail("Positional argument cannot follow a named argument.", DiagnosticKind.SYNTAX, node);

                if (arg.Position >= parameters.Count)
                {
                    varArgs.Add(arg);
                    continue;
                }

                bound[arg.Position] = CastArgument(ctx, arg, parameters[arg.Position], node);
                continue;
            }

            seenNamed = true;
            int index = FindParameterIndex(parameters, arg.Name);
            if (index < 0)
                ctx.Fail($"Unknown parameter '{arg.Name}'.", DiagnosticKind.NAME, node);

            if (bound[index] != null)
                ctx.Fail($"Argument for parameter '{arg.Name}' is specified more than once.", DiagnosticKind.TYPE, node);

            bound[index] = CastArgument(ctx, arg, parameters[index], node);
        }

        return CollectBoundArguments(ctx, bound, varArgs, parameters, callSite);
    }

    private static ArgumentInfo[] CollectBoundArguments(
        CodeGenContext ctx,
        ArgumentInfo?[] bound,
        List<ArgumentInfo> varArgs,
        IReadOnlyList<ParameterInfo> parameters,
        AstNode callSite)
    {
        var dispatcher = new ExpressionDispatcher(ctx);
        var result = new ArgumentInfo[parameters.Count + varArgs.Count];

        for (int i = 0; i < parameters.Count; i++)
        {
            var arg = bound[i];
            if (arg == null)
            {
                if (parameters[i].Value == null)
                {
                    ctx.Fail($"Missing argument for parameter '{parameters[i].Name}'.", DiagnosticKind.TYPE, callSite);
                    continue;
                }

                var defaultVal = EmitValue(ctx, dispatcher, parameters[i].Value);
                var defaultArg = new ArgumentInfo(parameters[i].Name, defaultVal, isPositional: false, Position: i);
                arg = CastArgument(ctx, defaultArg, parameters[i], parameters[i].Value);
            }

            result[i] = arg;
        }

        for (int i = 0; i < varArgs.Count; i++)
            result[parameters.Count + i] = varArgs[i];

        return result;
    }

    private static TypedValue EmitValue(CodeGenContext ctx, ExpressionDispatcher dispatcher, AstNode expr)
    {
        var val = dispatcher.EmitExpression(expr);
        return val;
    }

    private static ArgumentInfo CastArgument(CodeGenContext ctx, ArgumentInfo arg, ParameterInfo param, AstNode node)
    {
        if (!TypeCaster.CanImplicitCast(arg.Value.Type, param.Type))
            ctx.Fail($"Argument '{param.Name}' expects type '{param.Type}', but got '{arg.Value.Type}'",
                DiagnosticKind.TYPE, node);

        var castVal = TypeCaster.ImplicitCast(ctx, arg.Value, param.Type);
        return arg with { Value = castVal };
    }

    private static int FindParameterIndex(IReadOnlyList<ParameterInfo> parameters, string name)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Name == name)
                return i;
        }

        return -1;
    }
}
