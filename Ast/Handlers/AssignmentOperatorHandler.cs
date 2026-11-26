using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class AssignmentOperatorHandler(AstBuilder builder) : TokenHandler(builder)
    {
        public override void HandleToken(Token token)
        {
            if (Builder.Nodes.Count == 0)
            {
                Builder.Fail(
                    "Assignment operator used without a preceding expression. Please ensure an identifier is present before '='.",
                    DiagnosticKind.SYNTAX,
                    token
                );
            }
            if (Builder.Nodes.Count == 1)
            {
                HandleAssignment(token);
            }
            else
            {
                Builder.Fail(
                    "Unexpected use of the assignment operator. Check the syntax and try again.",
                    DiagnosticKind.SYNTAX,
                    token
                );
            }
        }
        private void HandleAssignment(Token token)
        {
            var node = Builder.Nodes[0];
            if (node is AstIdentifier || node is AstIndexOperator || node is AstMemberAccess)
            {
                var identifierNode = node;
                Builder.NextToken();
                var valueTokens = Builder.Tokens[Builder.TokenIndex..];
                var valueNode = Builder.ParseTokens(valueTokens);
                if (valueNode == null)
                {
                    Builder.Fail(ErrorMessage.EmptyValueAssigned(), DiagnosticKind.SYNTAX, token);
                }
                Builder.Nodes.Clear();
                Builder.CurrentNode = new AstAssignmentOperator(token, identifierNode, valueNode);
                Builder.MoveToEnd();
            }
            else
            {
                Builder.Fail(
                    "Assignment operator must follow an identifier or index operator or member access.",
                    DiagnosticKind.SYNTAX,
                    node.Token ?? token
                );
            }
        }
    }
}
