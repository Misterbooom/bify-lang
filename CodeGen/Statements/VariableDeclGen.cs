#nullable enable
using System;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Core.Validators;
using BifyLang.CodeGen.Expressions;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Statements;

public class VariableDeclGen(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public void EmitVariableDecl(AstVarDecl varDecl)
    {
        string varName = varDecl.AssignmentNode.Left.Token.Value;
        if (varDecl.IsPoison || varDecl.AssignmentNode.Right is AstPoison)
        {
            _ctx.CurrentScope.DefinePoison(varName, BifyType.CreateInt(_ctx));
            return;
        }
        try
        {
            using (_ctx.HeapControl.BeginTempScope())
            {
                if (!FlagChecker.CheckVariableFlags(varDecl.Flag?.Flags?.Select(i => i.Token.Value) ?? [], out var error))
                {
                    Fail(error ?? "Invalid variable flags.", DiagnosticKind.Syntax, varDecl);
                }

                if (varDecl.AssignmentNode.Right is AstNew && varDecl.Flag?.HasFlag("weak") == true)
                {
                    Fail("Can't use weak flag with new statement", DiagnosticKind.Type, varDecl);
                }

                var (varType, initialValue) = ResolveVariableType(_ctx, varDecl);


                bool isWeak = varDecl.Flag?.HasFlag("weak") == true;
                if (isWeak && !varType.IsReference)
                {
                    Fail("Can't use weak flag with non-reference type.", DiagnosticKind.Type, varDecl);
                }

                bool isConst = varDecl.Flag?.Token.Value == "val";
                LLVMValueRef alloca = _ctx.BuildEntryAlloca(varType.Type, varName);


                StoreInitialValue(alloca, varType, initialValue, isWeak);
                _ctx.CurrentScope.Define(varName, alloca, varType, isConst, isWeak);
            }
        }
        catch (BifyCompilationException)
        {
            _ctx.CurrentScope.DefinePoison(varName, BifyType.CreateInt(_ctx));
            throw;
        }
    }

    public static (BifyType VarType, TypedValue? InitialValue) ResolveVariableType(CodeGenContext ctx, AstVarDecl varDecl)
    {
        var expressionDispatcher = new ExpressionDispatcher(ctx);
        if (varDecl.Type is AstIdentifier id && id.Name == "void")
        {
            if (varDecl.AssignmentNode.Right == null)
                ctx.Fail("Implicitly-typed variables must be initialized.", DiagnosticKind.Type, varDecl);

            var init = expressionDispatcher.EmitExpression(varDecl.AssignmentNode.Right!);
            if (init.Type.Name == "void")
                ctx.Fail("Cannot declare a variable of type 'void'.", DiagnosticKind.Type, varDecl);

            return (init.Type, init);
        }

        var varType = TypeResolver.Resolve(varDecl.Type, ctx);
        TypedValue? initialValue = null;
        if (varDecl.AssignmentNode.Right != null)
        {
            var raw = expressionDispatcher.EmitExpression(varDecl.AssignmentNode.Right);
            initialValue = TypeCaster.ImplicitCast(ctx, raw, varType);
        }
        return (varType, initialValue);
    }

    private void StoreInitialValue(LLVMValueRef alloca, BifyType varType, TypedValue? initialValue, bool isWeak)
    {

        if (initialValue.HasValue)
        {
            var init = initialValue.Value;

            if (varType.IsReference)
            {
                _ctx.HeapControl.BuildArcStoreInit(alloca, init, isWeak);
            }

            else
            {
                _ctx.Builder.BuildStore(init.Value, alloca);
            }
        }
        else if (varType.IsReference || varType is StructType)
        {
            _ctx.Builder.BuildStore(LLVMValueRef.CreateConstNull(varType.Type), alloca);
        }
    }

    private unsafe LLVMValueRef FindStoredValue(LLVMValueRef alloca)
    {

        LLVMUseRef use = alloca.FirstUse;

        while (use != default)
        {
            LLVMValueRef user = LLVM.GetUser(use);

            if (user.InstructionOpcode == LLVMOpcode.LLVMStore)
            {
                LLVMValueRef destPtr = user.GetOperand(1);

                if (destPtr == alloca)
                {
                    return user.GetOperand(0);
                }
            }
            use = LLVM.GetNextUse(use);
        }

        throw new InvalidOperationException("Cant find use of alloca");
    }
}
