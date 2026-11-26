#nullable enable
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class NamedArgumentsTests : BifyTestBase
{
    [Test]
    public void FunctionCall_NamedArgumentsOutOfOrder_ExecutesCorrectly()
    {
        string code = """
        func sub(a: int, b: int) -> int {
            return a - b;
        }
        func main() -> int {
            return sub(b: 3, a: 10);
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(7));
    }

    [Test]
    public void FunctionCall_MixedPositionalAndNamedArguments_ExecutesCorrectly()
    {
        string code = """
        func calc(a: int, b: int, c: int) -> int {
            return a * 100 + b * 10 + c;
        }
        func main() -> int {
            return calc(1, c: 3, b: 2);
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(123));
    }

    [Test]
    public void MethodCall_NamedArgumentsOutOfOrder_ExecutesCorrectly()
    {
        string code = """
        class MathOps {
            public func compute(x: int, y: int) -> int {
                return x * 10 + y;
            }
        }
        func main() -> int {
            val m: MathOps = new MathOps();
            return m.compute(y: 4, x: 5);
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(54));
    }

    [Test]
    public void Constructor_NamedArgumentsOutOfOrder_ExecutesCorrectly()
    {
        string code = """
        class Point {
            public mut x: int;
            public mut y: int;
            public constructor(x: int, y: int) {
                this.x = x;
                this.y = y;
            }
        }
        func main() -> int {
            val p: Point = new Point(y: 7, x: 3);
            return p.x * 10 + p.y;
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(37));
    }

    [Test]
    public void Constructor_MixedPositionalAndNamedArguments_ExecutesCorrectly()
    {
        string code = """
        class Box {
            public mut total: int;
            public constructor(a: int, b: int, c: int) {
                this.total = a * 100 + b * 10 + c;
            }
        }
        func main() -> int {
            val b: Box = new Box(2, c: 4, b: 3);
            return b.total;
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(234));
    }

    [Test]
    public void NamedArgument_UnknownName_Fails() =>
        AssertCompileError(
            "func foo(a: int) {} func test() { foo(z: 1); }",
            DiagnosticKind.NAME);

    [Test]
    public void NamedArgument_DuplicateName_Fails() =>
        AssertCompileError(
            "func foo(a: int, b: int) {} func test() { foo(a: 1, a: 2); }",
            DiagnosticKind.TYPE);

    [Test]
    public void NamedArgument_ConflictsWithPositional_Fails() =>
        AssertCompileError(
            "func foo(a: int, b: int) {} func test() { foo(1, a: 2); }",
            DiagnosticKind.TYPE);

    [Test]
    public void NamedArgument_PositionalAfterNamed_Fails() =>
        AssertCompileError(
            "func foo(a: int, b: int) {} func test() { foo(a: 1, 2); }",
            DiagnosticKind.SYNTAX);

    [Test]
    public void Constructor_UnknownNamedArgument_Fails() =>
        AssertCompileError("""
        class Point {
            public constructor(x: int) {}
        }
        func test() {
            val p: Point = new Point(y: 1);
        }
        """, DiagnosticKind.NAME);

    [Test]
    public void Constructor_IncompatibleNamedArgumentType_Fails() =>
        AssertCompileError("""
        class Point {
            public constructor(x: int) {}
        }
        func test() {
            val p: Point = new Point(x: "bad");
        }
        """, DiagnosticKind.TYPE);
}
