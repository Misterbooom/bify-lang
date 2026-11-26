using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Validators
{
    internal class IndexOperatorValidator
    {
        public static bool Validate(AstBuilder builder, AstNode previousNode, AstNode indexNode)
        {
            if (previousNode == null || !IsIndexableNode(previousNode))
            {
                builder.Fail(ErrorMessage.InvalidIndexTarget(), DiagnosticKind.SYNTAX, previousNode?.Token);
            }

            if (indexNode != null && !IsValidIndexNode(indexNode))
            {
                builder.Fail(ErrorMessage.InvalidIndexExpression(), DiagnosticKind.SYNTAX, indexNode.Token);
            }

            return true;
        }

        private static bool IsIndexableNode(AstNode node) => node is AstArray || node is AstIdentifier || node is AstCall || node.Token.Type == TokenType.POINTER;

        private static bool IsValidIndexNode(AstNode node) => node is AstConstant || node is AstIdentifier ||
                node is AstRangeOperator ||
                node is AstIndexOperator ||
                node is AstBinaryOp && node.Token.Type != TokenType.COMMA;
    }
}
