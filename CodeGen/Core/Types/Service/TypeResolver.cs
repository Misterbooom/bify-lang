#nullable enable
using System.Diagnostics.CodeAnalysis;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Core.Types.Service;

public static class TypeResolver
{
    public static BifyType Resolve(AstNode? typeNode, CodeGenContext ctx)
    {
        if (typeNode == null)
            return ctx.TypeRegistry.GetBifyType("void");

        if (typeNode is AstIdentifier id)
        {
            if (ctx.TypeRegistry.TryGetType(id.Name, out var type))
            {
                return type;
            }

            BifyDebug.Log(ctx.TypeRegistry.ToString());
            if (ctx.TypeRegistry.TryGetType(NameMangler.GenerateTypeName(ctx.ModuleContext.Name, id.Name), out type))
            {
                if (IsPrivateType(type, ctx))
                {
                    return ctx.Fail<BifyType>(
                        $"cannot access private type '{type.Name}' in module '{((ICompositeType)type).ModuleName}'",
                        DiagnosticKind.ATTRIBUTE,
                        typeNode);
                }

                return type;
            }

            BifyDebug.Log(NameMangler.GenerateTypeName(ctx.ModuleContext.Name, id.Name));
            return ctx.Fail<BifyType>($"unknown type '{id.Name}'", DiagnosticKind.NAME, typeNode);
        }

        return typeNode switch
        {
            AstMemberAccess memberAccess => ResolveMemberType(memberAccess, ctx),
            // AstUnaryOperator unary when unary.Token.Type is TokenType.POINTER or TokenType.MUL =>
            //     ResolvePointer(unary, ctx),
            // AstIndexOperator indexOp =>
            //     ResolveArray(indexOp, ctx),
            _ => ctx.Fail<BifyType>($"invalid type '{typeNode.Token.Value}'", DiagnosticKind.UNKNOWN, typeNode)
        };
    }

    public static bool TryResolve(AstNode? typeNode, CodeGenContext ctx, out BifyType? resolvedType)
    {
        if (typeNode == null)
        {
            resolvedType = ctx.TypeRegistry.GetBifyType("void");
            return true;
        }

        if (typeNode is AstIdentifier id)
        {
            if (TryResolveIdentifier(id.Name, ctx.ModuleContext.Name, ctx, out resolvedType))
            {
                return true;
            }
        }

        resolvedType = null;
        if (typeNode is AstMemberAccess memberAccess)
        {
            return TryResolveMemberType(memberAccess, ctx, out resolvedType);
        }

        resolvedType = null;
        return false;
    }

    public static bool TryResolveIdentifier(string idName, string moduleName, CodeGenContext ctx,
        out BifyType? resolvedType)
    {
        resolvedType = null;
        if (ctx.TypeRegistry.TryGetType(idName, out var type))
        {
            resolvedType = type;
            return true;
        }

        if (ctx.TypeRegistry.TryGetType(NameMangler.GenerateTypeName(moduleName, idName), out type))
        {
            if (IsPrivateType(type, ctx))
            {
                resolvedType = null;
                return false;
            }

            resolvedType = type;
            return true;
        }

        return false;
    }

    private static BifyType ResolveMemberType(AstMemberAccess memberAccess, CodeGenContext ctx)
    {
        if (memberAccess.Left is AstIdentifier modId && memberAccess.Right is AstIdentifier typeId)
        {
            string mangled = NameMangler.GenerateTypeName(modId.Name, typeId.Name);
            if (ctx.TypeRegistry.TryGetType(mangled, out var type))
            {
                if (IsPrivateType(type, ctx))
                {
                    return ctx.Fail<BifyType>(
                        $"cannot access private type '{type.Name}' in module '{((ICompositeType)type).ModuleName}'",
                        DiagnosticKind.ATTRIBUTE,
                        memberAccess);
                }

                return type;
            }

            return ctx.Fail<BifyType>($"unknown type '{modId.Name}.{typeId.Name}'", DiagnosticKind.NAME,
                memberAccess);
        }

        return ctx.Fail<BifyType>("invalid type expression", DiagnosticKind.UNKNOWN, memberAccess);
    }

    private static bool TryResolveMemberType(
        AstMemberAccess memberAccess,
        CodeGenContext ctx,
        [NotNullWhen(true)] out BifyType? resolvedType)
    {
        if (memberAccess.Left is AstIdentifier modId && memberAccess.Right is AstIdentifier typeId)
        {
            string mangled = NameMangler.GenerateTypeName(modId.Name, typeId.Name);
            if (ctx.TypeRegistry.TryGetType(mangled, out var type) && !IsPrivateType(type, ctx))
            {
                resolvedType = type;
                return true;
            }
        }

        resolvedType = null;
        return false;
    }

    private static bool IsPrivateType(BifyType type, CodeGenContext ctx) =>
        type is ICompositeType compositeType &&
        !compositeType.Flags.HasFlag(TypeFlags.PUBLIC) &&
        ctx.ModuleContext.Name != compositeType.ModuleName;
}
