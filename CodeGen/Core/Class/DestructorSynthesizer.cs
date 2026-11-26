#nullable enable
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Class;

public static class DestructorSynthesizer
{
    public const string DestructorName = "destructor";

    public static ClassMethod CreateDefault(CodeGenContext ctx, ClassType classType)
    {
        var returnType = BifyType.CreateVoid(ctx);
        var paramTypes = new[] { LLVMTypeRef.CreatePointer(ctx.Context.Int8Type, 0) };
        var funcType = LLVMTypeRef.CreateFunction(returnType.Type, paramTypes, false);
        var mangledName = NameMangler.GenerateMethodName(ctx.ModuleContext.Name, classType.Name,
            DestructorSynthesizer.DestructorName);
        var signature = new CallableType(DestructorSynthesizer.DestructorName, mangledName, [], returnType, funcType);

        return new ClassMethod(
            name: DestructorName,
            signature: signature,
            ctx.Module.AddFunction(mangledName,funcType),
            accessLevel: AccessLevel.PUBLIC,
            vtableIndex: 0,
            declaringType: classType,
            body: null,
            flags: MethodFlags.VIRTUAL,
            parameters: []
        );
    }
}
