using System.Collections.Generic;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Types.Service;

public class TypeRegistry
{
    private readonly Dictionary<string, BifyType> _types = new();
    private readonly CodeGenContext _ctx;

    public TypeRegistry(CodeGenContext ctx)
    {
        _ctx = ctx;
        Register("int", BifyType.CreateInt(ctx));
        Register("float", BifyType.CreateFloat(ctx));
        Register("bool", BifyType.CreateBool(ctx));
        Register("void", BifyType.CreateVoid(ctx));
        Register("string", BifyType.CreateString(ctx));
        Register("null", BifyType.CreateNull(ctx));
        Register("double", BifyType.CreateDouble(ctx));
        Register("char", BifyType.CreateChar(ctx));
    }

    public void Register(string name, BifyType type)
    {
        _types[name] = type;
    }

    public void RegisterPoison(string name)
    {
        _types[name] = new BifyType(name, null){IsPoison = true};
    }
    public bool TryGetType(string name, out BifyType type) =>
        _types.TryGetValue(name, out type);

    public BifyType GetBifyType(string name)
    {
        if (TryGetType(name, out var existing))
            return existing;



        if (name.EndsWith("*"))
        {
            var baseType = GetBifyType(name[..^1]);
            var llvmType = baseType.Type == _ctx.Context.VoidType
                ? LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0)
                : LLVMTypeRef.CreatePointer(baseType.Type, 0);
            var ptrType = new BifyType(name, llvmType);
            _types[name] = ptrType;
            return ptrType;
        }

        if (name.EndsWith("]"))
        {
            int idx = name.LastIndexOf('[');
            string baseName = idx > 0 ? name[..idx] : name;
            var baseType = GetBifyType(baseName);
            var llvmType = LLVMTypeRef.CreatePointer(baseType.Type, 0);
            var arrType = new BifyType(name, llvmType);
            _types[name] = arrType;
            return arrType;
        }

        return _ctx.Fail<BifyType>($"unknown type '{name}'", DiagnosticKind.NAME);
    }
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"TypeRegistry [{_types.Count} type(s)]:");

        if (_types.Count == 0)
        {
            sb.AppendLine("  <empty>");
        }
        else
        {
            foreach (var (name, bifyType) in _types)
            {
                var llvmTypeStr = bifyType.Type.Handle != System.IntPtr.Zero
                    ? bifyType.Type.PrintToString()
                    : "null";
                sb.AppendLine($"  - {name}: {bifyType.GetType().Name} (LLVM: {llvmTypeStr}, 0x{bifyType.Type.Handle:X})");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
