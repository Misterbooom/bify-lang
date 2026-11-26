#nullable enable
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Validators;

internal static class SwitchValidator
{
    public static void Validate(AstBuilder builder, AstSwitch astSwitch)
    {
        if (astSwitch.ExpressionNode == null)
        {
            builder.Fail(ErrorMessage.ConditionIsRequiredInSwitch(), DiagnosticKind.SYNTAX, astSwitch.Token);
        }
    }

    public static void ThrowExpectedColonAfterCase(AstBuilder builder, Token token)
    {
        builder.Fail(ErrorMessage.ExpectedColonAfterCase(), DiagnosticKind.SYNTAX, token);
    }

    public static void ThrowExpectedColonAfterDefault(AstBuilder builder, Token token)
    {
        builder.Fail(ErrorMessage.ExpectedColonAfterDefault(), DiagnosticKind.SYNTAX, token);
    }

    public static void ThrowMultipleDefaultError(AstBuilder builder, Token token)
    {
        builder.Fail(ErrorMessage.MultipleDefaultInSwitch(), DiagnosticKind.SYNTAX, token);
    }

    public static void ThrowInvalidSwitchBodyToken(AstBuilder builder, Token token)
    {
        builder.Fail(ErrorMessage.InvalidSwitchBodyToken(token.Value), DiagnosticKind.SYNTAX, token);
    }
}
