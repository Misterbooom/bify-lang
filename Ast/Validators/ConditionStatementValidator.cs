using BifyLang.Exceptions;

namespace BifyLang.Ast.Validators
{
    internal class ConditionStatementValidator
    {
        public static void ThrowUnmatchedIfError(AstBuilder builder, AstElse astElse)
        {
            builder.Fail(ErrorMessage.ElseWithoutMatchingIf(), DiagnosticKind.SYNTAX, astElse.Token);
        }

        public static void ThrowUnmatchedIfError(AstBuilder builder, AstElseIf astElseIf)
        {
            builder.Fail(ErrorMessage.ElseIfWithoutMatchingIf(), DiagnosticKind.SYNTAX, astElseIf.Token);
        }

        public static void ThrowElseIfAfterElseError(AstBuilder builder, AstElseIf astElseIf)
        {
            builder.Fail(ErrorMessage.ElseIfCannotFollowElseDirectly(), DiagnosticKind.SYNTAX, astElseIf.Token);
        }
    }
}
