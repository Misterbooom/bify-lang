using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast
{
    internal class NodeConverter
    {
        public static AstNode TokenToNode(Token token, AstBuilder builder)
        {
            if (token.IsPoison && token.Type is not (TokenType.STRING or TokenType.CHAR))
            {
                return builder.AbortPoison<AstNode>();
            }

            switch (token.Type)
            {
                case TokenType.NUMBER:
                    if (token.Value.Contains('.'))
                    {
                        if (float.TryParse(token.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsedFloatValue))
                        {
                            if (float.IsInfinity(parsedFloatValue))
                            {
                                return builder.Fail<AstNode>(
                                    $"Token value '{token.Value}' exceeds the maximum allowable value for a float.",
                                    DiagnosticKind.OVERFLOW,
                                    token
                                );
                            }
                            return new AstFloat(token, parsedFloatValue);
                        }
                        return builder.Fail<AstNode>(
                            $"Token value '{token.Value}' is not a valid float.",
                            DiagnosticKind.OVERFLOW,
                            token
                        );
                    }
                    else
                    {
                        if (int.TryParse(token.Value, out int parsedIntValue))
                        {
                            return new AstInteger(token, parsedIntValue);
                        }
                        return builder.Fail<AstNode>(
                            $"Token value '{token.Value}' is not a valid integer.",
                            DiagnosticKind.OVERFLOW,
                            token
                        );
                    }

                case TokenType.IDENTIFIER or TokenType.CONST:
                    return new AstIdentifier(token, token.Value);
                case TokenType.STRING or TokenType.CHAR:
                    return new AstString(token, token.Value) { IsPoison = token.IsPoison };
                case TokenType.BREAK:
                    return new AstBreak(token);
                case TokenType.CONTINUE:
                    return new AstContinue(token);
                default: return null;
            }
        }
        public static int CountCommaNode(AstNode? node)
        {
            if (node == null)
                return 0;

            int count = (node.Token != null && node.Token.Type == TokenType.COMMA) ? 1 : 0;

            count += CountCommaNode(node.Left);
            count += CountCommaNode(node.Right);

            return count;
        }


    }
}
