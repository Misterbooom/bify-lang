using System.Linq;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Expressions;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Emitters;

public class StaticLifeCycle(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;
    private (LLVMTypeRef funcType, LLVMValueRef funcvalue) _initStatics;
    private (LLVMTypeRef funcType, LLVMValueRef funcvalue) _cleanUpStatics;
    public void DeclareInitStatics()
    {
        var funcType = LLVMTypeRef.CreateFunction(_ctx.Module.Context.VoidType, []);
        var funcVal = _ctx.Module.AddFunction("__init_statics", funcType);
        funcVal.AppendBasicBlock("entry");

        _initStatics = (funcType, funcVal);
    }

    public void DeclareCleanUpStatics()
    {
        var funcType = LLVMTypeRef.CreateFunction(_ctx.Module.Context.VoidType, []);
        var funcVal = _ctx.Module.AddFunction("__cleanup_statics", funcType);
        funcVal.AppendBasicBlock("entry");
        _cleanUpStatics = (funcType, funcVal);
    }

    public void GenerateInitStatics(ClassField[] staticFields)
    {
        BifyDebug.Log($"static fields count: {staticFields.Length}");
        var prevEntry = _ctx.FunctionEntry;
        var entry = _initStatics.funcvalue.EntryBasicBlock;
        _ctx.FunctionEntry = entry;
        _ctx.Builder.PositionAtEnd(entry);

        using (_ctx.EnterScope())
        {
            foreach (var field in staticFields)
            {
                if (field.IsPoison || field.ValueNode == null)
                    continue;

                var moduleName = field.DeclaringType?.ModuleName;
                using var _ = moduleName != null && _ctx.ModulesMap.TryGetValue(moduleName, out var mod)
                    ? _ctx.EnterModule(mod.ModuleContext)
                    : null;

                try
                {
                    using (_ctx.HeapControl.BeginTempScope())
                    {
                        var initVal = new ExpressionDispatcher(_ctx).EmitExpression(field.ValueNode);
                        initVal = TypeCaster.ImplicitCast(_ctx, initVal, field.Type);

                        if (field.Type.IsReference)
                            _ctx.HeapControl.BuildArcStoreInit(field.GlobalValue, initVal, field.IsWeak);
                        else
                            _ctx.Builder.BuildStore(initVal.Value, field.GlobalValue);
                    }
                }
                catch (BifyCompilationException)
                {
                }
            }
        }

        _ctx.Builder.BuildRetVoid();
        _ctx.FunctionEntry = prevEntry;
    }

    public void GenerateCleanUpStatics(ClassField[] staticFields)
    {
        _ctx.Builder.PositionAtEnd(_cleanUpStatics.funcvalue.EntryBasicBlock);
        foreach (var field in staticFields.Where(i => i is { IsPoison: false, IsStatic: true } && i.Type.IsReference))
        {
            var objPtr = _ctx.Builder.BuildLoad2(field.Type.Type, field.GlobalValue, $"{field.Name}_cleanup");
            if (field.IsWeak)
                _ctx.HeapControl.BuildReleaseWeak(objPtr);
            else
                _ctx.HeapControl.BuildReleaseStrong(objPtr);
        }
        _ctx.Builder.BuildRetVoid();
    }
    public void EmitCallInitStatics()
    {
        _ctx.Builder.BuildCall2(_initStatics.funcType, _initStatics.funcvalue, []);
    }

    public void EmitCallCleanUpStatics()
    {
        _ctx.Builder.BuildCall2(_cleanUpStatics.funcType, _cleanUpStatics.funcvalue, []);
    }
}
