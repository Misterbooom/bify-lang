using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class ImportHandler(AstBuilder builder) : TokenHandler(builder)
    {
        public override void HandleToken(Token token)
        {
            Builder.NextToken();

            if (Builder.IsAtEnd())
            {
                Builder.Fail("Expected string path after 'import'.", DiagnosticKind.SYNTAX, token);
            }

            Token pathToken = Builder.Consume(TokenType.STRING, "Expected string path after 'import'.");
            AstString pathNode = new(pathToken, pathToken.Value);

            if (!Builder.IsAtEnd() && Builder.Peek().Type == TokenType.SEMICOLON)
            {
                Builder.NextToken();
            }

            Builder.CurrentNode = new AstImport(token, pathNode);
        }
    }
}
