using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Declarations;

public class DeclarationDispatcher(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public void EmitModule(AstModule module)
    {
        foreach (var node in module.ChildNodes)
        {
            try
            {
                EmitDeclaration(node);
            }
            catch (BifyCompilationException)
            {
            }
        }
    }

    public void EmitDeclaration(AstNode node)
    {
        _ctx.CurrentLine = node.LineNumber;
        switch (node)
        {
            case AstPoison:
                break;
            case AstFunctionDecl functionDecl:
                new FunctionGen(_ctx).EmitFunction(functionDecl);
                break;
            case AstClass astClass:
                new ClassGen(_ctx).EmitClass(astClass);
                break;
            case AstImport astImport:
                new ImportGen(_ctx).EmitImport(astImport);
                break;
            case AstStruct astStruct:
                new StructGen(_ctx).EmitStruct(astStruct);
                break;
            default:
                Fail("expected top-level declaration", DiagnosticKind.SYNTAX, node);
                break;
        }
    }
}
