using System;
using System.Collections.Generic;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast.Handlers
{
    internal class ClassHandler(AstBuilder builder, DeclarationHeader header) : TokenHandler(builder)
    {
        private readonly  DeclarationHeader _header = header;
        public override void HandleToken(Token token)
        {
            bool isStruct = _header.Kind ==  DeclarationKind.STRUCT;
            string kindLower = isStruct ? "struct" : "class";
            string kindTitle = isStruct ? "Struct" : "Class";

            Builder.TokenIndex = _header.BodyStartIndex - 1;
            AstNode bodyNode = Builder.HandleBody(kindTitle);
            VerifyBody((AstBlock)bodyNode, kindLower, kindTitle);
            Builder.CurrentNode = isStruct
                ? new AstStruct(token, _header.NameNode, bodyNode, _header.TypeNode, _header.FlagNode)
                : new AstClass(token, _header.NameNode, bodyNode, _header.TypeNode, _header.FlagNode);
        }

        private void VerifyBody(AstBlock body, string kindLower, string kindTitle)
        {
            int methodsCount = 0;
            foreach (AstNode child in body.ChildNodes)
            {
                if (child is AstPoison)
                {
                    continue;
                }
                if (child is AstVarDecl)
                {
                    if (methodsCount > 0)
                    {
                        Builder.Error(
                            $"fields must precede methods in '{kindLower}'",
                            DiagnosticKind.SYNTAX,
                            child.Token
                        );
                    }
                }
                else if (child is AstFunctionDecl)
                {
                    methodsCount++;
                }
                else
                {
                    Builder.Error(
                        $"unexpected {child.GetType().Name.Replace("Ast", "").ToLower()} in '{kindLower}' body",
                        DiagnosticKind.SYNTAX,
                        child.Token
                    );
                }
            }
        }
    }
}
