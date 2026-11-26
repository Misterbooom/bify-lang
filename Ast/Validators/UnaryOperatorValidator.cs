using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Validators
{
    internal class UnaryOperatorValidator
    {
        public static bool Validate(AstBuilder builder, string op, Token identifierToken)
        {
            if (identifierToken == null)
            {
                builder.Fail(ErrorMessage.MissingOperandAfterUnaryOperator(op), DiagnosticKind.SYNTAX, null);
            }
            else if (identifierToken.Type != TokenType.IDENTIFIER)
            {
                builder.Fail(ErrorMessage.OperandMustBeIdentifierAfterUnaryOperator(op), DiagnosticKind.SYNTAX, identifierToken);
            }
            return true;
        }
    }
}
