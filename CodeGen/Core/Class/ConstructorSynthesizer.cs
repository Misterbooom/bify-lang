#nullable enable
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Class;

public static class ConstructorSynthesizer
{

    public static ClassMethod CreateDefault(CodeGenContext ctx, ClassType classType)
    {
        var returnType = BifyType.CreateVoid(ctx);
        var paramTypes = new[] { LLVMTypeRef.CreatePointer(ctx.Context.Int8Type, 0) };
        var funcType = LLVMTypeRef.CreateFunction(returnType.Type, paramTypes, false);
        var mangledName = NameMangler.GenerateMethodName(ctx.ModuleContext.Name, classType.Name,
            "constructor");
        var signature = new CallableType("constructor", mangledName, [], returnType, funcType);

        return new ClassMethod(
            name: "constructor",
            signature: signature,
            ctx.Module.AddFunction(mangledName,funcType),
            accessLevel: AccessLevel.PUBLIC,
            vtableIndex: 0,
            declaringType: classType,
            body: null,
            flags: MethodFlags.NONE,
            parameters: []
        );
    }
}
