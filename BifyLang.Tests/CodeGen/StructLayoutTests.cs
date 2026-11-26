#nullable enable
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;
using LLVMSharp.Interop;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class StructLayoutTests : BifyTestBase
{
    [Test]
    public void StructLayout_FieldsStartAtZero_UnpackedNoArcHeader()
    {
        string code = """
        struct Vec2 {
            mut x: int;
            val y: float;
            val z: bool;
        }
        func main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();

        var vecType = (StructType)m.Context.TypeRegistry.GetBifyType("main.Vec2");

        Assert.That(vecType.IsReference, Is.False);
        Assert.That(vecType.Fields["x"].Index, Is.EqualTo(0));
        Assert.That(vecType.Fields["y"].Index, Is.EqualTo(1));
        Assert.That(vecType.Fields["z"].Index, Is.EqualTo(2));

        Assert.That(vecType.LlvmStructType.StructElementTypesCount, Is.EqualTo(3));
        Assert.That(vecType.LlvmStructType.IsPackedStruct, Is.False);
        var elemTypes = vecType.LlvmStructType.GetStructElementTypes();
        Assert.That(elemTypes[0].Kind, Is.EqualTo(LLVMTypeKind.LLVMIntegerTypeKind));
        Assert.That(elemTypes[1].Kind, Is.EqualTo(LLVMTypeKind.LLVMFloatTypeKind));
        Assert.That(elemTypes[2].Kind, Is.EqualTo(LLVMTypeKind.LLVMIntegerTypeKind));
    }

    [Test]
    public void StructLayout_NestedStructAndClassContainingStruct()
    {
        string code = """
        struct Vec2 {
            mut x: int;
            mut y: int;
        }
        struct Rect {
            mut origin: Vec2;
            mut size: Vec2;
        }
        class Entity {
            public mut pos: Vec2;
            public mut id: int;
        }
        func main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();

        var vecType = (StructType)m.Context.TypeRegistry.GetBifyType("main.Vec2");
        var rectType = (StructType)m.Context.TypeRegistry.GetBifyType("main.Rect");
        var entityType = (ClassType)m.Context.TypeRegistry.GetBifyType("main.Entity");

        Assert.That(rectType.Fields["origin"].Type, Is.SameAs(vecType));
        Assert.That(rectType.Fields["origin"].Index, Is.EqualTo(0));
        Assert.That(rectType.Fields["size"].Index, Is.EqualTo(1));
        Assert.That(rectType.LlvmStructType.GetStructElementTypes()[0], Is.EqualTo(vecType.LlvmStructType));

        Assert.That(entityType.Fields["pos"].Index, Is.EqualTo(3));
        Assert.That(entityType.Fields["id"].Index, Is.EqualTo(4));
        Assert.That(entityType.StructType.GetStructElementTypes()[3], Is.EqualTo(vecType.LlvmStructType));
    }

    [Test]
    public void StructLayout_InheritanceRejected_RecoversCleanly()
    {
        string code = """
        struct Base {
            mut x: int;
        }
        struct Derived : Base {
            mut y: int;
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.TYPE);
    }

    [Test]
    public void StructLayout_ReferenceFieldRejected()
    {
        string code = """
        class Box {
            public mut v: int;
        }
        struct Holder {
            mut box: Box;
            mut count: int;
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.TYPE);
    }

    [Test]
    public void StructLayout_ConstructorAndDestructorRejected()
    {
        AssertCompileError("struct A { constructor() {} } func main() {}", DiagnosticKind.TYPE);
        AssertCompileError("struct B { destructor() {} } func main() {}", DiagnosticKind.TYPE);
    }

    [TestCase("virtual", TestName = "StructMethodFlag_VirtualRejected")]
    [TestCase("override", TestName = "StructMethodFlag_OverrideRejected")]
    [TestCase("protected", TestName = "StructMethodFlag_ProtectedRejected")]
    public void StructLayout_DisallowedMethodFlagsRejected(string flag)
    {
        string code = $$"""
        struct S {
            mut x: int;
            {{flag}} func foo() {}
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.ATTRIBUTE);
    }

    [TestCase("weak", TestName = "StructFieldFlag_WeakRejected")]
    [TestCase("protected", TestName = "StructFieldFlag_ProtectedRejected")]
    public void StructLayout_DisallowedFieldFlagsRejected(string flag)
    {
        string code = $$"""
        struct S {
            {{flag}} mut x: int;
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.ATTRIBUTE);
    }

    [Test]
    public void StructLayout_NameConflictsRejected()
    {
        AssertCompileError("struct Dup { mut x: int; } struct Dup { mut y: int; } func main() {}", DiagnosticKind.NAME);
        AssertCompileError("struct DupField { mut x: int; mut x: float; } func main() {}", DiagnosticKind.NAME);
        AssertCompileError("struct DupMethod { func f() {} func f() {} } func main() {}", DiagnosticKind.NAME);
        AssertCompileError("struct FieldMethodClash { mut x: int; func x() {} } func main() {}", DiagnosticKind.NAME);
    }

    [Test]
    public void StructLayout_DirectCycleRejected()
    {
        string code = """
        struct Node {
            mut next: Node;
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.TYPE);
    }

    [Test]
    public void StructLayout_MutualRecursionRejected()
    {
        string code = """
        struct A {
            mut b: B;
        }
        struct B {
            mut a: A;
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.TYPE);
    }
}
