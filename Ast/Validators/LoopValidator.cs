using System;
using System.Collections.Generic;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Validators
{
    internal class LoopValidator
    {
        public static void ValidateWhileStatement(AstBuilder builder, AstWhile astWhile)
        {
            if (astWhile.ConditionNode == null)
            {
                builder.Fail(ErrorMessage.ConditionIsRequired(), DiagnosticKind.SYNTAX, astWhile.Token);
            }
        }

        public static void ValidateForStatement(AstBuilder builder, List<List<Token>> splitedTokens, AstFor astFor)
        {
            if (splitedTokens.Count != 3)
            {
                builder.Fail(ErrorMessage.InvalidForLoopStructure(), DiagnosticKind.SYNTAX, astFor.Token);
            }

            ValidateTokens(builder, splitedTokens[0], ErrorMessage.InitStatementIsRequired(), astFor.Token);
            ValidateTokens(builder, splitedTokens[1], ErrorMessage.ConditionIsRequired(), astFor.Token);
            ValidateTokens(builder, splitedTokens[2], ErrorMessage.IncrementStatementIsRequired(), astFor.Token);

            ValidateNode(builder, astFor.InitNode, IsValidInitNode, ErrorMessage.InvalidInitNode(), ErrorMessage.InvalidInitNodeType(), astFor.Token);
            ValidateNode(builder, astFor.ConditionNode, IsValidConditionNode, ErrorMessage.InvalidConditionNode(), ErrorMessage.InvalidConditionNodeType(), astFor.Token);
            ValidateNode(builder, astFor.IncrementNode, IsValidIncrementNode, ErrorMessage.InvalidIncrementNode(), ErrorMessage.InvalidIncrementNodeType(), astFor.Token);

            if (astFor.BlockNode == null)
            {
                builder.Fail(ErrorMessage.InvalidForLoopStructure(), DiagnosticKind.SYNTAX, astFor.Token);
            }
        }

        private static void ValidateTokens(AstBuilder builder, List<Token> tokens, string errorMessage, Token token)
        {
            if (tokens.Count == 0)
            {
                builder.Fail(errorMessage, DiagnosticKind.SYNTAX, token);
            }
        }

        private static void ValidateNode(AstBuilder builder, AstNode? node, Func<AstNode, bool> isValid, string nullError, string typeError, Token token)
        {
            if (node == null)
            {
                builder.Fail(nullError, DiagnosticKind.SYNTAX, token);
            }
            else if (!isValid(node))
            {
                builder.Fail(typeError, DiagnosticKind.SYNTAX, node.Token ?? token);
            }
        }

        public static bool IsValidInitNode(AstNode node) =>
            node is AstVarDecl || node is AstAssignmentOperator;

        public static bool IsValidIncrementNode(AstNode node) =>
            node is AstAssignmentOperator || node is AstUnaryOperator;

        public static bool IsValidConditionNode(AstNode node) =>
            node is AstBinaryOp || node is AstUnaryOperator ||
            node is AstIdentifier || node is AstCall ||
            node is AstIndexOperator;
    }
}