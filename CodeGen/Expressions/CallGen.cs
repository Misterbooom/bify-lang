#nullable enable
using System;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Emitters;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Core.Validators;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public class CallGen(CodeGenContext ctx) : CodeGenBase(ctx)
{
    public TypedValue EmitCall(AstCall call)
    {
        if (call.CallableName is AstMemberAccess)
            return HandleMemberCall(call);

        var target = new ExpressionDispatcher(_ctx).EmitExpression(call.CallableName);
        if (target.Type is not CallableType fn)
            return Fail<TypedValue>($"cannot call '{call.CallableName.Token.Value}'", DiagnosticKind.TYPE, call);

        var args = ResolveArgs(call, fn);
        var raw = args.Select(a => a.Value.Value).ToArray();
        var res = _ctx.Builder.BuildCall2(fn.Type, target.Value, raw);
        return new TypedValue(res, fn.ReturnType);
    }

    private TypedValue HandleMemberCall(AstCall call)
    {
        if (call.CallableName is not AstMemberAccess access)
            throw new Exception("Target node is not member access");

        if (access.Right is not AstIdentifier id)
            return Fail<TypedValue>("expected member identifier", DiagnosticKind.ATTRIBUTE, call);

        var (thisPtr, type, moduleType) = ResolveReceiver(access.Left, call);
        if (moduleType != null)
            return HandleModuleCall(call, moduleType, id.Name);

        if (!type!.Methods.TryGetValue(id.Name, out var method))
            return Fail<TypedValue>($"no method '{id.Name}' in '{type.Name}'", DiagnosticKind.ATTRIBUTE, call);

        if (method.IsPoison)
            return AbortPoison<TypedValue>();

        MemberResolver.CheckAccess(_ctx, method.DeclaringType, method.AccessLevel, method.Name);

        var callee = ResolveMethodCallee(type, thisPtr!.Value, method);
        var args = ResolveArgs(call, method.Signature);
        LLVMValueRef[] raw = [thisPtr.Value, .. args.Select(a => a.Value.Value)];

        var res = _ctx.Builder.BuildCall2(method.Signature.Type, callee, raw);
        return new TypedValue(res, method.Signature.ReturnType);
    }

    private TypedValue HandleModuleCall(AstCall call, ModuleType mod, string name)
    {
        if (!mod.ModuleContext.CurrentScope.TryLookup(name, out var sym))
            return Fail<TypedValue>($"no function '{name}' in module '{mod.Name}'", DiagnosticKind.ATTRIBUTE, call);

        if (sym.AccessLevel == AccessLevel.PRIVATE)
            return Fail<TypedValue>($"cannot access private function '{name}' in module '{mod.Name}'", DiagnosticKind.ACCESS_VIOLATION, call.CallableName);

        if (sym.Type is not CallableType fn)
            return Fail<TypedValue>($"'{name}' in module '{mod.Name}' is not callable", DiagnosticKind.TYPE, call);

        var args = ResolveArgs(call, fn);
        var raw = args.Select(a => a.Value.Value).ToArray();
        var res = _ctx.Builder.BuildCall2(fn.Type, sym.Pointer, raw);
        return new TypedValue(res, fn.ReturnType);
    }

    private (LLVMValueRef? ThisPtr, ICompositeType? Type, ModuleType? moduleType) ResolveReceiver(AstNode node, AstCall call)
    {
        if (new LValueResolver(_ctx).TryResolveLValue(node, out var lval))
        {
            if (lval.Type is StructType st)
                return (lval.Pointer, st, null);

            if (lval.Type is ClassType cls)
            {
                var ptr = _ctx.Builder.BuildLoad2(cls.Type, lval.Pointer, "this_ptr");
                var val = UpgradeWeakReference(new TypedValue(ptr, cls, lval.IsWeak));
                return (val.Value, cls, null);
            }

            Fail(
                $"Cannot call member on non-composite type '{lval.Type.Name}'", DiagnosticKind.TYPE, call);
        }

        var left = new ExpressionDispatcher(_ctx).EmitExpression(node);
        if (left.Type is StructType rvalStruct)
        {
            var tmp = _ctx.BuildEntryAlloca(rvalStruct.LlvmStructType, "tmp_struct_this");
            _ctx.Builder.BuildStore(left.Value, tmp);
            return (tmp, rvalStruct, null);
        }

        if (left.Type is ClassType rvalClass)
        {
            var val = UpgradeWeakReference(left);
            return (val.Value, rvalClass, null);
        }

        if (left.Type is ModuleType moduleType)
        {
            return (null, null, moduleType);
        }
        return Fail<(LLVMValueRef?, ICompositeType?, ModuleType?)>(
            $"Cannot call member on non-composite type '{left.Type.Name}'", DiagnosticKind.TYPE, call);
    }

    private LLVMValueRef ResolveMethodCallee(ICompositeType type, LLVMValueRef thisPtr, ClassMethod method)
    {
        if (method.VTableIndex >= 0 && type is ClassType)
        {
            var ptr = LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0);
            var vptr = _ctx.Builder.BuildStructGEP2(type.LlvmStructType, thisPtr, 0, "vptr_slot");
            var vtable = _ctx.Builder.BuildLoad2(ptr, vptr, "vtable");
            LLVMValueRef[] idx = [LLVMValueRef.CreateConstInt(_ctx.Context.Int32Type, (ulong)method.VTableIndex, false)];
            var slot = _ctx.Builder.BuildGEP2(ptr, vtable, idx, "slot_ptr");
            return _ctx.Builder.BuildLoad2(ptr, slot, $"{method.Name}_fn");
        }

        var callee = _ctx.Module.GetNamedFunction(method.Signature.FullName);
        if (callee.Handle == IntPtr.Zero)
            throw new Exception($"Method {method.Signature.FullName} not found");

        return callee;
    }

    private TypedValue UpgradeWeakReference(TypedValue val)
    {
        if (!val.IsWeak)
            return val;

        var strong = _ctx.HeapControl.BuildUpgradeWeak(val.Value);
        var upgraded = new TypedValue(strong, val.Type, false);
        _ctx.HeapControl.TrackTemp(upgraded);
        return upgraded;
    }

    private ArgumentInfo[] ResolveArgs(AstCall call, CallableType fn) =>
        ArgumentResolver.Resolve(
            _ctx,
            call.Arguments,
            fn.ParametersType,
            call,
            "Function",
            fn.Type.IsFunctionVarArg);
}
