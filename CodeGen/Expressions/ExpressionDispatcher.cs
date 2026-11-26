using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Expressions;

public class ExpressionDispatcher(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public TypedValue EmitExpression(AstNode node)
    {
        if (node is AstPoison || node.IsPoison)
        {
            return AbortPoison<TypedValue>();
        }

        TypedValue value = node switch
        {
            AstInteger integer => LiteralGen.EmitInteger(_ctx, integer),
            AstFloat floatNode => LiteralGen.EmitFloat(_ctx, floatNode),
            AstBoolean boolean => LiteralGen.EmitBool(_ctx, boolean),
            AstString str => LiteralGen.EmitString(_ctx, str),
            AstNull nullNode => LiteralGen.EmitNull(_ctx, nullNode),
            AstIdentifier identifier => IdentifierGen.EmitIdentifier(_ctx, identifier),
            AstBinaryOp binaryOp => BinaryOpGen.EmitBinaryOp(_ctx, this, binaryOp),
            AstCast cast => CastGen.EmitCast(_ctx, this, cast),
            AstUnaryOperator unaryOperator => new UnaryOpGen(_ctx).EmitUnaryOperator(unaryOperator),
            AstCall astCall => new CallGen(_ctx).EmitCall(astCall),
            AstNew astNew => new NewGen(_ctx).EmitNew(astNew),
            AstMemberAccess astMemberAccess => new MemberAccessGen(_ctx).EmitMemberAccess(astMemberAccess),

            _ => Fail<TypedValue>($"'{node.Token.Value}' is not an expression", DiagnosticKind.SYNTAX, node),
        };
        if (value.IsReference && node is AstCall or AstNew)
        {
            _ctx.HeapControl.TrackTemp(value);
        }
        return value;
    }
}
