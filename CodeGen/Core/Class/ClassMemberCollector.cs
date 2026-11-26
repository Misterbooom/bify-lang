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
        if (!FlagChecker.ParseFieldFlags(varDecl.Flag, classType is StructType, out var accessLevel, out var fieldFlags, out var error))
            ctx.Fail(error ?? "invalid field flags", DiagnosticKind.ATTRIBUTE, varDecl);

        if (varDecl.Type is AstIdentifier id && id.Name == "void")
            ctx.Fail("field requires explicit type", DiagnosticKind.TYPE, varDecl);

        if (varDecl.AssignmentNode.Right is AstNew && fieldFlags.HasFlag(FieldFlags.WEAK))
            ctx.Fail("cannot use 'weak' with 'new'", DiagnosticKind.TYPE, varDecl);

        var type = TypeResolver.Resolve(varDecl.Type, ctx);
        VisibilityChecker.CheckField(ctx, classType, accessLevel, type, varDecl);
        return new ClassField(name, type, varDecl.AssignmentNode.Right, accessLevel, index, fieldFlags, classType);
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
        if (!FlagChecker.ParseMethodFlags(funcDecl.FlagNode, classType is StructType, out var accessLevel, out var methodFlags, out var error))
            ctx.Error(error ?? "invalid method flags", DiagnosticKind.ATTRIBUTE, funcDecl);

        if (methodFlags.HasFlag(MethodFlags.ABSTRACT))
            methodFlags |= MethodFlags.VIRTUAL;

        var parameters = ParameterResolver.Resolve(funcDecl.ArgumentsNode, ctx);
        var returnType = ResolveMethodReturnType(funcDecl, ctx, name, parameters, ref methodFlags);
        VisibilityChecker.CheckMethodSignature(ctx, classType, accessLevel, returnType, parameters, funcDecl);

        var paramTypes = new List<LLVMTypeRef> { LLVMTypeRef.CreatePointer(ctx.Context.Int8Type, 0) };
        paramTypes.AddRange(parameters.Select(p => p.Type.Type));
        var mangledName = NameMangler.GenerateMethodName(ctx.ModuleContext.Name, classType.Name, name);
        var funcType = LLVMTypeRef.CreateFunction(returnType.Type, paramTypes.ToArray(), false);
        var signature = new CallableType(name, mangledName, [..parameters], returnType, funcType, true);
        var functionValue = ctx.Module.AddFunction(mangledName, funcType);

        if (methodFlags.HasFlag(MethodFlags.ABSTRACT) && !classType.Flags.HasFlag(TypeFlags.ABSTRACT))
        {
            ctx.Error("abstract method in non-abstract class", DiagnosticKind.TYPE, funcDecl.FlagNode!.Flags.First(i => i.Token.Value == "abstract"));
        }

        return new ClassMethod(name, signature, functionValue, accessLevel, -1, classType, funcDecl.BlockNode, methodFlags, parameters);
    }

    public static ClassMethod CreatePoisonMethod(CodeGenContext ctx, string name, ICompositeType classType)
    {
        var voidType = BifyType.CreateVoid(ctx);
        var fnType = LLVMTypeRef.CreateFunction(voidType.Type, [], false);
        var type = new CallableType(name, name, [], voidType, fnType, true);
        return new ClassMethod(name, type, default, AccessLevel.PUBLIC, -1, classType, isPoison: true);
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
        ref MethodFlags methodFlags)
    {
        if (name == "constructor")
        {
            if (methodFlags.HasFlag(MethodFlags.VIRTUAL) ||
                methodFlags.HasFlag(MethodFlags.OVERRIDE) ||
                methodFlags.HasFlag(MethodFlags.ABSTRACT))
            {
                ctx.Fail("constructor cannot be 'virtual', 'override', or 'abstract'", DiagnosticKind.TYPE, funcDecl);
            }

            return BifyType.CreateVoid(ctx);
        }

        if (name == "destructor")
        {
            if (parameters.Count > 0)
                ctx.Fail("destructor cannot take parameters", DiagnosticKind.SYNTAX, funcDecl);

            methodFlags |= MethodFlags.VIRTUAL;
            return BifyType.CreateVoid(ctx);
        }

        return TypeResolver.Resolve(funcDecl.TypeNode, ctx);
    }
}


