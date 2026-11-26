using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using BifyLang.Parser;
namespace BifyLang.Ast
{
    internal class AstBuilder
    {
        public int TokenIndex = 0;
        public readonly List<Token> Tokens;
        public readonly DiagnosticBag Diagnostics;
        public readonly string FilePath;

        public AstNode? CurrentNode
        {
            get => _currentNode;
            set
            {
                _currentNode = value;
                if (_currentNode != null && _currentNode.Token != null)
                {
                    if (_currentNode.LineNumber == 0)
                    {
                        _currentNode.LineNumber = _currentNode.Token.Line;
                    }
                    if (_currentNode.ColumnNumber == 0)
                    {
                        _currentNode.ColumnNumber = _currentNode.Token.Column;
                    }
                }
            }
        }
        private AstNode? _currentNode;
        public readonly bool IsHandlingLine = true;
        public List<AstNode> Nodes { get; set; } = new List<AstNode>();

        public AstBuilder(List<Token> tokens, bool isHandlingLine = true)
            : this(tokens, null, "main", isHandlingLine)
        {
        }

        public AstBuilder(List<Token> tokens, DiagnosticBag? diagnostics, string filePath = "main", bool isHandlingLine = true)
        {
            Tokens = tokens;
            Diagnostics = diagnostics ?? new DiagnosticBag();
            FilePath = string.IsNullOrEmpty(filePath) ? "main" : filePath;
            IsHandlingLine = isHandlingLine;
        }

        public SourceLocation CreateLocation(Token? token = null)
        {
            Token? t = token ?? PeekTokenOrNull() ?? GetPreviousToken();
            return new SourceLocation(FilePath, t?.Line ?? 0, t?.Column ?? 0);
        }

        public void Error(string msg, DiagnosticKind kind = DiagnosticKind.SYNTAX, Token? token = null)
            => Diagnostics.Report(msg, kind, CreateLocation(token));

        [DoesNotReturn]
        public void Fail(string msg, DiagnosticKind kind = DiagnosticKind.SYNTAX, Token? token = null)
            => Diagnostics.Fail(msg, kind, CreateLocation(token));

        [DoesNotReturn]
        public T Fail<T>(string msg, DiagnosticKind kind = DiagnosticKind.SYNTAX, Token? token = null)
            => Diagnostics.Fail<T>(msg, kind, CreateLocation(token));

        [DoesNotReturn]
        public void AbortPoison() => throw new BifyCompilationException();

        [DoesNotReturn]
        public T AbortPoison<T>() => throw new BifyCompilationException();

        public AstNode? BuildNode()
        {
            if (Tokens == null || Tokens.Count == 0)
            {
                return null;
            }
            while (TokenIndex < Tokens.Count)
            {
                Token token = Tokens[TokenIndex];
                if (token.Type == TokenType.POISON)
                {
                    AbortPoison();
                }
                var handler = TokenHandlerFactory.CreateHandler(token, this);
                handler.HandleToken(token);

                if (CurrentNode != null)
                {
                    if (CurrentNode.Token != null)
                    {
                        if (CurrentNode.LineNumber == 0)
                        {
                            CurrentNode.LineNumber = CurrentNode.Token.Line;
                        }
                        if (CurrentNode.ColumnNumber == 0)
                        {
                            CurrentNode.ColumnNumber = CurrentNode.Token.Column;
                        }
                    }
                    Nodes.Add(CurrentNode);
                    CurrentNode = null;
                }
            }
            if (Nodes.Count > 1)
            {
                if (!IsHandlingLine)
                {
                    BifyDebug.LogError("\n" + string.Join("\n", Nodes));
                    Fail("invalid syntax", DiagnosticKind.SYNTAX, Nodes[1].Token);
                }
                else
                {
                    BifyDebug.LogError("\n" + string.Join("\n", Nodes));
                    HandleInvalidSyntax();
                }

            }
            return Nodes.Count == 1 ? Nodes[0] : null;
        }

        public List<AstNode> BuildNodes()
        {
            if (Tokens == null || Tokens.Count == 0)
            {
                return null;
            }
            while (TokenIndex < Tokens.Count)
            {
                Token token = Tokens[TokenIndex];
                if (token.Type == TokenType.POISON)
                {
                    AbortPoison();
                }
                var handler = TokenHandlerFactory.CreateHandler(token, this);
                handler.HandleToken(token);

                if (CurrentNode != null)
                {
                    if (CurrentNode.Token != null)
                    {
                        if (CurrentNode.LineNumber == 0)
                        {
                            CurrentNode.LineNumber = CurrentNode.Token.Line;
                        }
                        if (CurrentNode.ColumnNumber == 0)
                        {
                            CurrentNode.ColumnNumber = CurrentNode.Token.Column;
                        }
                    }
                    Nodes.Add(CurrentNode);
                    CurrentNode = null;
                }
            }
            return Nodes;
        }

        public Token GetPreviousToken() => TokensFormatter.GetTokenOrNull(Tokens, TokenIndex - 1);

        public Token Peek() => TokenIndex < Tokens.Count ? Tokens[TokenIndex] : null;

        public Token NextToken() => Tokens[TokenIndex++];

        public Token? PeekTokenOrNull() => TokenIndex >= Tokens.Count ? null : Tokens[TokenIndex];

        public bool IsAtEnd() => TokenIndex >= Tokens.Count;

        public Token Consume(TokenType type, string message = "Unexpected token")
        {
            Token? token = PeekTokenOrNull();
            if (token != null && token.Type == type)
            {
                return NextToken();
            }
            return Fail<Token>(message, DiagnosticKind.SYNTAX, token ?? GetPreviousToken());
        }

        public Token? GetNextToken()
        {
            if (TokenIndex + 1 >= Tokens.Count)
            {
                return null;
            }
            return Tokens[TokenIndex + 1];
        }

        public void MoveToEnd()
        {
            TokenIndex = Tokens.Count;
        }

        public AstBlock HandleBody(string name)
        {
            if (IsAtEnd())
            {
                return Fail<AstBlock>($"missing {name.ToLowerInvariant()} body", DiagnosticKind.SYNTAX, GetPreviousToken());
            }
            List<Token> bodyTokens = null;
            if (GetNextToken()?.Type == TokenType.LCUR)
            {
                bodyTokens = GetBlockTokens();
            }
            if (bodyTokens == null)
            {
                return Fail<AstBlock>($"missing {name.ToLowerInvariant()} body", DiagnosticKind.SYNTAX, PeekTokenOrNull() ?? GetPreviousToken());
            }

            AstBlock blockNode = ParseBlock(bodyTokens);
            TokenIndex++;
            return blockNode;
        }

        public List<Token> GetConditionTokens() =>
            TokensFormatter.GetTokensBetween(Tokens, ref TokenIndex, TokenType.LPAREN, TokenType.RPAREN, Diagnostics, FilePath);

        public List<Token> GetBlockTokens() =>
            TokensFormatter.GetTokensBetween(Tokens, ref TokenIndex, TokenType.LCUR, TokenType.RCUR, Diagnostics, FilePath);

        public AstNode? ParseTokens(List<Token> conditionTokens) =>
            new AstBuilder(conditionTokens, Diagnostics, FilePath, false).BuildNode();

        public List<AstNode> ParseTokensInNodes(List<Token> tokens) =>
            new AstBuilder(tokens, Diagnostics, FilePath, false).BuildNodes();

        public AstBlock ParseBlock(List<Token> blockTokens) =>
            new AstBlock(new AstTree([], Diagnostics, FilePath).ParseTokens(blockTokens).ChildNodes);

        private void HandleInvalidSyntax()
        {
            if (Nodes.Count == 2)
            {
                Nodes.WriteNodes();
                if (Nodes[0].Token.Type == TokenType.POINTER || Nodes[0] is AstIdentifier)
                {
                    Fail($"Invalid syntax.", DiagnosticKind.SYNTAX, Nodes[0].Token);
                }
                Fail("Opa 2 node", DiagnosticKind.SYNTAX, Nodes[1].Token);
            }
            Token? errToken = Nodes.Count > 1 ? Nodes[1].Token : Nodes.FirstOrDefault()?.Token;
            Fail("Invalid Syntax.", DiagnosticKind.SYNTAX, errToken);
        }
    }
}
