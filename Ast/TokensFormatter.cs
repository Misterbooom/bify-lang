using System.Collections.Generic;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast
{
    internal class TokensFormatter
    {
        public static List<List<Token>> SplitLines(List<Token> tokens, DiagnosticBag? diagnostics = null, string filePath = "main")
        {
            DiagnosticBag bag = diagnostics ?? new DiagnosticBag();
            string resolvedPath = string.IsNullOrEmpty(filePath) ? "main" : filePath;
            List<List<Token>> lines = new();
            List<Token> currentLine = new();

            for (int tokenIndex = 0; tokenIndex < tokens.Count; tokenIndex++)
            {
                Token token = tokens[tokenIndex];

                if (token.Type == TokenType.LCUR)
                {
                    List<Token> tokensInCur = GetTokensBetween(tokens, ref tokenIndex, TokenType.LCUR, TokenType.RCUR, bag, resolvedPath);
                    
                    currentLine.Add(token);
                    currentLine.AddRange(tokensInCur);
                    currentLine.Add(GetTokenOrNull(tokens, tokenIndex) ?? new Token(TokenType.RCUR, "}"));

                    Token? nextToken = GetTokenOrNull(tokens, tokenIndex + 1);
                    if (nextToken != null && nextToken.Type != TokenType.ELSE)
                    {
                        lines.Add(currentLine);
                        currentLine = new();
                    }
                }
                else if (token.Type == TokenType.EOL)
                {
                    if (currentLine.Count > 0)
                    {
                        lines.Add(currentLine);
                        currentLine = new();
                    }
                }
                else
                {
                    currentLine.Add(token);
                }
            }

            if (currentLine.Count > 0)
            {
                lines.Add(currentLine);
            }

            return lines;
        }

        private static bool IsConditionChainToken(Token? token)
        {
            return token?.Type is TokenType.IF or TokenType.ELSE;
        }

        private static bool IsNewCondition(Token token, Token? prevToken)
        {
            return token.Type switch
            {
                TokenType.IF => prevToken?.Type != TokenType.ELSE,
                TokenType.ELSE => prevToken == null,
                _ => false
            };
        }

        public static Token GetTokenOrNull(List<Token> tokens, int index)
        {
            return (uint)index < (uint)tokens.Count ? tokens[index] : null!;
        }

        public static List<List<Token>> SplitTokensByType(List<Token> tokens, TokenType type)
        {
            List<List<Token>> tokenGroups = new();
            List<Token> currentGroup = new();

            foreach (Token token in tokens)
            {
                if (token.Type == type)
                {
                    if (currentGroup.Count > 0)
                    {
                        tokenGroups.Add(currentGroup);
                        currentGroup = new();
                    }
                }
                else
                {
                    currentGroup.Add(token);
                }
            }

            if (currentGroup.Count > 0)
            {
                tokenGroups.Add(currentGroup);
            }

            return tokenGroups;
        }

        public static List<Token> GetTokensBetween(List<Token> tokens, ref int index, TokenType open, TokenType close, DiagnosticBag? diagnostics = null, string filePath = "main")
        {
            int count = 0;
            Token? unclosedOpenToken = null;
            List<Token> newTokens = new();

            while (index < tokens.Count)
            {
                Token token = tokens[index];

                if (token.Type == open)
                {
                    if (count == 0)
                    {
                        unclosedOpenToken = token;
                    }
                    else
                    {
                        newTokens.Add(token);
                    }
                    count++;
                }
                else if (token.Type == close)
                {
                    count--;
                    if (count <= 0)
                    {
                        break;
                    }
                    newTokens.Add(token);
                }
                else if (count > 0)
                {
                    newTokens.Add(token);
                }

                index++;
            }

            if (count > 0)
            {
                Token lastOpenToken = unclosedOpenToken ?? tokens[index - 1];
                DiagnosticBag bag = diagnostics ?? new DiagnosticBag();
                string resolvedPath = string.IsNullOrEmpty(filePath) ? "main" : filePath;
                bag.Fail(
                    ErrorMessage.UnmatchedToken(open.ToString(), close.ToString()),
                    DiagnosticKind.SYNTAX,
                    new SourceLocation(resolvedPath, lastOpenToken.Line, lastOpenToken.Column)
                );
            }

            return newTokens;
        }

        private static char GetKeyByValue(Dictionary<char, TokenType> dictionary, TokenType value)
        {
            foreach (var kvp in dictionary)
            {
                if (kvp.Value == value)
                {
                    return kvp.Key;
                }
            }
            return ' ';
        }
    }
}