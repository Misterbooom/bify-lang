using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class NewHandler(AstBuilder builder):TokenHandler(builder)
    {
        public override void HandleToken(Token token)
        {
            Builder.CurrentNode = ParseNewExpression(token);
        }
        public AstNode ParseNewExpression(Token token)
        {
            Builder.NextToken();
            AstNode nextNode = new BinaryOperatorHandler(Builder).ParsePrimary();
            if (nextNode is not AstCall)
            {
                Builder.Fail("expected argument list after 'new'", DiagnosticKind.SYNTAX, nextNode?.Token ?? token);
            }
            AstCall astCall = (AstCall)nextNode;
            AstNew astNew = new AstNew(token, astCall.CallableName, astCall.Arguments);
            return astNew;

        }
    }
}
