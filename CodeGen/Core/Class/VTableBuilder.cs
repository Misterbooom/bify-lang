#nullable enable
using System;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Class;

public static class VTableBuilder
{
    public static VTable Inherit(ClassType? parent)
    {
        return parent?.VTable != null ? parent.VTable.Clone() : new VTable();
    }

    public static int ApplyDestructor(VTable vtable, ClassMethod destructor)
    {
        if (vtable.Slots.Count == 0)
        {
            return vtable.AddMethod(destructor);
        }

        vtable.Replace(0, destructor);
        return 0;
    }

    public static int Apply(CodeGenContext ctx, VTable vtable, ClassMethod method, AstNode? node = null)
    {
        if (method.Name == DestructorSynthesizer.DestructorName)
            return ApplyDestructor(vtable, method);

        bool hasParentSlot = vtable.TryFind(method.Name, out var slot);

        if (method.IsOverride)
        {
            if (!hasParentSlot || slot == null)
                ctx.Fail($"Method '{method.Name}' is marked 'override' but no virtual method with this name was found in base classes.", DiagnosticKind.TYPE, node);

            VerifyOverrideSignature(ctx, slot.Method, method, node);
            vtable.Replace(slot.Index, method);
            return slot.Index;
        }

        if (method.IsVirtual)
        {
            if (hasParentSlot)
                ctx.Fail($"Method '{method.Name}' is marked 'virtual' but already exists in base class VTable. Use 'override' instead.", DiagnosticKind.TYPE, node);

            return vtable.AddMethod(method);
        }

        if (hasParentSlot)
            ctx.Fail($"Method '{method.Name}' hides inherited virtual method without 'override' flag.", DiagnosticKind.TYPE, node);

        return -1;
    }

    private static void VerifyOverrideSignature(CodeGenContext ctx, ClassMethod parentMethod, ClassMethod overrideMethod, AstNode? node)
    {
        if (parentMethod.Signature.Type != overrideMethod.Signature.Type)
        {
            ctx.Fail(
                $"Override method '{overrideMethod.Name}' signature does not match base method '{parentMethod.DeclaringType}.{parentMethod.Name}'.",
                DiagnosticKind.TYPE,
                node);
        }
    }
    public static void Build(VTable vTable,CodeGenContext ctx, string className)
    {
        var ptrType = LLVMTypeRef.CreatePointer(ctx.Context.Int8Type, 0);
        int count = vTable.Slots.Count;
        var vtableArrayType = LLVMTypeRef.CreateArray(ptrType, (uint)count);
        var vtableGlobal = ctx.Module.AddGlobal(vtableArrayType, NameMangler.GenerateVTable(ctx.ModuleContext.Name, className));
        vtableGlobal.IsGlobalConstant = true;
        vtableGlobal.Linkage = LLVMLinkage.LLVMInternalLinkage;
        if (count > 0)
        {
            var fnPtrs = new LLVMValueRef[count];
            for (int i = 0; i < count; i++)
            {
                var slot = vTable.Slots[i];
                var fn = slot.Method.FunctionValue;
                if (fn.Handle == IntPtr.Zero)
                {
                    string symbol = NameMangler.GenerateMethodName(ctx.ModuleContext.Name, className, slot.MethodName);
                    fn = ctx.Module.GetNamedFunction(symbol);
                    if (fn.Handle == IntPtr.Zero)
                    {
                        fn = ctx.Module.AddFunction(symbol, slot.Method.Signature.Type);
                    }
                }
                fnPtrs[i] = fn;
            }
            vtableGlobal.Initializer = LLVMValueRef.CreateConstArray(ptrType, fnPtrs);
        }
        else
        {
            vtableGlobal.Initializer = LLVMValueRef.CreateConstNull(vtableArrayType);
        }
        vTable.SetGlobal(vtableGlobal, vtableArrayType);
    }
}
