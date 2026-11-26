#nullable enable
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class ClassLayoutTests : BifyTestBase
{
    [Test]
    public void ClassLayout_StructAndFieldIndices()
    {
        string code = """
        class Base {
            public val x: int;
            public val y: float;
        }
        class Derived : Base {
            public val z: bool;
        }
        func main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();

        var baseType = (ClassType)m.Context.TypeRegistry.GetBifyType("main.Base");
        var derivedType = (ClassType)m.Context.TypeRegistry.GetBifyType("main.Derived");

        Assert.That(baseType.Fields["x"].Index, Is.EqualTo(3));
        Assert.That(baseType.Fields["y"].Index, Is.EqualTo(4));

        Assert.That(derivedType.Fields["x"].Index, Is.EqualTo(3));
        Assert.That(derivedType.Fields["y"].Index, Is.EqualTo(4));
        Assert.That(derivedType.Fields["z"].Index, Is.EqualTo(5));

        Assert.That(baseType.StructType.StructElementTypesCount, Is.EqualTo(5));
        Assert.That(derivedType.StructType.StructElementTypesCount, Is.EqualTo(6));

        Assert.That(baseType.Fields["x"].Type.Name, Is.EqualTo("int"));
        Assert.That(baseType.Fields["y"].Type.Name, Is.EqualTo("float"));
        Assert.That(derivedType.Fields["z"].Type.Name, Is.EqualTo("bool"));
    }

    [Test]
    public void ClassLayout_VirtualAndOverrideVTable()
    {
        string code = """
        class Animal {
            public virtual func speak() -> int { return 1; }
            public func walk() -> int { return 0; }
        }
        class Dog : Animal {
            public override func speak() -> int { return 2; }
            public virtual func bark() {}
        }
        func main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();

        var animal = (ClassType)m.Context.TypeRegistry.GetBifyType("main.Animal");
        var dog = (ClassType)m.Context.TypeRegistry.GetBifyType("main.Dog");

        Assert.That(animal.VTable.Slots.Count, Is.EqualTo(2));
        Assert.That(animal.VTable.Slots[0].MethodName, Is.EqualTo("destructor"));
        Assert.That(animal.Methods["destructor"].VTableIndex, Is.EqualTo(0));
        Assert.That(animal.VTable.Slots[1].MethodName, Is.EqualTo("speak"));
        Assert.That(animal.Methods["speak"].VTableIndex, Is.EqualTo(1));
        Assert.That(animal.Methods["walk"].VTableIndex, Is.EqualTo(-1));

        Assert.That(dog.VTable.Slots.Count, Is.EqualTo(3));
        Assert.That(dog.VTable.Slots[0].MethodName, Is.EqualTo("destructor"));
        Assert.That(dog.VTable.Slots[0].Method.DeclaringType.Name, Is.EqualTo("Dog"));
        Assert.That(dog.Methods["destructor"].VTableIndex, Is.EqualTo(0));

        Assert.That(dog.VTable.Slots[1].MethodName, Is.EqualTo("speak"));
        Assert.That(dog.VTable.Slots[1].Method.DeclaringType.Name, Is.EqualTo("Dog"));
        Assert.That(dog.Methods["speak"].VTableIndex, Is.EqualTo(1));

        Assert.That(dog.VTable.Slots[2].MethodName, Is.EqualTo("bark"));
        Assert.That(dog.Methods["bark"].VTableIndex, Is.EqualTo(2));
    }

    [Test]
    public void ClassLayout_MutualClassReferences()
    {
        string code = """
        class NodeA {
            public val next: NodeB;
        }
        class NodeB {
            public val prev: NodeA;
        }
        func main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();

        var nodeA = (ClassType)m.Context.TypeRegistry.GetBifyType("main.NodeA");
        var nodeB = (ClassType)m.Context.TypeRegistry.GetBifyType("main.NodeB");

        Assert.That(nodeA.Fields["next"].Type, Is.SameAs(nodeB));
        Assert.That(nodeB.Fields["prev"].Type, Is.SameAs(nodeA));
    }

    [Test]
    public void ClassLayout_OverrideWithoutBaseMethod_Throws() =>
        AssertCompileError("class Foo { public override func bar() {} } func main() {}", DiagnosticKind.TYPE);

    [Test]
    public void ClassLayout_VirtualMethodAlreadyExists_Throws() =>
        AssertCompileError("class A { public virtual func bar() {} } class B : A { public virtual func bar() {} } func main() {}", DiagnosticKind.TYPE);

    [Test]
    public void ClassLayout_HidingVirtualWithoutOverride_Throws() =>
        AssertCompileError("class A { public virtual func bar() {} } class B : A { public func bar() {} } func main() {}", DiagnosticKind.TYPE);

    [Test]
    public void ClassLayout_OverrideSignatureMismatch_Throws() =>
        AssertCompileError("class A { public virtual func bar(x: int) -> int { return x; } } class B : A { public override func bar(x: int) -> float { return 0.0; } } func main() {}", DiagnosticKind.TYPE);

    [Test]
    public void ClassLayout_DuplicateField_Throws() =>
        AssertCompileError("class A { public val x: int; } class B : A { public val x: int; } func main() {}", DiagnosticKind.NAME);

    [Test]
    public void ClassLayout_ConstructorCannotBeVirtual_Throws() =>
        AssertCompileError("class A { public virtual constructor() {} } func main() {}", DiagnosticKind.TYPE);
}
