using BifyLang.CodeGen.Core.Types.Data;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Types.Data;

public readonly struct TypedValue(LLVMValueRef value, BifyType type, bool isWeak = false)
{
    public LLVMValueRef Value { get; } = value;
    public BifyType Type { get; } = type;
    public bool IsWeak { get; } = isWeak;
    public bool IsReference => Type.IsReference;
    public override string ToString()
    {
        return $"{Type}({Value})";
    }
}
