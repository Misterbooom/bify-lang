#nullable enable
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class ClassCodeGenTests : BifyTestBase
{
    [Test]
    public void Class_EmptyClassAndInstantiation()
    {
        string code = """
        class Dummy {
        }
        func test() {
            val d: Dummy = new Dummy();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_AttributeDefaultValuesAndRead()
    {
        string code = """
        class Point {
            public val x: int = 10;
            public val y: int = 20;
        }
        func test() {
            val p: Point = new Point();
            val a: int = p.x;
            val b: int = p.y;
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_ConstructorInitializesAttributes()
    {
        string code = """
        class Point {
            public mut x: int;
            public mut y: int;
            public constructor(a: int, b: int) {
                this.x = a;
                this.y = b;
            }
        }
        func test() {
            val p: Point = new Point(5, 15);
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_MethodCallAndReturn()
    {
        string code = """
        class Calculator {
            public func add(a: int, b: int) -> int {
                return a + b;
            }
        }
        func test() {
            val c: Calculator = new Calculator();
            val res: int = c.add(3, 4);
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_MethodAccessAndMutateThis()
    {
        string code = """
        class Counter {
            public mut count: int = 0;
            public func increment() {
                this.count += 1;
            }
            public func getCount() -> int {
                return this.count;
            }
        }
        func test() {
            val c: Counter = new Counter();
            c.increment();
            val res: int = c.getCount();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_AttributeAssignmentFromOutside()
    {
        string code = """
        class Box {
            public mut value: int = 0;
        }
        func test() {
            val b: Box = new Box();
            b.value = 42;
            b.value += 8;
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_AssignToValAttributeThrows()
    {
        string code = """
        class Box {
            public val value: int = 10;
        }
        func test() {
            val b: Box = new Box();
            b.value = 42;
        }
        """;
        BifyAssert.CompileError(code, DiagnosticKind.TYPE);
    }

    [Test]
    public void Class_PrivateAttributeThrowsOutside()
    {
        string code = """
        class Secret {
            private val value: int = 100;
        }
        func test() {
            val s: Secret = new Secret();
            val x: int = s.value;
        }
        """;
        BifyAssert.CompileError(code, DiagnosticKind.ATTRIBUTE);
    }

    [Test]
    public void Class_PrivateMethodThrowsOutside()
    {
        string code = """
        class Secret {
            private func get() -> int {
                return 100;
            }
        }
        func test() {
            val s: Secret = new Secret();
            val x: int = s.get();
        }
        """;
        BifyAssert.CompileError(code, DiagnosticKind.ATTRIBUTE);
    }

    [Test]
    public void Class_MethodCallingAnotherMethod()
    {
        string code = """
        class MathHelper {
            public func square(x: int) -> int {
                return x * x;
            }
            public func sumOfSquares(a: int, b: int) -> int {
                return this.square(a) + this.square(b);
            }
        }
        func test() {
            val m: MathHelper = new MathHelper();
            val res: int = m.sumOfSquares(3, 4);
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_ImplicitThisInMethod()
    {
        string code = """
        class Storage {
            public mut value: int;
            public constructor(v: int) {
                value = v;
            }
            public func getValue() -> int {
                return value;
            }
        }
        func test() {
            val s: Storage = new Storage(99);
            val res: int = s.getValue();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_Inheritance_InheritAttributes()
    {
        string code = """
        class Animal {
            public val legs: int = 4;
        }
        class Dog : Animal {
            public val barkCount: int = 0;
        }
        func test() {
            val d: Dog = new Dog();
            val l: int = d.legs;
            val b: int = d.barkCount;
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_Inheritance_InheritMethods()
    {
        string code = """
        class Animal {
            public func getLegs() -> int {
                return 4;
            }
        }
        class Dog : Animal {
        }
        func test() {
            val d: Dog = new Dog();
            val l: int = d.getLegs();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_Inheritance_VirtualOverride()
    {
        string code = """
        class Base {
            public virtual func value() -> int {
                return 1;
            }
        }
        class Derived : Base {
            public override func value() -> int {
                return 2;
            }
        }
        func test() {
            val d: Derived = new Derived();
            val v: int = d.value();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_Inheritance_PolymorphicDispatch()
    {
        string code = """
        class Base {
            public virtual func value() -> int {
                return 1;
            }
        }
        class Derived : Base {
            public override func value() -> int {
                return 2;
            }
        }
        func test() {
            val b: Base = new Derived();
            val v: int = b.value();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_Inheritance_MultiLevel()
    {
        string code = """
        class A {
            public virtual func f() -> int {
                return 10;
            }
        }
        class B : A {
            public func g() -> int {
                return 20;
            }
        }
        class C : B {
            public override func f() -> int {
                return 30;
            }
        }
        func test() {
            val a: A = new C();
            val x: int = a.f();
            val b: B = new C();
            val y: int = b.g();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_Inheritance_ProtectedAccessInSubclass()
    {
        string code = """
        class Parent {
            protected val secret: int = 42;
            protected func getSecret() -> int {
                return this.secret;
            }
        }
        class Child : Parent {
            public func testSecret() -> int {
                return this.getSecret();
            }
        }
        func test() {
            val c: Child = new Child();
            val s: int = c.testSecret();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_Inheritance_ProtectedInaccessibleFromOutside()
    {
        string code = """
        class Parent {
            protected val secret: int = 42;
        }
        class Child : Parent {
        }
        func test() {
            val c: Child = new Child();
            val x: int = c.secret;
        }
        """;
        BifyAssert.CompileError(code, DiagnosticKind.ATTRIBUTE);
    }

    [Test]
    public void Class_Inheritance_InvalidBaseThrows()
    {
        string code = """
        class Child : NonExistent {
        }
        func test() {
        }
        """;
        BifyAssert.CompileError(code, DiagnosticKind.TYPE);
    }

    [Test]
    public void Class_Inheritance_AddDerivedMethod()
    {
        string code = """
        class Base {
            public func f() -> int {
                return 1;
            }
        }
        class Derived : Base {
            public func g() -> int {
                return 2;
            }
        }
        func test() {
            val d: Derived = new Derived();
            val a: int = d.f();
            val b: int = d.g();
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_ARC_VariableCopyAndReassign()
    {
        string code = """
        class Node {
            public val value: int = 42;
        }
        func test() {
            val a: Node = new Node();
            val b: Node = a;
            mut c: Node;
            c = b;
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_ARC_ScopeExitCleanup()
    {
        string code = """
        class Node {
            public val value: int = 1;
        }
        func test() {
            if (true) {
                val local: Node = new Node();
                val v: int = local.value;
            }
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_ARC_FunctionReturn()
    {
        string code = """
        class Node {
            public val value: int = 10;
        }
        func createNode() -> Node {
            val n: Node = new Node();
            return n;
        }
        func test() {
            val res: Node = createNode();
            val v: int = res.value;
        }
        """;
        AssertValidFunction(code);
    }

    [Test]
    public void Class_ARC_WeakReference()
    {
        string code = """
        class Node {
            public val value: int = 5;
        }
        func test() {
            val strongRef: Node = new Node();
            weak val weakRef: Node = strongRef;
            val recovered: Node = weakRef;
            val v: int = recovered.value;
        }
        """;
        AssertValidFunction(code);
    }
}
