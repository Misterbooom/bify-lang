using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using BifyLang.Exceptions;

namespace BifyLang.Lexer
{
    public  class MyLexer
    {
        private int _position;
        private readonly string _code;
        private readonly string _filePath;
        private int _lineCount = 1;
        private int _lineStartOffset;

        private readonly List<Token> _tokens = [];

        public DiagnosticBag Diagnostics { get; }

        public MyLexer(string code, DiagnosticBag? diagnostics = null, string filePath = "main")
        {
            _code = code;
            Diagnostics = diagnostics ?? new DiagnosticBag();
            _filePath = string.IsNullOrEmpty(filePath) ? "main" : filePath;
            _position = 0;
        }

        public List<Token> Tokenize()
        {
            Stack<int> parenthesesStack = new();
            Stack<int> squareBracketsStack = new();
            Stack<int> curlyBracesStack = new();

            while (_position < _code.Length)
            {
                char currentChar = _code[_position];
                if (currentChar == '\n')
                {
                    _lineCount++;
                    _position++;
                    _lineStartOffset = _position;
                    continue;
                }
                if (char.IsWhiteSpace(currentChar))
                {
                    _position++;
                    continue;
                }

                int tokenStart = _position;
                int tokenColumn = (tokenStart - _lineStartOffset) + 1;

                KeyValuePair<string, TokenType> multichar = GenerateMultiChar();

                if (currentChar == '(')
                {
                    parenthesesStack.Push(_lineCount);
                }
                else if (currentChar == ')')
                {
                    if (parenthesesStack.Count == 0)
                    {
                        Diagnostics.Report(
                            ErrorMessage.UnmatchedClosingParenthesis(),
                            DiagnosticKind.SYNTAX,
                            new SourceLocation(_filePath, _lineCount, tokenColumn)
                        );
                        AddToken(new Token(TokenType.POISON, ")") { IsPoison = true }, tokenColumn);
                        _position++;
                        continue;
                    }
                    parenthesesStack.Pop();
                }
                else if (currentChar == ';')
                {
                    if (parenthesesStack.Count == 0 && squareBracketsStack.Count == 0 && curlyBracesStack.Count == 0)
                    {
                        AddToken(new Token(TokenType.EOL, ";"), tokenColumn);
                    }
                    else
                    {
                        AddToken(new Token(TokenType.SEMICOLON, ";"), tokenColumn);
                    }
                    _position++;
                    continue;
                }

                if (currentChar == '"' || currentChar == '\'')
                {
                    AddToken(GenerateString(tokenColumn), tokenColumn);
                }
                else if (currentChar == '-' && _position + 1 < _code.Length && char.IsDigit(_code[_position + 1]))
                {
                    string digit = GenerateDigit();
                    AddToken(new Token(TokenType.NUMBER, digit), tokenColumn);
                }
                else if (char.IsDigit(currentChar))
                {
                    string digit = GenerateDigit();
                    AddToken(new Token(TokenType.NUMBER, digit), tokenColumn);
                }
                else if (IsIdentifier(currentChar))
                {
                    string identifier = GenerateIdentifier();
                    if (TokenConfig.MultiCharTokens.TryGetValue(identifier, out TokenType tokenType))
                    {
                        AddToken(new Token(tokenType, identifier), tokenColumn);
                    }
                    else
                    {
                        AddToken(new Token(TokenType.IDENTIFIER, identifier), tokenColumn);
                    }
                }
                else if (multichar.Key != " ")
                {
                    if (multichar.Value == TokenType.COMMENT)
                    {
                        SkipComment();
                        continue;
                    }
                    AddToken(new Token(multichar.Value, multichar.Key), tokenColumn);
                    _position += multichar.Key.Length - 1;
                }
                else if (TokenConfig.SingleCharTokens.TryGetValue(currentChar, out TokenType tokenType))
                {
                    AddToken(new Token(tokenType, currentChar.ToString()), tokenColumn);
                }
                else
                {
                    if (!char.IsWhiteSpace(currentChar))
                    {
                        Diagnostics.Report(
                            ErrorMessage.UnexpectedToken(currentChar.ToString()),
                            DiagnosticKind.SYNTAX,
                            new SourceLocation(_filePath, _lineCount, tokenColumn)
                        );
                        AddToken(new Token(TokenType.POISON, currentChar.ToString()) { IsPoison = true }, tokenColumn);
                    }
                }

                _position++;
            }
            return _tokens;
        }

        private void AddToken(Token token, int column)
        {
            token.Line = _lineCount;
            token.Column = column;
            _tokens.Add(token);
        }


        public static bool IsIdentifier(char identifier) => identifier == '_' || char.IsLetter(identifier) || char.IsDigit(identifier);
        public void SkipComment()
        {
            while (_position < _code.Length && _code[_position] != '\n')
            {
                _position++;
            }
        }
        public string GenerateDigit()
        {
            string digit = "";
            int dotCount = 0;

            while (_position < _code.Length)
            {
                char currentChar = _code[_position];

                if (char.IsWhiteSpace(currentChar))
                {
                    break;
                }

                if (currentChar == '.' && dotCount > 0)
                {
                    _position--;
                    break;
                }

                if (currentChar != '.' && !char.IsDigit(currentChar) && currentChar != '-')
                {
                    break;
                }

                if (currentChar == '.')
                {
                    dotCount++;
                }

                digit += currentChar;
                _position++;
            }

            _position--;

            return digit;


        }
        public KeyValuePair<string, TokenType> GenerateMultiChar()
        {
            KeyValuePair<string, TokenType>? longestMatch = null;

            foreach (KeyValuePair<string, TokenType> kvp in TokenConfig.MultiCharTokens)
            {
                if (_position + kvp.Key.Length <= _code.Length)
                {
                    string match = _code.Substring(_position, kvp.Key.Length);
                    if (match == kvp.Key)
                    {
                        if (longestMatch == null || kvp.Key.Length > longestMatch.Value.Key.Length)
                        {
                            longestMatch = kvp;
                        }
                    }
                }
            }

            if (longestMatch != null)
            {
                return longestMatch.Value;
            }

            return new KeyValuePair<string, TokenType>(" ", TokenType.WHITESPACE);
        }


        public Token GenerateString(int startColumn = 1)
        {
            StringBuilder result = new();

            char quoteChar = _code[_position];
            TokenType tokenType = quoteChar == '"' ? TokenType.STRING : TokenType.CHAR;
            _position++;

            while (_position < _code.Length)
            {
                char currentChar = _code[_position];

                if (currentChar == quoteChar)
                {
                    return new Token(tokenType, result.ToString());
                }

                if (currentChar == '\\')
                {
                    _position++;
                    if (_position >= _code.Length)
                        break;

                    char escapeChar = _code[_position];
                    switch (escapeChar)
                    {
                        case 'n': result.Append('\n'); break;
                        case 't': result.Append('\t'); break;
                        case 'r': result.Append('\r'); break;
                        case '\\': result.Append('\\'); break;
                        case '"': result.Append('"'); break;
                        case '\'': result.Append('\''); break;
                        case '0': result.Append('\0'); break;
                        default:
                            result.Append(escapeChar);
                            break;
                    }
                }
                else if (currentChar == '\n')
                {
                    _position--;
                    break;
                }
                else
                {
                    result.Append(currentChar);
                }
                _position++;
            }

            Diagnostics.Report(
                ErrorMessage.MissingCloseQuotationMark(),
                DiagnosticKind.SYNTAX,
                new SourceLocation(_filePath, _lineCount, startColumn)
            );
            return new Token(tokenType, result.ToString()) { IsPoison = true };
        }

        public string GenerateIdentifier()
        {
            string identifier = "";
            while (_position < _code.Length)
            {
                char currentChar = (_code[_position]);
                if (IsIdentifier(currentChar))
                {
                    identifier += currentChar;
                    _position++;
                }
                else
                {
                    break;
                }
            }
            _position--;
            return identifier;
        }
    }
}
