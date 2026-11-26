#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Emitters;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public class CallGen(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public TypedValue EmitCall(AstCall astCall)
    {
        if (astCall.CallableName is AstMemberAccess)
            return HandleMethodCall(astCall);

        var target = new ExpressionDispatcher(_ctx).EmitExpression(astCall.CallableName);
        if (target.Type is not CallableType callableType)
            return Fail<TypedValue>("Can't call  " + astCall.CallableName.Token.Value, DiagnosticKind.TYPE, astCall);

        var args = ResolveArgs(astCall, callableType);

        var callReturn = _ctx.Builder.BuildCall2(callableType.Type, target.Value, args.Select(i => i.Value.Value).ToArray());
        return new TypedValue(callReturn, callableType.ReturnType);
    }

    private TypedValue HandleMethodCall(AstCall astCall)
    {
        if (astCall.CallableName is not AstMemberAccess memberAccess)
            throw new Exception("Target node is not member access");

        if (memberAccess.Right is not AstIdentifier id)
            return Fail<TypedValue>("Invalid member identifier.", DiagnosticKind.ATTRIBUTE, astCall);

        var (thisPtr, compositeType) = ResolveReceiver(memberAccess.Left, astCall);

        if (!compositeType.Methods.TryGetValue(id.Name, out var method))
            return Fail<TypedValue>($"Method '{id.Name}' does not exist", DiagnosticKind.ATTRIBUTE, astCall);

        if (method.IsPoison)
            return AbortPoison<TypedValue>();

        MemberResolver.CheckAccess(_ctx, method.DeclaringType, method.AccessLevel, method.Name);
        var callee = ResolveMethodCallee(compositeType, thisPtr, method);
        var args = ResolveArgs(astCall, method.Signature);

        LLVMValueRef[] rawArgs = [thisPtr, .. args.Select(i => i.Value.Value)];
        var callReturn = _ctx.Builder.BuildCall2(method.Signature.Type, callee, rawArgs);
        return new TypedValue(callReturn, method.Signature.ReturnType);
    }
    private (LLVMValueRef ThisPtr, ICompositeType CompositeType) ResolveReceiver(AstNode leftNode, AstCall astCall)
    {
        var lvalueResolver = new LValueResolver(_ctx);
        if (lvalueResolver.TryResolveLValue(leftNode, out var lvalue))
        {
            if (lvalue.Type is StructType structType)
                return (lvalue.Pointer, structType);
            if (lvalue.Type is ClassType classType)
            {
                var loaded = _ctx.Builder.BuildLoad2(classType.Type, lvalue.Pointer, "this_ptr");
                var upgraded = UpgradeWeakReference(new TypedValue(loaded, classType, lvalue.IsWeak));
                return (upgraded.Value, classType);
            }
            return Fail<(LLVMValueRef, ICompositeType)>(
                $"Cannot call member on non-composite type '{lvalue.Type.Name}'", DiagnosticKind.TYPE, astCall);
        }
        var left = new ExpressionDispatcher(_ctx).EmitExpression(leftNode);
        if (left.Type is StructType rvalueStruct)
        {
            var tmpPtr = _ctx.BuildEntryAlloca(rvalueStruct.LlvmStructType, "tmp_struct_this");
            _ctx.Builder.BuildStore(left.Value, tmpPtr);
            return (tmpPtr, rvalueStruct);
        }
        if (left.Type is ClassType rvalueClass)
        {
            var upgraded = UpgradeWeakReference(left);
            return (upgraded.Value, rvalueClass);
        }
        return Fail<(LLVMValueRef, ICompositeType)>(
            $"Cannot call member on non-composite type '{left.Type.Name}'", DiagnosticKind.TYPE, astCall);
    }
    private LLVMValueRef ResolveMethodCallee(ICompositeType compositeType, LLVMValueRef thisReference, ClassMethod method)
    {
        if (method.VTableIndex >= 0 && compositeType is ClassType)
        {
            var ptrType = LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0);
            var vptrSlot = _ctx.Builder.BuildStructGEP2(compositeType.LlvmStructType, thisReference, 0, "vptr_slot");
            var vtable = _ctx.Builder.BuildLoad2(ptrType, vptrSlot, "vtable");
            LLVMValueRef[] indices = [LLVMValueRef.CreateConstInt(_ctx.Context.Int32Type, (ulong)method.VTableIndex, false)];
            var slotPtr = _ctx.Builder.BuildGEP2(ptrType, vtable, indices, "slot_ptr");
            return _ctx.Builder.BuildLoad2(ptrType, slotPtr, $"{method.Name}_fn");
        }

        var callee = _ctx.Module.GetNamedFunction(method.Signature.FullName);
        if (callee.Handle == IntPtr.Zero)
            throw new Exception($"Method {method.Signature.FullName} not found");

        return callee;
    }

    private TypedValue UpgradeWeakReference(TypedValue reference)
    {
        if (reference.IsWeak)
        {
            var upgraded = _ctx.HeapControl.BuildUpgradeWeak(reference.Value);
            reference = new TypedValue(upgraded, reference.Type, false);
            _ctx.HeapControl.TrackTemp(reference);
        }

        return reference;
    }

    private ArgumentInfo[] ResolveArgs(AstCall astCall, CallableType callableType) =>
        ArgumentResolver.Resolve(
            _ctx,
            astCall.Arguments,
            callableType.ParametersType,
            astCall,
            "Function",
            callableType.Type.IsFunctionVarArg);
}
