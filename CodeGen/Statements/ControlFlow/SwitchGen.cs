#nullable enable
using System;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Emitters.FlowControl;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Expressions;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Statements.ControlFlow;

public class SwitchGen(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public void EmitSwitch(AstSwitch astSwitch)
    {
        var currentFunction = _ctx.FunctionEntry.Parent;
        var defaultBlock = currentFunction.AppendBasicBlock("switch.default");
        var endBlock = currentFunction.AppendBasicBlock("switch.end");

        using (_ctx.EnterScope())
        {
            var exprVal = EmitSwitchSubject(astSwitch.ExpressionNode);

            if (exprVal.Type.Type.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
                EmitIntegerSwitch(astSwitch, exprVal, defaultBlock, endBlock);
            else
                EmitFallbackSwitch(astSwitch, exprVal, defaultBlock, endBlock);

            _ctx.Builder.PositionAtEnd(defaultBlock);
            if (astSwitch.DefaultNode is not null)
                LoopEmitter.EmitLoopBody(_ctx, defaultBlock, endBlock, _ctx.LoopContext.ContinueBlock, endBlock,
                    astSwitch.DefaultNode.BlockNode, reposition: false);
            else
                _ctx.Builder.BranchIfUnterminated(endBlock);

            _ctx.Builder.FinalizeMergeBlock(endBlock, defaultBlock);
        }
    }

    private TypedValue EmitSwitchSubject(AstNode exprNode)
    {
        TypedValue exprVal;
        using (_ctx.HeapControl.BeginTempScope())
        {
            exprVal = new ExpressionDispatcher(_ctx).EmitExpression(exprNode);
            if (exprVal.IsReference && _ctx.HeapControl.ConsumeTemp(exprVal.Value))
            {
                var alloca = _ctx.BuildEntryAlloca(exprVal.Type.Type, "switch_subj");
                _ctx.Builder.BuildStore(exprVal.Value, alloca);
                _ctx.CurrentScope.Define("$switch_subj", alloca, exprVal.Type, isConstant: true);
            }
        }
        return exprVal;
    }

    private void EmitIntegerSwitch(AstSwitch astSwitch, TypedValue exprVal, LLVMBasicBlockRef defaultBlock,
        LLVMBasicBlockRef endBlock)
    {
        var switchInst = _ctx.Builder.BuildSwitch(exprVal.Value, defaultBlock, (uint)astSwitch.Cases.Count);

        for (int i = 0; i < astSwitch.Cases.Count; i++)
        {
            var caseBlock = _ctx.FunctionEntry.Parent.AppendBasicBlock($"case.{i}");
            using (_ctx.HeapControl.BeginTempScope())
            {
                var caseVal =
                    CoerceSwitchConstant(
                        new ExpressionDispatcher(_ctx).EmitExpression(astSwitch.Cases[i].ValueNode).Value,
                        exprVal.Type.Type,
                        astSwitch.Cases[i].ValueNode);

                switchInst.AddCase(caseVal, caseBlock);
            }

            LoopEmitter.EmitLoopBody(_ctx, caseBlock, endBlock, _ctx.LoopContext.ContinueBlock, endBlock,
                astSwitch.Cases[i].BlockNode);
        }
    }

    private void EmitFallbackSwitch(AstSwitch astSwitch, TypedValue exprVal, LLVMBasicBlockRef defaultBlock,
        LLVMBasicBlockRef endBlock)
    {
        if (astSwitch.Cases.Count == 0)
        {
            _ctx.Builder.BuildBr(defaultBlock);
            return;
        }

        for (int i = 0; i < astSwitch.Cases.Count; i++)
        {
            var caseBlock = _ctx.FunctionEntry.Parent.AppendBasicBlock($"case.{i}");
            var nextBlock = i + 1 < astSwitch.Cases.Count
                ? _ctx.FunctionEntry.Parent.AppendBasicBlock($"switch.next.{i}")
                : defaultBlock;

            TypedValue cmp;
            using (_ctx.HeapControl.BeginTempScope())
            {
                var caseVal = new ExpressionDispatcher(_ctx).EmitExpression(astSwitch.Cases[i].ValueNode);
                cmp = BinaryOpGen.EmitBinary(_ctx, TokenType.EQEQ, exprVal, caseVal);
            }
            var condBool = TypeCaster.ImplicitCast(_ctx, cmp, BifyType.CreateBool(_ctx));

            _ctx.Builder.BuildCondBr(condBool.Value, caseBlock, nextBlock);
            LoopEmitter.EmitLoopBody(_ctx, caseBlock, endBlock, _ctx.LoopContext.ContinueBlock, endBlock,
                astSwitch.Cases[i].BlockNode);
            _ctx.Builder.PositionAtEnd(nextBlock);
        }
    }

    private LLVMValueRef CoerceSwitchConstant(LLVMValueRef caseVal, LLVMTypeRef expectedType, AstNode caseNode)
    {
        if (caseVal.TypeOf == expectedType)
            return caseVal;

        if (caseVal.IsAConstantInt.Handle == IntPtr.Zero)
            Fail("case value must be constant int", DiagnosticKind.TYPE, caseNode);

        return LLVMValueRef.CreateConstInt(expectedType, (ulong)caseVal.ConstIntSExt, false);
    }
}