namespace BifyLang.CodeGen.Core.Types.Data;

public class WrappedCompositeType(ICompositeType type):BifyType("wrapped", type.LlvmStructType)
{
    public readonly ICompositeType Type = type;
}
