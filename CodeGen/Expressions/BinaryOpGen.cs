#nullable enable
using System;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public class BinaryOpGen
{
    public static TypedValue EmitBinaryOp(CodeGenContext ctx, ExpressionDispatcher dispatcher, AstBinaryOp binaryOp)
    {
        var op = binaryOp.Token.Type;
        if (op is TokenType.AND or TokenType.OR)
            return EmitLogical(ctx, dispatcher, binaryOp, op);

        using (ctx.HeapControl.BeginTempScope())
        {
            TypedValue left = dispatcher.EmitExpression(binaryOp.Left);
            TypedValue right = dispatcher.EmitExpression(binaryOp.Right);
            return EmitBinary(ctx, op, left, right);
        }
        
    }

    private static TypedValue EmitLogical(CodeGenContext ctx, ExpressionDispatcher dispatcher, AstBinaryOp binaryOp, TokenType op)
    {
        TypedValue left;
        using (ctx.HeapControl.BeginTempScope())
            left = dispatcher.EmitExpression(binaryOp.Left);
        if (left.Type.Name != "bool")
            return ctx.Fail<TypedValue>($"Operator '{op}' is not supported for '{left.Type.Name}'", DiagnosticKind.TYPE, binaryOp);

        var isAnd = op == TokenType.AND;
        var function = ctx.FunctionEntry.Parent;
        var rhsBlock = function.AppendBasicBlock(isAnd ? "and_rhs" : "or_rhs");
        var mergeBlock = function.AppendBasicBlock(isAnd ? "and_merge" : "or_merge");

        var entryBlock = ctx.Builder.InsertBlock;
        if (isAnd)
            ctx.Builder.BuildCondBr(left.Value, rhsBlock, mergeBlock);
        else
            ctx.Builder.BuildCondBr(left.Value, mergeBlock, rhsBlock);

        ctx.Builder.PositionAtEnd(rhsBlock);
        TypedValue right;
        using (ctx.HeapControl.BeginTempScope())
            right = dispatcher.EmitExpression(binaryOp.Right);
        if (right.Type.Name != "bool")
            return ctx.Fail<TypedValue>($"Operator '{op}' is not supported between 'bool' and '{right.Type.Name}'", DiagnosticKind.TYPE, binaryOp);

        var rightExitBlock = ctx.Builder.InsertBlock;
        if (rightExitBlock.Terminator.Handle == IntPtr.Zero)
            ctx.Builder.BuildBr(mergeBlock);

        ctx.Builder.PositionAtEnd(mergeBlock);
        var boolType = BifyType.CreateBool(ctx);
        var phi = ctx.Builder.BuildPhi(boolType.Type, "logical_tmp");
        var shortCircuitVal = LLVMValueRef.CreateConstInt(boolType.Type, isAnd ? 0UL : 1UL);

        phi.AddIncoming(new[] { shortCircuitVal, right.Value }, new[] { entryBlock, rightExitBlock }, 2);
        return new TypedValue(phi, boolType);
    }

    public static TypedValue EmitBinary(CodeGenContext ctx, TokenType op, TypedValue left, TypedValue right)
    {
        if (left.Type.Name == "int" && right.Type.Name == "int")
            return EmitIntBinary(ctx, op, left.Value, right.Value);

        if (left.Type.Name is "float" or "double" || right.Type.Name is "float" or "double")
        {
            var targetType = (left.Type.Name == "double" || right.Type.Name == "double")
                ? BifyType.CreateDouble(ctx)
                : BifyType.CreateFloat(ctx);
            var leftVal = TypeCaster.ImplicitCast(ctx, left, targetType).Value;
            var rightVal = TypeCaster.ImplicitCast(ctx, right, targetType).Value;
            return EmitFloatBinary(ctx, op, leftVal, rightVal, targetType);
        }

        if (left.Type.Name == "bool" && right.Type.Name == "bool")
            return EmitBoolBinary(ctx, op, left.Value, right.Value);

        if (left.Type.Name == "string" && right.Type.Name == "string")
            return new StringCompareGen(ctx).Emit(op, left.Value, right.Value);

        if (left.Type.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind &&
            right.Type.Type.Kind == LLVMTypeKind.LLVMPointerTypeKind)
            return EmitPointerComparison(ctx, op, left.Value, right.Value);

        return ctx.Fail<TypedValue>($"Operator '{op}' is not supported between '{left.Type.Name}' and '{right.Type.Name}'", DiagnosticKind.TYPE);
    }

    private static TypedValue EmitIntBinary(CodeGenContext ctx, TokenType op, LLVMValueRef left, LLVMValueRef right)
    {
        var intType = BifyType.CreateInt(ctx);
        var boolType = BifyType.CreateBool(ctx);

        return op switch
        {
            TokenType.ADD => new TypedValue(ctx.Builder.BuildAdd(left, right, "addtmp"), intType),
            TokenType.SUB => new TypedValue(ctx.Builder.BuildSub(left, right, "subtmp"), intType),
            TokenType.MUL => new TypedValue(ctx.Builder.BuildMul(left, right, "multmp"), intType),
            TokenType.DIV => new TypedValue(ctx.Builder.BuildSDiv(left, right, "divtmp"), intType),
            TokenType.MOD => new TypedValue(ctx.Builder.BuildSRem(left, right, "modtmp"), intType),

            TokenType.EQ or TokenType.EQEQ => new TypedValue(
                ctx.Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, left, right, "eqtmp"), boolType),
            TokenType.NEQ => new TypedValue(ctx.Builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, left, right, "neqtmp"),
                boolType),
            TokenType.LT => new TypedValue(ctx.Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, left, right, "lttmp"),
                boolType),
            TokenType.LTE or TokenType.LTEQ => new TypedValue(
                ctx.Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLE, left, right, "ltetmp"), boolType),
            TokenType.GT => new TypedValue(ctx.Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGT, left, right, "gttmp"),
                boolType),
            TokenType.GTE or TokenType.GTEQ => new TypedValue(
                ctx.Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGE, left, right, "gtetmp"), boolType),

            TokenType.BIT_AND or TokenType.BITAND => new TypedValue(ctx.Builder.BuildAnd(left, right, "andtmp"),
                intType),
            TokenType.BIT_OR or TokenType.BITOR => new TypedValue(ctx.Builder.BuildOr(left, right, "ortmp"), intType),
            TokenType.BIT_XOR or TokenType.BITXOR => new TypedValue(ctx.Builder.BuildXor(left, right, "xortmp"),
                intType),

            _ => ctx.Fail<TypedValue>($"Unsupported binary operator: {op}", DiagnosticKind.SYNTAX)
        };
    }

    private static TypedValue EmitFloatBinary(CodeGenContext ctx, TokenType op, LLVMValueRef left, LLVMValueRef right,
        BifyType floatType)
    {
        var boolType = BifyType.CreateBool(ctx);

        return op switch
        {
            TokenType.ADD => new TypedValue(ctx.Builder.BuildFAdd(left, right, "faddtmp"), floatType),
            TokenType.SUB => new TypedValue(ctx.Builder.BuildFSub(left, right, "fsubtmp"), floatType),
            TokenType.MUL => new TypedValue(ctx.Builder.BuildFMul(left, right, "fmultmp"), floatType),
            TokenType.DIV => new TypedValue(ctx.Builder.BuildFDiv(left, right, "fdivtmp"), floatType),
            TokenType.MOD => new TypedValue(ctx.Builder.BuildFRem(left, right, "fremtmp"), floatType),

            TokenType.EQ or TokenType.EQEQ => new TypedValue(
                ctx.Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOEQ, left, right, "feqtmp"), boolType),
            TokenType.NEQ => new TypedValue(
                ctx.Builder.BuildFCmp(LLVMRealPredicate.LLVMRealONE, left, right, "fneqtmp"), boolType),
            TokenType.LT => new TypedValue(ctx.Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOLT, left, right, "flttmp"),
                boolType),
            TokenType.LTE or TokenType.LTEQ => new TypedValue(
                ctx.Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOLE, left, right, "fltetmp"), boolType),
            TokenType.GT => new TypedValue(ctx.Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOGT, left, right, "fgttmp"),
                boolType),
            TokenType.GTE or TokenType.GTEQ => new TypedValue(
                ctx.Builder.BuildFCmp(LLVMRealPredicate.LLVMRealOGE, left, right, "fgtetmp"), boolType),

            _ => ctx.Fail<TypedValue>($"Unsupported float binary operator: {op}", DiagnosticKind.SYNTAX)
        };
    }

    private static TypedValue EmitBoolBinary(CodeGenContext ctx, TokenType op, LLVMValueRef left, LLVMValueRef right)
    {
        var boolType = BifyType.CreateBool(ctx);

        return op switch
        {
            TokenType.AND => new TypedValue(ctx.Builder.BuildAnd(left, right, "andtmp"), boolType),
            TokenType.OR => new TypedValue(ctx.Builder.BuildOr(left, right, "ortmp"), boolType),
            TokenType.EQ or TokenType.EQEQ => new TypedValue(
                ctx.Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, left, right, "eqtmp"), boolType),
            TokenType.NEQ => new TypedValue(ctx.Builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, left, right, "neqtmp"),
                boolType),

            _ => ctx.Fail<TypedValue>($"Unsupported boolean binary operator: {op}", DiagnosticKind.SYNTAX)
        };
    }

    private static TypedValue EmitPointerComparison(CodeGenContext ctx, TokenType op, LLVMValueRef left,
        LLVMValueRef right)
    {
        var boolType = BifyType.CreateBool(ctx);

        return op switch
        {
            TokenType.EQ or TokenType.EQEQ => new TypedValue(
                ctx.Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, left, right, "ptreq"), boolType),
            TokenType.NEQ => new TypedValue(ctx.Builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, left, right, "ptrneq"),
                boolType),
            _ => ctx.Fail<TypedValue>($"Unsupported pointer comparison operator: {op}", DiagnosticKind.SYNTAX)
        };
    }
}