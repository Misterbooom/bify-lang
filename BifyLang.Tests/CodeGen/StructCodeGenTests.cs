#nullable enable
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class StructCodeGenTests : BifyTestBase
{
    [Test]
    public void Struct_NamedConstructionAndDefaults_ExecutesCorrectly()
    {
        string code = """
        struct Vec3 {
            mut x: int;
            val y: int = 20;
            val z: int = 3;
        }
        func main() -> int {
            val v1 = new Vec3(z: 5, x: 10);
            val v2 = new Vec3(x: 1, y: 2);
            return v1.x + v1.y + v1.z + v2.x + v2.y + v2.z;
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(10 + 20 + 5 + 1 + 2 + 3));
    }

    [Test]
    public void Struct_PositionalConstructionRejected()
    {
        string code = """
        struct Vec2 {
            mut x: int;
            mut y: int;
        }
        func main() -> int {
            val v = new Vec2(1, 2);
            return v.x;
        }
        """;

        AssertCompileError(code, DiagnosticKind.SYNTAX);
    }

    [Test]
    public void Struct_CopySemanticsOnVarDeclAndReassignment_NoAliasing()
    {
        string code = """
        struct Vec2 {
            mut x: int;
            mut y: int;
        }
        func main() -> int {
            mut a = new Vec2(x: 10, y: 20);
            mut b = a;
            b.x = 99;
            mut c = new Vec2(x: 1, y: 2);
            c = a;
            c.y = 77;
            return a.x + a.y + b.x + c.y;
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(10 + 20 + 99 + 77));
    }

    [Test]
    public void Struct_UninitializedLocal_ZeroInitialized()
    {
        string code = """
        struct Pair {
            mut a: int;
            mut b: int;
        }
        func main() -> int {
            mut p: Pair;
            p.a = 15;
            return p.a + p.b;
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(15));
    }

    [Test]
    public void Struct_PassAndReturnByValue_DoesNotMutateCaller()
    {
        string code = """
        struct Vec2 {
            mut x: int;
            mut y: int;
        }
        func addAndReturn(mut v: Vec2, dx: int) -> Vec2 {
            v.x = v.x + dx;
            return v;
        }
        func main() -> int {
            val orig = new Vec2(x: 5, y: 7);
            val moved = addAndReturn(orig, 10);
            return orig.x * 10 + moved.x;
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(65));
    }

    [Test]
    public void Struct_ChainedCallsAndRValueFieldOrMethodAccess()
    {
        string code = """
        struct Vec2 {
            mut x: int;
            mut y: int;
            public func sum() -> int {
                return x + y;
            }
        }
        func bump(mut v: Vec2) -> Vec2 {
            v.x = v.x + 1;
            v.y = v.y + 2;
            return v;
        }
        func forward(v: Vec2) -> Vec2 {
            return bump(v);
        }
        func main() -> int {
            val v = new Vec2(x: 1, y: 2);
            val a = forward(bump(v)).x;
            val b = bump(v).sum();
            val c = (new Vec2(x: 10, y: 20)).sum();
            val d = (new Vec2(x: 4, y: 5)).y;
            return a + b + c + d;
        }
        """;

        var result = NativeRunner.Run(code);
        // a = 3, b = (2 + 4) = 6, c = 30, d = 5 => 44
        Assert.That(result.ExitCode, Is.EqualTo(44));
    }

    [Test]
    public void Struct_MethodsMutateLValueInPlace_AndReturnThisByValue()
    {
        string code = """
        struct Counter {
            mut value: int;
            public func add(n: int) {
                value = value + n;
            }
            public func addViaThis(n: int) {
                this.value = this.value + n;
            }
            public func clone() -> Counter {
                return this;
            }
        }
        func main() -> int {
            mut c = new Counter(value: 10);
            c.add(5);
            c.addViaThis(3);
            mut copy = c.clone();
            copy.add(100);
            return c.value + copy.value;
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(18 + 118));
    }

    [Test]
    public void Struct_NestedStructMutationAndMethodCall()
    {
        string code = """
        struct Vec2 {
            mut x: int;
            mut y: int;
            public func translate(dx: int, dy: int) {
                x = x + dx;
                y = y + dy;
            }
        }
        struct Rect {
            mut origin: Vec2;
            mut width: int;
        }
        func main() -> int {
            val o = new Vec2(x: 2, y: 3);
            mut r = new Rect(origin: o, width: 10);
            r.origin.x = 5;
            r.origin.translate(4, 6);
            return r.origin.x + r.origin.y + r.width;
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(9 + 9 + 10));
    }

    [Test]
    public void Struct_InsideClass_FieldMutationAndMethodCall()
    {
        string code = """
        struct Vec2 {
            mut x: int;
            mut y: int;
            public func sum() -> int {
                return x + y;
            }
        }
        class Entity {
            public mut pos: Vec2 = new Vec2(x: 3, y: 4);
        }
        func main() -> int {
            val e = new Entity();
            e.pos.x = 10;
            return e.pos.sum();
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(14));
    }

    [Test]
    public void Struct_ImmutabilityAndInvalidOperations_Rejected()
    {
        // Assigning to val field on mut struct
        AssertCompileError("""
        struct S { val x: int; }
        func test() {
            mut s = new S(x: 1);
            s.x = 2;
        }
        """, DiagnosticKind.TYPE);

        // Assigning to mut field on val struct
        AssertCompileError("""
        struct S { mut x: int; }
        func test() {
            val s = new S(x: 1);
            s.x = 2;
        }
        """, DiagnosticKind.TYPE);

        // Assigning to nested mut field on val outer struct
        AssertCompileError("""
        struct Inner { mut x: int; }
        struct Outer { mut inner: Inner; }
        func test() {
            val o = new Outer(inner: new Inner(x: 1));
            o.inner.x = 2;
        }
        """, DiagnosticKind.TYPE);

        // Assigning to rvalue struct field
        AssertCompileError("""
        struct S { mut x: int; }
        func make() -> S { return new S(x: 1); }
        func test() {
            make().x = 2;
        }
        """, DiagnosticKind.TYPE);

        // Private field access on rvalue struct
        AssertCompileError("""
        struct S { private mut secret: int; }
        func make() -> S { return new S(secret: 1); }
        func test() -> int {
            return make().secret;
        }
        """, DiagnosticKind.ATTRIBUTE);

        // Weak flag on struct variable
        AssertCompileError("""
        struct S { mut x: int; }
        func test() {
            val s = new S(x: 1);
            weak mut w: S = s;
        }
        """, DiagnosticKind.TYPE);
    }
}
