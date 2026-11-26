using BifyLang.Ast.Handlers;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast;

internal static class TokenHandlerFactory
{
    public static TokenHandler CreateHandler(Token token, AstBuilder builder)
    {
        if (token.Type == TokenType.POISON)
        {
            return builder.AbortPoison<TokenHandler>();
        }

        if (builder.IsHandlingLine)
        {
            if (TokenConfig.BinaryOperators.ContainsValue(token.Type))
            {
                return builder.Fail<TokenHandler>(
                    "invalid expression statement",
                    DiagnosticKind.SYNTAX,
                    token
                );
            }
        }

        if (builder.IsHandlingLine && builder.TokenIndex == 0 && builder.Nodes.Count == 0)
        {


            if (new DeclarationParser(builder).TryParseDeclaration(out var declarationHeader))
            {
                BifyDebug.Log("Declaration kind: " + declarationHeader.Kind);
                if (declarationHeader.Kind == DeclarationKind.VARIABLE)
                {
                    return new VariableDeclarationHandler(builder, declarationHeader);
                }
                else if (declarationHeader.Kind is (DeclarationKind.CLASS or DeclarationKind.STRUCT))
                {
                    BifyDebug.Log("Handling type.");
                    return new ClassHandler(builder, declarationHeader);
                }
                else
                {
                    return new FunctionHandler(builder, declarationHeader);
                }
            }
        }


        return token.Type switch
        {
            _ when TokenConfig.BinaryOperators.ContainsValue(token.Type) => new BinaryOperatorHandler(builder),
            _ when TokenConfig.AssignmentOperators.ContainsValue(token.Type) => new AssignmentOperatorHandler(builder),
            TokenType.IDENTIFIER or TokenType.CONSTRUCTOR or TokenType.DESTRUCTOR => new IdentifierHandler(builder),
            TokenType.WHILE => new WhileHandler(builder),
            TokenType.FOR => new ForHandler(builder),
            TokenType.BREAK or TokenType.CONTINUE => new BreakContinueHandler(builder),
            TokenType.RETURN => new ReturnHandler(builder),
            TokenType.IF => new ConditionHandler(builder),
            TokenType.SWITCH => new SwitchHandler(builder),
            TokenType.NEW => new NewHandler(builder),
            TokenType.UNSAFE => new UnsafeHandler(builder),
            TokenType.IMPORT => new ImportHandler(builder),
            _ => new DefaultTokenHandler(builder),
        };
    }

}

internal class DefaultTokenHandler(AstBuilder builder) : TokenHandler(builder)
{
    public override void HandleToken(Token token)
    {
        Builder.CurrentNode = new BinaryOperatorHandler(Builder).ParsePrimary();
    }

}
