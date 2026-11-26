#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Validators;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Class;

public static class MemberResolver
{
    public static LLVMValueRef ResolveFieldPtr(CodeGenContext ctx, ICompositeType compositeType, LLVMValueRef instance, ClassField field) =>
        ctx.Builder.BuildStructGEP2(compositeType.LlvmStructType, instance, (uint)field.Index, field.Name);

    public static bool TryResolveThisField(CodeGenContext ctx, string name, out LLVMValueRef fieldPtr, out ClassField field)
    {
        fieldPtr = default;
        field = default;
        if (!ctx.CurrentScope.TryLookup("this", out var thisSym) ||
            thisSym.Type is not ICompositeType ct ||
            !ct.Fields.TryGetValue(name, out var found))
            return false;

        if (found.IsPoison)
            ctx.AbortPoison();

        field = found;
        CheckAccess(ctx, ct, field.AccessLevel, field.Name);
        var thisVal = thisSym.Type.IsReference
            ? ctx.Builder.BuildLoad2(thisSym.Type.Type, thisSym.Pointer, "this")
            : thisSym.Pointer;
        fieldPtr = ResolveFieldPtr(ctx, ct, thisVal, field);
        return true;
    }

    public static void CheckAccess(CodeGenContext ctx, ICompositeType compositeType, AccessLevel accessLevel, string memberName)
    {
        if (accessLevel == AccessLevel.PRIVATE)
        {
            if (!ctx.CurrentScope.TryLookup("this", out var thisSym) ||
                thisSym.Type is not ICompositeType ct ||
                ct.FullName != compositeType.FullName)
            {
                ctx.Fail($"cannot access private member '{memberName}' of '{compositeType.FullName}'", DiagnosticKind.ATTRIBUTE);
            }
        }
        else if (accessLevel == AccessLevel.PROTECTED)
        {
            bool allowed = false;
            if (ctx.CurrentScope.TryLookup("this", out var thisSym) && thisSym.Type is ClassType ct)
            {
                if (ctx.TypeRegistry.TryGetType(compositeType.FullName, out var declType) && declType is ClassType declClassType)
                {
                    allowed = ct.IsSubclassOf(declClassType);
                }
            }

            if (!allowed)
                ctx.Fail($"cannot access protected member '{memberName}' of '{compositeType.FullName}'", DiagnosticKind.ATTRIBUTE);
        }
    }

    public static TypeFlags ResolveTypeFlags(CodeGenContext ctx, AstTypeDecl typeDecl)
    {
        bool isStruct = typeDecl is AstStruct;
        if (!FlagChecker.ParseTypeFlags(typeDecl.FlagNode, isStruct, out var flags, out var error) &&
            !string.IsNullOrEmpty(error))
        {
            ctx.Error(error, DiagnosticKind.ATTRIBUTE, typeDecl.FlagNode);
        }

        return flags;
    }
    public static TypeFlags ResolveFunctionFlags(CodeGenContext ctx, AstFunctionDecl functionDecl)
    {
        AstFlag flagNode = functionDecl.FlagNode;
        TypeFlags flags = TypeFlags.NONE;
        FlagChecker.CheckFunctionFlags(functionDecl.FlagNode.ToStringArray(), out var error);
        if (!string.IsNullOrEmpty(error))
        {
            ctx.Error(error, DiagnosticKind.ATTRIBUTE, functionDecl.FlagNode);
        }

        if (flagNode.HasFlag("public")) flags |= TypeFlags.PUBLIC;
        if (flagNode.HasFlag("static")) flags |= TypeFlags.STATIC;
        if (flagNode.HasFlag("abstract")) flags |= TypeFlags.ABSTRACT;

        return flags;
    }
}
