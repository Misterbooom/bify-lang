#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public static class IdentifierGen
{
    public static TypedValue EmitIdentifier(CodeGenContext ctx, AstIdentifier identifier)
    {
        return identifier.Name switch
        {
            "true"  => LiteralGen.EmitBool(ctx, new AstBoolean(identifier.Token, true)),
            "false" => LiteralGen.EmitBool(ctx, new AstBoolean(identifier.Token, false)),
            "null"  => LiteralGen.EmitNull(ctx, new AstNull(identifier.Token)),
            _ => ResolveAndLoad(ctx, identifier)
        };
    }

    private static TypedValue ResolveAndLoad(CodeGenContext ctx, AstIdentifier identifier)
    {
        string name = identifier.Name;
        if (ctx.CurrentScope.TryLookup(name, out var symbol))
        {
            if (symbol.IsPoison)
                return ctx.AbortPoison<TypedValue>();

            return LoadSymbol(ctx, symbol);
        }

        if (MemberResolver.TryResolveThisField(ctx, name, out var fieldPtr, out var field))
        {
            var loaded = ctx.Builder.BuildLoad2(field.Type.Type, fieldPtr, field.Name);
            return new TypedValue(loaded, field.Type, field.IsWeak);
        }

        if (ctx.ModulesMap.TryGetValue(name, out var moduleType))
        {
            return new TypedValue(default, moduleType);
        }

        return ctx.Fail<TypedValue>($"undeclared identifier '{name}'", DiagnosticKind.NAME, identifier);
    }

    private static TypedValue LoadSymbol(CodeGenContext ctx, Symbol symbol)
    {
        if (symbol.Type.Type.Kind == LLVMTypeKind.LLVMFunctionTypeKind)
        {
            return new TypedValue(symbol.Pointer, symbol.Type);
        }
        var loaded = ctx.Builder.BuildLoad2(symbol.Type.Type, symbol.Pointer, symbol.Name);
        return new TypedValue(loaded, symbol.Type, symbol.IsWeak);
    }
}
