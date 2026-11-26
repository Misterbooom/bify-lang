#nullable enable
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Types.Service;

public static class TypeCaster
{
    public static bool CanImplicitCast(BifyType from, BifyType to)
    {
        if (from is ClassType fromClass && to is ClassType toClass)
            return fromClass.IsSubclassOf(toClass);
        if (from is StructType fromStruct && to is StructType toStruct)
        {
            return fromStruct.FullName == toStruct.FullName;
        }

        if (from.Name == to.Name || from.Type == to.Type)
            return true;

        if (from.Name == "int" && to.Name == "float")
            return true;

        if (from.Name == "int" && to.Name == "double")
            return true;

        if (from.Name == "float" && to.Name == "double")
            return true;

        if (from.Name == "bool" && to.Name == "int")
            return true;

        if (from.Name == "int" && to.Name == "bool")
            return true;

        if (from.Name == "null" && to.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind)
            return true;


        if (from.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind && to.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind)
            return true;

        return false;
    }


    public static TypedValue ImplicitCast(CodeGenContext ctx, TypedValue value, BifyType targetType)
    {
        if (value.Type is ClassType valClass && targetType is ClassType tgtClass)
        {
            if (valClass.IsSubclassOf(tgtClass))
                return new TypedValue(value.Value, targetType, value.IsWeak);

            return ctx.Fail<TypedValue>($"cannot convert '{valClass.Name}' to '{tgtClass.Name}'",
                DiagnosticKind.TYPE);
        }

        if (value.Type is StructType valStruct && targetType is StructType targetStruct)
        {
            if (valStruct.FullName == targetStruct.FullName)
                return value;

            return ctx.Fail<TypedValue>($"cannot convert '{valStruct.FullName}' to '{targetStruct.FullName}'",
                DiagnosticKind.TYPE);
        }

        if (value.Type.Name == targetType.Name || value.Type.Type == targetType.Type)
            return value;

        if (TryCastNumericImplicit(ctx, value, targetType, out var numeric))
            return numeric;

        if (value.Type.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind &&
            targetType.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind)
            return CastPointer(ctx, value, targetType, "conv_ptr");

        return ctx.Fail<TypedValue>($"cannot convert '{value.Type.Name}' to '{targetType.Name}'",
            DiagnosticKind.TYPE);
    }


    public static TypedValue ExplicitCast(CodeGenContext ctx, TypedValue value, BifyType targetType)
    {
        if (value.Type is ClassType valClass && targetType is ClassType tgtClass)
            return CastClass(ctx, valClass, tgtClass, value, targetType);

        if (value.Type is StructType valStruct && targetType is StructType targetStruct)
        {
            if (valStruct.FullName == targetStruct.FullName)
                return value;

            return ctx.Fail<TypedValue>($"cannot cast '{valStruct.FullName}' to '{targetStruct.FullName}'",
                DiagnosticKind.CAST);
        }

        if (value.Type.Name == targetType.Name)
            return value;

        if (value.Type.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind &&
            targetType.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind)
            return value.Type.Name == "null" ? value : CastPointer(ctx, value, targetType, "cast_ptr");

        if (CanImplicitCast(value.Type, targetType))
            return ImplicitCast(ctx, value, targetType);

        if (TryCastNumericExplicit(ctx, value, targetType, out var numeric))
            return numeric;

        if (value.Type.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind && targetType.Name == "int")
            return new TypedValue(ctx.Builder.BuildPtrToInt(value.Value, targetType.Type, "cast_ptrtoi"), targetType);

        if (value.Type.Name == "int" && targetType.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind)
            return new TypedValue(ctx.Builder.BuildIntToPtr(value.Value, targetType.Type, "cast_itoptr"), targetType);

        return ctx.Fail<TypedValue>($"cannot cast '{value.Type.Name}' to '{targetType.Name}'", DiagnosticKind.CAST);
    }

    private static TypedValue CastClass(CodeGenContext ctx, ClassType valClass, ClassType tgtClass, TypedValue value,
        BifyType targetType)
    {
        if (valClass.IsSubclassOf(tgtClass) || tgtClass.IsSubclassOf(valClass))
            return new TypedValue(value.Value, targetType, value.IsWeak);

        return ctx.Fail<TypedValue>($"cannot cast '{valClass.FullName}' to '{tgtClass.FullName}'", DiagnosticKind.CAST);
    }

    private static TypedValue CastPointer(CodeGenContext ctx, TypedValue value, BifyType targetType, string irName)
    {
        if (value.Value.TypeOf == targetType.Type)
            return new TypedValue(value.Value, targetType, value.IsWeak);

        var casted = ctx.Builder.BuildPointerCast(value.Value, targetType.Type, irName);
        var result = new TypedValue(casted, targetType, value.IsWeak);
        if (value.IsReference && ctx.HeapControl.ConsumeTemp(value.Value))
            ctx.HeapControl.TrackTemp(result);
        return result;
    }

    private static bool TryCastNumericImplicit(
        CodeGenContext ctx,
        TypedValue value,
        BifyType targetType,
        out TypedValue result)
    {
        if (value.Type.Name == "int" && targetType.Name is "float" or "double")
        {
            var tag = targetType.Name == "float" ? "conv_itof" : "conv_itod";
            result = new TypedValue(ctx.Builder.BuildSIToFP(value.Value, targetType.Type, tag), targetType);
            return true;
        }

        if (value.Type.Name == "float" && targetType.Name == "double")
        {
            result = new TypedValue(ctx.Builder.BuildFPExt(value.Value, targetType.Type, "conv_ftod"), targetType);
            return true;
        }

        if (value.Type.Name == "bool" && targetType.Name == "int")
        {
            result = new TypedValue(ctx.Builder.BuildZExt(value.Value, targetType.Type, "conv_btoi"), targetType);
            return true;
        }

        if (value.Type.Name == "int" && targetType.Name == "bool")
        {
            var zero = LLVMValueRef.CreateConstInt(value.Value.TypeOf, 0, false);
            result = new TypedValue(ctx.Builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, value.Value, zero, "conv_itob"),
                targetType);
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryCastNumericExplicit(
        CodeGenContext ctx,
        TypedValue value,
        BifyType targetType,
        out TypedValue result)
    {
        if (value.Type.Name is "float" or "double" && targetType.Name is "int" or "char")
        {
            result = new TypedValue(ctx.Builder.BuildFPToSI(value.Value, targetType.Type, "cast_fptosi"), targetType);
            return true;
        }

        if (value.Type.Name == "double" && targetType.Name == "float")
        {
            result = new TypedValue(ctx.Builder.BuildFPTrunc(value.Value, targetType.Type, "cast_fptrunc"), targetType);
            return true;
        }

        if (value.Type.Name is "float" or "double" && targetType.Name == "bool")
        {
            var zero = LLVMValueRef.CreateConstReal(value.Value.TypeOf, 0.0);
            result = new TypedValue(
                ctx.Builder.BuildFCmp(LLVMRealPredicate.LLVMRealONE, value.Value, zero, "cast_fptobool"), targetType);
            return true;
        }

        if (value.Type.Name == "bool" && targetType.Name is "float" or "double")
        {
            result = new TypedValue(ctx.Builder.BuildUIToFP(value.Value, targetType.Type, "cast_uitofp"), targetType);
            return true;
        }

        if (value.Type.Name == "int" && targetType.Name == "char")
        {
            result = new TypedValue(ctx.Builder.BuildTrunc(value.Value, targetType.Type, "cast_trunc"), targetType);
            return true;
        }

        if (value.Type.Name == "char" && targetType.Name == "int")
        {
            result = new TypedValue(ctx.Builder.BuildSExt(value.Value, targetType.Type, "cast_sext"), targetType);
            return true;
        }

        result = default;
        return false;
    }
}
