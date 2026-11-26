using System;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Emitters;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Declarations;

public class ClassGen(CodeGenContext ctx) : CodeGenBase(ctx)
{
    public void EmitClass(AstClass classNode)
    {
        string mangledName = NameMangler.GenerateTypeName(_ctx.ModuleContext.Name, classNode.NameNode.Token.Value);

        if (!_ctx.TypeRegistry.TryGetType(mangledName, out var type))
            throw new Exception($"Cannot resolve type '{mangledName}' from TypeRegistry. ");

        ClassType classType = (ClassType)type;
        MethodEmitter methodEmitter = new(_ctx);

        foreach (var method in classType.Methods.Values)
        {
            if (method.IsPoison)
                continue;

            try
            {
                methodEmitter.EmitMethod(method, classNode.NameNode);
            }
            catch (BifyCompilationException)
            {
            }
        }

        VTableBuilder.Build(classType.VTable, _ctx, classNode.NameNode.Token.Value);
        classType.VTableGlobal = classType.VTable.GlobalValue;
    }
}
