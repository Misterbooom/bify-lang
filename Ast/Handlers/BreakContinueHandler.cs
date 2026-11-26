using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class BreakContinueHandler(AstBuilder builder):TokenHandler(builder)
    {
        public override void HandleToken(Token token)
        {
            if (Builder.Tokens.Count > 1)
            {
                Builder.Fail($"Invalid syntax of {token.Value} statement!", DiagnosticKind.SYNTAX, token);
            }
            if (token.Type == TokenType.BREAK)
            {
                Builder.CurrentNode = new AstBreak(token);
            }
            else if (token.Type == TokenType.CONTINUE)
            {
                Builder.CurrentNode = new AstContinue(token);
            }

            Builder.MoveToEnd();

        }
    }
}
