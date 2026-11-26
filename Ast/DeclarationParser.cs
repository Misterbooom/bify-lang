using System;
using System.Collections.Generic;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using BifyLang.Parser;

namespace BifyLang.Ast;

internal class DeclarationParser(AstBuilder builder)
{
    private readonly AstBuilder _builder = builder;

    private static Dictionary<TokenType, DeclarationKind> _kindMap = new()
    {
        { TokenType.VAL, DeclarationKind.VARIABLE },
        { TokenType.MUT, DeclarationKind.VARIABLE },
        { TokenType.FUNCTION, DeclarationKind.FUNCTION },
        { TokenType.CLASS, DeclarationKind.CLASS },
        { TokenType.STRUCT, DeclarationKind.STRUCT },
        {TokenType.CONSTRUCTOR, DeclarationKind.FUNCTION},
        {TokenType.DESTRUCTOR, DeclarationKind.FUNCTION},
    };

    public bool TryParseDeclaration(out DeclarationHeader header)
    {
        header = default;
        List<Token> tokens = _builder.Tokens;
        int i = _builder.TokenIndex;

        List<AstNode> flags = CollectFlags(tokens, ref i);
        if (i >= tokens.Count)
            return false;

        Token keyToken = tokens[i];
        if (!IsDeclarationKeyword(keyToken.Type))
            return false;

        i++;

        if (keyToken.Type is TokenType.CONSTRUCTOR or TokenType.DESTRUCTOR)
            return TryBuildCtorOrDtor(tokens, i, keyToken, flags, out header);

        DeclarationKind kind = _kindMap[keyToken.Type];
        BifyDebug.Log("Find kind: "  + kind);
        if (kind == DeclarationKind.VARIABLE)
        {
            flags.Add(new AstIdentifier(keyToken, keyToken.Value));
        }

        AstIdentifier nameNode = ParseName(tokens, ref i);
        // AstNode typeNode = kind == DeclarationKind.Variable
        //     ?
        //     : ;
        int endIndex;
        int bodyStartIndex;
        AstNode typeNode = kind switch
        {
            DeclarationKind.VARIABLE => ParseVariableType(tokens, ref i, keyToken, out endIndex,
                out bodyStartIndex),
            DeclarationKind.FUNCTION => ParseFunctionType(tokens, ref i, keyToken, out endIndex,
                out  bodyStartIndex),
            DeclarationKind.CLASS or DeclarationKind.STRUCT => ParseParent(tokens, ref i, keyToken, out endIndex,
                out  bodyStartIndex),
            _ => throw new InvalidOperationException("Unexpected declaration kind."),
        };
        header = new DeclarationHeader
        {
            Kind = kind,
            FlagNode = CreateFlagNode(flags, keyToken),
            NameNode = nameNode,
            TypeNode = typeNode,
            IsCtorOrDtor = false,
            EndIndex = endIndex,
            BodyStartIndex = bodyStartIndex
        };
        return true;
    }

    private static List<AstNode> CollectFlags(List<Token> tokens, ref int i)
    {
        List<AstNode> flags = [];
        while (i < tokens.Count && tokens[i].Type is TokenType.IDENTIFIER)
        {
            Token flagTok = tokens[i++];
            flags.Add(new AstIdentifier(flagTok, flagTok.Value));
        }

        return flags;
    }

    private static AstFlag CreateFlagNode(List<AstNode> flags, Token fallbackToken) =>
        new(flags.Count > 0 ? flags[0].Token : fallbackToken, flags);

    private static bool IsDeclarationKeyword(TokenType type) =>
    _kindMap.ContainsKey(type);

    private bool TryBuildCtorOrDtor(
        List<Token> tokens, int i, Token ctorToken, List<AstNode> flags, out DeclarationHeader header)
    {
        if (i >= tokens.Count || tokens[i].Type != TokenType.LPAREN)
        {
            _builder.Fail(
                $"Expected '(' after '{ctorToken.Value}'.",
                DiagnosticKind.SYNTAX,
                i < tokens.Count ? tokens[i] : ctorToken
            );
        }

        int closeParen = FindClosing(tokens, i, TokenType.LPAREN, TokenType.RPAREN);
        if (closeParen < 0)
        {
            _builder.Fail($"Expected ')' in '{ctorToken.Value}' declaration.", DiagnosticKind.SYNTAX, tokens[i]);
        }

        header = new DeclarationHeader
        {
            Kind = DeclarationKind.FUNCTION,
            FlagNode = CreateFlagNode(flags, ctorToken),
            NameNode = new AstIdentifier(ctorToken, ctorToken.Value),
            TypeNode = CreateVoidType(ctorToken),
            IsCtorOrDtor = true,
            EndIndex = i,
            BodyStartIndex = closeParen + 1
        };
        return true;
    }
    private AstIdentifier ParseName(List<Token> tokens, ref int i)
    {
        if (i < tokens.Count && tokens[i].Type == TokenType.IDENTIFIER)
        {
            Token nameToken = tokens[i++];
            return new AstIdentifier(nameToken, nameToken.Value);
        }

        Token? unexpected = i < tokens.Count ? tokens[i] : null;
        _builder.Error(unexpected is null
            ? "Expected declaration name."
            : $"Invalid declaration name: '{unexpected.Value}'");

        Token poison = unexpected ?? new Token(TokenType.POISON, "unknown");
        return new AstIdentifier(poison, string.Empty) { IsPoison = true };
    }

    private AstNode ParseVariableType(
        List<Token> tokens, ref int i, Token fallbackToken, out int endIndex, out int bodyStartIndex)
    {
        AstNode typeNode = CreateVoidType(fallbackToken);

        if (i < tokens.Count && tokens[i].Type == TokenType.COLON)
        {
            Token colonTok = tokens[i++];
            List<Token> typeTokens = [];

            while (i < tokens.Count && tokens[i].Type is not (TokenType.ASSIGN or TokenType.SEMICOLON))
            {
                typeTokens.Add(tokens[i++]);
            }

            typeNode = ParseTypeTokens(typeTokens, colonTok, "Expected type after ':'.");
        }

        endIndex = i;
        bodyStartIndex = -1;
        return typeNode;
    }

    private AstNode? ParseParent(List<Token> tokens, ref int i, Token fallbackToken, out int endIndex, out int bodyStartIndex)
    {
        if (i < tokens.Count && tokens[i].Type == TokenType.COLON)
        {
            endIndex = i;

            List<Token> parentTokens = TokensFormatter.GetTokensBetween(tokens, ref i, TokenType.COLON, TokenType.LCUR);
            AstNode parent = _builder.ParseTokens(parentTokens);

            bodyStartIndex = i;
            return parent;
        }
        endIndex = i;
        bodyStartIndex = i;
        return null;
    }
    private AstNode ParseFunctionType(
        List<Token> tokens, ref int i, Token fallbackToken, out int endIndex, out int bodyStartIndex)
    {
        if (i < tokens.Count && tokens[i].Type == TokenType.LPAREN)
        {
            endIndex = i;
            int closeParen = FindClosing(tokens, i, TokenType.LPAREN, TokenType.RPAREN);
            if (closeParen >= 0)
                i = closeParen + 1;
            else
                i++;
        }
        else
        {
            _builder.Error(
                "Expected '(' after function name.",
                DiagnosticKind.SYNTAX,
                i < tokens.Count ? tokens[i] : fallbackToken
            );
            endIndex = i;
        }

        AstNode typeNode = CreateVoidType(fallbackToken);

        if (i < tokens.Count && tokens[i].Type is TokenType.ARROW or TokenType.COLON)
        {
            Token arrowTok = tokens[i++];
            List<Token> typeTokens = [];

            while (i < tokens.Count && tokens[i].Type != TokenType.LCUR)
            {
                typeTokens.Add(tokens[i++]);
            }

            typeNode = ParseTypeTokens(typeTokens, arrowTok, $"Expected return type after '{arrowTok.Value}'.");
        }

        bodyStartIndex = i;
        return typeNode;
    }

    private AstNode ParseTypeTokens(List<Token> typeTokens, Token anchorToken, string emptyMessage)
    {
        if (typeTokens.Count == 0)
        {
            _builder.Error(emptyMessage, DiagnosticKind.SYNTAX, anchorToken);
            return new AstPoison(anchorToken);
        }

        int suffixStart = 0;
        while (suffixStart < typeTokens.Count &&
               typeTokens[suffixStart].Type is not (TokenType.MUL or TokenType.POINTER or TokenType.LBRACKET))
        {
            suffixStart++;
        }

        if (suffixStart == 0)
        {
            _builder.Error("Invalid type annotation.", DiagnosticKind.TYPE, typeTokens[0]);
            return new AstPoison(typeTokens[0]);
        }

        AstNode? typeNode = _builder.ParseTokens(typeTokens[..suffixStart]);
        if (typeNode == null)
        {
            _builder.Error("Invalid type annotation.", DiagnosticKind.TYPE, typeTokens[0]);
            return new AstPoison(typeTokens[0]);
        }

        int idx = suffixStart;
        while (idx < typeTokens.Count)
        {
            Token tok = typeTokens[idx];
            if (tok.Type is TokenType.MUL or TokenType.POINTER)
            {
                Token ptrTok = new(TokenType.POINTER, "*") { Line = tok.Line, Column = tok.Column };
                typeNode = new AstUnaryOperator(ptrTok, typeNode, true);
                idx++;
            }
            else if (tok.Type == TokenType.LBRACKET)
            {
                int closeIdx = FindClosing(typeTokens, idx, TokenType.LBRACKET, TokenType.RBRACKET);
                if (closeIdx < 0)
                {
                    _builder.Error("Expected ']' in array type.", DiagnosticKind.SYNTAX, tok);
                    return new AstPoison(tok);
                }

                List<Token> innerTokens = typeTokens[(idx + 1)..closeIdx];
                AstNode? indexNode = innerTokens.Count > 0 ? _builder.ParseTokens(innerTokens) : null;
                typeNode = new AstIndexOperator(indexNode, typeNode);
                idx = closeIdx + 1;
            }
            else
            {
                _builder.Error($"Unexpected token '{tok.Value}' in type annotation.", DiagnosticKind.TYPE, tok);
                return new AstPoison(tok);
            }
        }

        return typeNode;
    }

    private static AstIdentifier CreateVoidType(Token refToken)
    {
        Token voidTok = new(TokenType.IDENTIFIER, "void")
        {
            Line = refToken.Line,
            Column = refToken.Column
        };
        return new AstIdentifier(voidTok, "void");
    }

    private static int FindClosing(List<Token> tokens, int openIndex, TokenType open, TokenType close)
    {
        int depth = 0;
        for (int i = openIndex; i < tokens.Count; i++)
        {
            if (tokens[i].Type == open) depth++;
            else if (tokens[i].Type == close && --depth == 0) return i;
        }

        return -1;
    }
}
