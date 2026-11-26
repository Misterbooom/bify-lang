#nullable enable
using System;
using System.Linq;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Expressions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Class;

public class ClassBuilder(CodeGenContext ctx) : CodeGenBase(ctx)
{
    public unsafe LLVMValueRef BuildInstance(ICompositeType compositeType) => compositeType switch
    {
        ClassType classType => BuildClassInstance(classType),
        StructType structType => BuildStructInstance(structType),
        _ => throw new NotSupportedException($"Unsupported composite type: {compositeType.GetType().Name}")
    };

    private unsafe LLVMValueRef BuildClassInstance(ClassType classType)
    {
        var vtableGlobal = classType.VTableGlobal.Handle != IntPtr.Zero
            ? classType.VTableGlobal
            : classType.VTable.GlobalValue;

        var sizeVal = LLVM.SizeOf(classType.StructType);
        var rawPtr = _ctx.HeapControl.BuildAlloc(sizeVal, vtableGlobal);
        return rawPtr;
    }

    private LLVMValueRef BuildStructInstance(StructType structType)
    {
        return _ctx.BuildEntryAlloca(structType.Type, $"{structType.Name}_inst");
    }

    public static void InitFields(CodeGenContext ctx, ICompositeType compositeType, LLVMValueRef instancePtr)
    {
        foreach (var field in compositeType.Fields.Values.OrderBy(f => f.Index))
        {
            if (field.IsPoison)
                continue;

            var fieldPtr = MemberResolver.ResolveFieldPtr(ctx, compositeType, instancePtr, field);

            if (field.ValueNode != null)
            {
                using (ctx.HeapControl.BeginTempScope())
                {
                    var initVal = new ExpressionDispatcher(ctx).EmitExpression(field.ValueNode);
                    initVal = TypeCaster.ImplicitCast(ctx, initVal, field.Type);

                    if (field.Type.IsReference)
                        ctx.HeapControl.BuildArcStoreInit(fieldPtr, initVal, field.IsWeak);
                    else
                        ctx.Builder.BuildStore(initVal.Value, fieldPtr);
                }
            }
            else
            {
                ctx.Builder.BuildStore(LLVMValueRef.CreateConstNull(field.Type.Type), fieldPtr);
            }
        }
    }
}
