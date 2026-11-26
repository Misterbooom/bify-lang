#nullable enable
using System;
using System.Collections.Generic;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Types.Data;
[Flags]
public enum TypeFlags
{
    None = 0,
    Public = 1 << 0,
    Static = 1 << 1,
    Final = 1 << 2
}
public interface ICompositeType
{
    string Name { get; }
    string FullName { get; }
    string ModuleName { get; }
    LLVMTypeRef LlvmStructType { get; }
    Dictionary<string, ClassField> Fields { get; }
    Dictionary<string, ClassMethod> Methods { get; }
    TypeFlags Flags { get; }
}
