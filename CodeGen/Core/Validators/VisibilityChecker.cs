#nullable enable
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Core.Validators;

public static class VisibilityChecker
{
    private static bool IsPrivateType(BifyType type) =>
        type is ICompositeType composite && !composite.Flags.HasFlag(TypeFlags.PUBLIC) ;

    private static bool IsLeakingPrivateType(
        BifyType type,
        AccessLevel memberAccess,
        ICompositeType? declaringType = null)
    {
        if (declaringType != null && !declaringType.Flags.HasFlag(TypeFlags.PUBLIC))
            return false;

        if (memberAccess is not (AccessLevel.PUBLIC or AccessLevel.PROTECTED))
            return false;

        return IsPrivateType(type);
    }

    public static void CheckFunctionSignature(
        CodeGenContext ctx,
        AccessLevel access,
        BifyType returnType,
        IReadOnlyList<ParameterInfo> parameters,
        AstFunctionDecl funcDecl)
    {
        if (IsLeakingPrivateType(returnType, access))
        {
            ctx.Fail(
                $"Cannot return private type '{returnType.Name}' from public function '{funcDecl.FunctionNameNode.Name}'.",
                DiagnosticKind.ACCESS_VIOLATION,
                funcDecl.TypeNode);
        }

        foreach (var param in parameters)
        {
            if (IsLeakingPrivateType(param.Type, access))
            {
                ctx.Fail(
                    $"Exposing private type '{param.Type.Name}' in parameter '{param.Name}' of public function '{funcDecl.FunctionNameNode.Name}'.",
                    DiagnosticKind.ACCESS_VIOLATION,
                    funcDecl.ArgumentsNode);
            }
        }
    }

    public static void CheckMethodSignature(
        CodeGenContext ctx,
        ICompositeType declaringType,
        AccessLevel access,
        BifyType returnType,
        IReadOnlyList<ParameterInfo> parameters,
        AstFunctionDecl funcDecl)
    {
        string visibility = access == AccessLevel.PROTECTED ? "protected" : "public";
        if (IsLeakingPrivateType(returnType, access, declaringType))
        {
            ctx.Error(
                $"Cannot return private type '{returnType.Name}' from {visibility} method '{declaringType.Name}.{funcDecl.FunctionNameNode.Name}'.",
                DiagnosticKind.ACCESS_VIOLATION,
                funcDecl.TypeNode);
        }

        foreach (var param in parameters)
        {
            if (IsLeakingPrivateType(param.Type, access, declaringType))
            {
                ctx.Error(
                    $"Exposing private type '{param.Type.Name}' in parameter '{param.Name}' of {visibility} method '{declaringType.Name}.{funcDecl.FunctionNameNode.Name}'.",
                    DiagnosticKind.ACCESS_VIOLATION,
                    funcDecl.ArgumentsNode);
            }
        }
    }

    public static void CheckField(
        CodeGenContext ctx,
        ICompositeType declaringType,
        AccessLevel access,
        BifyType fieldType,
        AstVarDecl varDecl)
    {
        if (!IsLeakingPrivateType(fieldType, access, declaringType))
            return;

        string visibility = access == AccessLevel.PROTECTED ? "protected" : "public";
        string fieldName = varDecl.AssignmentNode.Left.Token.Value;
        ctx.Error(
            $"Exposing private type '{fieldType.Name}' in {visibility} field '{declaringType.Name}.{fieldName}'.",
            DiagnosticKind.ACCESS_VIOLATION,
            varDecl.Type);
    }

    public static void CheckInheritance(
        CodeGenContext ctx,
        ClassType derivedType,
        ClassType parentType,
        AstNode? parentNode)
    {
        if (!derivedType.Flags.HasFlag(TypeFlags.PUBLIC) || parentType.Flags.HasFlag(TypeFlags.PUBLIC))
            return;

        ctx.Error(
            $"Public class '{derivedType.Name}' can't inherit from private class '{parentType.Name}'.",
            DiagnosticKind.ACCESS_VIOLATION,
            parentNode);
    }
}
