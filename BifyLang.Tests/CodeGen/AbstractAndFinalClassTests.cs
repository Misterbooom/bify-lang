#nullable enable
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class AbstractAndFinalClassTests : BifyTestBase
{
    [Test]
    public void AbstractClass_DirectInstantiation_Rejected()
    {
        string code = """
        abstract class Shape {
            public abstract func area() -> int;
        }
        func main() {
            val s = new Shape();
        }
        """;

        AssertCompileError(code, DiagnosticKind.CANNOT_INSTANTIATE_ABSTRACT_CLASS);
    }

    [Test]
    public void AbstractMethod_InNonAbstractClass_Rejected()
    {
        string code = """
        class Concrete {
            public abstract func foo() -> int;
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.TYPE);
    }

    [Test]
    public void AbstractMethod_NotImplementedInConcreteSubclass_Rejected()
    {
        string code = """
        abstract class Animal {
            public abstract func speak() -> int;
        }
        class Dog : Animal {
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.ABSTRACT_METHOD_NOT_IMPLEMENTED);
    }

    [Test]
    public void AbstractConstructor_Rejected()
    {
        string code = """
        abstract class Base {
            public abstract constructor() {}
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.TYPE);
    }

    [Test]
    public void AbstractClass_PolymorphicDispatchAndConcreteMembers_ExecutesCorrectly()
    {
        string code = """
        abstract class Shape {
            public mut scale: int;
            public constructor(scale: int) {
                this.scale = scale;
            }
            public abstract func baseArea() -> int;
            public func scaledArea() -> int {
                return this.baseArea() * scale;
            }
        }
        class Square : Shape {
            public mut side: int;
            public constructor(side: int, scale: int) {
                this.side = side;
                this.scale = scale;
            }
            public override func baseArea() -> int {
                return side * side;
            }
        }
        func main() -> int {
            val s: Shape = new Square(3, 4);
            return s.scaledArea();
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(36));
    }

    [Test]
    public void AbstractSubclass_OfAbstractBase_DefersImplementationToConcreteLeaf()
    {
        string code = """
        abstract class Base {
            public abstract func value() -> int;
        }
        abstract class Mid : Base {
            public func doubled() -> int {
                return this.value() * 2;
            }
        }
        class Leaf : Mid {
            public override func value() -> int {
                return 21;
            }
        }
        func main() -> int {
            val obj: Mid = new Leaf();
            return obj.doubled();
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(42));
    }

    [Test]
    public void FinalClass_InstantiationAndInheritanceFromBase_ExecutesCorrectly()
    {
        string code = """
        abstract class Base {
            public abstract func getCode() -> int;
        }
        final class SealedImpl : Base {
            public mut offset: int;
            public constructor(offset: int) {
                this.offset = offset;
            }
            public override func getCode() -> int {
                return 50 + offset;
            }
        }
        func main() -> int {
            val s = new SealedImpl(7);
            return s.getCode();
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(57));
    }

    [Test]
    public void FinalClass_Inheritance_Rejected()
    {
        string code = """
        final class Sealed {
            public mut x: int;
        }
        class Sub : Sealed {
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.TYPE);
    }

    [Test]
    public void AbstractFinalClass_Rejected()
    {
        string code = """
        abstract final class Bad {
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.ATTRIBUTE);
    }

    [Test]
    public void FinalOverrideMethod_ExecutesViaBaseReference()
    {
        string code = """
        abstract class Base {
            public abstract func getVal() -> int;
        }
        class Mid : Base {
            public override final func getVal() -> int {
                return 77;
            }
        }
        func main() -> int {
            val obj: Base = new Mid();
            return obj.getVal();
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(77));
    }

    [Test]
    public void FinalOverrideMethod_OverriddenInSubclass_Rejected()
    {
        string code = """
        abstract class Base {
            public abstract func getVal() -> int;
        }
        class Mid : Base {
            public override final func getVal() -> int {
                return 1;
            }
        }
        class Leaf : Mid {
            public override func getVal() -> int {
                return 2;
            }
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.TYPE);
    }

    [Test]
    public void FinalMethod_WithoutOverride_Rejected()
    {
        string code = """
        class Foo {
            public final func bar() -> int {
                return 1;
            }
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.ATTRIBUTE);
    }
}
