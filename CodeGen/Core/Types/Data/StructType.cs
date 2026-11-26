#nullable enable
using System.Collections.Generic;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Types.Data;

public class StructType(string name, string fullName, LLVMTypeRef structType, string moduleName) : BifyType(name, structType), ICompositeType
{
    public LLVMTypeRef LlvmStructType => Type;
    public string FullName { get; } = fullName;
    public string ModuleName { get; } = moduleName;
    public Dictionary<string, ClassField> Fields { get; } = new();
    public Dictionary<string, ClassMethod> Methods { get; } = new();
    public TypeFlags Flags { get; init; }
}
