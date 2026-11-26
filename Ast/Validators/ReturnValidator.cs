using BifyLang.Exceptions;

namespace BifyLang.Ast.Validators
{
    internal class ReturnValidator
    {
        public static void Validate(AstBuilder builder, AstReturn node)
        {
            if (node.ArgumentsNode == null)
            {
                return;
            }
            if (!CanBeReturned(node.ArgumentsNode))
            {
                builder.Fail(
                    $"'{node.GetType().Name.Replace("Ast", "")}' Cannot be returned",
                    DiagnosticKind.TYPE,
                    node.Token
                );
            }
        }
        private static bool CanBeReturned(AstNode node)
        {
            return node is AstConstant || node is AstIdentifier ||
                node is AstBinaryOp binaryOp &&
                binaryOp.Token.Type != Lexer.TokenType.COMMA ||
                node is AstCall;
        }

    }
}
