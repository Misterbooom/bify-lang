using BifyLang.Exceptions;

namespace BifyLang.Ast.Validators
{
    internal class OperandValidator
    {
        public static void Validate(AstBuilder builder, AstNode left, AstNode right, AstNode operatorNode)
        {
            if (left == null || right == null)
            {
                builder.Fail(
                    ErrorMessage.NotEnoughOperands(operatorNode.Token.Value),
                    DiagnosticKind.ARITHMETIC,
                    operatorNode?.Token
                );
            }
            CheckOperandType(builder, left, operatorNode);
            CheckOperandType(builder, right, operatorNode);
        }

        private static void CheckOperandType(AstBuilder builder, AstNode node, AstNode operatorNode)
        {
            if (!IsOperand(node))
            {
                builder.Fail(
                    ErrorMessage.InvalidOperandType(node.GetType().Name.Replace("Ast", "")),
                    DiagnosticKind.ARITHMETIC,
                    node.Token ?? operatorNode?.Token
                );
            }
        }

        private static bool IsOperand(AstNode node) => node is AstConstant ||
                   node is AstIdentifier ||
                   node is AstCall ||
                   node is AstUnaryOperator ||
                   node is AstArray ||
                   node is AstAssignment ||
                   node is AstBinaryOp ||
                   node is AstIndexOperator;

    }
}
