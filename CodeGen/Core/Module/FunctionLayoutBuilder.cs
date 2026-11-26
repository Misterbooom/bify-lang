using BifyLang.Ast;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Declarations;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Core.Module;

public class FunctionLayoutBuilder(CodeGenContext ctx) : LayoutBuilder(ctx)
{
    public override void Declare(ModuleInfo[] modules)
    {
    }

    public override void Build(ModuleInfo[] modules)
    {
        foreach (var moduleInfo in modules)
        {
            using (_ctx.EnterModule(moduleInfo.ModuleContext))
            {
                DeclareFunctions(moduleInfo.ModuleNode, moduleInfo.ModuleContext);
            }
        }

    }
    private void DeclareFunctions(AstModule module, ModuleContext moduleContext)
    {
        var funcGen = new FunctionGen(_ctx);

        foreach (var moduleChildNode in module.ChildNodes)
        {
            if (moduleChildNode is AstFunctionDecl functionChildNode)
            {
                _ctx.CurrentLine = functionChildNode.LineNumber;

                if (functionChildNode.IsPoison)
                {
                    _ctx.CurrentScope.DefinePoison(functionChildNode.FunctionNameNode.Name, BifyType.CreateVoid(_ctx));
                    continue;
                }
                try
                {
                    funcGen.DeclareFunction(functionChildNode);
                }
                catch (BifyCompilationException)
                {
                    _ctx.CurrentScope.DefinePoison(functionChildNode.FunctionNameNode.Name, BifyType.CreateVoid(_ctx));
                }
            }
        }
    }
}
