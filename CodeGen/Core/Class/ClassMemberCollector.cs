#nullable enable
using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.Ast.Handlers;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Emitters;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Core.Validators;
using BifyLang.CodeGen.Statements;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Class;

public static class ClassMemberCollector
{
    public static ClassField CollectField(AstVarDecl varDecl, CodeGenContext ctx, ICompositeType classType, int index)
    {
        string name = varDecl.AssignmentNode.Left.Token.Value;
        var flags = varDecl.Flag?.Flags?.Select(f => f.Token.Value) ?? [];

        if (classType is StructType)
        {
            if (!FlagChecker.CheckStructAttributeFlags(flags, out var error))
                ctx.Fail(error ?? "Invalid attribute flags.", DiagnosticKind.ATTRIBUTE, varDecl);
        }
        else if (classType is ClassType)
        {
            if (!FlagChecker.CheckAttributeFlags(flags, out var error))
                ctx.Fail(error ?? "Invalid attribute flags.", DiagnosticKind.ATTRIBUTE, varDecl);
        }

        if (varDecl.Type is AstIdentifier id && id.Name == "void")
        {
            ctx.Fail("Fields must have an explicit type.", DiagnosticKind.TYPE, varDecl);
        }

        bool isWeak = varDecl.Flag?.HasFlag("weak") ?? false;
        if (varDecl.AssignmentNode.Right is AstNew && isWeak)
        {
            ctx.Fail("Can't use weak flag with new statement.", DiagnosticKind.TYPE, varDecl);
        }

        var accessLevel = ParseAccessLevel(varDecl.Flag);
        var type = TypeResolver.Resolve(varDecl.Type, ctx);
        var value = varDecl.AssignmentNode.Right;
        bool isConst = varDecl.Flag?.HasFlag("val") ?? false;

        return new ClassField(name, type, value, accessLevel, index, isConst, isWeak, classType);
    }

    public static ClassField CollectFieldOrPoison(AstVarDecl varDecl, CodeGenContext ctx, ICompositeType classType, string name, int index)
    {
        if (varDecl.IsPoison || varDecl.AssignmentNode.Right is AstPoison)
        {
            return new ClassField(name, BifyType.CreateInt(ctx), null, AccessLevel.PUBLIC, index, declaringType: classType, isPoison: true);
        }

        try
        {
            return CollectField(varDecl, ctx, classType, index);
        }
        catch (BifyCompilationException)
        {
            return new ClassField(name, BifyType.CreateInt(ctx), null, AccessLevel.PUBLIC, index, declaringType: classType, isPoison: true);
        }
    }

    public static ClassMethod CollectMethod(AstFunctionDecl funcDecl, CodeGenContext ctx, ICompositeType classType)
    {
        string name = funcDecl.FunctionNameNode.Name;
        var flags = funcDecl.FlagNode?.Flags.Select(f => f.Token.Value) ?? [];

        if (classType is StructType)
        {
            if (!FlagChecker.CheckStructMethodFlags(flags, out var error))
                ctx.Fail(error ?? "Invalid method flags.", DiagnosticKind.ATTRIBUTE, funcDecl);
        }
        else if (classType is ClassType)
        {
            if (!FlagChecker.CheckMethodFlags(flags, out var error))
                ctx.Fail(error ?? "Invalid method flags.", DiagnosticKind.ATTRIBUTE, funcDecl);
        }


    var accessLevel = ParseAccessLevel(funcDecl.FlagNode);
        bool isVirtual = funcDecl.FlagNode?.HasFlag("virtual") ?? false;
        bool isOverride = funcDecl.FlagNode?.HasFlag("override") ?? false;
        var parameters = ParameterResolver.Resolve(funcDecl.ArgumentsNode, ctx);
        var returnType = ResolveMethodReturnType(funcDecl, ctx, name, parameters, ref isVirtual, isOverride);

        var paramTypes = new List<LLVMTypeRef> { LLVMTypeRef.CreatePointer(ctx.Context.Int8Type, 0) };
        paramTypes.AddRange(parameters.Select(p => p.Type.Type));
        var mangledName = NameMangler.GenerateMethodName(ctx.ModuleContext.Name, classType.Name, name);
        var funcType = LLVMTypeRef.CreateFunction(returnType.Type, paramTypes.ToArray(), false);
        var signature = new CallableType(name, mangledName, [..parameters], returnType, funcType, true);
        var functionValue = ctx.Module.AddFunction(mangledName, funcType);

        return new ClassMethod(name, signature, functionValue, accessLevel, -1, classType, funcDecl.BlockNode, isVirtual, isOverride, parameters);
    }

    public static ClassMethod CreatePoisonMethod(CodeGenContext ctx, string name, ICompositeType classType)
    {
        var voidType = BifyType.CreateVoid(ctx);
        var fnType = LLVMTypeRef.CreateFunction(voidType.Type, [], false);
        var sig = new CallableType(name, name, [], voidType, fnType, true);
        return new ClassMethod(name, sig, default, AccessLevel.PUBLIC, -1, classType, isPoison: true);
    }

    public static ClassMethod CollectDestructorOrDefault(AstFunctionDecl? dtorNode, CodeGenContext ctx, ClassType classType)
    {
        if (dtorNode == null)
            return DestructorSynthesizer.CreateDefault(ctx, classType);

        try
        {
            return CollectMethod(dtorNode, ctx, classType);
        }
        catch (BifyCompilationException)
        {
            return DestructorSynthesizer.CreateDefault(ctx, classType);
        }
    }

    private static BifyType ResolveMethodReturnType(
        AstFunctionDecl funcDecl,
        CodeGenContext ctx,
        string name,
        List<ParameterInfo> parameters,
        ref bool isVirtual,
        bool isOverride)
    {
        if (name == "constructor")
        {
            if (isVirtual || isOverride)
                ctx.Fail("Constructor cannot be marked 'virtual' or 'override'.", DiagnosticKind.TYPE, funcDecl);

            return BifyType.CreateVoid(ctx);
        }

        if (name == "destructor")
        {
            if (parameters.Count > 0)
                ctx.Fail("Destructor cannot have parameters.", DiagnosticKind.SYNTAX, funcDecl);

            isVirtual = true;
            return BifyType.CreateVoid(ctx);
        }

        return TypeResolver.Resolve(funcDecl.TypeNode, ctx);
    }

    private static AccessLevel ParseAccessLevel(AstFlag? flag)
    {
        if (flag == null)
            return AccessLevel.PUBLIC;
        if (flag.HasFlag("private"))
            return AccessLevel.PRIVATE;
        if (flag.HasFlag("protected"))
            return AccessLevel.PROTECTED;
        return AccessLevel.PUBLIC;
    }
}
