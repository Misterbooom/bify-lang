using BifyLang.Ast.Validators;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class ConditionHandler(AstBuilder builder) : TokenHandler(builder)
    {
        AstIf _currentIfNode = null;
        public override void HandleToken(Token token)
        {
            HandleIf(token);
            Builder.CurrentNode = _currentIfNode;
        }
        private void HandleIf(Token token)
        {
            AstNode conditionNode = Builder.ParseTokens(Builder.GetConditionTokens());
            AstNode blockNode = Builder.HandleBody("If");
            if (conditionNode == null)
            {
                Builder.Fail(ErrorMessage.ConditionIsRequired(), DiagnosticKind.SYNTAX, token);
            }
            _currentIfNode = new AstIf(token, conditionNode, blockNode);

            HandleElseOrElseIf(Builder.Peek());
        }
        private void HandleElseOrElseIf(Token? token)
        {
            if (token?.Type == TokenType.ELSE && Builder.GetNextToken()?.Type == TokenType.IF)
            {
                Builder.TokenIndex++;
                AstNode conditionNode = Builder.ParseTokens(Builder.GetConditionTokens());
                AstNode blockNode = Builder.HandleBody("Else-If");
                if (conditionNode == null)
                {
                    Builder.Fail(ErrorMessage.ConditionIsRequired(), DiagnosticKind.SYNTAX, token);
                }
                AstElseIf elseIfNode = new AstElseIf(token, blockNode, conditionNode);
                if (_currentIfNode.ElseNode != null)
                {
                    ConditionStatementValidator.ThrowElseIfAfterElseError(Builder, elseIfNode);
                }
                _currentIfNode.AddElseIfNode(elseIfNode);
                HandleElseOrElseIf(Builder.Peek());
            }
            else if (token?.Type == TokenType.ELSE)
            {
                AstNode blockNode = Builder.HandleBody("Else");
                AstElse elseNode = new AstElse(token, blockNode);
                if (_currentIfNode.ElseNode != null)
                {
                    Builder.Fail(
                        "An 'if' statement can only have one 'else'. Remove or merge the extra 'else' block.",
                        DiagnosticKind.SYNTAX,
                        token
                    );
                }
                _currentIfNode.ElseNode = elseNode;
                HandleElseOrElseIf(Builder.Peek());
            }
        }
    }
}
