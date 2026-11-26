#nullable enable
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class UnaryCodeGenTests : BifyTestBase
{
    [TestCase("val x: int = 42; val y: int = -x;", TestName = "Unary_NegateInt")]
    [TestCase("val f: float = 3.14; val g: float = -f;", TestName = "Unary_NegateFloat")]
    [TestCase("val a: bool = true; val b: bool = !a; val c: bool = !false;", TestName = "Unary_LogicalNot")]
    [TestCase("mut x: int = 10; val y: int = ++x;", TestName = "Unary_PrefixIncrement")]
    [TestCase("mut x: int = 10; val y: int = x++;", TestName = "Unary_PostfixIncrement")]
    [TestCase("mut x: int = 10; val y: int = --x;", TestName = "Unary_PrefixDecrement")]
    [TestCase("mut x: int = 10; val y: int = x--;", TestName = "Unary_PostfixDecrement")]
    [TestCase("mut x: int = 0; x++; ++x; x--; --x;", TestName = "Unary_IncrementAsStatement")]
    [TestCase("mut f: float = 1.5; f++; --f;", TestName = "Unary_FloatIncrement")]
    public void Unary_ValidOperations(string body)
    {
        AssertValidFunction($"func test() {{ {body} }}");
    }

    [Test]
    public void Unary_MutateConstThrows()
    {
        AssertCompileError("func test() { val c: int = 5; c++; }", DiagnosticKind.TYPE);
    }

    [Test]
    public void Unary_MutateUndefinedThrows()
    {
        AssertCompileError("func test() { missingVar++; }", DiagnosticKind.NAME);
    }

    [Test]
    public void Unary_InvalidStatementThrows()
    {
        AssertCompileError("func test() { -5; }", DiagnosticKind.SYNTAX);
    }
}
