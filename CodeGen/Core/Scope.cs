#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public readonly struct Symbol(
    string name,
    LLVMValueRef pointer,
    BifyType type,
    bool isConstant = false,
    bool isWeak = false,
    bool isPoison = false, bool isPublic = false, TypeFlags typeFlags = TypeFlags.NONE)
{
    public string Name { get; } = name;
    public LLVMValueRef Pointer { get; } = pointer;
    public BifyType Type { get; } = type;
    public bool IsConstant { get; } = isConstant;
    public bool IsWeak { get; } = isWeak;
    public bool IsPoison { get; } = isPoison;
    public TypeFlags TypeFlags { get; } = typeFlags;

}

public class Scope(Scope? parent = null)
{
    public Scope? Parent { get; } = parent;

    public bool IsDestructor = parent?.IsDestructor ?? false;
    public string FunctionName = parent?.FunctionName ?? "";

    private readonly Dictionary<string, Symbol> _symbols = new();

    public bool Define(string name, LLVMValueRef pointer, BifyType type, bool isConstant = false, bool isWeak = false, TypeFlags typeFlags = TypeFlags.NONE)
    {
        if (_symbols.ContainsKey(name))
        {
            return false;
        }

        _symbols[name] = new Symbol(name, pointer, type, isConstant, isWeak,typeFlags: typeFlags);
        return true;
    }

    public void DefinePoison(string name, BifyType fallbackType)
    {
        _symbols[name] = new Symbol(name, default, fallbackType, isPoison: true);
    }

    public bool TryLookup(string name, out Symbol symbol)
    {
        if (_symbols.TryGetValue(name, out symbol))
        {
            return true;
        }

        if (Parent != null)
        {
            return Parent.TryLookup(name, out symbol);
        }

        symbol = default;
        return false;
    }

    public bool IsDeclaredLocally(string name) => _symbols.ContainsKey(name);

    public void ReleaseLocals(CodeGenContext ctx)
    {
        if (ctx.Builder.InsertBlock.Handle == IntPtr.Zero ||
            ctx.Builder.InsertBlock.Terminator.Handle != IntPtr.Zero)
            return;

        foreach (var sym in _symbols.Values.Reverse())
        {
            if (sym.IsPoison || sym.Pointer.Handle == IntPtr.Zero)
                continue;

            if (sym.Name != "this" && sym.Type.IsReference)
            {
                var val = ctx.Builder.BuildLoad2(sym.Type.Type, sym.Pointer, "cleanup_" + sym.Name);
                if (sym.IsWeak)
                    ctx.HeapControl.BuildReleaseWeak(val);
                else
                    ctx.HeapControl.BuildReleaseStrong(val);
            }
        }
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        var current = this;
        var depth = 0;

        while (current != null)
        {
            var indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}Scope (Level {depth}) [{current._symbols.Count} symbol(s)]:");

            if (current._symbols.Count == 0)
            {
                sb.AppendLine($"{indent}  <empty>");
            }
            else
            {
                foreach (var sym in current._symbols.Values)
                {
                    var modifiers = (sym.IsConstant, sym.IsWeak) switch
                    {
                        (true, true) => " [const, weak]",
                        (true, false) => " [const]",
                        (false, true) => " [weak]",
                        _ => ""
                    };

                    sb.AppendLine(
                        $"{indent}  - {sym.Name}: {sym.Type.GetType().Name}({sym.Type.Name}){modifiers} (0x{sym.Pointer.Handle:X})");
                }
            }

            current = current.Parent;
            depth++;
        }

        return sb.ToString().TrimEnd();
    }
}
