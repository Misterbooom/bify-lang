#nullable enable
using System.Collections.Generic;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Types.Data;

public class ClassType(string name, string fullName, LLVMTypeRef pointerType, LLVMTypeRef structType, string moduleName, ClassType? parent = null) : ReferenceType(name, pointerType), ICompositeType
{
    public LLVMTypeRef StructType { get; set; } = structType;
    public LLVMTypeRef LlvmStructType => StructType;
    public readonly ClassType? Parent = parent;
    public LLVMValueRef VTableGlobal;
    public VTable VTable = new();
    public Dictionary<string, ClassField> Fields { get; } = new();
    public Dictionary<string, ClassMethod> Methods { get; } = new();
    public string FullName { get; } = fullName;
    public string ModuleName { get; } = moduleName;
    public int MinType { get; init; }
    public int MaxType { get; init; }
    public TypeFlags Flags { get; init; }


    public bool IsSubclassOf(ClassType other)
    {
        var cur = this;
        while (cur != null)
        {
            if (cur.FullName == other.FullName) return true;
            cur = cur.Parent;
        }
        return false;
    }
}
