using System;
using System.Collections.Generic;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Runtime;

public class HeapControl(CodeGenContext ctx)
{
    public readonly struct TempScope : IDisposable
    {
        private readonly HeapControl _heap;
        private readonly List<TypedValue> _frame;

        internal TempScope(HeapControl heap, List<TypedValue> frame)
        {
            _heap = heap;
            _frame = frame;
        }

        public void Dispose()
        {
            _heap.EndTempScope(_frame);
        }
    }

    private readonly CodeGenContext _ctx = ctx;
    private readonly Stack<List<TypedValue>> _tempScopes = new();

    private (LLVMTypeRef funcType, LLVMValueRef funcValue) _gcAlloc;
    private (LLVMTypeRef funcType, LLVMValueRef funcValue) _gcRetainStrong;
    private (LLVMTypeRef funcType, LLVMValueRef funcValue) _gcRetainWeak;
    private (LLVMTypeRef funcType, LLVMValueRef funcValue) _gcReleaseStrong;
    private (LLVMTypeRef funcType, LLVMValueRef funcValue) _gcReleaseWeak;
    private (LLVMTypeRef funcType, LLVMValueRef funcValue) _gcUpgradeWeak;

    private LLVMTypeRef VoidPtrType => LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0);

    public TempScope BeginTempScope()
    {
        var frame = new List<TypedValue>();
        _tempScopes.Push(frame);
        return new TempScope(this, frame);
    }

    public void TrackTemp(TypedValue value)
    {
        if (_tempScopes.Count == 0)
        {
            throw new InvalidOperationException(
                "Attempted to track a temporary ARC reference outside of active TempScope."
            );
        }
        _tempScopes.Peek().Add(value);
    }

    public bool ConsumeTemp(LLVMValueRef value)
    {
        if (_tempScopes.Count == 0)
            return false;

        return _tempScopes.Peek().RemoveAll(x => x.Value.Handle == value.Handle) > 0;
    }

    private void EndTempScope(List<TypedValue> expectedFrame)
    {
        if (_tempScopes.Count == 0 || !ReferenceEquals(_tempScopes.Peek(), expectedFrame))
        {
            throw new InvalidOperationException("TempScope stack corruption: scopes disposed out of order.");
        }

        var frame = _tempScopes.Pop();
        if (frame.Count == 0)
            return;

        var currentBlock = _ctx.Builder.InsertBlock;
        if (currentBlock.Handle == IntPtr.Zero)
            return;

        bool hasTerminator = currentBlock.Terminator.Handle != IntPtr.Zero;

        if (hasTerminator)
        {
            _ctx.Builder.PositionBefore(currentBlock.Terminator);
        }

        foreach (var item in frame)
        {
            BuildReleaseStrong(item.Value);
        }

        if (hasTerminator)
        {
            _ctx.Builder.PositionAtEnd(currentBlock);
        }
    }

    public void DefineRuntimeFunctions()
    {
        DefineGcAlloc();
        DefineGcRetainStrong();
        DefineGcRetainWeak();
        DefineGcReleaseStrong();
        DefineGcReleaseWeak();
        DefineGcUpgradeWeak();
    }

    private void DefineGcAlloc()
    {
        LLVMTypeRef[] paramTypes = [_ctx.Context.Int64Type, VoidPtrType];
        var funcType = LLVMTypeRef.CreateFunction(VoidPtrType, paramTypes);
        var funcVal = _ctx.Module.AddFunction("gc_alloc", funcType);
        _gcAlloc = (funcType, funcVal);
    }

    private void DefineGcRetainStrong()
    {
        LLVMTypeRef[] paramTypes = [VoidPtrType];
        var funcType = LLVMTypeRef.CreateFunction(_ctx.Context.VoidType, paramTypes);
        var funcVal = _ctx.Module.AddFunction("gc_retainStrong", funcType);
        _gcRetainStrong = (funcType, funcVal);
    }

    private void DefineGcRetainWeak()
    {
        LLVMTypeRef[] paramTypes = [VoidPtrType];
        var funcType = LLVMTypeRef.CreateFunction(_ctx.Context.VoidType, paramTypes);
        var funcVal = _ctx.Module.AddFunction("gc_retainWeak", funcType);
        _gcRetainWeak = (funcType, funcVal);
    }

    private void DefineGcReleaseStrong()
    {
        LLVMTypeRef[] paramTypes = [VoidPtrType];
        var funcType = LLVMTypeRef.CreateFunction(_ctx.Context.VoidType, paramTypes);
        var funcVal = _ctx.Module.AddFunction("gc_releaseStrong", funcType);
        _gcReleaseStrong = (funcType, funcVal);
    }

    private void DefineGcReleaseWeak()
    {
        LLVMTypeRef[] paramTypes = [VoidPtrType];
        var funcType = LLVMTypeRef.CreateFunction(_ctx.Context.VoidType, paramTypes);
        var funcVal = _ctx.Module.AddFunction("gc_releaseWeak", funcType);
        _gcReleaseWeak = (funcType, funcVal);
    }

    private void DefineGcUpgradeWeak()
    {
        LLVMTypeRef[] paramTypes = [VoidPtrType];
        var funcType = LLVMTypeRef.CreateFunction(VoidPtrType, paramTypes);
        var funcVal = _ctx.Module.AddFunction("gc_upgradeWeak", funcType);
        _gcUpgradeWeak = (funcType, funcVal);
    }

    public void BuildRetainStrong(LLVMValueRef objPtr)
    {
        LLVMValueRef[] args = [objPtr];
        _ctx.Builder.BuildCall2(_gcRetainStrong.funcType, _gcRetainStrong.funcValue, args);
    }

    public void BuildRetainWeak(LLVMValueRef objPtr)
    {
        LLVMValueRef[] args = [objPtr];
        _ctx.Builder.BuildCall2(_gcRetainWeak.funcType, _gcRetainWeak.funcValue, args);
    }

    public void BuildReleaseStrong(LLVMValueRef objPtr)
    {
        LLVMValueRef[] args = [objPtr];
        _ctx.Builder.BuildCall2(_gcReleaseStrong.funcType, _gcReleaseStrong.funcValue, args);
    }

    public void BuildReleaseWeak(LLVMValueRef objPtr)
    {
        LLVMValueRef[] args = [objPtr];
        _ctx.Builder.BuildCall2(_gcReleaseWeak.funcType, _gcReleaseWeak.funcValue, args);
    }

    public LLVMValueRef BuildUpgradeWeak(LLVMValueRef weakPtr)
    {
        LLVMValueRef[] args = [weakPtr];
        return _ctx.Builder.BuildCall2(_gcUpgradeWeak.funcType, _gcUpgradeWeak.funcValue, args, "upgraded_ptr");
    }

    public LLVMValueRef BuildAlloc(LLVMValueRef sizeVal, LLVMValueRef vtablePtr)
    {
        LLVMValueRef[] args = [sizeVal, vtablePtr];
        return _ctx.Builder.BuildCall2(_gcAlloc.funcType, _gcAlloc.funcValue, args, "inst_ptr");
    }

    public void BuildArcStoreInit(LLVMValueRef targetPtr, TypedValue value, bool isTargetWeak)
    {
        var (storeVal, _) = PrepareArcValue(value, isTargetWeak);
        _ctx.Builder.BuildStore(storeVal, targetPtr);
    }

    public void BuildArcAssign(LLVMValueRef targetPtr, TypedValue value, bool isTargetWeak)
    {
        var (storeVal, _) = PrepareArcValue(value, isTargetWeak);
        var oldVal = _ctx.Builder.BuildLoad2(VoidPtrType, targetPtr, "old_val");
        if (isTargetWeak)
            BuildReleaseWeak(oldVal);
        else
            BuildReleaseStrong(oldVal);

        _ctx.Builder.BuildStore(storeVal, targetPtr);
    }

    private (LLVMValueRef StoreValue, bool WasTemp) PrepareArcValue(TypedValue value, bool isTargetWeak)
    {
        if (value.Type.Name == "null")
        {
            return (value.Value, false);
        }
        bool isTemp = ConsumeTemp(value.Value);
        if (!isTargetWeak)
        {
            if (value.IsWeak)
            {
                var upgraded = BuildUpgradeWeak(value.Value);
                return (upgraded, isTemp);
            }

            if (!isTemp)
            {
                BuildRetainStrong(value.Value);
            }

            return (value.Value, isTemp);
        }
        if (isTemp)
        {
            TrackTemp(value);
        }

        BuildRetainWeak(value.Value);
        return (value.Value, isTemp);
    }
}
