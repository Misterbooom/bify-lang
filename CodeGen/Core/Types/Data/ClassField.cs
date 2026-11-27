#nullable enable
using System.Collections.Generic;
using BifyLang.Ast;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Types.Data;

public struct ClassField(
    string name,
    BifyType type,
    AstNode? value,
    AccessLevel accessLevel,
    int index = -1,
    FieldFlags flags = FieldFlags.NONE,
    ICompositeType declaringType = null!,
    bool isPoison = false,
    LLVMValueRef globalValue = default)
{
    public readonly string Name = name;
    public readonly BifyType Type = type;
    public LLVMValueRef Value = default;
    public readonly int Index = index;
    public readonly AccessLevel AccessLevel = accessLevel;
    public readonly FieldFlags Flags = flags;
    public readonly ICompositeType DeclaringType = declaringType;
    public readonly AstNode? ValueNode = value;
    public readonly bool IsPoison = isPoison;
    public readonly LLVMValueRef GlobalValue = globalValue;

    public bool IsConst => Flags.HasFlag(FieldFlags.CONST);
    public bool IsWeak => Flags.HasFlag(FieldFlags.WEAK);
    public bool IsStatic => Flags.HasFlag(FieldFlags.STATIC);
    public override string ToString()
    {
        string flags = Flags != FieldFlags.NONE ? $"{Flags} " : "";
        string owner = DeclaringType is not null ? $"{DeclaringType.FullName}." : "";
        string slot = IsStatic
            ? (GlobalValue != default ? " [global]" : " [uninit-static]")
            : (Index >= 0 ? $" @{Index}" : "");
        string poison = IsPoison ? " [POISON]" : "";

        return $"{AccessLevel} {flags}{owner}{Name}: {Type}{slot}{poison}";
    }
}

public struct ClassMethod(
    string name,
    CallableType signature,
    LLVMValueRef functionValue,
    AccessLevel accessLevel,
    int vtableIndex = -1,
    ICompositeType declaringType = null!,
    AstNode? body = null,
    MethodFlags flags = MethodFlags.NONE,
    List<ParameterInfo>? parameters = null,
    bool isPoison = false)
{
    public readonly string Name = name;
    public readonly CallableType Signature = signature;
    public LLVMValueRef FunctionValue = functionValue;
    public int VTableIndex = vtableIndex;
    public readonly AccessLevel AccessLevel = accessLevel;
    public readonly MethodFlags Flags = flags;
    public readonly ICompositeType DeclaringType = declaringType;
    public readonly AstNode? Body = body;
    public readonly List<ParameterInfo> Parameters = parameters ?? [];
    public readonly bool IsPoison = isPoison;

    public bool IsVirtual => Flags.HasFlag(MethodFlags.VIRTUAL);
    public bool IsOverride => Flags.HasFlag(MethodFlags.OVERRIDE);
    public bool IsAbstract => Flags.HasFlag(MethodFlags.ABSTRACT);
    public bool IsFinal => Flags.HasFlag(MethodFlags.FINAL);
    public bool IsStatic => Flags.HasFlag(MethodFlags.STATIC);
}
