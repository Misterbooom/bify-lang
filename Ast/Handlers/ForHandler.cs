using System.Linq;
using BifyLang.Ast.Validators;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class ForHandler(AstBuilder builder):TokenHandler(builder)
    {
        public override void HandleToken(Token token)
        {
            Builder.TokenIndex++;
            var splittedTokens = TokensFormatter.SplitTokensByType(Builder.GetConditionTokens(),TokenType.SEMICOLON);

            var initTokens = splittedTokens.ElementAtOrDefault(0);
            AstNode initNode = initTokens != null && initTokens.Count > 0
                ? new AstBuilder(initTokens, Builder.Diagnostics, Builder.FilePath, true).BuildNode()
                : null;

            AstNode conditionNode = Builder.ParseTokens(splittedTokens.ElementAtOrDefault(1));

            AstNode incrementNode = Builder.ParseTokens(splittedTokens.ElementAtOrDefault(2));
            if (Builder.GetNextToken()?.Type != TokenType.LCUR)
            {
                Builder.Fail("For body not found!", DiagnosticKind.SYNTAX, Builder.PeekTokenOrNull() ?? token);
            }
            var blockNode = Builder.HandleBody("For");
            AstFor astFor = new AstFor(token, blockNode, conditionNode, incrementNode, initNode);
            LoopValidator.ValidateForStatement(Builder, splittedTokens, astFor);
            Builder.MoveToEnd();
            Builder.CurrentNode = astFor;



        }
    }
}
