using System.Collections.Generic;

namespace BifyLang.Lexer
{
    public enum TokenType
    {
        NEW,
        CONSTRUCTOR,
        DESTRUCTOR,
        CLASS,
        CONST,
        CALL,
        IDENTIFIER,
        NUMBER,
        STRING,
        SEMICOLON,
        COMMA,
        EOL,
        VARDECL,
        ASSIGN,
        IF,
        ELSE,
        WHILE,
        FOR,
        ADD,
        SUB,
        MUL,
        DIV,
        FLOORDIV,
        POW,
        MOD,
        LT,
        GT,
        LTE,
        GTE,
        ADDE,
        SUBE,
        MULE,
        DIVE,
        FLOORDIVE,
        POWE,
        LPAREN,
        RPAREN,
        LBRACKET,
        RBRACKET,
        COLON,
        DOT,
        WHITESPACE,
        LSQUARE,
        RSQUARE,
        LCUR,
        RCUR,
        EQ,
        NEQ,
        ARRAY,
        BLOCK,
        NULL,
        TRUE,
        FALSE,
        FUNCTION,
        RETURN,
        GTEQ,
        LTEQ,
        EQEQ,
        LSHIFT,
        RSHIFT,
        BIT_AND,
        BIT_OR,
        BIT_XOR,
        AND,
        OR,
        NOT,
        BIT_NOT,
        BIT_NEG,
        INCR,
        DECR,
        ELSEIF,
        DECREMENT,
        INCREMENT,
        FUNCTIONDECL,
        NEXTLINE,
        BITAND,
        BITOR,
        BITXOR,
        BITNOT,
        BREAK,
        CONTINUE,
        TRY,
        CATCH,
        FINALLY,
        THROW,
        COMMENT,
        CONCAT,
        RANGE,
        INDEX_OPERATOR,
        POINTER,
        CHAR,
        PUBLIC,
        PRIVATE,
        PROTECTED,
        SWITCH,
        CASE,
        DEFAULT,
        UNSAFE,
        IMPORT,
        POISON,
        VAL,
        MUT,
        ARROW,
        STRUCT
    }
    public class Token(TokenType type, string value)
    {
        public TokenType Type = type;
        public readonly string Value = value;
        public int Line;
        public int Column;
        public bool IsPoison { get; init; }

        public override string ToString() =>
            //if (Tokens != null)
            //{
            //    return $"Token(type = '{Type}',value = '{Value} tokens - {Tokens.ToString()}')";

            //}
            $"Token(type = '{Type}', value = '{Value}', Line = {Line}' column - '{Column}')";

    }



}

