#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Emitters;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Expressions;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Statements;

public class AssignmentGen(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public void EmitAssign(AstAssignmentOperator assignmentOp)
    {
        var lValue = new LValueResolver(_ctx).ResolveLValue(assignmentOp.IdentifierNode);
        if (lValue.IsConstant)
            Fail("Assigning to constant variable!", DiagnosticKind.TYPE, assignmentOp);

        TypedValue value = new ExpressionDispatcher(_ctx).EmitExpression(assignmentOp.ValueNode);

        TokenType? binOp = assignmentOp.Token.Type switch
        {
            TokenType.ASSIGN => null,
            TokenType.ADDE => TokenType.ADD,
            TokenType.SUBE => TokenType.SUB,
            TokenType.MULE => TokenType.MUL,
            TokenType.DIVE or TokenType.FLOORDIVE => TokenType.DIV,
            _ => Fail<TokenType?>($"Unsupported assignment operator: {assignmentOp.Token.Value}", DiagnosticKind.SYNTAX, assignmentOp)
        };

        if (binOp.HasValue)
        {
            var currentVal = new TypedValue(_ctx.Builder.BuildLoad2(lValue.Type.Type, lValue.Pointer, "loaded_pointer"), lValue.Type);
            value = BinaryOpGen.EmitBinary(_ctx, binOp.Value, currentVal, value);
            StoreAssignedValue(lValue.Pointer, lValue.Type, value, lValue.IsWeak, allowCharFromInt: true);
            return;
        }

        StoreAssignedValue(lValue.Pointer, lValue.Type, value, lValue.IsWeak, allowCharFromInt: false);
    }

    private void StoreAssignedValue(LLVMValueRef pointer, BifyType type, TypedValue value, bool isWeak, bool allowCharFromInt)
    {
        if (type.IsArcAssignableFrom(value.Type))
        {
            TypedValue casted = TypeCaster.ImplicitCast(_ctx, value, type);
            _ctx.HeapControl.BuildArcAssign(pointer, casted, isWeak);
            return;
        }

        TypedValue scalar = allowCharFromInt && type.Name == "char" && value.Type.Name == "int"
            ? TypeCaster.ExplicitCast(_ctx, value, type)
            : TypeCaster.ImplicitCast(_ctx, value, type);
        _ctx.Builder.BuildStore(scalar.Value, pointer);
    }



}
