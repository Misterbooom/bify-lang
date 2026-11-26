#nullable enable
using System;

namespace BifyLang.CodeGen.Core.Types.Data;

public enum AccessLevel
{
    PUBLIC,
    PRIVATE,
    PROTECTED,
}

[Flags]
public enum FieldFlags
{
    NONE   = 0,
    CONST  = 1 << 0,
    WEAK   = 1 << 1,
    STATIC = 1 << 2,
}

[Flags]
public enum MethodFlags
{
    NONE     = 0,
    VIRTUAL  = 1 << 0,
    OVERRIDE = 1 << 1,
    ABSTRACT = 1 << 2,
    FINAL    = 1 << 3,
    STATIC   = 1 << 4,
}
