using System.Collections.Generic;
using BifyLang.Lexer;

namespace BifyLang.Ast;

internal enum DeclarationKind
{
    VARIABLE,
    FUNCTION,
    CLASS,
    STRUCT
}


internal readonly record struct DeclarationHeader(
    DeclarationKind Kind,
    AstFlag FlagNode,
    AstIdentifier NameNode,
    AstNode TypeNode,
    int EndIndex,
    int BodyStartIndex,
    bool IsCtorOrDtor = false
);
