#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public class UnaryOpGen(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public TypedValue EmitUnaryOperator(AstUnaryOperator op, bool isStatement = false)
    {
        if (op.Token.Type is TokenType.INCREMENT or TokenType.DECREMENT)
        {
            return HandleIncDec(op);
        }

        if (isStatement)
        {
            return Fail<TypedValue>("Invalid statement!", DiagnosticKind.SYNTAX, op);
        }

        var value = new ExpressionDispatcher(_ctx).EmitExpression(op.Operand);
        return op.Token.Type switch
        {
            TokenType.SUB => HandleNeg(value, op),
            TokenType.ADD => HandlePos(value, op),
            TokenType.NOT => HandleNot(value, op),
            _ => Fail<TypedValue>($"Unexpected unary operator: {op.Token.Value}", DiagnosticKind.SYNTAX, op)
        };
    }

    private (LLVMValueRef Pointer, BifyType Type, string Name) ResolveIncDecTarget(AstUnaryOperator op)
    {
        if (op.Operand is not AstIdentifier id)
            return Fail<(LLVMValueRef, BifyType, string)>($"Operator '{op.Token.Value}' requires a variable", DiagnosticKind.TYPE, op);

        if (!_ctx.CurrentScope.TryLookup(id.Name, out var symbol))
            return Fail<(LLVMValueRef, BifyType, string)>($"Variable '{id.Name}' is not defined", DiagnosticKind.NAME, id);

        if (symbol.IsPoison)
            return AbortPoison<(LLVMValueRef, BifyType, string)>();

        if (symbol.IsConstant)
            return Fail<(LLVMValueRef, BifyType, string)>($"Cannot mutate constant variable '{symbol.Name}'", DiagnosticKind.TYPE, op);

        return (symbol.Pointer, symbol.Type, symbol.Name);
    }

    private TypedValue HandleIncDec(AstUnaryOperator op)
    {
        var (pointer, type, name) = ResolveIncDecTarget(op);
        var curr = _ctx.Builder.BuildLoad2(type.Type, pointer, name);
        var isInc = op.Token.Type == TokenType.INCREMENT;

        LLVMValueRef next;
        if (type.Name is "int" or "char")
        {
            var step = LLVMValueRef.CreateConstInt(type.Type, 1, true);
            next = isInc ? _ctx.Builder.BuildAdd(curr, step, "inc") : _ctx.Builder.BuildSub(curr, step, "dec");
        }
        else if (type.Name is "float" or "double")
        {
            var step = LLVMValueRef.CreateConstReal(type.Type, 1.0);
            next = isInc ? _ctx.Builder.BuildFAdd(curr, step, "finc") : _ctx.Builder.BuildFSub(curr, step, "fdec");
        }
        else
        {
            return Fail<TypedValue>($"Operator '{op.Token.Value}' is not supported for type '{type.Name}'", DiagnosticKind.TYPE, op);
        }

        _ctx.Builder.BuildStore(next, pointer);
        return new TypedValue(op.IsPrefix ? next : curr, type);
    }

    private TypedValue HandleNeg(TypedValue val, AstUnaryOperator op)
    {
        if (val.Type.Name is "int" or "char")
            return new TypedValue(_ctx.Builder.BuildNeg(val.Value, "negtmp"), val.Type);

        if (val.Type.Name is "float" or "double")
            return new TypedValue(_ctx.Builder.BuildFNeg(val.Value, "fnegtmp"), val.Type);

        return Fail<TypedValue>($"Cannot negate type '{val.Type.Name}'", DiagnosticKind.TYPE, op);
    }

    private TypedValue HandlePos(TypedValue val, AstUnaryOperator op)
    {
        if (val.Type.Name is "int" or "char" or "float" or "double")
            return val;

        return Fail<TypedValue>($"Cannot apply '+' to type '{val.Type.Name}'", DiagnosticKind.TYPE, op);
    }

    private TypedValue HandleNot(TypedValue val, AstUnaryOperator op)
    {
        var boolType = BifyType.CreateBool(_ctx);
        if (val.Type.Name == "bool")
            return new TypedValue(_ctx.Builder.BuildNot(val.Value, "nottmp"), boolType);

        if (val.Type.Name is "int" or "char")
        {
            var zero = LLVMValueRef.CreateConstInt(val.Type.Type, 0, false);
            return new TypedValue(_ctx.Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, val.Value, zero, "nottmp"), boolType);
        }

        return Fail<TypedValue>($"Cannot apply '!' to type '{val.Type.Name}'", DiagnosticKind.TYPE, op);
    }
}