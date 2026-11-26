#nullable enable
using System;
using System.Collections.Generic;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Types.Data;
[Flags]
public enum TypeFlags
{
    NONE = 0,
    PUBLIC = 1 << 0,
    STATIC = 1 << 1,
    FINAL = 1 << 2,
    ABSTRACT = 1 << 3,
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
