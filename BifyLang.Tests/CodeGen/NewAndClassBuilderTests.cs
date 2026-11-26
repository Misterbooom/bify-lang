#nullable enable
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class NewAndClassBuilderTests : BifyTestBase
{
    [Test]
    public void New_InstantiatesEmptyClass()
    {
        string code = """
        class Dummy {}
        func test() {
            val d: Dummy = new Dummy();
        }
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();

        var dummyType = (ClassType)m.Context.TypeRegistry.GetBifyType("main.Dummy");
        Assert.That(dummyType.VTableGlobal.Handle, Is.Not.EqualTo(System.IntPtr.Zero));
    }

    [Test]
    public void New_InitializesFieldsWithDefaults()
    {
        string code = """
        class Config {
            public val port: int = 8080;
            public val ratio: float = 1.5;
            public val active: bool = true;
        }
        func test() {
            val c: Config = new Config();
        }
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();
    }

    [Test]
    public void New_InitializesUnassignedFieldsToZero()
    {
        string code = """
        class Node {
            public val value: int;
            public val next: Node;
        }
        func test() {
            val n: Node = new Node();
        }
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();
    }

    [Test]
    public void New_InvokesConstructorWithArguments()
    {
        string code = """
        class Service {
            public val port: int;
            public constructor(p: int) {
            }
        }
        func test() {
            val s: Service = new Service(9000);
        }
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();
    }

    [Test]
    public void New_DerivedClassInitializesInheritedAndOwnFields()
    {
        string code = """
        class Base {
            public val a: int = 1;
        }
        class Derived : Base {
            public val b: int = 2;
        }
        func test() {
            val d: Derived = new Derived();
        }
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();
    }

    [Test]
    public void New_NonClassType_Throws() =>
        AssertCompileError("func test() { val x: int = new int(); }", DiagnosticKind.TYPE);

    [Test]
    public void New_ConstructorArgumentMismatch_Throws() =>
        AssertCompileError("""
        class Point {
            public constructor(a: int, b: int) {}
        }
        func test() {
            val p: Point = new Point(10);
        }
        """, DiagnosticKind.TYPE);

    [Test]
    public void New_ArgumentsPassedToClassWithoutConstructor_Throws() =>
        AssertCompileError("""
        class Empty {}
        func test() {
            val e: Empty = new Empty(1, 2);
        }
        """, DiagnosticKind.TYPE);
}
