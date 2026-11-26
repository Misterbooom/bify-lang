#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.Exceptions;
using BifyLang.Lexer;

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
                        $"Cannot access private type '{type.Name}' from module '{((ICompositeType)type).ModuleName}'.",
                        DiagnosticKind.ACCESS_VIOLATION,
                        typeNode);
                }
                return type;
            }
            BifyDebug.Log(NameMangler.GenerateTypeName(ctx.ModuleContext.Name, id.Name));
            return ctx.Fail<BifyType>("Cannot resolve type " + id.Name, DiagnosticKind.NAME, typeNode);
        }
        return typeNode switch
        {
            AstMemberAccess memberAccess => ResolveMemberType(memberAccess, ctx),
            // AstUnaryOperator unary when unary.Token.Type is TokenType.POINTER or TokenType.MUL =>
            //     ResolvePointer(unary, ctx),
            // AstIndexOperator indexOp =>
            //     ResolveArray(indexOp, ctx),
            _ => ctx.Fail<BifyType>($"{typeNode.GetType()} is not a known type.", DiagnosticKind.UNKNOWN, typeNode)
        };
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
                        $"Cannot access private type '{type.Name}' from module '{((ICompositeType)type).ModuleName}'.",
                        DiagnosticKind.ACCESS_VIOLATION,
                        memberAccess);
                }
                return type;
            }
            return ctx.Fail<BifyType>($"Cannot resolve type '{modId.Name}.{typeId.Name}'.", DiagnosticKind.NAME, memberAccess);
        }
        return ctx.Fail<BifyType>("Invalid type expression.", DiagnosticKind.UNKNOWN, memberAccess);
    }

    private static bool IsPrivateType(BifyType type, CodeGenContext ctx) =>
        type is ICompositeType compositeType &&
        !compositeType.Flags.HasFlag(TypeFlags.PUBLIC) &&
        ctx.ModuleContext.Name != compositeType.ModuleName;
    // private static BifyType ResolvePointer(AstUnaryOperator unary, CodeGenContext ctx)
    // {
    //     var baseType = Resolve(unary.Operand, ctx);
    //     string baseName = baseType is ClassType cls ? cls.FullName : baseType.Name;
    //     return ctx.TypeRegistry.GetBifyType($"{baseName}*");
    // }
    //
    // private static BifyType ResolveArray(AstIndexOperator indexOp, CodeGenContext ctx)
    // {
    //     var elemType = Resolve(indexOp.TargetNode, ctx);
    //     string elemName = elemType is ClassType cls ? cls.FullName : elemType.Name;
    //     string arrName = indexOp.IndexNode is AstInteger num
    //         ? $"{elemName}[{num.Value}]"
    //         : $"{elemName}[]";
    //     return ctx.TypeRegistry.GetBifyType(arrName);
    // }
}
