using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Validators
{
    internal class FunctionDeclarationValidator
    {
        public static void Validate(AstBuilder builder, Token nameToken, AstNode parametersNode, AstNode blockNode, AstNode typeNode)
        {
            if (nameToken == null || (nameToken.Type != TokenType.IDENTIFIER && nameToken.Type != TokenType.CONSTRUCTOR && nameToken.Type != TokenType.DESTRUCTOR))
            {
                builder.Fail(ErrorMessage.ExpectedFunctionName(), DiagnosticKind.SYNTAX, nameToken ?? typeNode?.Token);
            }

            if (nameToken?.Type == TokenType.DESTRUCTOR && parametersNode != null)
            {
                builder.Fail("destructor cannot take parameters", DiagnosticKind.SYNTAX, nameToken);
            }

            if (typeNode == null || (typeNode is not AstIdentifier && typeNode.Token.Type != TokenType.POINTER && typeNode is not AstIndexOperator))
            {
                builder.Fail(ErrorMessage.InvalidFunctionType(), DiagnosticKind.TYPE, typeNode?.Token ?? nameToken);
            }
        }
    }
}
