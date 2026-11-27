using System;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Emitters;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Declarations;

public class StructGen(CodeGenContext ctx) : CodeGenBase(ctx)
{
    public void EmitStruct(AstStruct structNode)
    {
        string mangledName = NameMangler.GenerateTypeName(_ctx.ModuleContext.Name, structNode.NameNode.Token.Value);
        if (!_ctx.TypeRegistry.TryGetType(mangledName, out var type))
            throw new Exception($"Cannot resolve type '{mangledName}' from TypeRegistry. ");
        if (type.IsPoison)
        {
            AbortPoison();
        }
        StructType structType = (StructType)type;
        MethodEmitter methodEmitter = new(_ctx);
        _ctx.CurrentCompositeType = structType;
        foreach (var method in structType.Methods.Values)
        {
            if (method.IsPoison)
                continue;

            try
            {
                methodEmitter.EmitMethod(method, structNode.NameNode);
            }
            catch (BifyCompilationException)
            {
            }
        }
        _ctx.CurrentCompositeType = null;

    }
}
