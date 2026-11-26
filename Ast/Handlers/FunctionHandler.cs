using System.Collections.Generic;
using BifyLang.Ast.Validators;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class FunctionHandler(AstBuilder builder, DeclarationHeader declarationHeader) : TokenHandler(builder)
    {
        private readonly DeclarationHeader _header = declarationHeader;
        public override void HandleToken(Token token)
        {
            Builder.TokenIndex = _header.EndIndex;
            bool isPoison = _header.NameNode.IsPoison || _header.TypeNode is AstPoison || _header.TypeNode.IsPoison;
            AstBinaryOp parameters = null;
            if (Builder.TokenIndex < Builder.Tokens.Count && Builder.Tokens[Builder.TokenIndex].Type == TokenType.LPAREN)
            {
                List<List<Token>> splitedTokens = TokensFormatter.SplitTokensByType(Builder.GetConditionTokens(), TokenType.COMMA);
                AstParam[] parametersNodes = BuildParameters(splitedTokens, ref isPoison);
                parameters = ConnectParameters(parametersNodes);
            }
            else
            {
                isPoison = true;
            }

            Builder.TokenIndex = _header.BodyStartIndex - 1;

            AstBlock blockNode = Builder.HandleBody("Function");

            try
            {
                FunctionDeclarationValidator.Validate(Builder, _header.NameNode.Token, parameters, blockNode, _header.TypeNode);
            }
            catch (BifyCompilationException)
            {
                isPoison = true;
            }

            AstNode functionNode = new AstFunctionDecl(token, _header.TypeNode, _header.NameNode, parameters, blockNode, _header.FlagNode)
            {
                IsPoison = isPoison
            };
            Builder.CurrentNode = functionNode;
            Builder.TokenIndex++;
            Builder.Nodes.Clear();
        }


        private AstBinaryOp ConnectParameters(AstParam[] parametersNodes)
        {
            AstBinaryOp binaryOp = null;
            Token commaToken = new Token(TokenType.COMMA, ",");
            for (int i = 0; i < parametersNodes.Length; i++)
            {
                if (i == 0)
                {
                    binaryOp = new AstBinaryOp(commaToken, parametersNodes[i], null);
                }
                else
                {
                    binaryOp = new AstBinaryOp(commaToken, parametersNodes[i], binaryOp);
                }
            }
            return binaryOp;
        }

        private AstParam[] BuildParameters(List<List<Token>> splitedTokens, ref bool isPoison)
        {
            int paramIndex = 0;
            List<AstParam> parameters = new List<AstParam>();
            foreach (var group in splitedTokens)
            {
                int beforeCount = Builder.Diagnostics.Diagnostics.Count;
                try
                {
                    List<Token> paramTokens = NormalizeParameterTokens(group);
                    AstBuilder b = new(paramTokens, Builder.Diagnostics, Builder.FilePath, true);
                    AstVarDecl parametersNode = b.BuildNode() as AstVarDecl;

                    if (parametersNode == null)
                    {
                        if (Builder.Diagnostics.Diagnostics.Count == beforeCount)
                        {
                            Builder.Error(
                                $"Parameter #{paramIndex + 1}: Invalid parameter declaration.",
                                DiagnosticKind.SYNTAX,
                                group.Count > 0 ? group[0] : null
                            );
                        }
                        isPoison = true;
                        paramIndex++;
                        continue;
                    }

                    if (parametersNode.IsPoison)
                    {
                        isPoison = true;
                    }

                    AstParam param = new(parametersNode.Type, parametersNode.AssignmentNode.Left, parametersNode.AssignmentNode.Right, parametersNode.Flag);
                    parameters.Add(param);
                }
                catch (BifyCompilationException)
                {
                    if (Builder.Diagnostics.Diagnostics.Count == beforeCount && !group.Exists(t => t.IsPoison))
                    {
                        Builder.Error(
                            $"Parameter #{paramIndex + 1}: Invalid parameter declaration.",
                            DiagnosticKind.SYNTAX,
                            group.Count > 0 ? group[0] : null
                        );
                    }
                    isPoison = true;
                }
                paramIndex++;
            }
            return parameters.ToArray();
        }

        private static List<Token> NormalizeParameterTokens(List<Token> group)
        {
            if (group.Count == 0)
            {
                return group;
            }

            bool hasDeclKeyword = group.Exists(t => t.Type is TokenType.VAL or TokenType.MUT);
            bool hasColon = group.Exists(t => t.Type == TokenType.COLON);
            if (!hasDeclKeyword && hasColon)
            {
                Token first = group[0];
                Token valToken = new(TokenType.VAL, "val")
                {
                    Line = first.Line,
                    Column = first.Column
                };
                List<Token> normalized = [valToken];
                normalized.AddRange(group);
                return normalized;
            }

            return group;
        }
    }
}
