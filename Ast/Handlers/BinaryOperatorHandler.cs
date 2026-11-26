using System.Collections.Generic;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class BinaryOperatorHandler(AstBuilder builder) : TokenHandler(builder)
    {
        private Token _binaryOpToken = null;
        bool _isUnary = false;

        public override void HandleToken(Token token)
        {
            _binaryOpToken = token;

            if (Builder.Nodes.Count == 0)
            {
                _isUnary = true;
            }
            else
            {
                Token previousToken = Builder.GetPreviousToken();
                if (previousToken != null)
                {
                    _isUnary = previousToken.Type == TokenType.LPAREN ||
                               TokenConfig.BinaryOperators.ContainsValue(previousToken.Type);
                }
            }

            if (_isUnary)
            {
                Builder.TokenIndex++;
                AstNode operand = ParsePrimary();
                if (token.Type == TokenType.MUL)
                {
                    token.Type = TokenType.POINTER;
                }

                Builder.CurrentNode = new AstUnaryOperator(token, operand, true);
                return;
            }

            AstNode left = Builder.Nodes[^1];
            Builder.Nodes.RemoveAt(Builder.Nodes.Count - 1);

            int precedence = AstConfig.Precedence[token.Type];
            Builder.TokenIndex++;

            AstNode right = ParseRight(precedence);
            if (_isUnary)
            {
                Builder.Nodes.Add(left);
                Token newToken = token;
                if (token.Type == TokenType.MUL)
                {
                    newToken.Type = TokenType.POINTER;
                }

                Builder.CurrentNode = new AstUnaryOperator(newToken, right, true);
            }
            else
            {
                Builder.CurrentNode = new AstBinaryOp(token, left, right);
            }
        }

        private AstNode ParseRight(int minPrecedence)
        {
            AstNode left = ParsePrimary();
            while (!Builder.IsAtEnd())
            {
                Token next = Builder.Peek();

                if (_binaryOpToken.Type == TokenType.MUL && next.Type == TokenType.ASSIGN)
                {
                    _isUnary = true;
                    break;
                }

                if (!AstConfig.Precedence.TryGetValue(next.Type, out int prec) || prec < minPrecedence)
                    break;
                Token op = Builder.NextToken();
                AstNode right = ParseRight(prec + 1);
                left = new AstBinaryOp(op, left, right);
            }

            return left;
        }

        private bool CheckFunctionDeclaration()
        {
            return false;
        }


        public AstNode ParsePrimary(bool handlePostfix = true)
        {
            if (Builder.IsAtEnd())
            {
                Builder.Fail(
                    "unexpected end of expression",
                    DiagnosticKind.SYNTAX,
                    Builder.GetPreviousToken()
                );
            }

            Token token = Builder.NextToken();
            if (token.Type == TokenType.POISON)
            {
                return Builder.AbortPoison<AstNode>();
            }

            AstNode baseNode = token.Type switch
            {
                TokenType.IDENTIFIER or TokenType.CONST => new IdentifierHandler(Builder).ParseIdentifier(token, true),
                TokenType.NUMBER => NodeConverter.TokenToNode(token, Builder),
                TokenType.LPAREN => ParseParenthesizedExpression(),
                TokenType.SUB or TokenType.MUL or TokenType.INCREMENT or TokenType.DECREMENT or TokenType.NOT =>
                    HandleUnaryOperator(token),
                TokenType.LBRACKET => new ArrayHandler(Builder).GetArrayNode(token),
                TokenType.STRING or TokenType.CHAR => NodeConverter.TokenToNode(token, Builder),
                TokenType.NEW => new NewHandler(Builder).ParseNewExpression(token),
                _ => Builder.Fail<AstNode>(
                    $"Unexpected token '{token.Value}'.",
                    DiagnosticKind.SYNTAX,
                    token
                )
            };
            if (baseNode != null)
            {
                if (baseNode.LineNumber == 0)
                {
                    baseNode.LineNumber = token.Line;
                }

                if (baseNode.ColumnNumber == 0)
                {
                    baseNode.ColumnNumber = token.Column;
                }
            }

            if (handlePostfix)
                return ParsePostfix(baseNode);
            else
                return baseNode;
        }

        private AstNode ParsePostfix(AstNode expr)
        {
            while (!Builder.IsAtEnd())
            {
                Token next = Builder.Peek();
                if (next.Type == TokenType.LPAREN)
                {
                    List<List<Token>> splitedTokens = TokensFormatter.SplitTokensByType(Builder.GetConditionTokens(), TokenType.COMMA);
                    bool isPoison = false;
                    var args = BuildArguments(splitedTokens, ref isPoison);
                    expr = new AstCall(next, expr, args){IsPoison = isPoison};
                }
                else if (next.Type == TokenType.LBRACKET)
                {
                    var indexTokens = TokensFormatter.GetTokensBetween(
                        Builder.Tokens,
                        ref Builder.TokenIndex,
                        TokenType.LBRACKET,
                        TokenType.RBRACKET,
                        Builder.Diagnostics,
                        Builder.FilePath
                    );

                    var indexNode = Builder.ParseTokens(indexTokens);
                    expr = new AstIndexOperator(indexNode, expr);
                }
                else if (next.Type is TokenType.INCREMENT or TokenType.DECREMENT)
                {
                    expr = new AstUnaryOperator(next, expr);
                }
                else if (next.Type == TokenType.DOT)
                {
                    Builder.NextToken();
                    expr = new AstMemberAccess(next, expr, ParsePrimary(false));
                    Builder.TokenIndex--;
                }
                else
                {
                    break;
                }

                if (expr.LineNumber == 0)
                {
                    expr.LineNumber = next.Line;
                }

                if (expr.ColumnNumber == 0)
                {
                    expr.ColumnNumber = next.Column;
                }

                Builder.TokenIndex++;
            }

            return expr;
        }

        private AstNode[] BuildArguments(List<List<Token>> splitedTokens, ref bool isPoison)
        {
            int argIndex = 0;
            List<AstNode> arguments = new();

            foreach (var group in splitedTokens)
            {
                int beforeCount = Builder.Diagnostics.Diagnostics.Count;
                try
                {
                    if (group.Count == 0)
                    {
                        Builder.Error($"Argument #{argIndex + 1}: Empty argument.", DiagnosticKind.SYNTAX, null);
                        isPoison = true;
                        argIndex++;
                        continue;
                    }

                    bool isNamed = group.Count >= 2
                                   && group[0].Type == TokenType.IDENTIFIER
                                   && group[1].Type == TokenType.COLON;

                    Token? nameToken = isNamed ? group[0] : null;
                    List<Token> valueTokens = isNamed ? group.GetRange(2, group.Count - 2) : group;

                    if (valueTokens.Count == 0)
                    {
                        Builder.Error(
                            $"Argument #{argIndex + 1}: Expected expression after ':'.",
                            DiagnosticKind.SYNTAX,
                            group[1]
                        );
                        isPoison = true;
                        argIndex++;
                        continue;
                    }

                    AstBuilder b = new(valueTokens, Builder.Diagnostics, Builder.FilePath, true);
                    AstNode? valueNode = b.BuildNode();

                    if (valueNode == null)
                    {
                        if (Builder.Diagnostics.Diagnostics.Count == beforeCount)
                        {
                            Builder.Error(
                                $"Argument #{argIndex + 1}: Invalid argument expression.",
                                DiagnosticKind.SYNTAX,
                                valueTokens[0]
                            );
                        }

                        isPoison = true;
                        argIndex++;
                        continue;
                    }

                    if (valueNode.IsPoison)
                    {
                        isPoison = true;
                    }

                    AstNode argNode = nameToken != null
                        ? new AstNamedArg(new AstIdentifier(nameToken, nameToken.Value), valueNode)
                        : valueNode;

                    arguments.Add(argNode);
                }
                catch (BifyCompilationException)
                {
                    if (Builder.Diagnostics.Diagnostics.Count == beforeCount && !group.Exists(t => t.IsPoison))
                    {
                        Builder.Error(
                            $"Argument #{argIndex + 1}: Invalid argument expression.",
                            DiagnosticKind.SYNTAX,
                            group.Count > 0 ? group[0] : null
                        );
                    }

                    isPoison = true;
                }

                argIndex++;
            }

            return arguments.ToArray();
        }

        private AstBinaryOp? ConnectArguments(AstNode[] argNodes)
        {
            AstBinaryOp? binaryOp = null;
            Token commaToken = new(TokenType.COMMA, ",");

            for (int i = 0; i < argNodes.Length; i++)
            {
                binaryOp = i == 0
                    ? new AstBinaryOp(commaToken, argNodes[i], null)
                    : new AstBinaryOp(commaToken, argNodes[i], binaryOp);
            }

            return binaryOp;
        }

        private AstNode HandleUnaryOperator(Token opToken)
        {
            AstNode operand = ParsePrimary();
            if (opToken.Type == TokenType.MUL)
            {
                opToken.Type = TokenType.POINTER;
            }

            return new AstUnaryOperator(opToken, operand, true);
        }

        private AstNode ParseParenthesizedExpression()
        {
            Builder.TokenIndex--;
            List<Token> innerTokens = Builder.GetConditionTokens();
            AstNode node = Builder.ParseTokens(innerTokens);
            Builder.TokenIndex++;
            if (node?.Token.Type == TokenType.POINTER || node is AstIdentifier || IsQualifiedTypeNode(node))
            {
                AstNode valueNode = ParsePrimary();
                AstCast castNode = new(node.Token, node, valueNode);
                return castNode;
            }

            return node;
        }

        private static bool IsQualifiedTypeNode(AstNode node) =>
            node is AstMemberAccess { Right: AstIdentifier } ma &&
            (ma.Left is AstIdentifier || IsQualifiedTypeNode(ma.Left));
    }
}
