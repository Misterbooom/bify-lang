using System.Collections.Generic;
using BifyLang.Ast.Validators;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class VariableDeclarationHandler(AstBuilder builder, DeclarationHeader header) : TokenHandler(builder)
    {
        private readonly  DeclarationHeader _header = header;
        public override void HandleToken(Token token)
        {
            Builder.TokenIndex = _header.EndIndex;
            Token nameToken = _header.NameNode.Token;

            Token assignToken = new(TokenType.ASSIGN, "=") { Line = nameToken.Line, Column = nameToken.Column };
            AstNode valueNode = null;
            List<Token> valueTokens = [];
            bool isPoison = false;

            if (!Builder.IsAtEnd())
            {
                try
                {
                    assignToken = Builder.Consume(TokenType.ASSIGN, "Expected '=' after variable name.");
                    valueTokens = Builder.Tokens[Builder.TokenIndex..];
                    if (valueTokens.Count == 0)
                    {
                        Builder.Fail(ErrorMessage.EmptyValueAssigned(), DiagnosticKind.SYNTAX, assignToken);
                    }
                    valueNode = Builder.ParseTokens(valueTokens);
                    if (valueNode == null)
                    {
                        Builder.Fail(ErrorMessage.EmptyValueAssigned(), DiagnosticKind.SYNTAX, assignToken);
                    }
                    if (valueNode.IsPoison)
                    {
                        isPoison = true;
                    }
                }
                catch (BifyCompilationException)
                {
                    valueNode = new AstPoison(assignToken);
                    isPoison = true;
                }
            }
            Builder.MoveToEnd();

            if (!isPoison)
            {
                try
                {
                    VariableDeclarationValidator.Validate(
                        Builder,
                        _header.NameNode,
                        _header.TypeNode,
                        valueNode,
                        valueTokens,
                        _header.FlagNode,
                        assignToken
                    );
                }
                catch (BifyCompilationException)
                {
                    valueNode = new AstPoison(assignToken);
                    isPoison = true;
                }
            }

            AstAssignment astAssignment = new(assignToken, _header.NameNode, valueNode);
            Builder.Nodes.Clear();
            Builder.CurrentNode = new AstVarDecl(assignToken, astAssignment, _header.TypeNode, _header.FlagNode)
            {
                IsPoison = isPoison
            };
        }
    }
}
