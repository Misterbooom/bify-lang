#nullable enable
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class CallCodeGenTests : BifyTestBase
{
    [Test]
    public void Call_VoidNoArgs()
    {
        AssertValidFunction("func foo() {} func test() { foo(); }");
    }

    [Test]
    public void Call_WithArgs()
    {
        AssertValidFunction("func add(a: int, b: int) -> int { return a + b; } func test() { val res: int = add(10, 20); }");
    }

    [Test]
    public void Call_ImplicitArgCast()
    {
        AssertValidFunction("func scale(x: float) -> float { return x; } func test() { val res: float = scale(42); }");
    }

    [Test]
    public void Call_MultipleParametersOrder()
    {
        AssertValidFunction("func sub(a: int, b: int) -> int { return a - b; } func test() { val res: int = sub(10, 3); }");
    }

    [Test]
    public void Call_NestedCalls()
    {
        AssertValidFunction("func inc(x: int) -> int { return x + 1; } func test() { val res: int = inc(inc(5)); }");
    }

    [Test]
    public void Call_AsStatement()
    {
        AssertValidFunction("func log(v: int) {} func test() { log(42); }");
    }

    [Test]
    public void Call_ArgCountMismatchThrows()
    {
        AssertCompileError("func foo(a: int, b: int) {} func test() { foo(1); }", DiagnosticKind.TYPE);
    }

    [Test]
    public void Call_UndefinedFunctionThrows()
    {
        AssertCompileError("func test() { undefinedFunc(); }", DiagnosticKind.NAME);
    }
}
