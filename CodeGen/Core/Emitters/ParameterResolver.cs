#nullable enable
using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Runtime;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Expressions;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.CodeGen.Core.Emitters;

public static class ParameterResolver
{
    public static List<ParameterInfo> Resolve(AstNode? node, CodeGenContext ctx)
    {
        var parameters = new List<ParameterInfo>();
        Traverse(node, parameters, ctx);
        return parameters;
    }

    private static void Traverse(AstNode? node, List<ParameterInfo> parameters, CodeGenContext ctx)
    {
        if (node == null)
            return;

        if (node is AstParam astParam)
        {
            string name = astParam.Name.Token.Value;
            if (parameters.Any(p => p.Name == name))
                ctx.Fail($"Duplicated parameter: '{name}'", DiagnosticKind.NAME, astParam);

            if (astParam.Flag is AstFlag flag &&
                !Validators.FlagChecker.CheckParameterFlags(flag.Flags.Select(f => f.Token.Value), out var flagError))
            {
                ctx.Fail(flagError ?? "Invalid parameter flags.", DiagnosticKind.SYNTAX, astParam);
            }
            bool isConst = astParam.Flag is AstFlag paramFlag && paramFlag.HasFlag("val");
            BifyType type = TypeResolver.Resolve(astParam.Type, ctx);
            parameters.Add(new ParameterInfo(name, type, isConst, astParam.Value));

            return;
        }

        if (node.Token.Type == TokenType.COMMA)
        {
            Traverse(node.Right, parameters, ctx);
            Traverse(node.Left, parameters, ctx);
            return;
        }

        ctx.Fail("Expected an AstParam node", DiagnosticKind.SYNTAX, node);
    }
    public static void FlattenComma(AstNode? node, List<AstNode> list)
    {
        if (node == null) return;
        if (node is AstBinaryOp bin && bin.Token.Type == TokenType.COMMA)
        {
            FlattenComma(bin.Left, list);
            FlattenComma(bin.Right, list);
        }
        else
        {
            list.Add(node);
        }
    }
}
